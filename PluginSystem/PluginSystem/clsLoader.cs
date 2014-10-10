using System;
using System.IO;
using Microsoft.CSharp;
using System.Reflection;
using System.Diagnostics;
using System.CodeDom.Compiler;
using System.Collections.Generic;

namespace VT100.PluginSystem
{
    public static class PluginManager
    {
        private const string CR = "\r";
        private const string LF = "\n";
        private const string CRLF = CR + LF;

        public static string AppPath
        {
            get
            {
                string name = Process.GetCurrentProcess().MainModule.FileName;
                return name.Substring(0, name.LastIndexOf(Path.DirectorySeparatorChar));
            }
        }

        public static List<IPlugin> Plugins
        { get; private set; }

        /// <summary>
        /// Contains a List of Compiler Errors that may occur on the "Load" Call, if it failed.
        /// </summary>
        public static CompilerErrorCollection LastErrors
        { get; private set; }

        static PluginManager()
        {
            Plugins = new List<IPlugin>();
        }

        /// <summary>
        /// Loads a source file into Memory
        /// </summary>
        /// <param name="Content">Source Code to load</param>
        /// <returns>Server, or null if Error</returns>
        public static IPlugin LoadPlugin(string Source)
        {
            string[] Lines = File.ReadAllLines(Source);
            //Initialize Compiler
            string retValue = string.Empty;
            string Code = string.Empty;
            CodeDomProvider codeProvider = new CSharpCodeProvider();
            CompilerParameters compilerParams = new CompilerParameters();
            compilerParams.CompilerOptions = "/target:library /optimize";
            compilerParams.GenerateExecutable = false;
            compilerParams.GenerateInMemory = true;
            compilerParams.IncludeDebugInformation = false;
            compilerParams.ReferencedAssemblies.Add("mscorlib.dll");
            compilerParams.ReferencedAssemblies.Add("System.dll");
            compilerParams.ReferencedAssemblies.Add(Path.Combine(AppPath, "PluginSystem.dll"));

            foreach (string Line in Lines)
            {
                //Check if Include Statement or Code
                if (Line.Trim().ToLower().StartsWith("#include "))
                {
                    compilerParams.ReferencedAssemblies.Add(Line.Substring(9));
                    Code += CRLF;
                }
                else if (Line.Trim().Length > 0)
                {
                    Code += Line.Trim() + CRLF;
                }
            }

            //Compile that shit
            CompilerResults results = codeProvider.CompileAssemblyFromSource(compilerParams, new string[] { Code });

            //Check if Errors
            if (results.Errors.Count > 0)
            {
                LastErrors = results.Errors;
                throw new Exception("Compiler Error");
            }
            else
            {
                LastErrors = null;
            }
            return LoadPluginAssembly(results.CompiledAssembly);
        }

        /// <summary>
        /// Loads an Assembly ang gets its Name
        /// </summary>
        /// <param name="a">Assembly to load</param>
        /// <returns>Generic Server component</returns>
        private static IPlugin LoadPluginAssembly(Assembly a)
        {
            IPlugin retValue = null;

            foreach (Type type in a.GetTypes())
            {
                if (type.IsPublic) // Ruft einen Wert ab, der angibt, ob der Type als öffentlich deklariert ist. 
                {
                    if (!type.IsAbstract)  //nur Assemblys verwenden die nicht Abstrakt sind
                    {
                        // Sucht die Schnittstelle mit dem angegebenen Namen. 
                        Type typeInterface = type.GetInterface(Type.GetType("VT100.PluginSystem.IPlugin").ToString(), true);

                        //Make sure the interface we want to use actually exists
                        if (typeInterface != null)
                        {
                            object activedInstance = Activator.CreateInstance(type);
                            if (activedInstance != null)
                            {
                                IPlugin script = (IPlugin)activedInstance;
                                retValue = script;
                                Plugins.Add(retValue);
                            }
                        }

                        typeInterface = null;
                    }
                }
            }
            a = null;
            return retValue;
        }

        /// <summary>
        /// Loads a compiled Script into Memory
        /// </summary>
        /// <param name="Path">DLL File Name</param>
        /// <returns>Generic Server component</returns>
        public static IPlugin LoadPluginLib(string Path)
        {
            return LoadPluginAssembly(Assembly.LoadFrom(Path));
        }

        /// <summary>
        /// Compiles a Script to a DLL File
        /// </summary>
        /// <param name="Content">Source Code</param>
        /// <param name="DestinationDLL">DLL File Name</param>
        public static void Compile(string Content, string DestinationDLL)
        {
            //Initialize Compiler
            string retValue = string.Empty;
            string Code = string.Empty;
            CodeDomProvider codeProvider = new CSharpCodeProvider();
            CompilerParameters compilerParams = new CompilerParameters();
            compilerParams.CompilerOptions = "/target:library /optimize";
            compilerParams.GenerateExecutable = false;
            compilerParams.GenerateInMemory = false;
            compilerParams.IncludeDebugInformation = false;
            compilerParams.OutputAssembly = DestinationDLL;
            compilerParams.ReferencedAssemblies.Add("mscorlib.dll");
            compilerParams.ReferencedAssemblies.Add("System.dll");
            compilerParams.ReferencedAssemblies.Add(Path.Combine(AppPath, "PluginSystem.dll"));

            string[] Lines = Content.Split(new string[] { CRLF, CR, LF }, StringSplitOptions.None);
            foreach (string Line in Lines)
            {
                //Check if Include Statement or Code
                if (Line.Trim().ToLower().StartsWith("#include "))
                {
                    compilerParams.ReferencedAssemblies.Add(Line.Substring(9));
                    Code += CRLF;
                }
                else if (Line.Trim().Length > 0)
                {
                    Code += Line.Trim() + CRLF;
                }
            }

            //Compile that shit
            CompilerResults results = codeProvider.CompileAssemblyFromSource(compilerParams, new string[] { Code });

            //Check if Errors
            if (results.Errors.Count > 0)
            {
                LastErrors = results.Errors;
                throw new Exception("Compiler Error");
            }

        }
    }
}