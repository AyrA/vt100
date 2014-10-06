using System;
using System.IO;
using System.IO.Ports;
using System.Text;

namespace VT100
{
    public delegate void TerminalStateChangedHandler(VT100.VTconsole.TerminalState State);

    public class VTconsole
    {
        public event TerminalStateChangedHandler TerminalStateChanged;

        public enum TerminalState
        {
            /// <summary>
            /// Terminal is offline
            /// </summary>
            Offline,
            /// <summary>
            /// Terminal is starting or stopping
            /// </summary>
            Intermediate,
            /// <summary>
            /// Terminal is ready
            /// </summary>
            Ready
        }

        [Flags]
        public enum CharAttribute
        {
            Reset = 0,
            Underline = 1,
            Inverse = 2,
            Highligt = 4,
            Blink = 8
        }

        public enum Size
        {
            DoubleTopHalf,
            DoubleBottomHalf,
            Normal,
            DoubleWide
        }

        private const char ESC = '\x1B';
        private const string CRLF = "\r\n";
        private TerminalState lastState;
        private SerialPort Terminal;

        public bool KeyAvailable
        {
            get
            {
                try
                {
                    return Terminal.BytesToRead > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        public TerminalState State
        {
            get
            {
                if (Terminal.CDHolding && Terminal.CtsHolding && Terminal.DsrHolding)
                {
                    return TerminalState.Ready;
                }
                if (Terminal.CDHolding)
                {
                    return TerminalState.Intermediate;
                }
                return TerminalState.Offline;
            }
        }

        public VTconsole(SerialPort InOut)
        {
            Terminal = InOut;

            lastState = State;
            TerminalStateChanged += new TerminalStateChangedHandler(VTconsole_TerminalStateChanged);
            Terminal.DtrEnable = true;
            Terminal.RtsEnable = true;
            Terminal.PinChanged += new SerialPinChangedEventHandler(Terminal_PinChanged);
            if (!Terminal.IsOpen)
            {
                Terminal.Open();
            }
        }

        void VTconsole_TerminalStateChanged(TerminalState State)
        {
            //Console.WriteLine("State={0}", State);
        }

        private void Terminal_PinChanged(object sender, SerialPinChangedEventArgs e)
        {
            if (State != lastState)
            {
                lastState = State;
                TerminalStateChanged(State);
            }
        }

        private void sendCode(string code)
        {
            sendESC(string.Format("[{0}", code));
        }

        private void sendESC(string code)
        {
            Terminal.Write(string.Format("{0}{1}", ESC, code));
        }

        public void Beep()
        {
            Terminal.BaseStream.WriteByte(7);
        }

        public void Clear()
        {
            sendCode("2J");
            sendCode("H");
        }

        public int Read()
        {
            return Terminal.ReadByte();
        }

        public string ReadLine()
        {
            return Terminal.ReadLine();
        }

        public void SetCursorPosition(int left, int top)
        {
            sendCode(string.Format("{1};{0}H", left, top));
        }

        public void setAttribute(CharAttribute C)
        {
            string s = string.Empty;
            if ((C & CharAttribute.Blink) == CharAttribute.Blink)
            {
                s += ";5";
            }
            if ((C & CharAttribute.Highligt) == CharAttribute.Highligt)
            {
                s += ";1";
            }
            if ((C & CharAttribute.Inverse) == CharAttribute.Inverse)
            {
                s += ";7";
            }
            if ((C & CharAttribute.Underline) == CharAttribute.Underline)
            {
                s += ";4";
            }
            sendCode("0" + s + "m");
        }

        public void setSize(Size cSize)
        {
            string s = string.Empty;
            switch (cSize)
            {
                case Size.DoubleTopHalf:
                    s = "3";
                    break;
                case Size.DoubleBottomHalf:
                    s = "4";
                    break;
                case Size.DoubleWide:
                    s = "5";
                    break;
                case Size.Normal:
                    s = "6";
                    break;
            }
            sendESC("#" + s);
        }

        public void Write(bool value)
        {
            Terminal.Write(value.ToString());
        }
        public void Write(char value)
        {
            Terminal.Write(value.ToString());
        }
        public void Write(char[] buffer)
        {
            Terminal.Write(buffer, 0, buffer.Length);
        }
        public void Write(decimal value)
        {
            Terminal.Write(value.ToString());
        }
        public void Write(double value)
        {
            Terminal.Write(value.ToString());
        }
        public void Write(float value)
        {
            Terminal.Write(value.ToString());
        }
        public void Write(int value)
        {
            Terminal.Write(value.ToString());
        }
        public void Write(long value)
        {
            Terminal.Write(value.ToString());
        }
        public void Write(object value)
        {
            Terminal.Write(value.ToString());
        }
        public void Write(string value)
        {
            Terminal.Write(value);
        }
        public void Write(uint value)
        {
            Terminal.Write(value.ToString());
        }
        public void Write(ulong value)
        {
            Terminal.Write(value.ToString());
        }
        public void Write(string format, object arg0)
        {
            Terminal.Write(string.Format(format, arg0));
        }
        public void Write(string format, params object[] arg)
        {
            Terminal.Write(string.Format(format, arg));
        }
        public void Write(char[] buffer, int index, int count)
        {
            Terminal.Write(buffer, index, count);
        }
        public void Write(byte[] buffer, int index, int count)
        {
            Terminal.BaseStream.Write(buffer, index, count);
        }
        public void Write(string format, object arg0, object arg1)
        {
            Terminal.Write(string.Format(format, arg0, arg1));
        }
        public void Write(string format, object arg0, object arg1, object arg2)
        {
            Terminal.Write(string.Format(format, arg0, arg1, arg2));
        }
        public void Write(string format, object arg0, object arg1, object arg2, object arg3)
        {
            Terminal.Write(string.Format(format, arg0, arg2, arg3));
        }

        public void WriteLine()
        {
            Terminal.Write(CRLF);
        }
        public void WriteLine(bool value)
        {
            Terminal.Write(value.ToString() + CRLF);
        }
        public void WriteLine(char value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        public void WriteLine(char[] buffer)
        {
            Terminal.WriteLine(new string(buffer) + CRLF);
        }
        public void WriteLine(decimal value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        public void WriteLine(double value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        public void WriteLine(float value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        public void WriteLine(int value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        public void WriteLine(long value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        public void WriteLine(object value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        public void WriteLine(string value)
        {
            Terminal.WriteLine(value + CRLF);
        }
        public void WriteLine(uint value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        public void WriteLine(ulong value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        public void WriteLine(string format, object arg0)
        {
            Terminal.WriteLine(string.Format(format + CRLF, arg0));
        }
        public void WriteLine(string format, params object[] arg)
        {
            Terminal.WriteLine(string.Format(format + CRLF, arg));
        }
        public void WriteLine(char[] buffer, int index, int count)
        {
            Terminal.WriteLine(new string(buffer, index, count) + CRLF);
        }
        public void WriteLine(string format, object arg0, object arg1)
        {
            Terminal.WriteLine(string.Format(format, arg0, arg1));
        }
        public void WriteLine(string format, object arg0, object arg1, object arg2)
        {
            Terminal.WriteLine(string.Format(format + CRLF, arg0, arg1, arg2));
        }
        public void WriteLine(string format, object arg0, object arg1, object arg2, object arg3)
        {
            Terminal.WriteLine(string.Format(format + CRLF, arg0, arg1, arg2, arg3));
        }

    }
}
