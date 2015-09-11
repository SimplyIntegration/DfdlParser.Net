using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

namespace DfdlParser
{
    class SchemaParser
    {
        
        private Dictionary<string, Entity> _entities;
        private string _filename;
        private XmlNamespaceManager _nsmgr;
        
        public SchemaParser()
        {
            _nsmgr = new XmlNamespaceManager(new NameTable());
            _nsmgr.AddNamespace("xsd", "http://www.w3.org/2001/XMLSchema");
            _nsmgr.AddNamespace("dfdl", "http://www.ogf.org/dfdl/dfdl-1.0/");
        }

        public List<Entity> Load(string dfdl, string fileToParse)
        {
            _entities = new Dictionary<string, Entity>();
            XmlSchema custSchema = ReadAndCompileSchema(dfdl);
            var dfdlProperties = new Dictionary<string, string>();

            foreach (var include in custSchema.Includes)
            {
                if(include is XmlSchemaImport)
                {
                    var schema = ((XmlSchemaImport)include).Schema;
                    foreach(var item in schema.Items)
                    {
                        if(item is XmlSchemaAnnotation)
                        {
                            var annotation = (XmlSchemaAnnotation)item;
                            foreach(var annotationItem in annotation.Items)
                            {
                                if(annotationItem is XmlSchemaAppInfo)
                                {
                                    var appInfoNodes = ((XmlSchemaAppInfo)annotationItem).Markup;

                                    var dfdlDefineEscapeSchema = appInfoNodes.Where(x => x.Name == "dfdl:defineEscapeScheme").SingleOrDefault();
                                    var dfdlEscapeSchema = dfdlDefineEscapeSchema.SelectNodes("dfdl:escapeScheme", _nsmgr);
                                    foreach(var s in dfdlEscapeSchema)
                                    {
                                        if (s is XmlElement)
                                        {
                                            foreach (XmlAttribute a in ((XmlElement)s).Attributes)
                                            {
                                                dfdlProperties.Add(a.Name, a.Value);
                                            }
                                        }
                                    }

                                    //dfdlEscapeSchema.
                                    var dfdlDefineFormat = appInfoNodes.Where(x => x.Name == "dfdl:defineFormat").SingleOrDefault();
                                    var dfdlFormat = dfdlDefineFormat.SelectNodes("dfdl:format", _nsmgr);
                                    foreach (var s in dfdlFormat)
                                    {
                                        if (s is XmlElement)
                                        {
                                            foreach (XmlAttribute a in ((XmlElement)s).Attributes)
                                            {
                                                dfdlProperties.Add(a.Name, a.Value);
                                            }
                                        }
                                    }

                                }
                                
                            }
                        }
                        
                    }
                }
                
            }

            foreach (XmlSchemaElement elem in
                                custSchema.Elements.Values)
            {
                ProcessElement(null, elem);
            }


            return _entities.Values.ToList();
        }

        private static XmlSchema ReadAndCompileSchema(string filename)
        {
            var tr = new XmlTextReader(filename,
                                new NameTable());

            // The Read method will throw errors encountered
            // on parsing the schema
            XmlSchema schema = XmlSchema.Read(tr,
                   new ValidationEventHandler(ValidationCallbackOne));
            tr.Close();

            XmlSchemaSet xset = new XmlSchemaSet();
            xset.Add(schema);

            xset.ValidationEventHandler += new ValidationEventHandler(ValidationCallbackOne);

            // The Compile method will throw errors
            // encountered on compiling the schema
            xset.Compile();

            return schema;
        }

