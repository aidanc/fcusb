// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Usb2Xchange.FlexColorAspiLauncher
{
    internal sealed class PeImageInfo
    {
        internal PeImageInfo(ushort machine, ushort optionalHeaderMagic,
            ICollection<string> exports)
        {
            Machine = machine;
            OptionalHeaderMagic = optionalHeaderMagic;
            Exports = new HashSet<string>(exports,
                StringComparer.Ordinal);
        }

        internal ushort Machine { get; private set; }
        internal ushort OptionalHeaderMagic { get; private set; }
        internal HashSet<string> Exports { get; private set; }
    }

    internal static class PeExportInspector
    {
        internal static PeImageInfo Inspect(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 0x40 || bytes[0] != (byte)'M' ||
                bytes[1] != (byte)'Z')
            {
                throw new InvalidDataException("Missing DOS header: " + path);
            }
            int pe = ReadInt32(bytes, 0x3c);
            RequireRange(bytes, pe, 24, "PE header");
            if (bytes[pe] != (byte)'P' || bytes[pe + 1] != (byte)'E' ||
                bytes[pe + 2] != 0 || bytes[pe + 3] != 0)
            {
                throw new InvalidDataException("Missing PE signature: " + path);
            }

            ushort machine = ReadUInt16(bytes, pe + 4);
            ushort sectionCount = ReadUInt16(bytes, pe + 6);
            ushort optionalSize = ReadUInt16(bytes, pe + 20);
            int optional = pe + 24;
            RequireRange(bytes, optional, optionalSize, "optional header");
            ushort magic = ReadUInt16(bytes, optional);
            int dataDirectory = magic == 0x010b ? optional + 96 :
                magic == 0x020b ? optional + 112 : -1;
            if (dataDirectory < 0 || dataDirectory + 8 > optional + optionalSize)
            {
                throw new InvalidDataException("Unsupported PE optional header.");
            }

            uint exportRva = ReadUInt32(bytes, dataDirectory);
            var sections = new List<Section>();
            int sectionTable = optional + optionalSize;
            for (int index = 0; index < sectionCount; ++index)
            {
                int entry = checked(sectionTable + index * 40);
                RequireRange(bytes, entry, 40, "section table");
                sections.Add(new Section(
                    ReadUInt32(bytes, entry + 12),
                    ReadUInt32(bytes, entry + 8),
                    ReadUInt32(bytes, entry + 16),
                    ReadUInt32(bytes, entry + 20)));
            }

            var exports = new List<string>();
            if (exportRva != 0)
            {
                int exportOffset = RvaToOffset(bytes, sections, exportRva);
                RequireRange(bytes, exportOffset, 40, "export directory");
                uint nameCount = ReadUInt32(bytes, exportOffset + 24);
                uint namesRva = ReadUInt32(bytes, exportOffset + 32);
                if (nameCount > 4096)
                {
                    throw new InvalidDataException("Implausible export count.");
                }
                int namesOffset = RvaToOffset(bytes, sections, namesRva);
                RequireRange(bytes, namesOffset, checked((int)nameCount * 4),
                    "export name table");
                for (int index = 0; index < nameCount; ++index)
                {
                    uint nameRva = ReadUInt32(bytes, namesOffset + index * 4);
                    int nameOffset = RvaToOffset(bytes, sections, nameRva);
                    exports.Add(ReadAsciiZ(bytes, nameOffset, 256));
                }
            }
            return new PeImageInfo(machine, magic, exports);
        }

        private static int RvaToOffset(byte[] bytes, List<Section> sections,
            uint rva)
        {
            foreach (Section section in sections)
            {
                uint span = Math.Max(section.VirtualSize, section.RawSize);
                if (rva >= section.VirtualAddress &&
                    (ulong)rva < (ulong)section.VirtualAddress + span)
                {
                    ulong offset = (ulong)section.RawOffset +
                        (rva - section.VirtualAddress);
                    if (offset >= (ulong)bytes.LongLength)
                    {
                        break;
                    }
                    return checked((int)offset);
                }
            }
            if (rva < bytes.Length)
            {
                return checked((int)rva);
            }
            throw new InvalidDataException(string.Format(
                "RVA 0x{0:X8} is outside the PE image.", rva));
        }

        private static string ReadAsciiZ(byte[] bytes, int offset,
            int maximumLength)
        {
            RequireRange(bytes, offset, 1, "export name");
            int end = offset;
            while (end < bytes.Length && end - offset < maximumLength &&
                bytes[end] != 0)
            {
                ++end;
            }
            if (end == bytes.Length || end - offset == maximumLength)
            {
                throw new InvalidDataException("Unterminated export name.");
            }
            return Encoding.ASCII.GetString(bytes, offset, end - offset);
        }

        private static void RequireRange(byte[] bytes, int offset, int length,
            string description)
        {
            if (offset < 0 || length < 0 ||
                (long)offset + length > bytes.LongLength)
            {
                throw new InvalidDataException(
                    "Invalid " + description + " range.");
            }
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            RequireRange(bytes, offset, 2, "16-bit field");
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static int ReadInt32(byte[] bytes, int offset)
        {
            return unchecked((int)ReadUInt32(bytes, offset));
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            RequireRange(bytes, offset, 4, "32-bit field");
            return (uint)bytes[offset] | ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) |
                ((uint)bytes[offset + 3] << 24);
        }

        private sealed class Section
        {
            internal Section(uint virtualAddress, uint virtualSize,
                uint rawSize, uint rawOffset)
            {
                VirtualAddress = virtualAddress;
                VirtualSize = virtualSize;
                RawSize = rawSize;
                RawOffset = rawOffset;
            }

            internal uint VirtualAddress { get; private set; }
            internal uint VirtualSize { get; private set; }
            internal uint RawSize { get; private set; }
            internal uint RawOffset { get; private set; }
        }
    }
}
