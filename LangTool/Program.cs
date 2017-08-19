using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using LangTool.Lang;
using LangTool.Utility;
using System.Linq;

namespace LangTool
{
    internal class Program
    {
        const string DefaultDictionaryPath = "lang_dictionary.txt";

        private static void Main(string[] args)
        {
            if (args.Length == 0 || args.Length > 3)
            {
                ShowUsageInfo();
                return;
            }

            string path = args[0];
            bool outputHashes = false;
            string dictionaryPath = DefaultDictionaryPath;

            if (args.Length > 1) {
                for (int i = 1; i < args.Length; i++) {
                    string arg = args[i];
                    string argL = args[i].ToLower();
                    if (argL == "-outputhashes" || argL == "-o") {
                        outputHashes = true;
                    } else {
                        if (argL == "-dictionary" || argL == "-d") {
                            if (i + 1 < args.Length) {
                                dictionaryPath = args[i + 1];
                            }
                        }
                    }
                }
            }

            if (File.Exists(path) == false)
            {
                ShowUsageInfo();
                return;
            }

            string extension = Path.GetExtension(path);
            if (String.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase))
            {
                using (FileStream inputStream = new FileStream(path, FileMode.Open))
                using (StreamReader xmlReader = new StreamReader(inputStream, Encoding.UTF8))
                using (FileStream outputStream = new FileStream(path.Substring(0, path.Length - 4), FileMode.Create))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof (LangFile));
                    LangFile file = serializer.Deserialize(xmlReader) as LangFile;
                    if (file == null)
                    {
                        Console.WriteLine("XML was not not a valid LangFile");
                        return;
                    }
                    file.Write(outputStream);
                }
            }
            else if (String.Equals(extension, ".lng", StringComparison.OrdinalIgnoreCase)
                     || String.Equals(extension, ".lng2", StringComparison.OrdinalIgnoreCase))
            {
                var dictionary = GetDictionary(dictionaryPath);
                using (FileStream inputStream = new FileStream(path, FileMode.Open))
                using (FileStream outputStream = new FileStream(path + ".xml", FileMode.Create))
                using (StreamWriter xmlWriter = new StreamWriter(outputStream, Encoding.UTF8))
                {
                    LangFile file = LangFile.ReadLangFile(inputStream, dictionary);
                    XmlSerializer serializer = new XmlSerializer(typeof (LangFile));
                    serializer.Serialize(xmlWriter, file);
                    if (outputHashes) {
                        HashSet<string> uniqueHashes = new HashSet<string>();
                        foreach (LangEntry entry in file.Entries) {
                            ulong langIdHash = entry.Key;
                            uniqueHashes.Add(langIdHash.ToString("x"));
                        }
                        List<string> hashes = uniqueHashes.ToList<string>();
                        hashes.Sort();
                        string fileDirectory = Path.GetDirectoryName(path);
                        string hashesOutputPath = Path.Combine(fileDirectory, string.Format("{0}_langIdHashes.txt", Path.GetFileName(path)));
                        File.WriteAllLines(hashesOutputPath, hashes.ToArray<string>());
                    }
                }
            }
            else
            {
                ShowUsageInfo();
            }
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
            Console.WriteLine("LangTool by Atvaark\n" +
                              "  A tool for converting between Fox Engine .lng/.lng2 files and xml.\n" +
                              "Usage:\n" +
                              "  LangTool file_path.lng|file_path.lng2|file_path.xml [-Dictionary] <dictionary path> [-ExportHashes]\n" +
                              "Examples:\n" +
                              "  LangTool gz_cassette.eng.lng     - Converts the lng file to xml\n" +
                              "  LangTool gz_cassette.eng.lng.xml - Converts the xml file to lng\n" +
                              "Options:\n" +
                              "  -Dictionary <file path> - Specify file path of dictionary to use. Defaults to lang_dictionary.txt\n" +   
                              "  -OutputHashes - Outputs all StrCode32 langId hashes to <fileName>_langIdHashes.txt");
        }
    }
}
