using System;
using System.IO;
using System.IO.Ports;
using System.Text;

namespace VT100
{
    /// <summary>
    /// terminal state change delegate
    /// </summary>
    /// <param name="State">Terminal state</param>
    public delegate void TerminalStateChangedHandler(VT100.VTconsole.TerminalState State);

    /// <summary>
    /// provides VT100 functionality
    /// </summary>
    public class VTconsole
    {
        /// <summary>
        /// Terminal event if the state changes
        /// </summary>
        public event TerminalStateChangedHandler TerminalStateChanged;

        /// <summary>
        /// Possible terminal states
        /// </summary>
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

        /// <summary>
        /// Text Attributes
        /// </summary>
        [Flags]
        public enum CharAttribute
        {
            /// <summary>
            /// nothing (regular text)
            /// </summary>
            Reset = 0,
            /// <summary>
            /// underline line
            /// </summary>
            Underline = 1,
            /// <summary>
            /// inverse B/W
            /// </summary>
            Inverse = 2,
            /// <summary>
            /// highlight (text is white instead gray)
            /// </summary>
            Highligt = 4,
            /// <summary>
            /// text blinks
            /// </summary>
            Blink = 8
        }

        /// <summary>
        /// Text size constant
        /// </summary>
        public enum Size
        {
            /// <summary>
            /// Double row size, top half
            /// </summary>
            DoubleTopHalf,
            /// <summary>
            /// double row size, bottom half
            /// </summary>
            DoubleBottomHalf,
            /// <summary>
            /// normal text
            /// </summary>
            Normal,
            /// <summary>
            /// double width, single height
            /// </summary>
            DoubleWide
        }

        /// <summary>
        /// Escape char (N° 27)
        /// </summary>
        private const char ESC = '\x1B';
        /// <summary>
        /// CRLF string
        /// </summary>
        private const string CRLF = "\r\n";
        /// <summary>
        /// Last terminal state
        /// </summary>
        private TerminalState lastState;
        /// <summary>
        /// Terminal
        /// </summary>
        private SerialPort Terminal;

        /// <summary>
        /// checks if data is available to read
        /// </summary>
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

        /// <summary>
        /// Gets the terminal state
        /// </summary>
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

        /// <summary>
        /// Initializes a new VT100 terminal with the given Serial Port
        /// </summary>
        /// <param name="InOut">Serial Port. Automatically opened if closed</param>
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

        /// <summary>
        /// to prevent NullReference
        /// </summary>
        /// <param name="State"></param>
        void VTconsole_TerminalStateChanged(TerminalState State)
        {
            //Console.WriteLine("State={0}", State);
        }

        /// <summary>
        /// check for terminal ready on pin change
        /// </summary>
        /// <param name="sender">Serial port</param>
        /// <param name="e">Pin arguments</param>
        private void Terminal_PinChanged(object sender, SerialPinChangedEventArgs e)
        {
            if (State != lastState)
            {
                lastState = State;
                TerminalStateChanged(State);
            }
        }

        /// <summary>
        /// sends a terminal code
        /// </summary>
        /// <param name="code">terminal code</param>
        private void sendCode(string code)
        {
            sendESC(string.Format("[{0}", code));
        }

        /// <summary>
        /// sends an escape code
        /// </summary>
        /// <param name="code">Escape code</param>
        private void sendESC(string code)
        {
            Terminal.Write(string.Format("{0}{1}", ESC, code));
        }

        /// <summary>
        /// beeps
        /// </summary>
        public void Beep()
        {
            Terminal.BaseStream.WriteByte(7);
        }

        /// <summary>
        /// clears the terminal and sets the cursor to (0,0)
        /// </summary>
        public void Clear()
        {
            sendCode("2J");
            sendCode("H");
        }

        /// <summary>
        /// reads a single byte. May return -1 if none is available
        /// </summary>
        /// <returns>byte read</returns>
        public int Read()
        {
            return Terminal.ReadByte();
        }

        /// <summary>
        /// Reads a line of chars until \r is found
        /// </summary>
        /// <returns>line (without \r)</returns>
        public string ReadLine()
        {
            return Terminal.ReadLine();
        }

        /// <summary>
        /// Sets the cursor position
        /// </summary>
        /// <param name="left">Left (0 based)</param>
        /// <param name="top">Top (0 based)</param>
        public void SetCursorPosition(int left, int top)
        {
            sendCode(string.Format("{1};{0}H", left, top));
        }

        /// <summary>
        /// Sets a text attribute. previously set attributes are cleared
        /// </summary>
        /// <param name="C">Text attribute (can be combined)</param>
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

        /// <summary>
        /// Sets the text size of the VT100 terminal
        /// </summary>
        /// <param name="cSize">Text size constant</param>
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

        /// <summary>
        /// Flushes the Input stream and returns the number of bytes ignored.
        /// </summary>
        /// <returns>number of bytes ignored</returns>
        public int Flush()
        {
            int i = Terminal.BytesToRead;
            Terminal.DiscardInBuffer();
            return i;
        }

