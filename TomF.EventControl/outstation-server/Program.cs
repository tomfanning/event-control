using Nancy;
using Nancy.Bootstrapper;
using Nancy.Conventions;
using Nancy.ErrorHandling;
using Nancy.Hosting.Self;
using Nancy.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

            DataManager.Topics.Add(new Table { ID = 100, Issued = DateTimeOffset.Parse("2017-08-27 13:00Z"), Name = "Test table", Serial = 1, Headers = new[] { "col1", "col2" }, Data = new string[][] { new string[] { "a1", "b1" }, new string[] { "a2", "b2" } } });
            DataManager.Topics.Add(new FreeText { ID = 200, Issued = DateTimeOffset.Parse("2017-08-27 13:15Z"), Name = "Test free text", Serial = 1, Content = "Hello world\r\nthis is a second line"});

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

    public static class DataManager
    {
        static DataManager()
        {
            Topics = new List<Topic>();
        }

        public static List<Topic> Topics { get; set; }
    }

    public class Topic
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int Serial { get; set; }
        public DateTimeOffset Issued { get; set; }
        public string Type { get; set; }
    }

    public class Table : Topic
    {
        public Table()
        {
            Type = GetType().Name;
        }

        public string[] Headers { get; set; }
        public string[][] Data { get; set; }
    }

    public class FreeText : Topic
    {
        public FreeText()
        {
            Type = GetType().Name;
        }

        public string Content { get; set; }
    }

    public class JsonNetSerializer : ISerializer
    {
        private readonly JsonSerializer _serializer;

        public JsonNetSerializer()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.Indented,
            };

            _serializer = JsonSerializer.Create(settings);
        }

        public IEnumerable<string> Extensions
        {
            get
            {
                return new string[0];
            }
        }

        public bool CanSerialize(string contentType)
        {
            return contentType == "application/json";
        }

        public void Serialize<TModel>(string contentType, TModel model, Stream outputStream)
        {
            using (var writer = new JsonTextWriter(new StreamWriter(outputStream)))
            {
                _serializer.Serialize(writer, model);
                writer.Flush();
            }
        }
    }

    public class ApiModule : NancyModule
    {
        public ApiModule() : base("/api/v1")
        {
            Get["/data/table/{number}"] = parameters => { return GetTable(parameters); };
            Get["/longpoll/table/{number}"] = parameters => { return TopicHasUpdate(parameters); };
            Get["/topics"] = parameters => { return Response.AsJson(GetTopics()); };
        }

        private Topic[] GetTopics()
        {
            return DataManager.Topics.Select(t => new Topic
            {
                ID = t.ID,
                Issued = t.Issued,
                Name = t.Name,
                Serial = t.Serial,
                Type = t.Type
            }).ToArray();
        }

        private dynamic TopicHasUpdate(dynamic parameters)
        {
            int tableNum = int.Parse(parameters.number);
            int clientLastUpdateNum = int.Parse(this.Request.Query["last"]);

            var topic = DataManager.Topics.SingleOrDefault(t => t.ID == tableNum);

            if (topic == null)
                return new { status = "invalidTopic" };

            if (clientLastUpdateNum > topic.Serial)
            {
                return new { status = "higherThanIssued" };
            }

            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < TimeSpan.FromMinutes(1))
            {
                if (topic.Serial > clientLastUpdateNum)
                {
                    return new { status = "updateAvailable" };
                }

                Thread.Sleep(100);
            }

            return new { status = "nothingNew" };
        }

        private dynamic GetTable(dynamic parameters)
        {
            int num = int.Parse(parameters.number);

            var topic = DataManager.Topics.SingleOrDefault(t => t.ID == num);

            return topic as Table;
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
