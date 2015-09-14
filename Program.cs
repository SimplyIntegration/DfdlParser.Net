using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DfdlParser
{
    class Program
    {
        static void Main(string[] args)
        {
            var dfdl = @"d:\dfdltests\dfdl.xsd";
            var feed = @"d:\dfdltests\sampleinput2.txt";

            var parser = new SchemaParser(dfdl);

            
            parser.Parse(feed);
            
        }
    }
}
