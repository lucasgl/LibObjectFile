﻿// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license.
// See the license.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LibObjectFile.Utils;

namespace LibObjectFile.Dwarf
{
    [DebuggerDisplay("Count = {Ranges.Count,nq}")]
    public class DwarfAddressRangeTable : DwarfSection
    {
        public DwarfAddressRangeTable()
        {
            Ranges = new List<DwarfAddressRange>();
            Version = 2;
        }

        public ushort Version { get; set; }

        public bool Is64BitEncoding { get; set; }

        public bool Is64BitAddress { get; set; }

        public byte SegmentSelectorSize { get; set; }

        public ulong DebugInfoOffset { get; private set; }

        public DwarfUnit Unit { get; set; }
        
        public List<DwarfAddressRange> Ranges { get; }

        public ulong HeaderLength => Size - DwarfHelper.SizeOfUnitLength(Is64BitEncoding);

        internal void Read(DwarfReader reader)
        {
            if (reader.Context.DebugAddressRangeStream.Stream == null)
            {
                return;
            }

            var currentStream = reader.Stream;
            try
            {
                reader.Stream = reader.Context.DebugAddressRangeStream;
                ReadInternal(reader, reader.Context.DebugAddressRangeStream.Printer);
            }
            finally
            {
                reader.Stream = currentStream;
            }
        }

        private void ReadInternal(DwarfReaderWriter reader, TextWriter dumpLog)
        {
            Offset = reader.Offset;
            var unitLength = reader.ReadUnitLength();
            Is64BitEncoding = reader.Is64BitEncoding;
            Version = reader.ReadU16();

            if (Version != 2)
            {
                reader.Diagnostics.Error(DiagnosticId.DWARF_ERR_VersionNotSupported, $"Version {Version} for .debug_aranges not supported");
                return;
            }

            DebugInfoOffset = reader.ReadUIntFromEncoding();

            var address_size = reader.ReadU8();
            if (address_size != 4 && address_size != 8)
            {
                reader.Diagnostics.Error(DiagnosticId.DWARF_ERR_InvalidAddressSize, $"Unsupported address size {address_size}. Must be 4 (32 bits) or 8 (64 bits).");
                return;
            }
            // TODO: Support AddressKind instead of Is64BitAddress
            Is64BitAddress = address_size == 8;

            var segment_selector_size = reader.ReadU8();
            SegmentSelectorSize = segment_selector_size;

            var align = (ulong)segment_selector_size + (ulong)address_size * 2;

            // SPECS 7.21: The first tuple following the header in each set begins at an offset that is a multiple of the size of a single tuple
            reader.Offset = AlignHelper.AlignToUpper(reader.Offset, align);

            while (true)
            {
                ulong segment = 0;
                switch (segment_selector_size)
                {
                    case 2:
                        segment = reader.ReadU16();
                        break;

                    case 4:
                        segment = reader.ReadU32();
                        break;

                    case 8:
                        segment = reader.ReadU64();
                        break;

                    case 0:
                        break;
                }

                ulong address = 0;
                ulong length = 0;
                switch (address_size)
                {
                    case 2:
                        address = reader.ReadU16();
                        length = reader.ReadU16();
                        break;
                    case 4:
                        address = reader.ReadU32();
                        length = reader.ReadU32();
                        break;
                    case 8:
                        address = reader.ReadU64();
                        length = reader.ReadU64();
                        break;
                }

                if (segment == 0 && address == 0 && length == 0)
                {
                    break;
                }

                Ranges.Add(new DwarfAddressRange(segment, address, length));
            }

            Size = reader.Offset - Offset;
        }

        public override void Verify(DiagnosticBag diagnostics)
        {
            base.Verify(diagnostics);

            if (Version != 2)
            {
                diagnostics.Error(DiagnosticId.DWARF_ERR_VersionNotSupported, $"Non supported version {Version} for .debug_aranges");
            }

            if (Unit == null)
            {
                diagnostics.Error(DiagnosticId.DWARF_ERR_InvalidNullUnitForAddressRangeTable, $"Invalid {nameof(Unit)} for .debug_aranges that cannot be null");
            }
            else
            {
                var parentFile = Unit.GetParentFile();
                if (this.Parent != parentFile)
                {
                    diagnostics.Error(DiagnosticId.DWARF_ERR_InvalidParentUnitForAddressRangeTable, $"Invalid parent {nameof(DwarfFile)} of {nameof(Unit)} for .debug_aranges that doesn't match the parent of instance");
                }
            }
        }

