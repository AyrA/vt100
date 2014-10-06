using System;
using System.IO;
using VT100;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VT100.PluginSystem
{
    public class Network : IPlugin
    {
        private struct CONST
        {
            public const byte IAC = 255;
            public const byte DONT = 254;
            public const byte DO = 253;
            public const byte WONT = 252;
            public const byte WILL = 251;
            public const byte SB = 250;
            public const byte AYT = 246;
            public const byte NOP = 241;
            public const byte SE = 240;
            public const byte ECHO = 25;
            public const byte IS = 0;
            public const byte SEND = 1;
            public const byte TERMTYPE = 24;
            public const byte TERMSPEED = 32;
        }

        private Thread T;
        private VTconsole C;
        private bool cont;
        private string initC;

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

        public void Stop()
        {
            if (cont && T != null)
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
        }

        public void RecMessage(IPlugin source, string Message)
        {
            if (!cont)
            {
                if (Message == "GETCOMMAND")
                {
                    source.RecMessage(this, "GETCOMMAND:NET");
                }
                else if (Message == "GETHELP")
                {
                    source.RecMessage(this, "GETHELP:Poor mans telnet");
                }
                else
                {
                    initC = Message;
                }
            }
            else
            {
                throw new Exception("This plugin does not accepts messages if Start() has been called.");
            }
        }

        public void RecMessage(IPlugin source, byte[] Message, int index, int Length)
        {
            throw new Exception("This plugin does not accepts binary messages");
        }

        private void run()
        {
            string s = string.Empty;
            char c;
            cont = true;
            C.Clear();
            if (!string.IsNullOrEmpty(initC))
            {
                exec("OPEN " + initC);
            }
            C.WriteLine("Type HELP for more information");
            C.WriteLine();
            C.Write("NET>");
            while (cont)
            {
                while (cont && !C.KeyAvailable)
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
                                C.Write("NET>");
                            }
                            break;
                        default:
                            C.Write(c);
                            s += c;
                            break;
                    }
                }
            }
            T = null;
        }

        private void exec(string p)
        {
            if (p.ToUpper() == "HELP")
            {
                C.WriteLine("HELP                - Get Help");
                C.WriteLine("EXIT                - Exit plugin");
                C.WriteLine("OPEN Host[:Port]  - Connect to 'Domain' on port 'Port'");
                C.WriteLine("Port is optional and defaults to 23");
                C.WriteLine();
                C.WriteLine("Press [ESC] 3 times during a connection to close it.");
                C.WriteLine();
            }
            else if (p.ToUpper() == "EXIT")
            {
                cont = false;
            }
            else if (p.ToUpper() == "OPEN")
            {
                C.WriteLine("OPEN Host:[Port]");
                C.WriteLine("Opens a connection to the specified host");
                C.WriteLine("Port is optional and defaults to 23");
            }
            else if (p.ToUpper().StartsWith("OPEN "))
            {
                if (!p.Contains(":"))
                {
                    p += ":23";
                }
                string[] Parts = p.Substring(5).Split(':');
                IPAddress A = IPAddress.Any;
                ushort Port = ushort.MinValue;

                if (ushort.TryParse(Parts[1], out Port))
                {
                    C.WriteLine("Connecting...");
                    if (!IPAddress.TryParse(Parts[0], out A))
                    {
                        try
                        {
                            A = Dns.GetHostAddresses(Parts[0])[0];
                        }
                        catch
                        {
                            A = IPAddress.Any;
                            C.WriteLine("Cannot resolve host name '{0}'", Parts[0]);
                        }
                    }
                    if (!A.Equals(IPAddress.Any))
                    {
                        DoNetwork(A, Port);
                        C.WriteLine();
                    }
                }
                else
                {
                    C.WriteLine("specified Port is invalid");
                }
            }
            else
            {
                C.WriteLine("Command not understood. Type HELP for more information");
            }
        }

        private void DoNetwork(IPAddress A, ushort Port)
        {
            TcpClient Cli = new TcpClient();
            try
            {
                Cli.Connect(new IPEndPoint(A, Port));
            }
            catch
            {
                Cli = null;
            }
            if (Cli != null)
            {
                NetworkStream NS = new NetworkStream(Cli.Client, true);
                bool NetCont = true;
                byte buffer;
                int ESC = 0;
                while (NetCont && cont)
                {
                    while (NS.DataAvailable && !C.KeyAvailable)
                    {
                        buffer = (byte)NS.ReadByte();
                        if (buffer == CONST.IAC)
                        {
                            DoIAC(NS);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write("#");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            if (buffer <= ' ')
                            {
                                Console.Write('.');
                            }
                            else
                            {
                                Console.Write((char)buffer);
                            }
                            C.Write((char)buffer);
                        }
                    }
                    while (C.KeyAvailable)
                    {
                        buffer = (byte)C.Read();
                        if (buffer == 27)
                        {
                            if (++ESC == 3)
                            {
                                NetCont = false;
                            }
                        }
                        else
                        {
                            ESC = 0;
                            try
                            {
                                NS.WriteByte(buffer);
                            }
                            catch
                            {
                                NetCont = false;
                            }
                        }
                        Console.ForegroundColor = ConsoleColor.Green;
                        if (buffer <= ' ')
                        {
                            Console.Write('.');
                        }
                        else
                        {
                            Console.Write((char)buffer);
                        }
                    }
                    Thread.Sleep(100);
                }
                Console.ResetColor();
                NS.Close();
                NS.Dispose();
                Cli.Close();
                Cli = null;
                NS = null;
            }
            else
            {
                C.WriteLine("Can't connect to {0}:{1}", A, Port);
            }
        }

        private void DoIAC(NetworkStream NS)
        {
            byte[] b = new byte[] { 255, (byte)NS.ReadByte(), (byte)NS.ReadByte() };

            if (b[1] == CONST.DO)
            {
                switch (b[2])
                {
                    case CONST.TERMTYPE:
                    case CONST.TERMSPEED:
                        b[1] = CONST.WILL;
                        NS.Write(b, 0, 3);
                        break;
                    default:
                        b[1] = CONST.WONT;
                        NS.Write(b, 0, 3);
                        break;
                }
            }
            else if(b[1] == CONST.SB)
            {
                NS.ReadByte(); //SEND
                NS.ReadByte(); //IAC
                NS.ReadByte(); //SE
                switch (b[2])
                {
                    case CONST.TERMSPEED:
                        NS.Write(b, 0, 3); //send back IAC+SB
                        NS.WriteByte(CONST.IS);
                        b = Encoding.UTF8.GetBytes("9600,9600");
                        NS.Write(b, 0, b.Length); //send back "vt100"
                        NS.WriteByte(CONST.IAC);
                        NS.WriteByte(CONST.SE);
                        break;
                    case CONST.TERMTYPE:
                        NS.Write(b, 0, 3); //send back IAC+SB
                        NS.WriteByte(CONST.IS);
                        b = Encoding.UTF8.GetBytes("vt100");
                        NS.Write(b, 0, b.Length); //send back "vt100"
                        NS.WriteByte(CONST.IAC);
                        NS.WriteByte(CONST.SE);
                        break;
                    default:
                        break;
                }
            }
        }

        public void Block()
        {
            T.Join();
        }

        public bool Block(int Timeout)
        {
            return T.Join(Timeout);
        }

        public override string ToString()
        {
            return "INTERNAL:NET";
        }
    }
}
