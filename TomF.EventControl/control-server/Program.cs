using Newtonsoft.Json;
using shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace control_server
{
    class Program
    {
        static TcpClient cli = new TcpClient();

        static void Main(string[] args)
        {
            cli.Connect("trackerpi", 8001);

            // rf out: info broadcasts
            // rf in: requests for updates, messaging

            // webserver: UI for control desk

            Bulletin b = new Bulletin();
            b.FreeText = "here is some free text";
            b.List = new[] { "this", "is", "a", "list", "of", "strings" };
            b.Originator = "M0LTE-control";
            b.PageNo = 123;
            b.Produced = DateTimeOffset.Now;
            b.Table = new string[][]{
                new[] { "a1", "b1", "c1" },
                new[] { "a2", "b2", "c2" },
                new[] { "a3", "b3", "c3" },
            };
            b.Tags = new[] { "tag1", "tag2" };
            b.Title = "bulletin title";
            b.Validity = TimeSpan.FromMinutes(20);

            try
            {
                Send(b);
            }
            finally
            {
                cli.Dispose();
            }
        }

        const string sample = @"c0 00 82 a0 ae ae 62 60  e0 9a 60 98 a8 8a 40 64
ae 92 88 8a 62 40 62 ae  92 88 8a 64 40 63 03 f0
40 31 39 35 31 33 33 68  35 31 32 36 2e 38 34 4e
2f 30 30 31 30 31 2e 36  36 57 6c 41 50 52 53 2d
49 53 20 66 6f 72 20 57  69 6e 33 32 c0";

        // M0LTE-2>APWW10,WIDE1-1,WIDE2-1:
        const string calls = @"82 a0 ae ae 62 60  e0 9a 60 98 a8 8a 40 64
ae 92 88 8a 62 40 62 ae  92 88 8a 64 40 63 03 f0";

        private static void Send(Bulletin b)
        {
            string msg = JsonConvert.SerializeObject(b, Formatting.Indented);

            string base64Encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(msg));

            byte[] msgBytes = Encoding.ASCII.GetBytes(base64Encoded);

            // 0xc0 needs to be transposed to 0xdc, and 0xdb needs to be transposed to 0xdd,
            // however both of those bytes are far higher than anything that will show up in base64 text encoded to ASCII bytes.
            // if the encoding scheme changes, we might need to take that into account.

            byte[] sendBuf = new byte[] { 0xc0, 0x00 }.Concat(HexStringToBytes(calls)).Concat(msgBytes).Concat(new byte[] { 0xc0 }).ToArray();

            foreach (var byt in sendBuf.Skip(1).Take(sendBuf.Length-2))
            {
                if (byt == 0xc0)
                {
                    Debugger.Break();
                }
            }

            //byte[] sendBuf = new byte[] { 0xc0, 0x00 }.Concat(HexStringToBytes(calls)).Concat(new byte[] { 0x54, 0x45, 0x53, 0x54, 0xc0 }).ToArray();

            //byte[] sendBuf = HexStringToBytes(sample);

            var str = cli.GetStream();

            cli.NoDelay = true;

            Console.WriteLine("waiting");
            var key = Console.ReadKey();
            if (key.KeyChar == 'x')
                return;

            while (true)
            {
                //str.Write(new byte[] { 0x0d }, 0, 1);
                str.Write(sendBuf, 0, sendBuf.Length);
                //str.Write(new byte[] { 0x0d }, 0, 1);

                Console.WriteLine("press enter or x");
                var key1 = Console.ReadKey();
                if (key1.KeyChar == 'x')
                    return;
            }

        }

        private static byte[] HexStringToBytes(string sample)
        {
            List<byte> bytes = new List<byte>();

            foreach (var line in sample.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                foreach (var token in line.Split(' ').Select(t=>t.Trim()).Where(t=>!string.IsNullOrWhiteSpace(t)))
                {
                    byte b = Convert.ToByte(token, 16);

                    bytes.Add(b);
                }
            }

            return bytes.ToArray();
        }
    }
}