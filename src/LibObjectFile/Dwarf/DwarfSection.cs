﻿// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license.
// See the license.txt file in the project root for more information.

namespace LibObjectFile.Dwarf
{
    public abstract class DwarfSection : DwarfContainer
    {
        public new DwarfFile Parent => (DwarfFile) base.Parent;

        public override void Verify(DiagnosticBag diagnostics)
        {
        }
    }
}