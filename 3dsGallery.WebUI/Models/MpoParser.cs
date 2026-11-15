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
        private const ushort MARKER_APP2 = 0xFFE2;
        private const uint MP_LITTLE_ENDIAN = 0x49492A00;

        /// <summary>
        /// Gets the image sources from the specified .mpo file.
        /// </summary>
        /// <param name="path">The path to the .mpo file.</param>
        /// <returns>Enumeration of image sources. Caller must dispose each returned image.</returns>
        public static IEnumerable<Image> GetImageSources(string path)
        {
            foreach (var buffer in GetImageData(path))
            {
                using (var stream = new MemoryStream(buffer))
                using (var image = Image.FromStream(stream))
                {
                    yield return new Bitmap(image);
                }
            }
        }

        /// <summary>
        /// Gets the image sources from a byte array (in-memory MPO/JPEG data).
        /// Returns an empty enumeration for plain JPEG files.
        /// </summary>
        /// <remarks>Caller must dispose each returned image.</remarks>
        public static IEnumerable<Image> GetImageSources(byte[] data)
        {
            foreach (var buffer in GetImageData(data))
            {
                using (var stream = new MemoryStream(buffer))
                using (var image = Image.FromStream(stream))
                {
                    yield return new Bitmap(image);
                }
            }
        }

        /// <summary>
        /// Gets the image data buffers (jpeg images) from the specified .mpo file.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>Enumeration of data buffers.</returns>
        public static IEnumerable<byte[]> GetImageData(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                foreach (var imageData in GetImageData(stream))
                    yield return imageData;
            }
        }

        /// <summary>
        /// Gets the image data buffers (jpeg images) from an in-memory byte array.
        /// </summary>
        public static IEnumerable<byte[]> GetImageData(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                foreach (var imageData in GetImageData(stream))
                    yield return imageData;
            }
        }

        private static IEnumerable<byte[]> GetImageData(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                var soi = ReadUShort(reader);
                if (soi != MARKER_SOI)
                    yield break;

                while (stream.Position < stream.Length)
                {
                    var marker = ReadUShort(reader);
                    var length = ReadUShort(reader);

                    if (marker == MARKER_APP2)
                    {
                        var identifier = Encoding.ASCII.GetString(reader.ReadBytes(4));
                        length -= 4;

                        if (identifier == "MPF\0")
                        {
                            var startOfOffset = stream.Position;
                            var mpEndian = ReadUInt(reader);
                            var isLittleEndian = mpEndian == MP_LITTLE_ENDIAN;
                            var offsetToFirstIFD = ReadUInt(reader, isLittleEndian);
                            stream.Position = startOfOffset + offsetToFirstIFD;

                            var count = ReadUShort(reader, isLittleEndian);
                            uint numberOfImages = 0;

                            for (int i = 0; i < count; i++)
                            {
                                var tag = ReadUShort(reader, isLittleEndian);
                                ReadUShort(reader, isLittleEndian);
                                ReadUInt(reader, isLittleEndian);
                                switch (tag)
                                {
                                    case 0xB000:
                                        reader.ReadBytes(4);
                                        break;
                                    case 0xB001:
                                        numberOfImages = ReadUInt(reader, isLittleEndian);
                                        break;
                                    default:
                                        ReadUInt(reader, isLittleEndian);
                                        break;
                                }
                            }

                            ReadUInt(reader, isLittleEndian);

                            for (uint i = 0; i < numberOfImages; i++)
                            {
                                ReadUInt(reader, isLittleEndian);
                                var imageSize = ReadUInt(reader, isLittleEndian);
                                var dataOffset = ReadUInt(reader, isLittleEndian);
                                ReadUShort(reader, isLittleEndian);
                                ReadUShort(reader, isLittleEndian);

                                long offset = i == 0 ? 0 : dataOffset + startOfOffset;
                                long saved = stream.Position;
                                stream.Position = offset;
                                yield return reader.ReadBytes((int)imageSize);
                                stream.Position = saved;
                            }

                            yield break;
                        }
                    }

                    reader.ReadBytes(length - 2);
                }
            }
        }

        private static ushort ReadUShort(BinaryReader reader, bool isLittleEndian = false)
        {
            var bytes = reader.ReadBytes(2);
            if (isLittleEndian != BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return BitConverter.ToUInt16(bytes, 0);
        }

        private static uint ReadUInt(BinaryReader reader, bool isLittleEndian = false)
        {
            var bytes = reader.ReadBytes(4);
            if (isLittleEndian != BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return BitConverter.ToUInt32(bytes, 0);
        }
    }
}
