﻿// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license.
// See the license.txt file in the project root for more information.

namespace LibObjectFile.Elf
{
    /// <summary>
    /// An encoder interface for the various Elf types that provides encoding of data based on LSB/MSB.
    /// </summary>
    /// <seealso cref="IElfDecoder"/>
    public interface IElfEncoder
    {
        void Encode(out RawElf.Elf32_Half dest, ushort value);
        
        void Encode(out RawElf.Elf64_Half dest, ushort value);

        void Encode(out RawElf.Elf32_Word dest, uint value);

        void Encode(out RawElf.Elf64_Word dest, uint value);

        void Encode(out RawElf.Elf32_Sword dest, int value);

        void Encode(out RawElf.Elf64_Sword dest, int value);

        void Encode(out RawElf.Elf32_Xword dest, ulong value);

        void Encode(out RawElf.Elf32_Sxword dest, long value);

        void Encode(out RawElf.Elf64_Xword dest, ulong value);

        void Encode(out RawElf.Elf64_Sxword dest, long value);

        void Encode(out RawElf.Elf32_Addr dest, uint value);

        void Encode(out RawElf.Elf64_Addr dest, ulong value);
        
        void Encode(out RawElf.Elf32_Off dest, uint offset);

        void Encode(out RawElf.Elf64_Off dest, ulong offset);

        void Encode(out RawElf.Elf32_Section dest, ushort index);

        void Encode(out RawElf.Elf64_Section dest, ushort index);

        void Encode(out RawElf.Elf32_Versym dest, ushort value);

        void Encode(out RawElf.Elf64_Versym dest, ushort value);
    }
}