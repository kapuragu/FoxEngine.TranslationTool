﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using FfntTool.Ffnt;

namespace FfntTool
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string path = args[i];
                if (File.Exists(path))
                {
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    string outputPath = Path.Combine(Path.GetDirectoryName(path), fileName);

                    if (path.EndsWith(".ffnt", StringComparison.InvariantCultureIgnoreCase))
                    {
                        UnpackFfnt(path, fileName, outputPath);
                    }
                    else if (path.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase))
                    {
                        PackFfnt(path, fileName, outputPath);
                    }
                }
            };
        }

        private static void PackFfnt(string path, string fileName, string outputPath)
        {
            string outputFilePath = Path.Combine(outputPath, fileName + ".ffnt");

            using (FileStream fontInputStream = new FileStream(path, FileMode.Open))
            using (FileStream outputStream = new FileStream(outputFilePath, FileMode.Create))
            {
                XmlSerializer serializer = new XmlSerializer(typeof (FfntFile),
                    new[] {typeof (GlyphMap), typeof (FontData)});


                FfntFile ffntFile = serializer.Deserialize(fontInputStream) as FfntFile;
                if (ffntFile != null)
                {
                    ffntFile.Entries.OfType<FontData>().Single().Data = ReadFontLayers(Path.GetDirectoryName(path),
                        fileName);
                    ffntFile.Write(outputStream);
                }
            }
        }

        private static byte[] ReadFontLayers(string directory, string fileName)
        {
            const int maxLayers = 8;
            List<byte[]> layers = new List<byte[]>();

            for (int i = 0; i < maxLayers; i++)
            {
                byte[] layer = ReadFontLayer(directory, fileName, i);
                if (layer != null)
                {
                    layers.Add(layer);
                }
            }

            if (layers.Count > 0)
            {
                byte[] result = new byte[layers.First().Length];

                foreach (var layer in layers)
                {
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = (byte) (result[i] | layer[i]);
                    }
                }
                return result;
            }
            return null;
        }

        private static byte[] ReadFontLayer(string directory, string fileName, int layerIndex)
        {
            byte layerMask = (byte) (1 << layerIndex);
            string layerFilePath = Path.Combine(directory, string.Format("{0}_{1}" + ".png", fileName, layerIndex));

            try
            {
                using (Image image = Image.FromFile(layerFilePath))
                using (Bitmap bitmap = new Bitmap(image))
                {
                    byte[] result = new byte[bitmap.Width*bitmap.Height];

                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            var pixel = bitmap.GetPixel(x, y);
                            if (pixel.R == 255 && pixel.G == 255 && pixel.B == 255)
                            {
                                result[y*bitmap.Width + x] = layerMask;
                            }
                        }
                    }
                    return result;
                }
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }

        private static void UnpackFfnt(string path, string fileName, string outputPath)
        {
            using (FileStream inputStream = new FileStream(path, FileMode.Open))
            {
                FfntFile ffntFile = FfntFile.ReadFfntFile(inputStream);

                if (ffntFile.Entries.Count != 2)
                    return;

                GlyphMap glyphMap = ffntFile.Entries.ElementAt(0) as GlyphMap;
                FontData fontData = ffntFile.Entries.ElementAt(1) as FontData;

                if (glyphMap == null || fontData == null)
                    return;

                SaveFont(ffntFile, fileName, outputPath);
                Size size = CalculateSize(fontData.Data.Length);
                SaveFontLayers(fontData.Data, size, fileName, outputPath);
            }
        }

        private static Size CalculateSize(int area)
        {
            int height;
            int width;
            if (Math.Sqrt(area)%1 == 0) // Squared (e.g. the latin font)
            {
                height = width = (int) Math.Sqrt(area);
            }
            else if (area/2%2 == 0 && Math.Sqrt(area/2)%1 == 0) // Rectangle with width = 2*height (e.g. the kanji font)
            {
                height = (int) Math.Sqrt(area/2);
                width = 2*height;
            }
            else
            {
                // TODO: Add width and height options to specify custom dimensions.
                throw new Exception("Unknown bitmap font dimensions.");
            }

            return new Size(width, height);
        }

        private static void SaveFont(FfntFile ffntFile, string fileName, string outputPath)
        {
            XmlSerializer serializer = new XmlSerializer(typeof (FfntFile), new[] {typeof (GlyphMap), typeof (FontData)});

            string outputFilePath = Path.Combine(outputPath, fileName + ".xml");
            using (var outputStream = new FileStream(outputFilePath, FileMode.Create))
            {
                serializer.Serialize(outputStream, ffntFile);
            }
        }

        private static void SaveFontLayers(byte[] ffntData, Size size, string fileName, string outputPath)
        {
            const int maxLayers = 8;
            for (int i = 0; i < maxLayers; i++)
            {
                byte[] layer = GetLayer(ffntData, i);
                if (layer != null)
                {
                    using (Bitmap bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format8bppIndexed))
                    {
                        BitmapData bitmapData = bitmap.LockBits(
                            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                            ImageLockMode.WriteOnly,
                            bitmap.PixelFormat);

                        Marshal.Copy(layer, 0, bitmapData.Scan0, layer.Length);

                        bitmap.UnlockBits(bitmapData);

                        string outputFilePath = Path.Combine(outputPath, string.Format("{0}_{1}" + ".png", fileName, i));
                        bitmap.Save(outputFilePath, ImageFormat.Png);
                    }
                }
            }
        }

        private static byte[] GetLayer(byte[] ffntData, int layerIndex)
        {
            int layerMask = 1 << layerIndex;
            byte[] layer = new byte[ffntData.Length];
            bool emptyLayer = true;

            for (int i = 0; i < ffntData.Length; i++)
            {
                if ((ffntData[i] & layerMask) > 0)
                {
                    layer[i] = 0xFF;
                    emptyLayer = false;
                }
            }

            return emptyLayer ? null : layer;
        }
    }
}
