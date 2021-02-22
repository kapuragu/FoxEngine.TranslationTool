using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using SubpTool.Subp;
using System.Collections.Generic;
using System.Diagnostics;
using SubpTool.Utility;

namespace SubpTool
{
    public static class Program
    {
        static HashSet<string> encodingArgs = new HashSet<string> {
            "-rus",
            "-jpn",
            "-ara",
            "-por",
            "-fre",
            "-ger",
            "-spa",
            "-ita",
            "-eng",
        };

        const string DefaultDictionaryPath = "subp_dictionary.txt";

        public static void Main(string[] args)
        {
            if (args.Length == 0 || args.Length > 3)
            {
                ShowUsageInfo();
                return;
            }

            string path = args[0];
            Encoding encoding = GetEncodingFromArgument("");
            bool outputHashes = false;
            string dictionaryPath = DefaultDictionaryPath;

            if (args.Length > 1)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (encodingArgs.Contains(arg))
                    {
                        if (encoding != null)
                        {
                            Console.WriteLine("Can only define one encoding");
                            return;
                        }
                        encoding = GetEncodingFromArgument(arg);
                    }
                    else
                    {
                        if (arg.ToLower() == "-outputhashes" || arg.ToLower() == "-o")
                        {
                            outputHashes = true;
                        }
                        else
                        {
                            path = arg;
                        }
                    }
                }
            }

            if (File.Exists(path) == false)
            {
                Console.WriteLine("Could not find file " + path);
                return;
            }

            if (path.EndsWith(".subp"))
            {
                var dictionary = GetDictionary(dictionaryPath);
                UnpackSubp(path, encoding, dictionary, outputHashes);
                return;
            }
            if (path.EndsWith(".xml"))
            {
                PackSubp(path, encoding);
                return;
            }

            ShowUsageInfo();
        }

        private static Dictionary<uint, string> GetDictionary(string path)
        {
            var dictionary = new Dictionary<uint, string>();
            try
            {
                var values = File.ReadAllLines(path);
                foreach (var value in values)
                {
                    var code = Fox.GetStrCode32(value);
                    DebugCheckCollision(dictionary, code, value);
                    dictionary[code] = value;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to read the dictionary " + path + " " + e);
            }

            return dictionary;
        }

        private static Encoding GetEncodingFromArgument(string encoding)
        {
            if (encoding == null)
            {
                encoding = "";
            }
            switch (encoding)
            {
                case "-rus":
                    return Encoding.GetEncoding("ISO-8859-5");
                case "-jpn":
                case "-ara":
                case "-por":
                    return Encoding.UTF8;
                case "-fre":
                case "-ger":
                case "-spa":
                case "-ita":
                case "-eng":
                default:
                    return Encoding.GetEncoding("ISO-8859-1");
            }
        }

        private static void UnpackSubp(string path, Encoding encoding, Dictionary<uint, string> dictionary, bool outputHashes = false)
        {
            string fileDirectory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string outputFileName = fileName + ".xml";
            string outputFilePath = Path.Combine(fileDirectory, outputFileName);


            using (FileStream inputStream = new FileStream(path, FileMode.Open))
            using (XmlWriter outputWriter = XmlWriter.Create(outputFilePath, new XmlWriterSettings
            {
                NewLineHandling = NewLineHandling.Entitize,
                Indent = true
            }))
            {
                SubpFile subpFile = SubpFile.ReadSubpFile(inputStream, encoding, dictionary);
                // TODO: Change XML Encoding
                XmlSerializer serializer = new XmlSerializer(typeof(SubpFile));
                serializer.Serialize(outputWriter, subpFile);
                if (outputHashes)
                {
                    HashSet<string> uniqueHashes = new HashSet<string>();
                    foreach (SubpEntry entry in subpFile.Entries)
                    {
                        ulong hash = entry.SubtitleIdHash;
                        uniqueHashes.Add(hash.ToString());
                    }
                    List<string> hashes = uniqueHashes.ToList<string>();
                    hashes.Sort();
                    string hashesOutputPath = Path.Combine(fileDirectory, string.Format("{0}_subpIdHashes.txt", Path.GetFileName(path)));
                    File.WriteAllLines(hashesOutputPath, hashes.ToArray<string>());
                }
            }
        }

        private static void PackSubp(string path, Encoding encoding)
        {
            string fileDirectory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string outputFileName = fileName + ".subp";
            string outputFilePath = Path.Combine(fileDirectory, outputFileName);

            using (FileStream inputStream = new FileStream(path, FileMode.Open))
            using (XmlReader xmlReader = XmlReader.Create(inputStream, CreateXmlReaderSettings<SubpFile>()))
            using (FileStream outputStream = new FileStream(outputFilePath, FileMode.Create))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SubpFile));
                SubpFile subpFile = serializer.Deserialize(xmlReader) as SubpFile;
                subpFile?.Write(outputStream, encoding);
            }
        }

        private static XmlReaderSettings CreateXmlReaderSettings<T>()
        {
            XmlSchemas schemas = new XmlSchemas();
            XmlSchemaExporter exporter = new XmlSchemaExporter(schemas);
            XmlTypeMapping mapping = new XmlReflectionImporter().ImportTypeMapping(typeof(T));
            exporter.ExportTypeMapping(mapping);
            XmlSchemaSet schemaSet = new XmlSchemaSet();
            foreach (XmlSchema schema in schemas)
            {
                schemaSet.Add(schema);
            }

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.Schemas = schemaSet;
            settings.ValidationType = ValidationType.Schema;
            settings.ValidationEventHandler += HandleXmlReaderValidation;
            return settings;
        }

        private static void HandleXmlReaderValidation(object sender, ValidationEventArgs args)
        {
            if (args.Severity == XmlSeverityType.Warning)
            {
                Console.WriteLine($"{args.Severity} at line '{args.Exception?.LineNumber}' position '{args.Exception?.LinePosition}':\n{args.Message}");
            }
            else
            {
                throw args.Exception;
            }
        }

        [Conditional("DEBUG")]
        private static void DebugCheckCollision(Dictionary<uint, string> dictionary, uint code, string newValue)
        {
            string originalValue;
            if (dictionary.TryGetValue(code, out originalValue))
            {
                Debug.WriteLine("StrCode32 collision detected ({0}). Overwriting '{1}' with '{2}'", code, originalValue, newValue);
            }
        }

        private static void ShowUsageInfo()
        {
            string[] usageInfo = {
                "SubpTool by Atvaark",
                "Description",
                "  Converts Fox Engine subtitle pack (.subp) files to xml.",
                "Usage:",
                "  SubpTool.exe filename.subp -Unpacks the subtitle pack file",
                "  SubpTool.exe [-<encoding>] filename.xml -Packs the subtitle pack file",
                "Options:",
                "  Encoding: -ara, -eng, -fre, -ger, -ita, -jpn, -por, -rus and -spa",
                "  -OutputHashes - Outputs all StrCode32 subtitleId hashes to <fileName>_subtitleIdHashes.txt",
            };

            foreach (string line in usageInfo)
            {
                Console.WriteLine(line);
            }
        }
    }
}
