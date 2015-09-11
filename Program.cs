using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DfdlParser
{
    class Program
    {
        static void Main(string[] args)
        {
            var dfdl = @"d:\dfdltests\ruralfinance.xsd";
            var feed = @"d:\dfdltests\feed.txt";

            var parser = new SchemaParser();

            var xml = parser.Load(dfdl, feed);
        }
    }
}
