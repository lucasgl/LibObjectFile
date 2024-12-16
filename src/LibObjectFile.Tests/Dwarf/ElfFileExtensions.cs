using System.Collections.Generic;
using LibObjectFile;
using System.IO;
using System.Linq;
using LibObjectFile.Dwarf;
using LibObjectFile.Elf;
using System.Text;

namespace LibObjectFile.Tests.Dwarf;

public static class ElfFileExtensions
{
    public static List<VariableEntry> GetVariableEntries(this ElfObjectFile elf)
    {
        var entries = new List<VariableEntry>();
        var symbolTable = elf.Sections.FirstOrDefault(s => s is ElfSymbolTable) as ElfSymbolTable;
        var objectSymbols = symbolTable.Entries.Where(e => e.Type == ElfSymbolType.Object);

        var dwarf = DwarfFile.ReadFromElf(elf, out DiagnosticBag _);
        var compilationDIES = dwarf.InfoSection.Units
            .Where(unit => unit.Root != null && unit.Root.Children.Count > 0)
            .Select(unit => unit.Root);

        //Loop all compilation DIEs childrens that are variable information entries.
        foreach (var compilationDIE in compilationDIES)
        {
            foreach (var variableDIE in compilationDIE.Children.Where(die => die.Tag.Equals(DwarfTagEx.Variable)))
            {
                var fileName = Path.GetFileName(compilationDIE.FindAttributeByKey(DwarfAttributeKind.Name).ValueAsObject.ToString());
                var objectName = variableDIE.FindAttributeByKey(DwarfAttributeKind.Name).ValueAsObject.ToString();
                var variableOffset = objectSymbols.FirstOrDefault(i => i.Name.Value.TrimStart('_') == objectName).Value;
                entries.AddMember(fileName, variableDIE, objectName, variableOffset);
            }
        }
        return entries;
    }

    public static void AddMember(this List<VariableEntry> variableEntries, string fileName, DwarfDIE die, string parentMemberName, ulong offset, DwarfTagEx? parentType = null)
    {
        uint? bitsize = die.FindAttributeByKey(DwarfAttributeKind.BitSize)?.ValueAsU32;

        var rootType = die.FindAttributeByKey(DwarfAttributeKind.Type).ValueAsObject as DwarfDIE;
        var typeRef = NavigateToBaseType(rootType);
        var isDieVariableType = die.Tag.Equals(DwarfTag.Variable);
        var isDieMemberType = die.Tag.Equals(DwarfTag.Member);
        var isPointerType = typeRef.Tag.Equals(DwarfTag.PointerType);
        var isArrayType = isDieVariableType ? typeRef.Tag.Equals(DwarfTag.ArrayType) : rootType.Tag.Equals(DwarfTag.ArrayType);
        uint upperBound = 0;

        if (isArrayType) upperBound = rootType.Children[0].FindAttributeByKey(DwarfAttributeKind.UpperBound).ValueAsU32 + 1;
        StringBuilder name = new();
        name.Append(parentMemberName);
        if(isDieMemberType) name.Append($".{die.FindAttributeByKey(DwarfAttributeKind.Name)?.ValueAsObject ?? "unnamed"}");
        name.Append($"{(bitsize.HasValue ? (":" + bitsize) : "")}{(isPointerType ? "*" : "")}{(isArrayType ? "[" + upperBound + "]" : "")}");
        var tagType = typeRef.Tag;
        var typeName = (typeRef.FindAttributeByKey(DwarfAttributeKind.Name)?.ValueAsObject.ToString()) ?? (isPointerType ? "unsigned long" : typeRef.Tag.ToString());
        if (typeRef.Tag.Equals(DwarfTag.StructureType) || typeRef.Tag.Equals(DwarfTag.UnionType))
        {
            uint memberBitSize = 0;
            uint byteSize = 0;
            uint? previousBitsize = null;
            foreach (var member in typeRef.Children)
            {
                bitsize = member.FindAttributeByKey(DwarfAttributeKind.BitSize)?.ValueAsU32;
                if (memberBitSize > 0 && memberBitSize < (byteSize * 8))
                {
                    if (
                        //previous bitmap never reached type size;
                        (!bitsize.HasValue && previousBitsize.HasValue) ||
                       //new bitmap overflow type size, it will be allocated in a new position.
                       (bitsize.HasValue && (bitsize.Value + memberBitSize > byteSize * 8))
                    )
                    {
                        memberBitSize = 0;
                        offset += byteSize;
                    }
                }
                previousBitsize = bitsize;
                variableEntries.AddMember(fileName, member, name.ToString(), offset, typeRef.Tag);
                bool updateOffset = member.Parent is not DwarfDIEUnionType;
                byteSize = GetByteSize(member.FindAttributeByKey(DwarfAttributeKind.Type).ValueAsObject as DwarfDIE); //update the bytesize of the current member by type.

                if (bitsize.HasValue)
                {
                    updateOffset = false;
                    memberBitSize += bitsize.Value;
                    if (memberBitSize >= (byteSize * 8))
                        updateOffset = true;
                }

                if (updateOffset)
                {
                    memberBitSize = 0;
                    offset += byteSize;
                }
            }
        }
        else
        {
            if(parentType != null) tagType = parentType.Value;
            variableEntries.Add(new(fileName, name.ToString(), tagType.ToString(), typeName, offset));
        }
    }