        public override bool TryUpdateLayout(DiagnosticBag diagnostics)
        {
            ulong sizeOf = 0;
            // unit_length
            sizeOf += DwarfHelper.SizeOfUnitLength(Is64BitEncoding);

            // version
            sizeOf += 2;

            // debug_info_offset
            sizeOf += DwarfHelper.SizeOfUInt(Is64BitEncoding);

            // Address size
            sizeOf += 1;

            // segment selector size
            sizeOf += 1;

            var align = (ulong)SegmentSelectorSize + (ulong)(Is64BitAddress ? 8 : 4 ) * 2;

            // SPECS 7.21: The first tuple following the header in each set begins at an offset that is a multiple of the size of a single tuple
            sizeOf = AlignHelper.AlignToUpper(sizeOf, align);

            // SizeOf ranges + 1 (for last 0 entry)
            sizeOf += ((ulong)Ranges.Count + 1UL) * align;

            Size = sizeOf;

            if (Unit != null)
            {
                DebugInfoOffset = Unit.Offset;
            }

            return true;
        }

        internal void Write(DwarfWriter writer)
        {
            if (writer.Context.DebugAddressRangeStream.Stream == null)
            {
                return;
            }

            var previousStream = writer.Stream;
            writer.Stream = writer.Context.DebugAddressRangeStream.Stream;

            try
            {
                WriteInternal(writer);
            }
            finally
            {
                writer.Stream = previousStream;
            }
        }

        private void WriteInternal(DwarfWriter writer)
        {
            var startOffset = writer.Offset;

            // unit_length
            writer.WriteUnitLength(Size - DwarfHelper.SizeOfUnitLength(Is64BitEncoding));

            // version
            writer.WriteU16(Version);

            // debug_info_offset
            writer.WriteUInt(DebugInfoOffset);

            // address_size
            var address_size = (byte) (Is64BitAddress ? 8 : 4);
            writer.WriteU8(address_size);

            writer.WriteU8(SegmentSelectorSize);

            var align = (ulong)SegmentSelectorSize + (ulong)address_size * 2;

            // SPECS 7.21: The first tuple following the header in each set begins at an offset that is a multiple of the size of a single tuple
            var nextOffset = AlignHelper.AlignToUpper(writer.Offset, align);
            for (ulong offset = writer.Offset; offset < nextOffset; offset++)
            {
                writer.WriteU8(0);
            }
            Debug.Assert(writer.Offset == nextOffset);

            foreach (var range in Ranges)
            {
                if (SegmentSelectorSize != 0)
                {
                    switch (SegmentSelectorSize)
                    {
                        case 2:
                            writer.WriteU16((ushort)range.Segment);
                            break;
                        case 4:
                            writer.WriteU32((uint)range.Segment);
                            break;
                        case 8:
                            writer.WriteU64((ulong)range.Segment);
                            break;
                    }
                }

                switch (address_size)
                {
                    case 2:
                        writer.WriteU16((ushort)range.Address);
                        writer.WriteU16((ushort)range.Length);
                        break;
                    case 4:
                        writer.WriteU32((uint)range.Address);
                        writer.WriteU32((uint)range.Length);
                        break;
                    case 8:
                        writer.WriteU64(range.Address);
                        writer.WriteU64(range.Length);
                        break;
                }
            }

            if (SegmentSelectorSize != 0)
            {
                switch (SegmentSelectorSize)
                {
                    case 2:
                        writer.WriteU16(0);
                        break;
                    case 4:
                        writer.WriteU32(0);
                        break;
                    case 8:
                        writer.WriteU64(0);
                        break;
                }
            }

            switch (address_size)
            {
                case 2:
                    writer.WriteU32(0);
                    break;
                case 4:
                    writer.WriteU64(0);
                    break;
                case 8:
                    writer.WriteU64(0);
                    writer.WriteU64(0);
                    break;
            }

            Debug.Assert(writer.Offset - startOffset == Size);
        }
    }
}