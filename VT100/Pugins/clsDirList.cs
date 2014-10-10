using System;
using System.IO;
using VT100;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Threading;

namespace VT100.PluginSystem
{
    /// <summary>
    /// directory listing and plugin management plugin
    /// </summary>
    public class DirList : IPlugin
    {
        private Thread T;
        private VTconsole C;
        private bool cont;
        private bool hookEvent;
        private Dictionary<string, IPlugin> PluginCommands;
        private Dictionary<IPlugin, string> PluginHelp;

        /// <summary>
        /// Initializing
        /// </summary>
        public DirList()
        {
            PluginCommands = new Dictionary<string, IPlugin>();
            PluginHelp = new Dictionary<IPlugin, string>();
        }

        /// <summary>
        /// returns true, if not dead (yet)
        /// </summary>
        public bool IsRunning
        {
            get
            {
                if (T != null)
                {
                    return T.IsAlive;
                }
                return false;
            }
        }

        /// <summary>
        /// starts the plugin
        /// </summary>
        /// <param name="Con">Console</param>
        /// <returns>true, if started successfully</returns>
        public bool Start(VTconsole Con)
        {
            if (T == null)
            {
                C = Con;

                C.TerminalStateChanged += new TerminalStateChangedHandler(C_TerminalStateChanged);

                T = new Thread(run);
                T.IsBackground = true;
                T.Start();
            }
            else
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// sends the main prompt if the terminal changes to ready
        /// </summary>
        /// <param name="State">terminal state</param>
        private void C_TerminalStateChanged(VTconsole.TerminalState State)
        {
            if (hookEvent && State == VTconsole.TerminalState.Ready)
            {
                //reinitializes the terminal
                C.Clear();
                C.Beep();
                C.WriteLine("Type HELP for more information");
                C.WriteLine();
                C.Write(Environment.CurrentDirectory + ">");
            }
        }

        /// <summary>
        /// Stops the plugin
        /// </summary>
        public void Stop()
        {
            cont = false;

            if (!T.Join(2000))
            {
                try
                {
                    T.Abort();
                }
                catch
                {
                }
            }
            T = null;
        }

        /// <summary>
        /// Used to receive the command and help messages from plugins
        /// </summary>
        /// <param name="source">source plugin</param>
        /// <param name="Message">message</param>
        public void RecMessage(IPlugin source, string Message)
        {
            if (Message.StartsWith("GETCOMMAND:"))
            {
                PluginCommands.Add(Message.Substring(11), source);
            }
            else if (Message.StartsWith("GETHELP:"))
            {
                PluginHelp.Add(source, Message.Substring(8));
            }
        }

        /// <summary>
        /// receives messages in binary. (Not used here)
        /// </summary>
        /// <param name="source">(Not used here)</param>
        /// <param name="Message">(Not used here)</param>
        /// <param name="index">(Not used here)</param>
        /// <param name="Length">(Not used here)</param>
        public void RecMessage(IPlugin source, byte[] Message, int index, int Length)
        {
        }

        /// <summary>
        /// Thread loop
        /// </summary>
        private void run()
        {
#if DEBUG
            //Environment.CurrentDirectory = @"C:\";
#endif

            hookEvent = true;

            C.Flush();
            C.Clear();
            C.WriteLine("Type HELP for more information");
            C.WriteLine();
            C.Write(Environment.CurrentDirectory + ">");
            string s = string.Empty;
            char c;
            cont = true;
            while (cont)
            {
                while (!C.KeyAvailable && cont)
                {
                    Thread.Sleep(100);
                }
                if (cont)
                {
                    c = (char)C.Read();
                    switch ((int)c)
                    {
                        case 27:
                            cont = false;
                            C.WriteLine();
                            C.Beep();
                            break;
                        case 8:
                            if (s.Length > 0)
                            {
                                C.Write("\x8 \x8");
                                s = s.Substring(0, s.Length - 1);
                            }
                            else
                            {
                                C.Beep();
                            }
                            break;
                        case 13:
                            C.WriteLine();
                            exec(s.Trim());
                            if (cont)
                            {
                                s = string.Empty;
                                C.Write(Environment.CurrentDirectory + ">");
                            }
                            break;
                        default:
                            C.Write(c);
                            s += c;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// executes a given command
        /// </summary>
        /// <param name="s">command</param>
        private void exec(string s)
        {
            switch (s.Trim().ToUpper())
            {
                case "":
                    return;
                case "CLS":
                    C.Clear();
                    return;
                case "HELP":
                    C.WriteLine("Internal Commands:");
                    C.WriteLine("DIR   - Show possible files to execute");
                    C.WriteLine("CLS   - Clear screen");
                    C.WriteLine("LOAD  - Loads the specified file as plugin (source or DLL)");
                    C.WriteLine("COMP  - Compiles the specified source file into a DLL");
                    C.WriteLine("TYPE  - sends a file as is to the terminal");
                    C.WriteLine("CD    - change directory");
                    C.WriteLine("HELP  - Shows this message");
                    C.WriteLine("EXIT  - Closes this plugin");
                    C.WriteLine();
                    C.WriteLine("Loaded Plugin commands:");
                    foreach(string pKey in PluginCommands.Keys)
                    {
                        C.WriteLine("{0} - {1}", pKey.ToUpper(), PluginHelp[PluginCommands[pKey]]);
                    }
                    C.WriteLine();
                    return;
                case "DIR":
                    List<string> TempList = new List<string>();
                    TempList.AddRange(Directory.GetDirectories(Environment.CurrentDirectory));
                    TempList.Add(string.Empty);
                    TempList.AddRange(Directory.GetFiles(Environment.CurrentDirectory));
                    printList(TempList.ToArray());
                    TempList.Clear();
                    C.WriteLine();
                    return;
                case "EXIT":
                    cont = false;
                    return;
            }
            if ((s.ToUpper().StartsWith("COMP ")))
            {
                if (File.Exists(s.Substring(5)))
                {
                    try
                    {
                        PluginManager.Compile(File.ReadAllText(s.Substring(5)), s.Substring(5, s.LastIndexOf('.') - 5) + ".dll");
                        C.WriteLine("Successfully compiled");
                    }
                    catch (Exception ex)
                    {
                        C.WriteLine("Cannot compile. Error:\r\n{0}", ex.Message);
                    }
                }
                else
                {
                    C.WriteLine("File not found");
                }
                return;
            }
            if ((s.ToUpper().StartsWith("LOAD ")))
            {
                if (File.Exists(s.Substring(5)))
                {
                    IPlugin P = null;
                    try
                    {
                        if (s.ToLower().EndsWith(".dll"))
                        {
                            P = PluginManager.LoadPluginLib(s.Substring(5));
                        }
                        else
                        {
                            P = PluginManager.LoadPlugin(s.Substring(5));
                        }
                        if (P != null)
                        {
                            P.RecMessage(this, "GETCOMMAND");
                            P.RecMessage(this, "GETHELP");
                            C.WriteLine("Plugin loaded");
                        }
                        else
                        {
                            C.WriteLine("Not a plugin: {0}", s.Substring(5));
                        }
                    }
                    catch (Exception ex)
                    {
                        if (PluginManager.LastErrors != null)
                        {
                            C.WriteLine("Compiler Error!");
                            foreach (CompilerError EE in PluginManager.LastErrors)
                            {
                                C.WriteLine("[{0};{1}] {2} {3}", EE.Line, EE.Column, EE.ErrorNumber, EE.ErrorText);
                            }
                        }
                        else
                        {
                            C.WriteLine("Error: {0}", ex.Message);
                        }
                    }
                }
                else
                {
                    C.WriteLine("File not found");
                }
                return;
            }
            if ((s.ToUpper().StartsWith("TYPE ")))
            {
                if (File.Exists(s.Substring(5)))
                {
                    byte[] bb = new byte[80];
                    int readed = 0;
                    FileStream S;

                    try
                    {
                        S = File.OpenRead(s.Substring(5));
                    }
                    catch (Exception ex)
                    {
                        C.WriteLine("Error: {0}", ex.Message);
                        return;
                    }
                    do
                    {
                        C.Write(bb, 0, readed = S.Read(bb, 0, bb.Length));
                    } while (readed > 0);
                    S.Close();
                    S.Dispose();
                }
                else
                {
                    C.WriteLine("File not found: {0}", s.Substring(5));
                }
                return;
            }
            if ((s.ToUpper().StartsWith("CD")))
            {
                if (s.Length > 2)
                {
                    try
                    {
                        Environment.CurrentDirectory = Path.Combine(Environment.CurrentDirectory, s.Substring(3));
                    }
                    catch (Exception ex)
                    {
                        C.WriteLine("Error: {0}", ex.Message);
                    }
                }
                else
                {
                    C.WriteLine(Environment.CurrentDirectory);
                }
                return;
            }
            foreach (string cmd in PluginCommands.Keys)
            {
                if (s.ToUpper() == cmd.ToUpper())
                {
                    IPlugin P = PluginCommands[cmd];
                    P.Start(C);
                    hookEvent = false;
                    while (cont && P.IsRunning)
                    {
                        P.Block(100);
                    }
                    P.Stop();
                    hookEvent = true;
                    return;
                }
                else if (s.ToUpper().StartsWith(cmd.ToUpper()+" "))
                {
                    IPlugin P = PluginCommands[cmd];
                    P.RecMessage(this, s.Substring(s.IndexOf(' ') + 1));
                    P.Start(C);
                    while (cont && P.IsRunning)
                    {
                        P.Block(100);
                    }
                    P.Stop();
                    return;
                }
            }


            C.WriteLine("Command not found/invalid '{0}'", s);
        }

        /// <summary>
        /// blocks the caller until thread exit
        /// </summary>
        public void Block()
        {
            T.Join();
        }

        /// <summary>
        /// Blocks the caller until thread exit or the specified time
        /// </summary>
        /// <param name="Timeout">maximum time to wait</param>
        /// <returns>true, if exited</returns>
        public bool Block(int Timeout)
        {
            return T.Join(Timeout);
        }

        /// <summary>
        /// prints a list of directories and files in multiple autodetected columns
        /// </summary>
        /// <param name="List">List to print</param>
        private void printList(string[] List)
        {
            bool dir = true;
            int PAD = 2;
            int len = 0;
            int pos = 0;

            //detects item length
            for (int i = 0; i < List.Length; i++)
            {
                if (!string.IsNullOrEmpty(List[i]))
                {
                    List[i] = List[i].Substring(List[i].LastIndexOf(Path.DirectorySeparatorChar) + 1);
                    if (List[i].Length > 79)
                    {
                        List[i] = List[i].Substring(0, 76) + "...";
                    }
                    if (List[i].Length > len)
                    {
                        len = List[i].Length + PAD;
                    }
                }
            }
            //lists all items
            foreach (string s in List)
            {
                //directories and files are seperated into two groubs by an
                //empty entry in the list.
                if (string.IsNullOrEmpty(s))
                {
                    dir=false;
                }
                else
                {
                    if (pos + len > 79)
                    {
                        C.WriteLine();
                        pos = 0;
                    }
                    if (dir)
                    {
                        C.setAttribute(VTconsole.CharAttribute.Inverse);
                        C.Write(s);
                        C.setAttribute(VTconsole.CharAttribute.Reset);
                        C.Write(string.Empty.PadRight(len - s.Length));
                    }
                    else
                    {
                        C.Write(s.PadRight(len));
                    }
                    pos += len;
                }
            }
            if (pos > 0)
            {
                C.WriteLine();
            }
        }

        /// <summary>
        /// human readable name for the plugin
        /// </summary>
        /// <returns>Plugin name</returns>
        public override string ToString()
        {
            return "INTERNAL:SimpleConsole";
        }
    }
}
