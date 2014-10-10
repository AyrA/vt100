using System;
using System.IO.Ports;
using VT100.PluginSystem;
using System.Threading;
using System.IO;
using System.CodeDom.Compiler;

namespace VT100
{
    class Program
    {
        /// <summary>
        /// Contains full serial port information
        /// </summary>
        private struct SerialOptions
        {
            public string Portname;
            public int Baudrate;
            public int Databits;
            public StopBits Stopbits;
            public Parity Parity;
            public Handshake Handshake;
            public bool valid;
        }

        /// <summary>
        /// main entry point
        /// </summary>
        /// <param name="args">Arguments</param>
        /// <returns>some sort of weird exit code</returns>
        static int Main(string[] args)
        {
#if DEBUG
            //while debugging we supply fake arguments matching our serial port
            //args is different from C++ "argv", as it does not contains the
            //executable call itself, so a length of 0 means 0 arguments.
            if (args.Length == 0)
            {
                args = new string[] { "COM3,9600,8,1,n,n" };
                //args = new string[] { "net.cs" };
            }
#endif
            SerialOptions SO = parseParams(args);

            if (SO.valid)
            {
                SerialPort SP = new SerialPort(SO.Portname, SO.Baudrate, SO.Parity, SO.Databits, SO.Stopbits);
                SP.Handshake = SO.Handshake;
                SP.NewLine = "\r"; //VT100 return key is '\r'
                SP.Open();
                VTconsole C = new VTconsole(SP);

                if (C.State != VTconsole.TerminalState.Ready)
                {
                    Console.WriteLine("Waiting for terminal ready on {0}...", SO.Portname);
                    while (C.State != VTconsole.TerminalState.Ready)
                    {
                        if (consoleExit())
                        {
                            return 1;
                        }
                        Thread.Sleep(2000);
                    }
                    Console.WriteLine("Terminal ready");
                }

                IPlugin P = null;

                if (File.Exists("INIT.cs") || File.Exists("INIT.dll"))
                {
                    Console.WriteLine("Found 'INIT' Plugin. Loading external plugin...");
                    try
                    {
                        P = File.Exists("INIT.cs") ? PluginManager.LoadPlugin("INIT.cs") : PluginManager.LoadPluginLib("INIT.dll");
                        if (P == null)
                        {
                            throw new Exception("Cannot run INIT Plugin, see errors below");
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("Loading failed.\r\nError: {0}", ex.Message);
                    }
                    if (P == null)
                    {
                        if (PluginManager.LastErrors != null)
                        {
                            foreach (CompilerError EE in PluginManager.LastErrors)
                            {
                                C.WriteLine("[{0};{1}] {2} {3}", EE.Line, EE.Column, EE.ErrorNumber, EE.ErrorText);
                            }
                        }
                        Console.WriteLine("Defaulting to main plugin...");
                        P = new DirList();
                    }
                }
                else
                {
                    Console.WriteLine("Starting main Plugin...");
                    P = new DirList();

                }
                P.Start(C);

                while (!consoleExit() && P.IsRunning)
                {
                    Thread.Sleep(500);
                }
                P.Stop();

                C.Clear();
                SP.Close();

                Console.WriteLine("Application terminated");
            }
            else
            {
                if (args.Length > 0 && File.Exists(args[0]))
                {
                    try
                    {
                        PluginManager.Compile(File.ReadAllText(args[0]), args[0].Substring(0, args[0].LastIndexOf('.')) + ".dll");
                        Console.WriteLine("File compiled");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error compiling {0}.\r\n{1}", args[0], ex.Message);
                    }
                }
                else
                {
                    help();
                }
            }
            return 0;
        }

        /// <summary>
        /// shows the command line help
        /// </summary>
        private static void help()
        {
            //display help
            Console.WriteLine(@"VT100 Terminal Helper Application
vt100.exe File | Port[,[Baud][,[Databits][,[Stopbits]
                     [,[Parity][,[Handshake]]]]]]");
            Console.WriteLine(@"
Port      - Name of Port (COM1, COM2, ...)
Baud      - Baud Rate (Defaults to 9600)
Databits  - Databits (5-8, defaults to 8)
Stopbits  - Stop bits: 0, 1, 1.5, 2 (defaults to 1)
Parity    - [N]one, [O]dd, [E]ven, [S]pace, [M]ark (Defaults to N)
Handshake - Protocol handshake: [N]one, [X]on/Xoff, [R]ts, [B]oth (Default: N)

File      - Instead of parameters, supply a file name of a script and it will
            be compiled into a DLL file.

To supply parameters, do not use spaces and specify all parameters in front
of it. The parameter list looks complicated but it indicates, that you can
specify a row of commas as default. If you want to specify COM3 but only want
to specify the parity, specify 'COM3,,,,O'");
        }

        /// <summary>
        /// Extracts serial port options from arguments
        /// </summary>
        /// <param name="args">arguments</param>
        /// <returns>SerialOptions struct filled out as good as possible</returns>
        private static SerialOptions parseParams(string[] args)
        {
            SerialOptions SO = new SerialOptions();

            //defaults
            SO.Baudrate = 9600;
            SO.Databits = 8;
            SO.Stopbits = StopBits.One;
            SO.Parity = Parity.None;
            SO.Handshake = Handshake.None;
            SO.valid = false;

            if (args.Length > 0)
            {
                args = args[0].Split(',');
                string[] ports = SerialPort.GetPortNames();
                foreach (string port in ports)
                {
                    if (port.ToLower() == args[0].ToLower())
                    {
                        SO.Portname = port;
                        SO.valid = true;
                    }
                }
            }
            if (args.Length > 1 && SO.valid)
            {
                SO.valid = int.TryParse(args[1], out SO.Baudrate);
            }
            if (args.Length > 2 && SO.valid)
            {
                SO.valid = int.TryParse(args[2], out SO.Databits);
            }
            if (args.Length > 3 && SO.valid)
            {
                SO.valid = true;
                switch(args[3])
                {
                    case "0":
                        SO.Stopbits = StopBits.None;
                        break;
                    case "1":
                        SO.Stopbits = StopBits.One;
                        break;
                    case "1.5":
                        SO.Stopbits = StopBits.OnePointFive;
                        break;
                    case "2":
                        SO.Stopbits = StopBits.Two;
                        break;
                    default:
                        SO.valid = false;
                        break;
                }
            }
            if (args.Length > 4 && SO.valid)
            {
                SO.valid = true;
                switch (args[4].ToUpper())
                {
                    case "N":
                        SO.Parity = Parity.None;
                        break;
                    case "E":
                        SO.Parity = Parity.Even;
                        break;
                    case "O":
                        SO.Parity = Parity.Odd;
                        break;
                    case "S":
                        SO.Parity = Parity.Space;
                        break;
                    case "M":
                        SO.Parity = Parity.Mark;
                        break;
                    default:
                        SO.valid = false;
                        break;
                }
            }
            if (args.Length > 5 && SO.valid)
            {
                SO.valid = true;
                switch (args[5].ToUpper())
                {
                    case "N":
                        SO.Handshake = Handshake.None;
                        break;
                    case "X":
                        SO.Handshake = Handshake.XOnXOff;
                        break;
                    case "R":
                        SO.Handshake = Handshake.RequestToSend;
                        break;
                    case "B":
                        SO.Handshake = Handshake.RequestToSendXOnXOff;
                        break;
                    default:
                        SO.valid = false;
                        break;
                }
            }
            return SO;
        }

        /// <summary>
        /// checks for esc key
        /// </summary>
        /// <returns>true, if esc was pressed</returns>
        private static bool consoleExit()
        {
            while (Console.KeyAvailable)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Escape)
                {
                    return true;
                }
                else
                {
                    Console.Beep();
                }
            }
            return false;
        }
    }
}
