using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ax25lib
{
    public class Ax25Frame
    {
        public AddressField Source { get; set; }
        public AddressField Dest { get; set; }
        public AddressField[] Digis { get; set; }

        public string Info { get; set; }
        public byte[] InfoBytes { get; set; }

        public Ax25Frame()
        {
        }

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
            ax25Frame.Source = new AddressField(getSourceBytes(kissFrame));
            ax25Frame.Dest = new AddressField(getDestBytes(kissFrame));
            ax25Frame.Digis = getDigis(kissFrame).Select(f => new AddressField(f)).ToArray();
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
    }

    [DebuggerDisplay("{Call}")]
    public class AddressField
    {
        public AddressField(byte[] bytes)
        {
            this.Call = getCallsign(bytes);
            this.Bytes = bytes;
        }

        public AddressField(string call)
        {
            throw new NotImplementedException();
        }

        public string Call { get; set; }
        public byte[] Bytes { get; set; }

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
}
