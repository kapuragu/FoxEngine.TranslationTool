using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace SubpTool.Subp
{
    [XmlType("SubpFile")]
    public class SubpFile
    {
        private const short MagicNumber = 0x0113; //tex: DEBUGNOW where did atvaark derive this from? GZ magicnumber is 0x014C, 
        //and TPP is two bytes, 1st being?? but varies among files, and 2nd being langId
        //unk0 - 0x 12, 13, 1b ..??

        //PlatformId and EncodingId:
        //GZ:
        //01 4C - PC GZ
        //01 42 - PS3 GZ
        //Seemingly no difference in voice version!

        //TPP:
        //Version:
        //13 - PC ENG
        //03 - PC JPN
        //E1 - PS3 (uses 01 for two Arabic files it has, GZ leftover?)

        //EncodingId:
        //TPP langids
        //byte
        //00 - jpn
        //01 - eng
        //02 - fre
        //03 - ita
        //04 - ger
        //05 - spa
        //06 - por
        //07 - rus
        //08 - ara?
        //09 - cht (ssd)
        //0a - kor (ssd)

        // what other langs did tpp have that I don't have files for? ara (gz had)? 

        public SubpFile()
        {
            Entries = new List<SubpEntry>();
        }

        [XmlAttribute("SortType")]
        public uint SortType { get; set; }

        [XmlAttribute("VoiceType")]
        public uint VoiceType { get; set; }

        [XmlAttribute("LanguageType")]
        public uint LanguageType { get; set; }

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
            //why not just take these bytes and use encoding with them?
            /*byte versionByte = reader.ReadByte();
            FileVersion versionId = (FileVersion)(byte)(versionByte & 0x0F);
            VoiceVersion voiceVersion = (VoiceVersion)(byte)((versionByte & 0xF0)>>4);
            EncodingId encodingId = (EncodingId)reader.ReadByte();*/

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

            //writer.Write(MagicNumber);
            writer.Write((byte)(SortType&0b11 | (byte)(VoiceType <<4)));
            writer.Write((byte)LanguageType);

            writer.Write((short) Entries.Count);
            long indicesPosition = outputStream.Position;
            outputStream.Position = outputStream.Position + SubpIndex.Size*Entries.Count;

            List<SubpIndex> indices = new List<SubpIndex>();

            foreach (var entry in Entries)
            {
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
