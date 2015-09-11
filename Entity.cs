using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DfdlParser
{
    public class Entity
    {
        public string Namespace { get; set; }
        public string Name { get; set; }

        public string Description { get; set; }

        public bool IsRoot { get; set; }

        public bool IsComposable { get; set; }

        public List<Attribute> Attributes { get; set; }
    }
}
