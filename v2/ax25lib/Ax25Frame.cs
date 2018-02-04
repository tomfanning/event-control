using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ax25lib
{
    [DebuggerDisplay("{ToString()}")]
    public class CallField
    {
        public CallField()
        {
            Bit5Reserved = Bit6Reserved = true; // 1 if unimplemented
        }

        public string Call { get; set; }

        /// <summary>
        /// <para>In the destination field, this is the AX.25 command/response (C) bit.</para>
        /// <para>In the repeater field, this is the Is Repeated (H) bit.</para>
        /// </summary>
        public bool CHBit { get; set; }

        public bool IsLast { get; set; }

        public bool Bit5Reserved { get; set; }
        public bool Bit6Reserved { get; set; }

        public override string ToString()
        {
            return $"{Call ?? "NULL"} CBit={(CHBit ? 1 : 0)} IsLast={(IsLast ? 1 : 0)} Res={(Bit6Reserved ? 1 : 0)}{(Bit5Reserved ? 1 : 0)}";
        }

        public CallField Clone()
        {
            throw new NotImplementedException();
        }
    }
    public class Ax25Frame
    {
        public Ax25Frame()
        {
            this.Digis = new List<CallField>();
        }

        public CallField Source { get; set; }
        public CallField Dest { get; set; }
        public List<CallField> Digis { get; set; }

        public string Info { get; set; }
        public byte[] InfoBytes { get; set; }

        public static ParseResult TryParse(byte[] kissFrame, out Ax25Frame ax25Frame)
        {
            ax25Frame = null;

            if (kissFrame[0] != 0xc0 || kissFrame[1] != 0x00)
            {
                return new ParseResult { FailReason = String.Format("Not a KISS frame, expected c0 00, got {0:X2} {1:X2}", kissFrame[0], kissFrame[1]) };
            }

            if (kissFrame.Last() != 0xc0)
            {
                return new ParseResult { FailReason = String.Format("Frame looks incomplete, expected c0 at end, got {0:X2}", kissFrame.Last()) };
            }

            ax25Frame = new Ax25Frame();
            ax25Frame.Source = DecodeCallsign(getSourceBytes(kissFrame));
            ax25Frame.Dest = DecodeCallsign(getDestBytes(kissFrame));

            byte[][] digis = getDigis(kissFrame);
            foreach (byte[] digiField in digis)
            {
                CallField call = DecodeCallsign(digiField);
                Debug.WriteLine(call);
                ax25Frame.Digis.Add(call);
            }

            ax25Frame.InfoBytes = getInfo(kissFrame);
            ax25Frame.Info = Encoding.ASCII.GetString(ax25Frame.InfoBytes);

            return new ParseResult { Result = true };
        }

        static byte[] getDestBytes(byte[] frame)
        {
            return frame.Skip(2).Take(7).ToArray();
        }

        static byte[] getSourceBytes(byte[] frame)
        {
            return frame.Skip(9).Take(7).ToArray();
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

        public byte[] ToKissFrame()
        {
            /*
C0    00         AA A2 A4 AC AA A2 60  9A 60 98 A8 8A 40 E0  AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 65   03       F0      27 77 59 44 6C 20 1C 5B 2F 3E 0D  C0
C0    00         AA A2 A4 AC AA A2 60  9A 60 98 A8 8A 40 EA  AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 65   03       F0      27 77 59 44 6C 20 1C 5B 2F 3E 0D  C0
FEND  Dataframe  dest----------------  source--------------  digi_1--------------   digi_2--------------   control  proto   info----------------------------  FEND

no flags 0x7e, expected start and end
no FCS, dealt with at KISS level?*/

            var buffer = new List<byte>();
            buffer.AddRange(new byte[] { 0xc0, 0 }); // FEND, DataFrame
            buffer.AddRange(EncodeCallsign(Dest));
            buffer.AddRange(EncodeCallsign(Source));

            for (int i=0; i< Digis.Count; i++)
            {
                // for the routing info, we need to set the IsLast bit ourselves because we know better, otherwise should pass through as-is.
                var digiCall = Digis[i];
                digiCall.IsLast = i + 1 == Digis.Count;
                byte[] digiBytes = EncodeCallsign(digiCall);
                buffer.AddRange(digiBytes);
            }

            buffer.AddRange(new byte[] { 0x03 }); // control
            buffer.AddRange(new byte[] { 0xF0 }); // proto
            buffer.AddRange(InfoBytes); // info
            buffer.AddRange(new byte[] { 0xC0 }); // FEND

            return buffer.ToArray();
        }

        static byte[] EncodeCallsign(CallField cf)
        {
            string callAndSsid = cf.Call;

            var parts = (callAndSsid ?? "").Trim().Split('-');
            if (parts.Length != 1 && parts.Length != 2)
            {
                throw new ArgumentException($"Invalid callsign {callAndSsid}");
            }

            if (string.IsNullOrWhiteSpace(parts[0]))
            {
                throw new ArgumentException($"Invalid callsign {parts[0]}");
            }

            int ssid;
            if (parts.Length == 1)
            {
                ssid = 0;
            }
            else
            {
                ssid = int.Parse(parts[1]);
            }

            if (ssid < 0 || ssid > 15)
            {
                throw new ArgumentException($"Invalid SSID {ssid} in {callAndSsid}");
            }

            string call = parts[0].Trim();
            while (call.Length < 6)
            {
                call += " ";
            }

            // callsign bytes
            var result = new byte[7];
            for (int i = 0; i < 6; i++)
            {
                result[i] = (byte)(call[i] << 1);
            }

            // ssid byte

            // bits numbered from the right, per fig 3.5 of http://www.tapr.org/pdf/AX25.2.2.pdf

            // bit 0 (rightmost) is the HDLC address extension bit, set to zero on all but the last octet in the address field, where it is set to one.
            result[6] = result[6].SetBit(0, cf.IsLast);

            // ssid is next, it's just a 4-bit number, 0-15
            result[6] = SetSsid(result[6], ssid);

            // reserved bits, set to 1 when not implemented
            result[6] = result[6].SetBit(5, cf.Bit5Reserved); 
            result[6] = result[6].SetBit(6, cf.Bit6Reserved);

            // command/response bit of an LA PA frame, see section 6.1.2 of http://www.tapr.org/pdf/AX25.2.2.pdf
            result[6] = result[6].SetBit(7, cf.CHBit); 

            return result;
        }

        internal static CallField DecodeCallsign(byte[] addressField)
        {
            var result = new CallField();

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
            result.IsLast = ssidByte.GetBit(0);

            // ssid is next, it's just a 4-bit number, 0-15
            int ssid = GetSsid(ssidByte);

            // reserved bits, set to 1 when not implemented
            bool reserved1 = ssidByte.GetBit(5);
            bool reserved2 = ssidByte.GetBit(6);

            // command/response bit of an LA PA frame, see section 6.1.2 of http://www.tapr.org/pdf/AX25.2.2.pdf
            result.CHBit = ssidByte.GetBit(7);

            if (ssid == 0)
            {
                result.Call = call;
            }
            else
            {
                result.Call = String.Format("{0}-{1}", call, ssid);
            }

            return result;
        }

        static byte SetSsid(byte ssidByte, int ssid)
        {
            bool[] bits = ssid.ToBits(4);

            ssidByte = ssidByte.SetBit(1, bits[3]);
            ssidByte = ssidByte.SetBit(2, bits[2]);
            ssidByte = ssidByte.SetBit(3, bits[1]);
            ssidByte = ssidByte.SetBit(4, bits[0]);

            return ssidByte;
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

            for (int i = 0; i < 4; i++)
            {
                if (ssidBits[i])
                    ssid |= 1 << i;
                else
                    ssid &= ~(1 << i);
            }

            return ssid;
        }
    }
    
    public class InfoField
    {
        public string Ascii { get; set; }
        public string Data { get; set; }
    }

    public class ParseResult
    {
        public bool Result { get; set; }
        public string FailReason { get; set; }
    }

    public static class ExtensionMethods
    {
        /// <summary>
        /// Left to right
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static bool[] ToBits(this int number, int zeroPad)
        {
            const int mask = 1;
            var binary = new List<bool>();
            while (number > 0)
            {
                // Logical AND the number and prepend it to the result string
                int b = number & mask;
                binary.Insert(0, b == 0 ? false : true);
                number = number >> 1;
            }

            while (binary.Count() < zeroPad)
            {
                binary.Insert(0, false);
            }

            return binary.ToArray();
        }
    }
}
