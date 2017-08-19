using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace SubpTool.Subp
{
    [XmlType("SubpFile")]
    public class SubpFile
    {
        private const short MagicNumber = 0x0113;

        public SubpFile()
        {
            Entries = new List<SubpEntry>();
        }

        [XmlArray("Entries")]
        public List<SubpEntry> Entries { get; set; }

        public static SubpFile ReadSubpFile(Stream input, Encoding encoding, Dictionary<uint, string> subtitleIdDictionary)
        {
            SubpFile subpFile = new SubpFile();
            subpFile.Read(input, encoding, subtitleIdDictionary);
            return subpFile;
        }

        public void Read(Stream input, Encoding encoding, Dictionary<uint, string> subtitleIdDictionary)
        {
            BinaryReader reader = new BinaryReader(input, Encoding.Default, true);
            short magicNumber = reader.ReadInt16();
            short entryCount = reader.ReadInt16();

            List<SubpIndex> indices = new List<SubpIndex>();
            for (int i = 0; i < entryCount; i++)
            {
                indices.Add(SubpIndex.ReadSubpIndex(input));
            }

            foreach (var index in indices)
            {
                input.Position = index.Offset;
                var entry = SubpEntry.ReadSubpEntry(input, encoding);
                entry.SubtitleIdHash = index.SubtitleIdHash;
                string subtitleId;
                if (subtitleIdDictionary.TryGetValue(entry.SubtitleIdHash, out subtitleId)) {
                    entry.SubtitleId = subtitleId;
                }

                Entries.Add(entry);
            }
        }

        public void Write(Stream outputStream, Encoding encoding)
        {
            BinaryWriter writer = new BinaryWriter(outputStream, encoding, true);
            writer.Write(MagicNumber);
            writer.Write((short) Entries.Count);
            long indicesPosition = outputStream.Position;
            outputStream.Position = outputStream.Position + SubpIndex.Size*Entries.Count;

            List<SubpIndex> indices = new List<SubpIndex>();
            foreach (var entry in Entries)
            {
                entry.UpdateSubtitleIdHash();
                indices.Add(entry.GetIndex(outputStream));
                entry.Write(outputStream, encoding);
            }
            long endPosition = outputStream.Position;
            outputStream.Position = indicesPosition;
            foreach (var index in indices)
            {
                index.Write(outputStream);
            }
            outputStream.Position = endPosition;
        }
    }
}
