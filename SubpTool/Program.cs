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
using System.Reflection;

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

        const string DefaultDictionaryPath = "subp_subtitleid_dictionary.txt";
        public static string GetPathNearApp(string name)
        {
            if (!File.Exists(name))
            {
                return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/" + Path.GetFileName(name);
            }
            return name;
        }
        private const string fileType = "subp";

        class RunSettings
        {
            public bool outputHashes = false;
            public string gameId = "TPP";
            public string outputPath = @"D:\Github\mgsv-lookup-strings";
        }//RunSettings

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                ShowUsageInfo();
                return;
            }

            Encoding encoding = null;
            string dictionaryPath = DefaultDictionaryPath;

            RunSettings runSettings = new RunSettings();

            List<string> files = new List<string>();
            int idx = 0;
            if (args[idx].ToLower() == "-outputhashes" || args[idx].ToLower() == "-o")
            {
                runSettings.outputHashes = true;
                runSettings.outputPath = args[idx += 1];
                runSettings.gameId = args[idx += 1].ToUpper();
                Console.WriteLine("Adding to file list");
                for (int i = idx += 1; i < args.Length; i++)
                {
                    AddToFiles(files, args[i], fileType);
                }
            }
            else
            {
                Console.WriteLine("Adding to file list");
                foreach (var arg in args)
                {
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
                        AddToFiles(files, arg, "*");
                    }

                }//foreach args
            }//handle args

            if (encoding == null) {
                encoding = GetEncodingFromArgument("");
            }

            if (files.Count() == 0)
            {
                Console.WriteLine("Could not find any files");
                return;
            }

            foreach (string path in files) { 
                if (path.EndsWith(".subp"))
                {                        
                    var dictionary = GetDictionary(GetPathNearApp(dictionaryPath));
                    if (!runSettings.outputHashes)
                    {
                        Console.WriteLine($"Unpacking {path}");
                        UnpackSubp(path, encoding, dictionary);
                    } else
                    {
                        OutputHashes(path, encoding, dictionary, runSettings);
                    }
                }
                if (path.EndsWith(".xml"))
                {
                    Console.WriteLine($"Packing {path}");
                    PackSubp(path, encoding);
                }
            }

           // ShowUsageInfo();
        }//main

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

        public enum SortType
        {
            GZ = 0x01,
            RANDOM = 0x02,
            ASCENDING = 0x03,
        };

        public enum VoiceVersion
        {
            jpn = 0x00,
            eng = 0x01,
            ps3 = 0x0e,
        };

        public enum EncodingId
        {
            jpn = 0x00,
            eng = 0x01,
            fre = 0x02,
            ita = 0x03,
            ger = 0x04,
            spa = 0x05,
            por = 0x06,
            rus = 0x07,
            ara = 0x08,
            cht = 0x09,
            kor = 0x0a,

            gzpc = 0x4c,
            gzps3 = 0x42,
        };
        private static void UnpackSubp(string path, Encoding encoding, Dictionary<uint, string> dictionary)
        {
            string fileDirectory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string outputFileName = fileName + ".xml";
            string outputFilePath = Path.Combine(fileDirectory, outputFileName);

            SortType sortType;
            VoiceVersion voiceVersion;
            EncodingId encodingId;

            using (FileStream inputStream = new FileStream(path, FileMode.Open))
            {
                BinaryReader reader = new BinaryReader(inputStream, Encoding.Default, true);
                //short magicNumber = reader.ReadInt16();
                //why not just take these bytes and use encoding with them?
                byte versionByte = reader.ReadByte();
                sortType = (SortType)(byte)(versionByte & 0b11);
                voiceVersion = (VoiceVersion)(byte)((versionByte & 0xF0) >> 4);
                encodingId = (EncodingId)reader.ReadByte();

                Encoding newEncoding;
                switch (encodingId)
                {
                    case EncodingId.rus:
                        newEncoding = Encoding.GetEncoding("ISO-8859-5");
                        break;
                    case EncodingId.jpn:
                    case EncodingId.ara:
                    case EncodingId.por:
                        newEncoding = Encoding.UTF8;
                        break;
                    case EncodingId.fre:
                    case EncodingId.ger:
                    case EncodingId.spa:
                    case EncodingId.ita:
                    case EncodingId.eng:
                    default:
                        newEncoding = encoding;
                        break;
                }
                if (sortType == SortType.ASCENDING|| sortType == SortType.RANDOM)
                {
                    encoding = newEncoding;
                };
            }

            using (FileStream inputStream = new FileStream(path, FileMode.Open))
            using (XmlWriter outputWriter = XmlWriter.Create(outputFilePath, new XmlWriterSettings {
                NewLineHandling = NewLineHandling.Entitize,
                Indent = true,
                Encoding = encoding,
            }))
            {

                SubpFile subpFile = new SubpFile();
                subpFile.SortType = (uint)sortType;
                subpFile.VoiceType = (uint)voiceVersion;
                subpFile.LanguageType = (uint)encodingId;
                subpFile.Read(inputStream, encoding, dictionary);

                //tex TODO: think if it's better for user to sort by (unhashed) subTitleId
                // TODO: Change XML Encoding
                XmlSerializer serializer = new XmlSerializer(typeof(SubpFile));
                serializer.Serialize(outputWriter, subpFile);
            }
        }

        private static void PackSubp(string path, Encoding encoding)
        {
            string fileDirectory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string outputFileName = fileName + ".subp";
            string outputFilePath = Path.Combine(fileDirectory, outputFileName);

            //tex really seems like there should be a way to access the xml declaration from xmlreader iteself, but I guess not
            Encoding xmlEncoding = FindXmlEncoding(path);
            if (xmlEncoding != null) {
                encoding = xmlEncoding;
            }

            using (FileStream inputStream = new FileStream(path, FileMode.Open))
            using (XmlReader xmlReader = XmlReader.Create(inputStream, CreateXmlReaderSettings<SubpFile>()))
            using (FileStream outputStream = new FileStream(outputFilePath, FileMode.Create))
            {       
                XmlSerializer serializer = new XmlSerializer(typeof(SubpFile));
                SubpFile subpFile = serializer.Deserialize(xmlReader) as SubpFile;
                //DEBUGNOW
                foreach (var entry in subpFile.Entries)
                {
                    entry.UpdateSubtitleIdHash();
                }
                if (subpFile.SortType == (uint)SortType.ASCENDING)
                {
                    //tex vanilla files are sorted by hash ascending
                    //Sorting thanks to Joey:
                    int CompareHashes(SubpEntry x, SubpEntry y)
                    {
                        var xHash = x.SubtitleIdHash;
                        var yHash = y.SubtitleIdHash;
                        if (xHash < yHash)
                            return -1;
                        else if (xHash == yHash)
                            return 0;
                        else
                            return +1;
                    }
                    subpFile.Entries.Sort(CompareHashes);
                }
                subpFile?.Write(outputStream, encoding);
                for (int i = 0; i < subpFile.Entries.Count; i++)
                {
                    Console.WriteLine($"#{i}={subpFile.Entries[i].SubtitleIdHash}|{subpFile.Entries[i].SubtitleId}");
                }
                Console.ReadLine();
            }
        }

        private static void OutputHashes(string path, Encoding encoding, Dictionary<uint, string> dictionary, RunSettings runSettings)
        {
            using (FileStream inputStream = new FileStream(path, FileMode.Open))
            {
                SubpFile subpFile = SubpFile.ReadSubpFile(inputStream, encoding, dictionary);

                //var rawHashes = new List<string>();
                var subtitleIdHashSet = new HashSet<string>();
                foreach (SubpEntry entry in subpFile.Entries)
                {
                    ulong hash = entry.SubtitleIdHash;
                    subtitleIdHashSet.Add(hash.ToString());
                }
                WriteHashes(subtitleIdHashSet, path, "SubtitleId", "StrCode32", runSettings.gameId, runSettings.outputPath);
            }
        }//OutputHashes

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

        private static Encoding FindXmlEncoding(string path) 
        {
            using (XmlTextReader reader = new XmlTextReader(path)) {
                reader.Read();
                if (reader.NodeType == XmlNodeType.XmlDeclaration) {
                    while (reader.MoveToNextAttribute()) {
                        if (reader.Name == "encoding") {
                            string encodingName = reader.Value;
                            if (encodingName != null && encodingName != "") {
                                return Encoding.GetEncoding(encodingName);
                            }
                        }
                    }
                }
            }
            return null;
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

        private static void AddToFiles(List<string> files, string path, string fileType)
        {
            if (File.Exists(path))
            {
                files.Add(path);
            }
            else
            {
                if (Directory.Exists(path))
                {
                    var dirFiles = Directory.GetFiles(path, $"*.{fileType}", SearchOption.AllDirectories);
                    foreach (var file in dirFiles)
                    {
                        files.Add(file);
                    }
                }
            }
        }//AddToFiles
        private static string GetAssetsPath(string inputPath)
        {
            int index = inputPath.LastIndexOf("Assets");
            if (index != -1)
            {
                return inputPath.Substring(index);
            }
            return Path.GetFileName(inputPath);
        }//GetAssetsPath
        //tex outputs to mgsv-lookup-strings repo layout
        private static void WriteHashes(HashSet<string> hashSet, string inputFilePath, string hashName, string hashTypeName, string gameId, string outputPath)
        {
            if (hashSet.Count > 0)
            {
                string assetsPath = GetAssetsPath(inputFilePath);
                //OFF string destPath = {inputFilePath}_{hashName}_{hashTypeName}.txt" //Alt: just output to input file path_whatev
                string destPath = Path.Combine(outputPath, $"{fileType}\\Hashes\\{gameId}\\{hashName}\\{assetsPath}_{hashName}_{hashTypeName}.txt");

                List<string> hashes = hashSet.ToList<string>();
                hashes.Sort();

                string destDir = Path.GetDirectoryName(destPath);
                DirectoryInfo di = Directory.CreateDirectory(destDir);
                File.WriteAllLines(destPath, hashes.ToArray());
            }
        }//WriteHashes
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
