using Nancy;
using Nancy.Bootstrapper;
using Nancy.Conventions;
using Nancy.ErrorHandling;
using Nancy.Hosting.Self;
using Nancy.Responses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace outstation_server
{
    public class Program : NancyModule
    {
        //const string url = "http://localhost:8282";
        const string url = "http://*:8282";

        static void Main(string[] args)
        {
            // start VS as admin to run this

            var server = new NancyHost(new CustomBootstrapper(), GetUriParams(8282));
            server.Start();
            Console.WriteLine("started!");
            Console.WriteLine("press any key to exit");

            //Process.Start("http://localhost:8282");
            Console.ReadKey();
        }

        public Program()
        {
            Get["/testnancy"] = _ => { return "test nancy"; };
        }

        static Uri[] GetUriParams(int port)
        {
            var uriParams = new List<Uri>();
            string hostName = Dns.GetHostName();

            // Host name URI
            string hostNameUri = string.Format("http://{0}:{1}", Dns.GetHostName(), port);
            uriParams.Add(new Uri(hostNameUri));

            // Host address URI(s)
            var hostEntry = Dns.GetHostEntry(hostName);
            foreach (var ipAddress in hostEntry.AddressList)
            {
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)  // IPv4 addresses only
                {
                    var addrBytes = ipAddress.GetAddressBytes();
                    string hostAddressUri = string.Format("http://{0}.{1}.{2}.{3}:{4}",
                    addrBytes[0], addrBytes[1], addrBytes[2], addrBytes[3], port);
                    uriParams.Add(new Uri(hostAddressUri));
                }
            }

            // Localhost URI
            uriParams.Add(new Uri(string.Format("http://localhost:{0}", port)));
            return uriParams.ToArray();
        }
    }

    public class CustomBootstrapper : DefaultNancyBootstrapper
    {
        protected override void ConfigureConventions(NancyConventions nancyConventions)
        {
            nancyConventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory("/", "Content"));
            base.ConfigureConventions(nancyConventions);
        }
    }

    public class ApiModule : NancyModule
    {
        public ApiModule() : base("/api/v1")
        {
            Get["/data/table/{number}"] = parameters => { return GetTable(parameters); };
            Get["/longpoll/table/{number}"] = parameters => { return TableHasUpdate(parameters); };
        }

        int serverLatestUpdateNum { get { return (int)(DateTime.Now - DateTime.Today).TotalSeconds / 10; } }

        private dynamic TableHasUpdate(dynamic parameters)
        {
            int tableNum = int.Parse(parameters.number);
            int clientLastUpdateNum = int.Parse(this.Request.Query["last"]);

            if (clientLastUpdateNum > serverLatestUpdateNum)
            {
                return new { status = "higherThanIssued" };
            }

            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < TimeSpan.FromMinutes(1))
            {
                if (serverLatestUpdateNum > clientLastUpdateNum)
                {
                    return new { status = "updateAvailable" };
                }

                Thread.Sleep(1000);
            }

            return new { status = "nothingNew" };
        }

        private dynamic GetTable(dynamic parameters)
        {
            int num = int.Parse(parameters.number);

            int ser = serverLatestUpdateNum;

            return new PackedData
            {
                Serial = ser,
                Headers = new[] { "col1", "col2", "col3" },
                Data = new string[][]{
                    new[] { "test1", "test2", "test3" },
                    new[] { "test4", "test5", "test6 " + ser }
                }
            };
        }
    }

    public class PackedData
    {
        public int Serial { get; set; }
        public string[] Headers { get; set; }
        public string[][] Data { get; set; }
    }

    public class IndexModule : NancyModule
    {
        public IndexModule() : base("")
        {
            Get["/"] = parameters => { return Response.AsFile("Content/index.htm", "text/html"); };
            Get["/fonts/fontawesome-webfont.woff2"] = parameters => { return Response.AsFile("Content/fontawesome-webfont.woff2", "application/font-woff"); };
        }
    }
}
