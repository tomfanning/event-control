using APRS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp2
{
    class Program
    {
        static void Main(string[] args)
        {
            var sw = new Stopwatch();

            using (var sp = new SerialPort("COM4", 38400))
            {
                sp.Open();

                var buf = new List<byte>();

                bool midPacket = false;

                while (true)
                {
                    byte i = (byte)sp.ReadByte();

                    Debug.Write(i);
                    Debug.Write(" ");

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

        static void Process(byte[] buf)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                Console.Write("{0:X2} ", buf[i]);
            }
            Console.WriteLine();

            /*
C0    00         AA A2 A4 AC AA A2 60  9A 60 98 A8 8A 40 E0  AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 65   03       F0      27 77 59 44 6C 20 1C 5B 2F 3E 0D  C0
FEND  Dataframe  dest----------------  source--------------  digi_1--------------   digi_2--------------   control  proto   info----------------------------  FEND

no flags 0x7e, expected start and end
no FCS, dealt with at KISS level?*/

            if (buf[0] != 0xc0 || buf[1] != 0x00)
            {
                throw new Exception(String.Format("Not a KISS frame, expected c0 00, got {0:X2} {1:X2}", buf[0], buf[1]));
            }

            if (buf.Last() != 0xc0)
            {
                throw new Exception(String.Format("Frame looks incomplete, expected c0 at end, got {0:X2}", buf.Last()));
            }

            byte[] src = getSourceBytes(buf);
            byte[] dest = getDestBytes(buf);
            byte[][] digis = getDigis(buf);
            byte[] info = getInfo(buf);

            Console.WriteLine("Source: " + src.ToHexString());
            Console.WriteLine("Dest:   " + dest.ToHexString());
            foreach (var d in digis)
            {
                Console.WriteLine("Digi:  " + d.ToHexString());
            }
            Console.WriteLine("Info:   " + info.ToHexString());

            Console.WriteLine("Source call: " + getCallsign(src));

        //https://www.tapr.org/pub_ax25.html#2.2.13
        //http://www.aprs.org/doc/APRS101.PDF
        }

        static byte[] getInfo(byte[] frame)
        {
            int cur = 16;
            var buf = new List<Byte>();

            bool care = false;
            while (true)
            {
                if (frame[cur] == 0x03 && frame[cur + 1] == 0xf0)
                {
                    care = true;
                    cur += 2;
                }

                if (care)
                {
                    if (frame[cur] == 0xc0)
                    {
                        return buf.ToArray();
                    }

                    buf.Add(frame[cur]);
                }
                cur++;
            }
        }

        static byte[][] getDigis(byte[] frame)
        {
            int cur = 16;
            var buf = new List<Byte>();

            while (true)
            {
                if (frame[cur] == 0x03 && frame[cur + 1] == 0xf0)
                {
                    int numDigis = buf.Count / 7;
                    var result = new byte[numDigis][];

                    for (int digiNum = 0; digiNum < numDigis; digiNum++)
                    {
                        var thisDigiBytes = buf.Skip(digiNum * 7).Take(7);

                        result[digiNum] = thisDigiBytes.ToArray();
                    }

                    return result;
                }
                else
                {
                    buf.Add(frame[cur]);
                    cur++;
                }
            }
        }

        static byte[] getDestBytes(byte[] frame)
        {
            return frame.Skip(2).Take(7).ToArray();
        }

        static byte[] getSourceBytes(byte[] frame)
        {
            return frame.Skip(9).Take(7).ToArray();
        }

        static string getCallsign(byte[] addressField)
        {
            BitArray ba = new BitArray(addressField);

            Console.WriteLine(ba.ToOnesAndZeroes());
            var baShifted = ba.ShiftRight();
            Console.WriteLine(baShifted.ToOnesAndZeroes());

            return Encoding.ASCII.GetString(baShifted.ToByteArray());
        }
    }

    static class Extensions
    {
        public static string ToOnesAndZeroes(this BitArray bits)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < bits.Length; i++)
            {
                sb.Append(bits[i] ? "1" : "0"); 
            }

            return sb.ToString();
        }
        public static byte[] ToByteArray(this BitArray bits)
        {
            byte[] ret = new byte[(bits.Length - 1) / 8 + 1];
            bits.CopyTo(ret, 0);
            return ret;
        }

        public static BitArray ShiftRight(this BitArray instance)
        {
            return new BitArray(new bool[] { false }.Concat(instance.Cast<bool>().Take(instance.Length - 1)).ToArray());
        }

        public static BitArray ShiftLeft(this BitArray instance)
        {
            bool newState = false;

            return new BitArray((instance.Cast<bool>().Take(instance.Length - 1).ToArray()).Concat(new bool[] { newState }).ToArray());
        }

        public static string ToHexString(this IEnumerable<byte> bytes)
        {
            return string.Join(" ", bytes.Select(b => String.Format("{0:X2}", b)));
        }
    }
}