    public static DwarfDIE NavigateToBaseType(this DwarfDIE die, int level = 0)
    {
        if (die.Tag.Equals(DwarfTag.PointerType) ||
            die.Tag.Equals(DwarfTag.UnionType) ||
            die.Tag.Equals(DwarfTag.SubroutineType) ||
            die.Tag.Equals(DwarfTag.BaseType) ||
            die.Tag.Equals(DwarfTag.StructureType))
            return die;

        if (die.FindAttributeByKey(DwarfAttributeKind.Type)?.ValueAsObject is not DwarfDIE typeRef)
            return die; //just return the current die as the found type and let the client decide the resolution.
        return typeRef.NavigateToBaseType(level + 1);
    }

    /// <summary>
    /// Find the first reference type that contains byte size.
    /// </summary>
    /// <param name="die"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    public static DwarfDIE NavigateToByteSizeableType(this DwarfDIE die)
    {
        if (die.Tag.Equals(DwarfTag.PointerType) || //if pointer return itself so user can decide the size.
           die.Tag.Equals(DwarfTag.SubroutineType) ||
           die.Tag.Equals(DwarfTag.Subprogram) ||
           die.FindAttributeByKey(DwarfAttributeKind.ByteSize) is not null ||
           //found the last type in the chain that does not reference another type. This should not happen but not time to find the issue in the base lib.
           die.FindAttributeByKey(DwarfAttributeKind.Type)?.ValueAsObject is not DwarfDIE typeRef)
        {
            return die;
        }

        return typeRef.NavigateToByteSizeableType();
    }

    public static uint GetByteSize(this DwarfDIE die, uint pointerSize = 4)
    {
        var typeWithByteSize = die.NavigateToByteSizeableType();
        //this should not happen, but the lib can't parse some enumerations correctly, return size 1 for this cases (typedef types).
        var typeSize = typeWithByteSize.FindAttributeByKey(DwarfAttributeKind.ByteSize)?.ValueAsU32 ?? (typeWithByteSize.Tag.Equals(DwarfTag.Typedef) ? 1u : pointerSize); //subrotine or pointer would not contain bytesize, and we default to 4 as size of 32bits address.
        if (die is DwarfDIEArrayType)
            return (die.Children[0].FindAttributeByKey(DwarfAttributeKind.UpperBound).ValueAsU32 + 1) * typeSize;

        return typeSize;
    }
}
