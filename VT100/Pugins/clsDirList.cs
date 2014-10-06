using System;
using System.IO;
using VT100;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Threading;

namespace VT100.PluginSystem
{
    public class DirList : IPlugin
    {
        private Thread T;
        private VTconsole C;
        private bool cont;
        private bool hookEvent;
        private Dictionary<string, IPlugin> PluginCommands;
        private Dictionary<IPlugin, string> PluginHelp;

        public DirList()
        {
            PluginCommands = new Dictionary<string, IPlugin>();
            PluginHelp = new Dictionary<IPlugin, string>();
        }

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

        void C_TerminalStateChanged(VTconsole.TerminalState State)
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

        public void RecMessage(IPlugin source, byte[] Message, int index, int Length)
        {
        }

        private void run()
        {
            Environment.CurrentDirectory = @"C:\";

            hookEvent = true;

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

        private void exec(string s)
        {
            switch (s.ToUpper())
            {
                case "CLS":
                    C.Clear();
                    return;
                case "HELP":
                    C.WriteLine("Internal Commands:");
                    C.WriteLine("DIR   - Show possible files to execute");
                    C.WriteLine("CLS   - Clear screen");
                    C.WriteLine("LOAD  - Loads the specified file as plugin");
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

            if ((s.ToUpper().StartsWith("LOAD ")))
            {
                if (File.Exists(s.Substring(5)))
                {
                    try
                    {
                        IPlugin P = PluginManager.LoadPlugin(s.Substring(5));
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
                    catch(Exception ex)
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

        public void Block()
        {
            T.Join();
        }

        public bool Block(int Timeout)
        {
            return T.Join(Timeout);
        }

        private void printList(string[] List)
        {
            bool dir = true;
            int PAD = 2;
            int len = 0;
            int pos = 0;
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
            foreach (string s in List)
            {
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

        public override string ToString()
        {
            return "INTERNAL:SimpleConsole";
        }
    }
}
