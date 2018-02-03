//using APRS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ax25lib
{
    class Program
    {
        static void Main2(string[] args)
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


        static void Main()
        {
            string call = Ax25Frame.DecodeCallsign(StringToByteArray("9A 60 98 A8 8A 40 EA"), out bool isLastAddress, out bool cBit);
            Debugger.Break();
            //byte b = (byte)'M';
            //BitArray ba = new BitArray(new[] { b });
            //Console.WriteLine(ba.ToOnesAndZeroes());

            //string frame = "C0    00         AA A2 A4 AC AA A2 60  9A 60 98 A8 8A 40 E0  AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 65                        03 F0   27 77 59 44 6C 20 1C 5B 2F 3E 0D  C0";
            string frame = "C0    00         AA A2 A4 AC AA A2 60  9A 60 98 A8 8A 40 EA  AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 65                          03 F0   27 77 59 44 6C 20 1C 5B 2F 3E 0D  C0";
            //              C0    00         AA A2 A4 AC AA A2 60  9A 60 98 A8 8A 40 6A  AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 64   9A 60 98 A8 8A 40 73   03 F0   27 77 59 44 6C 20 1C 5B 2F 3E 0D  C0
            //                                                     M0LTE-5           **                                           **
            //              C0    00         AA A2 A4 AC AA A2 E0  9A 60 98 A8 8A 40 EA  AE 92 88 8A 62 40 E2   AE 92 88 8A 64 40 E4 9A 60 98 A8 8A 40 F3 03 F0 27 77 59 44 6C 20 1C 5B 2F 3E 0D C0

            byte[] frameBytes = StringToByteArray(frame);

            Ax25Frame.TryParse(frameBytes, out Ax25Frame f);
            byte[] kf = f.ToKissFrame();
            string s2 = kf.ToHexString();
            Ax25Frame.TryParse(StringToByteArray(s2), out Ax25Frame f2);

            //Process(frameBytes);

            Debugger.Break();
        }

        public static byte[] StringToByteArray(string hexRaw)
        {
            string hex = hexRaw.Replace(" ", "");

            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
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
C0    00         AA A2 A4 AC AA A2 60  9A 60 98 A8 8A 40 EA  AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 65   03       F0      27 77 59 44 6C 20 1C 5B 2F 3E 0D  C0
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

            bool isLast;
            Console.WriteLine("Source call: {0} (last:{1})", getCallsign(src, out isLast), isLast ? "yes" : "no");

            Console.WriteLine("Dest call: {0} (last:{1})", getCallsign(dest, out isLast), isLast ? "yes" : "no");

            foreach (var d in digis)
            {
                Console.WriteLine("Digi: {0} (last:{1})", getCallsign(d, out isLast), isLast ? "yes" : "no");
            }

            //https://www.tapr.org/pub_ax25.html#2.2.13
            //http://www.aprs.org/doc/APRS101.PDF
            //http://destevez.net/2016/06/kiss-hdlc-ax-25-and-friends/
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
            bool isLastAddress;
            return getCallsign(addressField, out isLastAddress);
        }

        static string getCallsign(byte[] addressField, out bool isLastAddress)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 6; i++)
            {
                char c = (char)(addressField[i] >> 1);
                sb.Append(c);
            }
            string call = sb.ToString().Trim();

            byte ssidByte = addressField[6];

            // bits numbered from the right, per fig 3.5 of http://www.tapr.org/pdf/AX25.2.2.pdf

            // bit 0 (rightmost) is the HDLC address extension bit, set to zero on all but the last octet in the address field, where it is set to one.
            isLastAddress = ssidByte.GetBit(0);

            // ssid is next, it's just a 4-bit number, 0-15
            int ssid = GetSsid(ssidByte);

            // reserved bits, set to 1 when not implemented
            bool reserved1 = ssidByte.GetBit(5);
            bool reserved2 = ssidByte.GetBit(6);

            // command/response bit of an LA PA frame, see section 6.1.2 of http://www.tapr.org/pdf/AX25.2.2.pdf
            bool cBit = ssidByte.GetBit(7);

            if (ssid == 0)
            {
                return call;
            }
            else
            {
                return String.Format("{0}-{1}", call, ssid);
            }
        }

        /// <summary>
        /// Decode SSID - it's a four bit number, 0-15
        /// </summary>
        /// <param name="ssidByte"></param>
        /// <returns></returns>
        static int GetSsid(byte ssidByte)
        {
            var ssidBits = new bool[4];
            ssidBits[0] = ssidByte.GetBit(1);
            ssidBits[1] = ssidByte.GetBit(2);
            ssidBits[2] = ssidByte.GetBit(3);
            ssidBits[3] = ssidByte.GetBit(4);

            int ssid = 0;

            for (int i = 0; i < 3; i++)
            {
                if (ssidBits[i])
                    ssid |= 1 << i;
                else
                    ssid &= ~(1 << i);
            }

            return ssid;
        }
    }

    static class Extensions
    {
        public static bool GetBit(this byte b, int bitNumber)
        {
            bool bit = (b & (1 << bitNumber)) != 0;

            return bit;
        }

        public static byte SetBit(this byte b, int index, bool value)
        {
            int byteIndex = index / 8;
            int bitIndex = index % 8;
            byte mask = (byte)(1 << bitIndex);

            b = (byte)(value ? (b | mask) : (b & ~mask));

            return b;
        }

        public static string ToOnesAndZeroes(this BitArray bits)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < bits.Count; i += 8)
            {
                // take eight bits, print them backwards
                for (int j = 7; j >= 0; j--)
                {
                    sb.Append(bits[i + j] ? "1" : "0");
                }
                sb.Append(" ");
            }

            return sb.ToString().Trim();
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