        /// <summary>
        /// Writes the given data as string to the terminal
        /// </summary>
        /// <param name="value">data</param>
        public void Write(bool value)
        {
            Terminal.Write(value.ToString());
        }
        /// <summary>
        /// Writes the given data as string to the terminal
        /// </summary>
        /// <param name="value">data</param>
        public void Write(char value)
        {
            Terminal.Write(value.ToString());
        }
        /// <summary>
        /// Writes the given chars to the terminal
        /// </summary>
        /// <param name="buffer">data</param>
        public void Write(char[] buffer)
        {
            Terminal.Write(buffer, 0, buffer.Length);
        }
        /// <summary>
        /// Writes the given data as string to the terminal
        /// </summary>
        /// <param name="value">data</param>
        public void Write(decimal value)
        {
            Terminal.Write(value.ToString());
        }
        /// <summary>
        /// Writes the given data as string to the terminal
        /// </summary>
        /// <param name="value">data</param>
        public void Write(double value)
        {
            Terminal.Write(value.ToString());
        }
        /// <summary>
        /// Writes the given data as string to the terminal
        /// </summary>
        /// <param name="value">data</param>
        public void Write(float value)
        {
            Terminal.Write(value.ToString());
        }
        /// <summary>
        /// Writes the given data as string to the terminal
        /// </summary>
        /// <param name="value">data</param>
        public void Write(int value)
        {
            Terminal.Write(value.ToString());
        }
        /// <summary>
        /// Writes the given data as string to the terminal
        /// </summary>
        /// <param name="value">data</param>
        public void Write(long value)
        {
            Terminal.Write(value.ToString());
        }
        /// <summary>
        /// Writes the given data as string to the terminal
        /// </summary>
        /// <param name="value">data</param>
        public void Write(object value)
        {
            Terminal.Write(value.ToString());
        }
        /// <summary>
        /// Writes the given string to the terminal
        /// </summary>
        /// <param name="value">data</param>
        public void Write(string value)
        {
            Terminal.Write(value);
        }
        /// <summary>
        /// Writes the given data as string to the terminal
        /// </summary>
        /// <param name="value">data</param>
        public void Write(uint value)
        {
            Terminal.Write(value.ToString());
        }
        /// <summary>
        /// Writes the given data as string to the terminal
        /// </summary>
        /// <param name="value">data</param>
        public void Write(ulong value)
        {
            Terminal.Write(value.ToString());
        }
        /// <summary>
        /// Writes the given string to the terminal
        /// </summary>
        public void Write(string format, object arg0)
        {
            Terminal.Write(string.Format(format, arg0));
        }
        /// <summary>
        /// Writes the given string to the terminal
        /// </summary>
        public void Write(string format, params object[] arg)
        {
            Terminal.Write(string.Format(format, arg));
        }
        /// <summary>
        /// Writes the given data to the terminal
        /// </summary>
        public void Write(char[] buffer, int index, int count)
        {
            Terminal.Write(buffer, index, count);
        }
        /// <summary>
        /// Writes the given data to the terminal
        /// </summary>
        public void Write(byte[] buffer, int index, int count)
        {
            Terminal.BaseStream.Write(buffer, index, count);
        }
        /// <summary>
        /// Writes the given string to the terminal
        /// </summary>
        public void Write(string format, object arg0, object arg1)
        {
            Terminal.Write(string.Format(format, arg0, arg1));
        }
        /// <summary>
        /// Writes the given string to the terminal
        /// </summary>
        public void Write(string format, object arg0, object arg1, object arg2)
        {
            Terminal.Write(string.Format(format, arg0, arg1, arg2));
        }
        /// <summary>
        /// Writes the given string to the terminal
        /// </summary>
        public void Write(string format, object arg0, object arg1, object arg2, object arg3)
        {
            Terminal.Write(string.Format(format, arg0, arg2, arg3));
        }

        /// <summary>
        /// Writes a CRLF to the terminal
        /// </summary>
        public void WriteLine()
        {
            Terminal.Write(CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(bool value)
        {
            Terminal.Write(value.ToString() + CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(char value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(char[] buffer)
        {
            Terminal.WriteLine(new string(buffer) + CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(decimal value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(double value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(float value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(int value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(long value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(object value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(string value)
        {
            Terminal.WriteLine(value + CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(uint value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(ulong value)
        {
            Terminal.WriteLine(value.ToString() + CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(string format, object arg0)
        {
            Terminal.WriteLine(string.Format(format + CRLF, arg0));
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(string format, params object[] arg)
        {
            Terminal.WriteLine(string.Format(format + CRLF, arg));
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(char[] buffer, int index, int count)
        {
            Terminal.WriteLine(new string(buffer, index, count) + CRLF);
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(string format, object arg0, object arg1)
        {
            Terminal.WriteLine(string.Format(format, arg0, arg1));
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(string format, object arg0, object arg1, object arg2)
        {
            Terminal.WriteLine(string.Format(format + CRLF, arg0, arg1, arg2));
        }
        /// <summary>
        /// Writes the given data with a CRLF to the terminal
        /// </summary>
        public void WriteLine(string format, object arg0, object arg1, object arg2, object arg3)
        {
            Terminal.WriteLine(string.Format(format + CRLF, arg0, arg1, arg2, arg3));
        }
    }
}
