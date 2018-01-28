using ax25lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Digipeater
{
    class Program
    {
        static SerialPort sp;
        const string MYCALL = "M0LTE-15";

        static void Main(string[] args)
        {
            using (sp = new SerialPort("COM2", 38400))
            {
                sp.Open();
                sp.ReadTimeout = 100;

                var buf = new List<byte>();

                bool midPacket = false;

                while (true)
                {
                    byte i = (byte)sp.ReadByte();

                    if (i == 0xc0)
                    {
                        if (buf.Count > 0)
                        {
                            if (buf.Last() != 0xc0) // ignore back-to-back FEND
                            {
                                // packet end
                                buf.Add(i);
                                Process(buf.ToArray());
                                buf.Clear();
                                midPacket = false;
                            }
                        }
                        else
                        {
                            // start of packet
                            midPacket = true;
                            buf.Add(i);
                        }
                    }
                    else if (midPacket)
                    {
                        buf.Add(i);
                    }
                    else
                    {
                        // garbage
                        Console.Write(Convert.ToChar(i));
                    }
                }
            }
        }

        static void Process(byte[] kissFrame)
        {
            var parseResult = Ax25Frame.TryParse(kissFrame, out Ax25Frame ax25Frame);
            if (!parseResult.Result)
            {
                Console.WriteLine("FAIL: " + parseResult.FailReason);
                return;
            }

            // when via contains wide1-1 or mycall, digipeat.
            // to digipeat, add own call to the end of the digi list.
            // set H bit
            // make sure that only last address has "last address" bit set

            Ax25Frame outbound = new Ax25Frame();
            outbound.Source = ax25Frame.Source;
            outbound.Dest = ax25Frame.Dest;
            outbound.Digis = new AddressField[ax25Frame.Digis.Length + 1];
            for (int i=0; i<ax25Frame.Digis.Length;i++)
            {
                outbound.Digis[i] = ax25Frame.Digis[i];
            }
            outbound.Digis[ax25Frame.Digis.Length] = new AddressField(MYCALL);
            outbound.Info = ax25Frame.Info;

            byte[] buf = outbound.ToKissFrame();
            sp.Write(buf, 0, buf.Length);
        }
    }
}
