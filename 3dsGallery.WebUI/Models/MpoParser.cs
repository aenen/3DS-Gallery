namespace _3dsGallery.WebUI.Models
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Loads jpegs from a .mpo file.
    /// </summary>
    /// <remarks>
    /// Based on 
    /// CIPA-DC-007-Translation-2009 
    /// Multi-picture format
    /// http://www.cipa.jp/english/hyoujunka/kikaku/pdf/DC-007_E.pdf
    /// See also
    /// http://www.exif.org/Exif2-2.PDF
    /// </remarks>
    public static class MpoParser
    {
        private const ushort MARKER_SOI = 0xFFD8;
        private const ushort MARKER_APP1 = 0xFFE1;
        private const ushort MARKER_APP2 = 0xFFE2;
        private const ushort MARKER_DQT = 0xFFDB;
        private const ushort MARKER_DHT = 0xFFC4;
        private const ushort MARKER_DRI = 0xFFDD;
        private const ushort MARKER_SOF = 0xFFC0;
        private const ushort MARKER_SOS = 0xFFDA;
        private const ushort MARKER_EOI = 0xFFD9;

        private const uint MP_LITTLE_ENDIAN = 0x49492A00;

        /// <summary>
        /// Gets the image sources from the specified .mpo file.
        /// </summary>
        /// <param name="path">The path to the .mpo file.</param>
        /// <returns>Enumeration of image sources.</returns>
        public static IEnumerable<Image> GetImageSources(string path)
        {
            foreach (var buffer in GetImageData(path))
            {
                yield return Image.FromStream(new MemoryStream(buffer));
            }
            yield break;
        }

        /// <summary>
        /// Gets the image data buffers (jpeg images) from the specified .mpo file.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>Enumeration of data buffers.</returns>
        public static IEnumerable<byte[]> GetImageData(string path)
        {
            using (var s = File.OpenRead(path))
            {
                using (var r = new BinaryReader(s))
                {
                    var soi = ReadUShort(r);
                    if (soi != MARKER_SOI)
                    {
                        yield break;
                        //throw new FormatException("SOI marker missing");
                    }

                    while (s.Position < s.Length)
                    {
                        var marker = ReadUShort(r);
                        var length = ReadUShort(r);

                        if (marker == MARKER_APP2)
                        {
                            // Read APP2 block
                            var identifier = Encoding.ASCII.GetString(r.ReadBytes(4));
                            length -= 4;

                            if (identifier == "MPF\0")
                            {
                                // Read MP Extensions
                                var startOfOffset = s.Position;
                                var mpEndian = r.ReadUInt();
                                var isLittleEndian = mpEndian == MP_LITTLE_ENDIAN;
                                var offsetToFirstIFD = r.ReadUInt(isLittleEndian);
                                s.Position = startOfOffset + offsetToFirstIFD;

                                var count = ReadUShort(r, isLittleEndian);

                                // Read the MP Index IFD
                                string version = null;
                                uint numberOfImages = 0;
                                uint mpEntry = 0;
                                uint imageUIDList = 0;
                                uint totalFrames = 0;
                                for (int i = 0; i < count; i++)
                                {
                                    var tag = r.ReadUShort(isLittleEndian);
                                    var type = r.ReadUShort(isLittleEndian);
                                    var count2 = r.ReadUInt(isLittleEndian);
                                    switch (tag)
                                    {
                                        case 0xB000:
                                            version = Encoding.ASCII.GetString(r.ReadBytes(4));
                                            break;
                                        case 0xB001:
                                            numberOfImages = r.ReadUInt(isLittleEndian);
                                            break;
                                        case 0xB002:
                                            mpEntry = r.ReadUInt(isLittleEndian);
                                            break;
                                        case 0xB003:
                                            imageUIDList = r.ReadUInt(isLittleEndian);
                                            break;
                                        case 0xB004:
                                            totalFrames = r.ReadUInt(isLittleEndian);
                                            break;
                                    }
                                }

                                var offsetNext = ReadUInt(r, isLittleEndian);

                                // Read the values of the MP Index IFD
                                for (uint i = 0; i < numberOfImages; i++)
                                {
                                    // var bytes = r.ReadBytes(16);
                                    var iattr = r.ReadUInt(isLittleEndian);
                                    var imageSize = r.ReadUInt(isLittleEndian);
                                    var dataOffset = r.ReadUInt(isLittleEndian);
                                    var d1EntryNo = r.ReadUShort(isLittleEndian);
                                    var d2EntryNo = r.ReadUShort(isLittleEndian);

                                    // Calculate offset from beginning of file
                                    long offset = i == 0 ? 0 : dataOffset + startOfOffset;

                                    // store the current position
                                    long o = s.Position;

                                    // read the image
                                    s.Position = offset;
                                    var image = r.ReadBytes((int)imageSize);
                                    yield return image;

                                    // restore the current position                                    
                                    s.Position = o;
                                }
                                yield break;
                            }
                        }
                        r.ReadBytes(length - 2);
                    }

                    yield break;
                }
            }
        }

        /// <summary>
        /// Reads a 2-byte unsigned integer with the specified endian.
        /// </summary>
        /// <param name="r">The reader.</param>
        /// <param name="isLittleEndian">read with little endian if set to <c>true</c>.</param>
        /// <returns>The unsigned integer.</returns>
        private static ushort ReadUShort(this BinaryReader r, bool isLittleEndian = false)
        {
            var bytes = r.ReadBytes(2);
            if (isLittleEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt16(bytes, 0);
        }

        /// <summary>
        /// Reads a 4-byte unsigned integer with the specified endian.
        /// </summary>
        /// <param name="r">The reader.</param>
        /// <param name="isLittleEndian">read with little endian if set to <c>true</c>.</param>
        /// <returns>The unsigned integer.</returns>
        private static uint ReadUInt(this BinaryReader r, bool isLittleEndian = false)
        {
            var bytes = r.ReadBytes(4);
            if (isLittleEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt32(bytes, 0);
        }
    }
}
