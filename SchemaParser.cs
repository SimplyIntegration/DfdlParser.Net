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

        public string TranslationXslt { get; set; }


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
            //var stream = new MemoryStream();
            var stream = new FileStream(@"d:\dfdltests\output.xml", FileMode.Create);
            var xmlWriter = new XmlTextWriter(stream, Encoding.UTF8);
            xmlWriter.Formatting = Formatting.Indented;

            doc.WriteContentTo(xmlWriter);
            xmlWriter.Flush();
            stream.Flush();
            stream.Position = 0;

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

        private bool ProcessElement(XmlSchemaElement elem, ref int startIndex, XmlDocument xmlDoc, XmlNode xmlElement, DfdlProperties dfdlProperties)
        {
            bool doMore = true;
            int loopIndex = 0;
            
            if (elem.ElementSchemaType is XmlSchemaComplexType)
            {
                //Repopulate dfdlProperties
                dfdlProperties = new DfdlProperties(elem);
                while ((doMore || IsNextChildValid(elem, startIndex) ) && loopIndex < dfdlProperties.MaxOccurs)
                //while (doMore && loopIndex < dfdlProperties.MaxOccurs)
                {
                    if (IsValidInitiator(elem, startIndex))
                    {
                        if (loopIndex == 0)
                            xmlElement = xmlElement.AppendChild(xmlDoc.CreateElement(elem.Name));
                        else
                            xmlElement = xmlElement.ParentNode.InsertAfter(xmlDoc.CreateElement(elem.Name), xmlElement);
                        var ct =
                            elem.ElementSchemaType as XmlSchemaComplexType;

                        doMore = ProcessSchemaObject(ct.ContentTypeParticle, ref startIndex, xmlDoc, xmlElement,
                            dfdlProperties, doMore);
                        loopIndex++;
                    }
                    else
                    {
                        doMore = false;
                    }
                    
                }


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
                    return true;
                }

                    
            }
            return false;

        }

        //How can this be made a lot better? or can the algorithm be improved so calling this is not required?
        private bool IsNextChildValid(XmlSchemaElement elem, int startIndex)
        {
            var ct = elem.ElementSchemaType as XmlSchemaComplexType;
            var x = ct.ContentTypeParticle;

            if (x is XmlSchemaSequence)
            {
                var seq = (XmlSchemaSequence) x;
                if (seq.Items != null && seq.Items.Count > 0)
                {
                    var firstChild = seq.Items[0];
                    if (firstChild is XmlSchemaElement && ((XmlSchemaElement)firstChild).ElementSchemaType is XmlSchemaComplexType)
                    {
                        var dfdlProperties = new DfdlProperties((XmlSchemaElement) firstChild);

                        if (dfdlProperties.Initiator != null && _startPos == 0)
                            _startPos = dfdlProperties.Initiator.Length;

                        if (startIndex < _lines.Count() && _lines[startIndex].StartsWith(dfdlProperties.Initiator))
                        {
                            return true;
                        }

                    }
                }
            }

            return false;
        }

        private bool IsValidInitiator(XmlSchemaElement elem, int startIndex)
        {
            var dfdlProperties = new DfdlProperties(elem);

            
            if (dfdlProperties.Initiator != null && _startPos == 0)
                _startPos = dfdlProperties.Initiator.Length;

            if (startIndex < _lines.Count() && _lines[startIndex].StartsWith(dfdlProperties.Initiator))
            {
                return true;
            }
                    
            return false;
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

        private bool ProcessSequence(XmlSchemaSequence sequence, ref int startIndex, XmlDocument xmlDoc, XmlNode xmlElement, DfdlProperties dfdlProperties, bool doMore)
        {
            doMore = ProcessItemCollection(sequence.Items, ref startIndex, xmlDoc, xmlElement, dfdlProperties, doMore);
            return doMore;
        }

        private bool ProcessChoice(XmlSchemaChoice choice, ref int startIndex, XmlDocument xmlDoc, XmlNode xmlElement, DfdlProperties dfdlProperties, bool doMore)
        {
            Console.WriteLine("Choice");
            ProcessItemCollection(choice.Items, ref startIndex, xmlDoc, xmlElement, dfdlProperties, doMore);
            return true;
        }

        private bool ProcessItemCollection(XmlSchemaObjectCollection objs, ref int startIndex, XmlDocument xmlDoc, XmlNode xmlElement, DfdlProperties dfdlProperties, bool doMore)
        {
            foreach (XmlSchemaObject obj in objs)
                doMore = ProcessSchemaObject(obj, ref startIndex, xmlDoc,xmlElement, dfdlProperties, doMore);
            return doMore;
        }

        private bool ProcessSchemaObject(XmlSchemaObject obj, ref int startIndex, XmlDocument xmlDoc, XmlNode xmlElement, DfdlProperties dfdlProperties, bool doMore)
        {
            if (obj is XmlSchemaElement)
                doMore = ProcessElement(obj as XmlSchemaElement, ref startIndex, xmlDoc, xmlElement, dfdlProperties);
            if (obj is XmlSchemaChoice)
                doMore = ProcessChoice(obj as XmlSchemaChoice, ref startIndex, xmlDoc, xmlElement, dfdlProperties, doMore);
            if (obj is XmlSchemaSequence)
                doMore = ProcessSequence( obj as XmlSchemaSequence, ref startIndex, xmlDoc, xmlElement, dfdlProperties, doMore);

            return doMore;
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

                int result = 0;

                if (int.TryParse(elem.MinOccursString, out result))
                {
                    MinOccurs = result;
                }
                else
                {
                    MinOccurs = 1;
                }
                
                if(elem.MaxOccursString == null)
                    MaxOccurs = 1;
                else if (int.TryParse(elem.MaxOccursString, out result))
                {
                    MaxOccurs = result;
                }
                else
                {
                    MaxOccurs = int.MaxValue;
                }

            }
        }

        public string Initiator { get; set; }
        public string Terminator { get; set; }

        public int MinOccurs { get; set; }

        public int MaxOccurs { get; set; }


    }
}