        private void ProcessElement(Entity parent, XmlSchemaElement elem)
        {

            string ns = elem.QualifiedName.Namespace;
            if (string.IsNullOrEmpty(ns))
            {
                ns = "Default";
            }

            if (elem.ElementSchemaType is XmlSchemaComplexType)
            {
                Entity entity = new Entity()
                {
                    Name = elem.Name,
                    Namespace = ns,
                    Description = elem.QualifiedName.Namespace,
                    IsComposable = (parent != null),
                    IsRoot = (parent == null),

                };

                List<Attribute> attributes = new List<Attribute>();
                var ct =
                    elem.ElementSchemaType as XmlSchemaComplexType;

                foreach (DictionaryEntry obj in ct.AttributeUses)
                {
                    var xsdAttribute = obj.Value as XmlSchemaAttribute;
                    
                    var attribute = new Attribute()
                    {
                        Name = xsdAttribute.Name,
                        DataType = this.GetDataType(xsdAttribute.AttributeSchemaType),
                        DefaultValue = xsdAttribute.DefaultValue,
                        Description = (xsdAttribute.Annotation != null ? xsdAttribute.Annotation.ToString() : ""),
                        MinOccurs = (xsdAttribute.Use == XmlSchemaUse.Required ? 1 : 0),
                        MaxOccurs = 1

                    };

                    attributes.Add(attribute);
                }



                entity.Attributes = new List<Attribute>(attributes);
                ProcessSchemaObject(entity, ct.ContentTypeParticle);

                _entities.Add(entity.Name, entity);
            }

            if (parent != null)
            {
                if (elem.Name == "note")
                    Console.WriteLine("Got here");
                string dt = "Entity";
                Entity entityType = null;

                //Is this a simplement element type?
                if (elem.ElementSchemaType is XmlSchemaSimpleType)
                {
                    dt = GetDataType(elem.ElementSchemaType);
                }
                else
                {
                    _entities.TryGetValue(ns + ":" + elem.Name, out entityType);
                }



                var attribute = new Attribute()
                {
                    Name = elem.Name,
                    DataType = dt,
                    DefaultValue = elem.DefaultValue,
                    Description = (elem.Annotation != null ? elem.Annotation.ToString() : ""),
                    MinOccurs = (int)elem.MinOccurs,
                    MaxOccurs = (int)(elem.MaxOccurs == decimal.MaxValue ? -1 : elem.MaxOccurs),

                    //MinLength = xsdAttribute.
                    EntityType = entityType

                };

                if (elem.UnhandledAttributes != null)
                {
                    foreach(var attr in elem.UnhandledAttributes)
                    {
                        switch(attr.Name)
                        {
                            case "dfdl:length":
                                attribute.Length = int.Parse( attr.Value);
                                break;
                            case "dfdl:lengthKind":
                                attribute.Terminator = attr.Value;
                                break;
                            case "dfdl:occursCountKind":
                                attribute.OccursCountKind = attr.Value;
                                break;
                            case "dfdl:initiator":
                                attribute.Initiator = attr.Value;
                                break;
                            case "dfdl:terminator":
                                attribute.Terminator = attr.Value;
                                break;
                            case "ibmDfdlExtn:sampleValue":
                                break;
                            default:
                                break;
                        }
                    }
                }

                parent.Attributes.Add(attribute);
            }


        }

        private void ProcessSequence(Entity parent, XmlSchemaSequence sequence)
        {
            Console.WriteLine("Sequence");
            //dbServices.insertObject(sequence);
            ProcessItemCollection(parent, sequence.Items);
        }

        private void ProcessChoice(Entity parent, XmlSchemaChoice choice)
        {
            Console.WriteLine("Choice");
            ProcessItemCollection(parent, choice.Items);
        }

        private void ProcessItemCollection(Entity parent, XmlSchemaObjectCollection objs)
        {
            foreach (XmlSchemaObject obj in objs)
                ProcessSchemaObject(parent, obj);
        }

        private void ProcessSchemaObject(Entity parent, XmlSchemaObject obj)
        {
            if (obj is XmlSchemaElement)
                ProcessElement(parent, obj as XmlSchemaElement);
            if (obj is XmlSchemaChoice)
                ProcessChoice(parent, obj as XmlSchemaChoice);
            if (obj is XmlSchemaSequence)
                ProcessSequence(parent, obj as XmlSchemaSequence);
        }


        private static void ValidationCallbackOne(object sender, ValidationEventArgs args)
        {
            Console.WriteLine("Exception Severity: " + args.Severity);
            Console.WriteLine(args.Message);
        }

        /*
        private static DataType GetDataType(XmlSchemaSimpleType xmlType)
        {
            var dt = xmlType.Datatype;
            DataType result;
            string attributeType = dt.ValueType.Name.ToLower();
            switch (attributeType)
            {
                case "string":
                    result = _domainProxy.GetDataType("string");
                    break;
                default:
                    throw new Exception(string.Format("DataType {0} is not recognized", dt.ValueType.Name.ToLower()));
            }
            return result;
        }*/


        private string GetDataType(XmlSchemaType xmlType)
        {
            var dt = xmlType.Datatype;
            string attributeType = dt.ValueType.Name.ToLower();

            return attributeType;

            /*
            switch (attributeType)
            {
                case "base64Binary":
                    result = _domainProxy.GetDataType("Binary");
                    break;
                case "boolean":
                    result = _domainProxy.GetDataType("Boolean");
                    break;
                case "token":
                case "normalizedString":
                case "string":
                    result = _domainProxy.GetDataType("String");
                    break;
                case "decimal":
                case "float":
                case "double":
                    result = _domainProxy.GetDataType("Number");
                    break;
                case "byte":
                case "short":
                case "int":
                case "integer":
                case "long":
                case "unsignedByte":
                case "unsignedInt":
                case "unsignedLong":
                case "unsignedShort":
                case "positiveinteger":
                    result = _domainProxy.GetDataType("Int");
                    break;
                case "datetime":
                case "time":
                    result = _domainProxy.GetDataType("DateTime");
                    break;
                default:
                    return _domainProxy.GetDataType("Entity");
            }*/

        }
    }
}
