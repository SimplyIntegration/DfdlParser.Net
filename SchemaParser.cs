using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace DfdlParser
{
    class SchemaParser
    {
        
        private List<Entity> _entities;
        private XmlNamespaceManager _nsmgr;
        private Dictionary<string, string> _dfdlProperties;

        public SchemaParser(string dfdl)
        {
            _nsmgr = new XmlNamespaceManager(new NameTable());
            _nsmgr.AddNamespace("xsd", "http://www.w3.org/2001/XMLSchema");
            _nsmgr.AddNamespace("dfdl", "http://www.ogf.org/dfdl/dfdl-1.0/");
            _dfdlProperties = new Dictionary<string, string>();

            Load(dfdl);
        }



        private void Load(string dfdl)
        {
            _entities = new List<Entity>();
            XmlSchema custSchema = ReadAndCompileSchema(dfdl);

            //Get global properties
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

                                    var dfdlDefineEscapeSchema = appInfoNodes.SingleOrDefault(x => x.Name == "dfdl:defineEscapeScheme");
                                    var dfdlEscapeSchema = dfdlDefineEscapeSchema.SelectNodes("dfdl:escapeScheme", _nsmgr);
                                    foreach(var s in dfdlEscapeSchema)
                                    {
                                        if (s is XmlElement)
                                        {
                                            foreach (XmlAttribute a in ((XmlElement)s).Attributes)
                                            {
                                                _dfdlProperties.Add(a.Name, a.Value);
                                            }
                                        }
                                    }

                                    //dfdlEscapeSchema.
                                    var dfdlDefineFormat = appInfoNodes.SingleOrDefault(x => x.Name == "dfdl:defineFormat");
                                    var dfdlFormat = dfdlDefineFormat.SelectNodes("dfdl:format", _nsmgr);
                                    foreach (var s in dfdlFormat)
                                    {
                                        if (s is XmlElement)
                                        {
                                            foreach (XmlAttribute a in ((XmlElement)s).Attributes)
                                            {
                                                _dfdlProperties.Add(a.Name, a.Value);
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

        }

        public string Parse(string filename)
        {
            var fileContents = "";

            var doc = new XmlDocument();
            var rootNode = doc.CreateElement("root");

            var dfdlRoot = _entities.FirstOrDefault(e => e.IsRoot);

            using (var sr = new StreamReader(filename))
            {
                fileContents = sr.ReadToEnd();
            }

            var lines = fileContents.Split(new string[] { GetVar(_dfdlProperties["outputNewLine"]) }, StringSplitOptions.None);

            int startIndex = 0;

            ParseEntity(lines, ref startIndex, dfdlRoot, doc, rootNode);
            

            var ms = new MemoryStream();
            var sw = new StreamWriter(ms);
            ms.Position = 0;
            doc.WriteTo(new XmlTextWriter(ms, Encoding.UTF8));

            return doc.ToString();

            
        }

        private void ParseEntity(string[] fileLines, ref int startIndex, Entity entity, XmlDocument doc, XmlNode xmlElement)
        {
            foreach (var attribute in entity.Attributes)
            {
                if (attribute.DataType == "Entity")
                {
                    ParseEntityAttribute(fileLines, ref startIndex, attribute, doc, xmlElement);
                }
                else
                {
                    //ParseLine(fileLines[startIndex], ref startIndex, attribute, doc, xmlElement);
                }
                
            }
        }

        private void ParseEntityAttribute(string[] fileLines, ref int startIndex, Attribute attribute, XmlDocument doc, XmlNode xmlElement)
        {
            int startPos = 0;
            if (fileLines[startIndex].StartsWith(attribute.Initiator))
            {
                xmlElement = xmlElement.AppendChild(doc.CreateElement(attribute.Name));
                startPos = attribute.Initiator.Length;

            }

            var entityType = (from x in _entities
                              where x.Name == attribute.Name
                              select x).SingleOrDefault();

            
            if (entityType.Attributes != null)
            {
                foreach (var childAttribute in entityType.Attributes)
                {
                    if (childAttribute.DataType == "Entity")
                    {
                        ParseEntityAttribute(fileLines, ref startIndex, childAttribute, doc, xmlElement);
                    }
                    else
                    {
                        ParseLine(fileLines[startIndex], startPos, entityType, doc, xmlElement);
                        startIndex++;
                    }
                    
                }
                    
            }
        }


        private void ParseLine(string fileLine, int startPos, Entity entityType, XmlDocument doc, XmlNode xmlElement)
        {
            foreach (var attribute in entityType.Attributes)
            {
                if (attribute.Length > 0)
                {
                    var childElement = xmlElement.AppendChild(doc.CreateElement(attribute.Name));
                    childElement.InnerText = fileLine.Substring(startPos, attribute.Length);
                    startPos += attribute.Length;

                }
            }
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

                _entities.Add(entity);
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
                    entityType = _entities.Where(x => x.Name == ns + ":" + elem.Name).SingleOrDefault();
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

        private static string GetVar(string input)
        {
            switch (input)
            {
                case "%CR;%LF;":
                    return "\n";
                default:
                    return input;
            }
        }
    }
}
