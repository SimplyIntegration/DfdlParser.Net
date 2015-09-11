using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DfdlParser
{
    public class Attribute
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public string DataType { get; set; }

        public int MinOccurs { get; set; }

        public int MaxOccurs { get; set; }

        public string DefaultValue { get; set; }

        public Entity EntityType { get; set; }

        public int MinLength
        {
            get;set;
        }

        public int MaxLength
        {
            get; set;
        }

        public int Length
        {
            get;set;
        }

        public string LengthKind
        {
            get; set;
        }

        public string OccursCountKind
        {
            get; set;
        }

        public string Initiator { get; set; }

        public string Terminator { get; set; }


    }
}
