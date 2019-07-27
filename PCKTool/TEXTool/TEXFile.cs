﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HedgeLib.Exceptions;
using HedgeLib.IO;
using Scarlet.Drawing;
using Scarlet.IO;

namespace TEXTool
{
    public class TEXFile : FileBase
    {

        public class Frame
        {
            public float frameWidth;
            public float frameHeight;
            public float LeftScale;
            public float TopScale;
            public float RightScale;
            public float BottomScale;
        }

        public List<Frame> Frames = new List<Frame>();
        public short SheetWidth = 0;
        public short SheetHeight = 0;
        public byte[] SheetPixels = null;

        public override void Load(Stream fileStream)
        {
            var reader = new ExtendedBinaryReader(fileStream);
            string sig = ReadPCKSig(reader);
            if (sig != "Texture")
                throw new InvalidSignatureException("Texture", sig);
            int textureSectionSize = reader.ReadInt32();
            int compression = reader.ReadInt32();
            int version = reader.ReadInt32();
            if (version != 0x8100000 && version != 0x1100000 && version != 0x2100000)
            {
                Console.WriteLine("Error: File Not Supported Yet! Expected: {0:X4} or {1:X4} or {2:X4} Got {3:X4}", 0x8100000, 0x1100000, 0x2100000, version);
                Console.ReadKey(true);
            }
            int dataLength = reader.ReadInt32();
            SheetWidth = reader.ReadInt16();
            SheetHeight = reader.ReadInt16();
            SheetPixels = reader.ReadBytes(dataLength);

            // DXT Decompression
            ImageBinary image;
            switch (compression)
            {
                case 1: // DXT1
                    image = new ImageBinary(SheetWidth, SheetHeight, PixelDataFormat.FormatDXT1Rgba,
                        Endian.LittleEndian, PixelDataFormat.FormatAbgr8888, Endian.LittleEndian, SheetPixels);
                    SheetPixels = image.GetOutputPixelData(0);
                    break;
                case 2: // DXT5
                    image = new ImageBinary(SheetWidth, SheetHeight, PixelDataFormat.FormatDXT5,
                        Endian.LittleEndian, PixelDataFormat.FormatAbgr8888, Endian.LittleEndian, SheetPixels);
                    SheetPixels = image.GetOutputPixelData(0);
                    break;
                default:
                    break;
            }


            // Parts
            sig = ReadPCKSig(reader);
            if (sig != "Parts")
                throw new InvalidSignatureException("Parts", sig);
            int partsSectionSize = reader.ReadInt32();
            int partCount = reader.ReadInt32();
            for (int i = 0; i < partCount; ++i)
            {
                reader.JumpAhead(8); // Unknown
                float frameWidth = reader.ReadSingle();
                float frameHeight = reader.ReadSingle();
                float frameXScale = reader.ReadSingle();
                float frameYScale = reader.ReadSingle();
                float frameWidthScale = reader.ReadSingle();
                float frameHeightScale = reader.ReadSingle();
                Frames.Add(new Frame
                {
                    frameWidth = frameWidth,
                    frameHeight = frameHeight,
                    LeftScale = frameXScale,
                    TopScale = frameYScale,
                    RightScale = frameWidthScale,
                    BottomScale = frameHeightScale
                });
            }

            // Anime
            reader.FixPadding(0x8);
            sig = ReadPCKSig(reader);
            if (sig != "Anime")
                throw new InvalidSignatureException("Anime", sig);
            int animeSectionSize = reader.ReadInt32();
            
        }

        // May not work, Only tested with title.tex
        public override void Save(Stream fileStream)
        {
            var writer = new ExtendedBinaryWriter(fileStream);
            
            WritePCKSig(writer, "Texture");
            writer.AddOffset("HeaderSize");
            writer.Write(0x4000);
            writer.Write(0x8100000); // Version? Most are 10 08 while some others are 10 02
            writer.AddOffset("Unknown");
            writer.Write(SheetWidth);
            writer.Write(SheetHeight);
            writer.Write(SheetPixels);
            writer.FillInOffset("Unknown", (uint)writer.BaseStream.Position - 0x28);
            
            // Parts
            writer.FillInOffset("HeaderSize");
            long header = writer.BaseStream.Position;
            WritePCKSig(writer, "Parts");
            writer.AddOffset("HeaderSize");
            writer.Write(Frames.Count);

            foreach (var frame in Frames)
            {
                writer.WriteNulls(8);
                writer.Write(frame.frameWidth);
                writer.Write(frame.frameHeight);
                writer.Write(frame.LeftScale);
                writer.Write(frame.TopScale);
                writer.Write(frame.RightScale);
                writer.Write(frame.BottomScale);
            }
            writer.FillInOffset("HeaderSize", (uint)(writer.BaseStream.Position - header));
            writer.FixPadding(0x8);

            // Anime
            header = writer.BaseStream.Position;
            WritePCKSig(writer, "Anime");
            writer.AddOffset("HeaderSize");
            writer.WriteNulls(4);
            writer.FillInOffset("HeaderSize", (uint)(writer.BaseStream.Position - header));
            writer.FixPadding(0x10);

        }

        public void SaveImage(string path)
        {
            var Image = new Bitmap(SheetWidth, SheetHeight, PixelFormat.Format32bppArgb);

            // Copy Data into Bitmap
            var bitmap = Image.LockBits(new Rectangle(0, 0, SheetWidth, SheetHeight), ImageLockMode.ReadWrite, Image.PixelFormat);
            int bitmapDataSize = bitmap.Stride * bitmap.Height;
            byte[] buffer = new byte[4];
            for (int i = 0; i < SheetPixels.Length; i += 4)
            {
                buffer[0] = SheetPixels[i + 0];
                buffer[1] = SheetPixels[i + 1];
                buffer[2] = SheetPixels[i + 2];
                buffer[3] = SheetPixels[i + 3];
                SheetPixels[i + 0] = buffer[2];
                SheetPixels[i + 1] = buffer[1];
                SheetPixels[i + 2] = buffer[0];
                SheetPixels[i + 3] = buffer[3];
            }

            Marshal.Copy(SheetPixels, 0, bitmap.Scan0, SheetWidth * SheetHeight * 4);
            Image.UnlockBits(bitmap);
            Image.Save(path);
        }

        public string ReadPCKSig(ExtendedBinaryReader reader)
        {
            string s = Encoding.ASCII.GetString(reader.ReadBytes(0x14));
            return s.Substring(0, s.IndexOf(" ", StringComparison.Ordinal));
        }

        public void WritePCKSig(ExtendedBinaryWriter writer, string sig)
        {
            writer.WriteSignature(sig + new string(' ', 0x14 - sig.Length));
        }
    }
}
