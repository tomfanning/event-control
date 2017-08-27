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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace outstation_server
{
    public class Program : NancyModule
    {
        static void Main(string[] args)
        {
            // start VS as admin to run this

            var server = new NancyHost(new CustomBootstrapper(), new Uri("http://localhost:8282"));
            server.Start();
            Console.WriteLine("started!");
            Console.WriteLine("press any key to exit");

            Process.Start("http://localhost:8282");
            Console.ReadKey();
        }

        public Program()
        {
            Get["/testnancy"] = _ => { return "test nancy"; };
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

        private dynamic TableHasUpdate(dynamic parameters)
        {
            int tableNum = int.Parse(parameters.number);
            int lastUpdateNum = int.Parse(this.Request.Query["last"]);

            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < TimeSpan.FromMinutes(1))
            {
                if ((int)(DateTime.Now - DateTime.Today).TotalSeconds / 10 > lastUpdateNum)
                {
                    return true;
                }

                Thread.Sleep(1000);
            }

            return false;
        }

        private dynamic GetTable(dynamic parameters)
        {
            int num = int.Parse(parameters.number);

            var ser = (int)(DateTime.Now - DateTime.Today).TotalSeconds / 10;

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
