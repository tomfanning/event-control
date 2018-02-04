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
        //const string MYCALL = "M0LTE-15";

        static void Main(string[] args)
        {
            // this byte is getting parsed wrong                                                                                           **
            Process("C0 00   AA A2 A4 AC AA A2 60   9A 60 98 A8 8A 40 EA   AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 65                          03 F0 27 77 59 44 6C 20 1C 5B 2F 3E 0D   C0".ToByteArray());
            // 
            return;

            using (sp = new SerialPort("COM4", 38400))
            {
                sp.Open();
                //sp.ReadTimeout = 100;

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
                                Console.WriteLine("Waiting...");
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

        const string MYCALL = "M0LTE-9";

        static void Process(byte[] inboundKissFrameFromModem)
        {
            byte[] unescaped = UnescapeFrameFromModem(inboundKissFrameFromModem);

            var parseResult = Ax25Frame.TryParse(unescaped, out Ax25Frame rxFrame);
            if (!parseResult.Result)
            {
                Console.WriteLine("FAIL: " + parseResult.FailReason);
                return;
            }

            var txFrame = new Ax25Frame();
            txFrame.Source = rxFrame.Source;
            txFrame.Dest = rxFrame.Dest;
            foreach (var digi in rxFrame.Digis)
            {
                txFrame.Digis.Add(digi);
                // isLast bit is handled by .ToKissFrame()
                // C/H bit should be passed through untouched
            }
            txFrame.Digis.Add(new CallField { Call = MYCALL, CHBit = true /* https://www.tapr.org/pub_ax25.html#2.2.13.3 */ });
            txFrame.InfoBytes = rxFrame.InfoBytes;
            byte[] txBytes = txFrame.ToKissFrame();

            Ax25Frame.TryParse(txBytes, out Ax25Frame checkFrame);

            /*
             DEST                   SOURCE                 DIGI 1                 DIGI 2                 DIGI 3                 INFO                                     FEND
 RX: C0 00   AA A2 A4 AC AA A2 60   9A 60 98 A8 8A 40 EA   AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 65                          03 F0 27 77 59 44 6C 20 1C 5B 2F 3E 0D   C0
 TX: C0 00   AA A2 A4 AC AA A2 60   9A 60 98 A8 8A 40 EA   AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 64   9A 60 98 A8 8A 40 F3   03 F0 27 77 59 44 6C 20 1C 5B 2F 3E 0D   C0
CHK: C0 00   AA A2 A4 AC AA A2 60   9A 60 98 A8 8A 40 EA   AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 64   9A 60 98 A8 8A 40 E3   03 F0 27 77 59 44 6C 20 1C 5B 2F 3E 0D   C0
                                                                                                       
																									   
																									RX: 1100101            RX: 11110011
																									TX: 1100100            TX: 11100011
																									          *                   *
                                                                                                              *                   1001 = 9
                                                                                                              *                   0001 = 1
             */
            Console.Write(" RX: ");
            Console.WriteLine(unescaped.ToHexString());
            Console.Write(" TX: ");
            Console.WriteLine(txBytes.ToHexString());
            Console.Write("CHK: ");
            Console.WriteLine(checkFrame.ToKissFrame().ToHexString());
            Console.WriteLine();

            Console.WriteLine("Source address");
            Console.WriteLine($"   Recevied: {rxFrame.Source}");
            Console.WriteLine($"     For TX: {txFrame.Source}");
            Console.WriteLine($"      Check: {checkFrame.Source}");
            Console.WriteLine();
            Console.WriteLine("Dest address");
            Console.WriteLine($"   Recevied: {rxFrame.Dest}");
            Console.WriteLine($"     For TX: {txFrame.Dest}");
            Console.WriteLine($"      Check: {checkFrame.Dest}");
            Console.WriteLine();
            
            Console.WriteLine("Digis");
            foreach (var digi in rxFrame.Digis)
            {
                Console.WriteLine($"   Recevied: {digi}");
            }
            foreach (var digi in txFrame.Digis)
            {
                Console.WriteLine($"     For TX: {digi}");
            }
            foreach (var digi in checkFrame.Digis)
            {
                Console.WriteLine($"      Check: {digi}");
            }
            Console.WriteLine();

            if (false)
            {
                byte[] escapedForTransmit = EscapeFrameForSending(txBytes);
                sp.Write(escapedForTransmit, 0, escapedForTransmit.Length);
            }
            else
            {
                Console.WriteLine("tx inhibited");
            }

            // when via contains wide1-1 or mycall, digipeat.
            // to digipeat, add own call to the end of the digi list.
            // set H bit
            // make sure that only last address has "last address" bit set

            /*Ax25Frame outbound = new Ax25Frame();
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
            sp.Write(buf, 0, buf.Length);*/
        }

        static byte[] EscapeFrameForSending(byte[] outboundKissFrameToModem)
        {
            // If the FEND or FESC codes appear in the data to be transferred, they need to be escaped. 
            // The FEND code is then sent as FESC, TFEND and the FESC is then sent as FESC, TFESC.
            // C0 is sent as DB DC
            // DB is sent as DB DD

            // 0xC0	   FEND	Frame End
            // 0xDB    FESC Frame Escape
            // 0xDC    TFEND Transposed Frame End
            // 0xDD    TFESC Transposed Frame Escape

            var result = new List<byte>();

            for (int i = 0; i < outboundKissFrameToModem.Length; i++)
            {
                // first byte should be 0xC0 and should not be escaped
                if (i == 0 && outboundKissFrameToModem[i] == 0xc0)
                {
                    result.Add(outboundKissFrameToModem[i]);
                    continue;
                }

                // last byte should be 0xC0 and should not be escaped
                if (i == outboundKissFrameToModem.Length - 1 && outboundKissFrameToModem[i] == 0xc0)
                {
                    result.Add(outboundKissFrameToModem[i]);
                    continue;
                }

                // if we encounter C0, replace it with DB DC
                // if we encounter DB, replace it with DB DD

                if (outboundKissFrameToModem[i] == 0xc0)
                {
                    result.AddRange(new byte[] { 0xdb, 0xdc });
                }
                else if (outboundKissFrameToModem[i] == 0xdb)
                {
                    result.AddRange(new byte[] { 0xdb, 0xdd });
                }
                else
                {
                    result.Add(outboundKissFrameToModem[i]);
                }
            }

            return result.ToArray();
        }

        static byte[] UnescapeFrameFromModem(byte[] inboundKissFrameFromModem)
        {
            var result = new List<byte>();

            for (int i = 0; i < inboundKissFrameFromModem.Length; i++)
            {
                // first byte should be 0xC0 and is not escaped
                if (i == 0 && inboundKissFrameFromModem[i] == 0xc0)
                {
                    result.Add(inboundKissFrameFromModem[i]);
                    continue;
                }

                // last byte should be 0xC0 and is not escaped
                if (i == inboundKissFrameFromModem.Length - 1 && inboundKissFrameFromModem[i] == 0xc0)
                {
                    result.Add(inboundKissFrameFromModem[i]);
                    continue;
                }

                // if we encounter DB DC, replace it with C0
                // if we encounter DB DD, replace it with DB

                if (inboundKissFrameFromModem[i] == 0xdb && inboundKissFrameFromModem[i + 1] == 0xdc)
                {
                    result.Add(0xc0);
                }
                else if (inboundKissFrameFromModem[i] == 0xdb && inboundKissFrameFromModem[i + 1] == 0xdd)
                {
                    result.Add(0xdb);
                }
                else
                {
                    result.Add(inboundKissFrameFromModem[i]);
                }
            }


            return result.ToArray();
        }


    }

    static class ExtensionMethods
    {
        public static string ToHexString(this IEnumerable<byte> bytes)
        {
            return string.Join(" ", bytes.Select(b => String.Format("{0:X2}", b)));
        }

        public static byte[] ToByteArray(this string hexRaw)
        {
            string hex = hexRaw.Replace(" ", "");

            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
}
