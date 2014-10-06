using System;
using System.IO.Ports;
using VT100.PluginSystem;
using System.Threading;

namespace VT100
{
    class Program
    {
        static int Main(string[] args)
        {
            SerialPort SP = new SerialPort("COM3", 9600, Parity.None, 8, StopBits.One);
            SP.Handshake = Handshake.None;
            SP.NewLine = "\r"; //VT100 return key is '\r'
            SP.Open();
            VTconsole C = new VTconsole(SP);

            if (C.State != VTconsole.TerminalState.Ready)
            {
                Console.WriteLine("Waiting for terminal ready on COM3...");
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

            return 0;
        }

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
