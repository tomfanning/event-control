using Nancy;
using Nancy.Bootstrapper;
using Nancy.Conventions;
using Nancy.ErrorHandling;
using Nancy.Hosting.Self;
using Nancy.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

    public class IndexModule : NancyModule
    {
        public IndexModule() : base("")
        {
            Get["/"] = parameters => { return Response.AsFile("Content/index.htm", "text/html"); };
            Get["/fonts/fontawesome-webfont.woff2"] = parameters => { return Response.AsFile("Content/fontawesome-webfont.woff2", "application/font-woff"); };
        }
    }
}
