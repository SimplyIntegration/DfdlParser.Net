using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

namespace DfdlParser
{
    public class SchemaParser
    {
        private string[] _lines;
        private XmlNamespaceManager _nsmgr;
        private Dictionary<string, string> _dfdlProperties;
        private XmlSchema _dfdlSchema;
        private int _startPos = 0;

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
            _dfdlSchema = ReadAndCompileSchema(dfdl);


            //Get global properties
            foreach (var include in _dfdlSchema.Includes)
            {
                if (include is XmlSchemaImport)
                {
                    var schema = ((XmlSchemaImport)include).Schema;
                    foreach (var item in schema.Items)
                    {
                        var schemaAnnotation = item as XmlSchemaAnnotation;
                        if (schemaAnnotation != null)
                        {
                            var annotation = schemaAnnotation;
                            foreach (var annotationItem in annotation.Items)
                            {
                                if (annotationItem is XmlSchemaAppInfo)
                                {
                                    var appInfoNodes = ((XmlSchemaAppInfo)annotationItem).Markup;

                                    var dfdlDefineEscapeSchema = appInfoNodes.SingleOrDefault(x => x.Name == "dfdl:defineEscapeScheme");
                                    var dfdlEscapeSchema = dfdlDefineEscapeSchema.SelectNodes("dfdl:escapeScheme", _nsmgr);
                                    foreach (var s in dfdlEscapeSchema)
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

            

        }

        public string Parse(string filename)
        {
            var fileContents = "";

            var doc = new XmlDocument();
            var xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);

            var root = doc.DocumentElement;
            doc.InsertBefore(xmlDeclaration, root);

            var rootNode = doc.AppendChild(doc.CreateElement(string.Empty, "root", string.Empty));

            using (var sr = new StreamReader(filename))
            {
                fileContents = sr.ReadToEnd();
            }

            _lines = fileContents.Split(new string[] { GetVar(_dfdlProperties["outputNewLine"]) }, StringSplitOptions.None);

            int startIndex = 0;

            foreach (XmlSchemaElement elem in
                                _dfdlSchema.Elements.Values)
            {
                var dfdlProperties = new DfdlProperties(elem);


                ProcessElement(elem, ref startIndex, doc, rootNode, dfdlProperties);
            }

            var result = "";
            using (var sw = new StringWriter())
            {
                using (var xw = XmlWriter.Create(sw))
                {
                    doc.WriteTo(xw);
                    xw.Flush();
                    result = sw.GetStringBuilder().ToString();
                }
            }

            return result;


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

        private void ProcessElement(XmlSchemaElement elem, ref int startIndex, XmlDocument xmlDoc, XmlNode xmlElement, DfdlProperties dfdlProperties)
        {
            int startPos = 0;
            var name = elem.Name;
            string ns = elem.QualifiedName.Namespace;
            if (string.IsNullOrEmpty(ns))
            {
                ns = "Default";
            }

            if (elem.ElementSchemaType is XmlSchemaComplexType)
            {
                xmlElement = xmlElement.AppendChild(xmlDoc.CreateElement(elem.Name));               

                var ct =
                    elem.ElementSchemaType as XmlSchemaComplexType;

                ProcessSchemaObject(ct.ContentTypeParticle, ref startIndex, xmlDoc, xmlElement, new DfdlProperties(elem) );

                //_entities.Add(entity);
            }

            if (xmlElement != null)
            {
                if(dfdlProperties.Initiator != null && _startPos == 0)
                    _startPos = dfdlProperties.Initiator.Length;

                if (startIndex < _lines.Count() && _lines[startIndex].StartsWith(dfdlProperties.Initiator))
                {
                    ParseField(elem, ref startIndex, xmlDoc, xmlElement, dfdlProperties);
                    if (_lines[startIndex].Length <= _startPos + 1)
                    {
                        startIndex++;
                        _startPos = 0;
                    }
                }

                    
            }


        }


        private void ParseField(XmlSchemaElement elem, ref int startIndex, XmlDocument xmlDoc, XmlNode xmlElement, DfdlProperties dfdlProperties)
        {
            var attr = elem.UnhandledAttributes.SingleOrDefault(x => x.Name == "dfdl:length");
            if (attr != null)
            {
                int length = int.Parse(attr.Value);
                var value = _lines[startIndex].Substring(_startPos, length);

                var newElement = xmlDoc.CreateElement(elem.Name);
                newElement.InnerText = value;
                xmlElement.AppendChild(newElement);
                _startPos += length;
            }
            


        }

        private void ProcessSequence(XmlSchemaSequence sequence, ref int startIndex, XmlDocument xmlDoc, XmlNode xmlElement, DfdlProperties dfdlProperties)
        {

            ProcessItemCollection(sequence.Items, ref startIndex, xmlDoc, xmlElement, dfdlProperties);
        }

        private void ProcessChoice(XmlSchemaChoice choice, ref int startIndex, XmlDocument xmlDoc, XmlNode xmlElement, DfdlProperties dfdlProperties)
        {
            Console.WriteLine("Choice");
            ProcessItemCollection(choice.Items, ref startIndex, xmlDoc, xmlElement, dfdlProperties);
        }

        private void ProcessItemCollection(XmlSchemaObjectCollection objs, ref int startIndex, XmlDocument xmlDoc, XmlNode xmlElement, DfdlProperties dfdlProperties)
        {
            foreach (XmlSchemaObject obj in objs)
                ProcessSchemaObject(obj, ref startIndex, xmlDoc,xmlElement, dfdlProperties);
        }

        private void ProcessSchemaObject(XmlSchemaObject obj, ref int startIndex, XmlDocument xmlDoc, XmlNode xmlElement, DfdlProperties dfdlProperties)
        {
            if (obj is XmlSchemaElement)
                ProcessElement(obj as XmlSchemaElement, ref startIndex, xmlDoc, xmlElement, dfdlProperties);
            if (obj is XmlSchemaChoice)
                ProcessChoice(obj as XmlSchemaChoice, ref startIndex, xmlDoc, xmlElement, dfdlProperties);
            if (obj is XmlSchemaSequence)
                ProcessSequence( obj as XmlSchemaSequence, ref startIndex, xmlDoc, xmlElement, dfdlProperties);
        }


        private static void ValidationCallbackOne(object sender, ValidationEventArgs args)
        {
            Console.WriteLine("Exception Severity: " + args.Severity);
            Console.WriteLine(args.Message);
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

    class DfdlProperties
    {
        public DfdlProperties(XmlSchemaElement elem)
        {
            if (elem != null)
            {
                Initiator = elem.UnhandledAttributes.SingleOrDefault(x => x.Name == "dfdl:initiator")!=null ? elem.UnhandledAttributes.SingleOrDefault(x => x.Name == "dfdl:initiator").Value : "";
                Terminator = elem.UnhandledAttributes.SingleOrDefault(x => x.Name == "dfdl:terminator") != null ? elem.UnhandledAttributes.SingleOrDefault(x => x.Name == "dfdl:terminator").Value : "";
            }
        }

        public string Initiator { get; set; }
        public string Terminator { get; set; }


    }
}
