using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace shared
{
    public class Bulletin
    {
        public int PageNo { get; set; }
        public DateTimeOffset Produced { get; set; }
        public TimeSpan Validity { get; set; }
        public string Originator { get; set; }

        public string Title { get; set; }
        public string[] Tags { get; set; }
        public string[] List { get; set; }
        public string[][] Table { get; set; }
        public string FreeText { get; set; }
    }
}