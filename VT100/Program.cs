using System;
using System.IO.Ports;
using VT100.PluginSystem;
using System.Threading;

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

                Console.WriteLine("Starting main Plugin...");
                DirList D = new DirList();

                D.Start(C);

                Console.WriteLine("Main plugin started");

                while (!consoleExit() && D.IsRunning)
                {
                    Thread.Sleep(500);
                }
                D.Stop();

                C.Clear();
                SP.Close();

                Console.WriteLine("Application terminated");
            }
            else
            {
                //display help
                Console.WriteLine("vt100.exe Port[,[Baud][,[Databits][,[Stopbits][,[Parity][,[Handshake]]]]]]");
                Console.WriteLine(@"
Port      - Name of Port (COM1, COM2, ...)
Baud      - Baud Rate (Defaults to 9600)
Databits  - Databits (5-8, defaults to 8)
Stopbits  - Stop bits: 0, 1, 1.5, 2 (defaults to 1)
Parity    - [N]one, [O]dd, [E]ven, [S]pace, [M]ark (Defaults to N)
Handshake - Protocol handshake: [N]one, [X]on/Xoff, [R]ts, [B]oth (Default: N)

To supply parameters, do not use spaces and specify all parameters in front
of it. The parameter list looks complicated but it indicates, that you can
specify a row of commas as default. If you want to specify COM3 but only want
to specify the parity, specify 'COM3,,,,O'");
            }
            return 0;
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
