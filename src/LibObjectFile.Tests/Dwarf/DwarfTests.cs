// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license.
// See the license.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibObjectFile.Dwarf;
using LibObjectFile.Elf;
using NUnit.Framework;

namespace LibObjectFile.Tests.Dwarf
{
    public class DwarfTests
    {
        [TestCase(0UL)]
        [TestCase(1UL)]
        [TestCase(50UL)]
        [TestCase(0x7fUL)]
        [TestCase(0x80UL)]
        [TestCase(0x81UL)]
        [TestCase(0x12345UL)]
        [TestCase(2147483647UL)] // int.MaxValue
        [TestCase(4294967295UL)] // uint.MaxValue
        [TestCase(ulong.MaxValue)]
        public void TestLEB128(ulong value)
        {
            var stream = new MemoryStream();

            stream.WriteULEB128(value);
            
            Assert.AreEqual((uint)stream.Position, DwarfHelper.SizeOfULEB128(value));

            stream.Position = 0;
            var readbackValue = stream.ReadULEB128();

            Assert.AreEqual(value, readbackValue);
        }

        [TestCase(0L)]
        [TestCase(1L)]
        [TestCase(50L)]
        [TestCase(0x7fL)]
        [TestCase(0x80L)]
        [TestCase(0x81L)]
        [TestCase(0x12345L)]
        [TestCase(2147483647L)] // int.MaxValue
        [TestCase(4294967295L)] // uint.MaxValue
        [TestCase(long.MinValue)]
        [TestCase(long.MaxValue)]
        public void TestSignedLEB128(long value)
        {
            var stream = new MemoryStream();

            {
                // check positive
                stream.WriteILEB128(value);
                Assert.AreEqual((uint)stream.Position, DwarfHelper.SizeOfILEB128(value));

                stream.Position = 0;
                var readbackValue = stream.ReadSignedLEB128();
                Assert.AreEqual(value, readbackValue);
            }

            {
                stream.Position = 0;
                // Check negative
                value = -value;
                stream.WriteILEB128(value);
                Assert.AreEqual((uint)stream.Position, DwarfHelper.SizeOfILEB128(value));

                stream.Position = 0;
                var readbackValue = stream.ReadSignedLEB128();
                Assert.AreEqual(value, readbackValue);
            }
        }


        [Test]
        public void TestDebugLineHelloWorld()
        {
            var cppName = "helloworld";
            var cppExe = $"{cppName}_debug";
            LinuxUtil.RunLinuxExe("gcc", $"{cppName}.cpp -g -o {cppExe}");

            ElfObjectFile elf;
            using (var inStream = File.OpenRead(cppExe))
            {
                Console.WriteLine($"ReadBack from {cppExe}");
                elf = ElfObjectFile.Read(inStream);
                elf.Print(Console.Out);
            }

            var elfContext = new DwarfElfContext(elf);
            var inputContext = new DwarfReaderContext(elfContext);
            inputContext.DebugLinePrinter = Console.Out;
            var dwarf = DwarfFile.Read(inputContext, out DiagnosticBag _);

            inputContext.DebugLineStream.Position = 0;

            var copyInputDebugLineStream = new MemoryStream();
            inputContext.DebugLineStream.CopyTo(copyInputDebugLineStream);
            inputContext.DebugLineStream.Position = 0;

            var outputContext = new DwarfWriterContext
            {
                IsLittleEndian = inputContext.IsLittleEndian,
                EnableRelocation = false,
                AddressSize = inputContext.AddressSize,
                DebugLineStream = new MemoryStream()
            };
            dwarf.Write(outputContext);

            Console.WriteLine();
            Console.WriteLine("=====================================================");
            Console.WriteLine("Readback");
            Console.WriteLine("=====================================================");
            Console.WriteLine();

            var reloadContext = new DwarfReaderContext()
            {
                IsLittleEndian = outputContext.IsLittleEndian,
                AddressSize = outputContext.AddressSize,
                DebugLineStream = outputContext.DebugLineStream
            };

            reloadContext.DebugLineStream.Position = 0;
            reloadContext.DebugLineStream = outputContext.DebugLineStream;
            reloadContext.DebugLinePrinter = Console.Out;

            var dwarf2 = DwarfFile.Read(reloadContext, out DiagnosticBag _);

            var inputDebugLineBuffer = copyInputDebugLineStream.ToArray();
            var outputDebugLineBuffer = ((MemoryStream)reloadContext.DebugLineStream).ToArray();
            Assert.AreEqual(inputDebugLineBuffer, outputDebugLineBuffer);
        }

        [Test]
        public void TestDebugLineLibMultipleObjs()
        {
            var cppName = "lib";
            var libShared = $"{cppName}_debug.so";
            LinuxUtil.RunLinuxExe("gcc", $"{cppName}_a.cpp {cppName}_b.cpp  -g -shared -o {libShared}");

            ElfObjectFile elf;
            using (var inStream = File.OpenRead(libShared))
            {
                Console.WriteLine($"ReadBack from {libShared}");
                elf = ElfObjectFile.Read(inStream);
                elf.Print(Console.Out);
            }

            var elfContext = new DwarfElfContext(elf);
            var inputContext = new DwarfReaderContext(elfContext);
            inputContext.DebugLinePrinter = Console.Out;
            var dwarf = DwarfFile.Read(inputContext, out DiagnosticBag _);

            inputContext.DebugLineStream.Position = 0;

            var copyInputDebugLineStream = new MemoryStream();
            inputContext.DebugLineStream.CopyTo(copyInputDebugLineStream);
            inputContext.DebugLineStream.Position = 0;

            var outputContext = new DwarfWriterContext
            {
                IsLittleEndian = inputContext.IsLittleEndian,
                EnableRelocation = false,
                AddressSize = inputContext.AddressSize,
                DebugLineStream = new MemoryStream()
            };
            dwarf.Write(outputContext);

            Console.WriteLine();
            Console.WriteLine("=====================================================");
            Console.WriteLine("Readback");
            Console.WriteLine("=====================================================");
            Console.WriteLine();

            var reloadContext = new DwarfReaderContext()
            {
                IsLittleEndian = outputContext.IsLittleEndian,
                AddressSize = outputContext.AddressSize,
                DebugLineStream = outputContext.DebugLineStream
            };

            reloadContext.DebugLineStream.Position = 0;
            reloadContext.DebugLineStream = outputContext.DebugLineStream;
            reloadContext.DebugLinePrinter = Console.Out;

            var dwarf2 = DwarfFile.Read(reloadContext, out DiagnosticBag _);

            var inputDebugLineBuffer = copyInputDebugLineStream.ToArray();
            var outputDebugLineBuffer = ((MemoryStream)reloadContext.DebugLineStream).ToArray();
            Assert.AreEqual(inputDebugLineBuffer, outputDebugLineBuffer);
        }

        [Test]
        public void TestDebugLineSmall()
        {
            var cppName = "small";
            var cppObj = $"{cppName}_debug.o";
            LinuxUtil.RunLinuxExe("gcc", $"{cppName}.cpp -g -c -o {cppObj}");
            ElfObjectFile elf;
            using (var inStream = File.OpenRead(cppObj))
            {
                Console.WriteLine($"ReadBack from {cppObj}");
                elf = ElfObjectFile.Read(inStream);
                elf.Print(Console.Out);
            }

            var elfContext = new DwarfElfContext(elf);
            var inputContext = new DwarfReaderContext(elfContext);
            inputContext.DebugLinePrinter = Console.Out;
            var dwarf = DwarfFile.Read(inputContext, out DiagnosticBag _);

            inputContext.DebugLineStream.Position = 0;
            var copyInputDebugLineStream = new MemoryStream();
            inputContext.DebugLineStream.CopyTo(copyInputDebugLineStream);
            inputContext.DebugLineStream.Position = 0;

            var outputContext = new DwarfWriterContext
            {
                IsLittleEndian = inputContext.IsLittleEndian,
                AddressSize = inputContext.AddressSize,
                DebugLineStream = new MemoryStream()
            };
            dwarf.Write(outputContext);

            Console.WriteLine();
            Console.WriteLine("=====================================================");
            Console.WriteLine("Readback");
            Console.WriteLine("=====================================================");
            Console.WriteLine();

            var reloadContext = new DwarfReaderContext()
            {
                IsLittleEndian = outputContext.IsLittleEndian,
                AddressSize = outputContext.AddressSize,
                DebugLineStream = outputContext.DebugLineStream
            };

            reloadContext.DebugLineStream.Position = 0;
            reloadContext.DebugLineStream = outputContext.DebugLineStream;
            reloadContext.DebugLinePrinter = Console.Out;

            var dwarf2 = DwarfFile.Read(reloadContext, out DiagnosticBag _);

            var inputDebugLineBuffer = copyInputDebugLineStream.ToArray();
            var outputDebugLineBuffer = ((MemoryStream)reloadContext.DebugLineStream).ToArray();
            Assert.AreEqual(inputDebugLineBuffer, outputDebugLineBuffer);
        }



        [Test]
        public void TestDebugLineMultipleFunctions()
        {
            var cppName = "multiple_functions";
            var cppObj = $"{cppName}_debug.o";
            LinuxUtil.RunLinuxExe("gcc", $"{cppName}.cpp -g -c -o {cppObj}");

            ElfObjectFile elf;
            using (var inStream = File.OpenRead(cppObj))
            {
                Console.WriteLine($"ReadBack from {cppObj}");
                elf = ElfObjectFile.Read(inStream);
                elf.Print(Console.Out);
            }

            var elfContext = new DwarfElfContext(elf);
            var inputContext = new DwarfReaderContext(elfContext);
            inputContext.DebugLinePrinter = Console.Out;
            var dwarf = DwarfFile.Read(inputContext, out DiagnosticBag _);

            inputContext.DebugLineStream.Position = 0;
            var copyInputDebugLineStream = new MemoryStream();
            inputContext.DebugLineStream.CopyTo(copyInputDebugLineStream);
            inputContext.DebugLineStream.Position = 0;

            var outputContext = new DwarfWriterContext
            {
                IsLittleEndian = inputContext.IsLittleEndian,
                AddressSize = inputContext.AddressSize,
                DebugLineStream = new MemoryStream()
            };
            dwarf.Write(outputContext);

            Console.WriteLine();
            Console.WriteLine("=====================================================");
            Console.WriteLine("Readback");
            Console.WriteLine("=====================================================");
            Console.WriteLine();

            var reloadContext = new DwarfReaderContext()
            {
                IsLittleEndian = outputContext.IsLittleEndian,
                AddressSize = outputContext.AddressSize,
                DebugLineStream = outputContext.DebugLineStream
            };

            reloadContext.DebugLineStream.Position = 0;
            reloadContext.DebugLineStream = outputContext.DebugLineStream;
            reloadContext.DebugLinePrinter = Console.Out;

            var dwarf2 = DwarfFile.Read(reloadContext, out DiagnosticBag _);

            var inputDebugLineBuffer = copyInputDebugLineStream.ToArray();
            var outputDebugLineBuffer = ((MemoryStream)reloadContext.DebugLineStream).ToArray();
            Assert.AreEqual(inputDebugLineBuffer, outputDebugLineBuffer);
        }


        [Test]
        public void TestDebugInfoSmall()
        {
            var cppName = "small";
            var cppObj = $"{cppName}_debug.o";
            LinuxUtil.RunLinuxExe("gcc", $"{cppName}.cpp -g -c -o {cppObj}");

            ElfObjectFile elf;
            using (var inStream = File.OpenRead(cppObj))
            {
                elf = ElfObjectFile.Read(inStream);
                elf.Print(Console.Out);
            }

            var elfContext = new DwarfElfContext(elf);
            var inputContext = new DwarfReaderContext(elfContext);
            var dwarf = DwarfFile.Read(inputContext, out DiagnosticBag _);

            dwarf.AbbreviationTable.Print(Console.Out);
            dwarf.InfoSection.Print(Console.Out);
            dwarf.AddressRangeTable.Print(Console.Out);

            PrintStreamLength(inputContext);

            Console.WriteLine();
            Console.WriteLine("====================================================================");
            Console.WriteLine("Write Back");
            Console.WriteLine("====================================================================");

            var outputContext = new DwarfWriterContext
            {
                IsLittleEndian = inputContext.IsLittleEndian,
                AddressSize = inputContext.AddressSize,
                DebugAbbrevStream = new MemoryStream(),
                DebugLineStream = new MemoryStream(),
                DebugInfoStream = new MemoryStream(),
                DebugStringStream =  new MemoryStream(),
                DebugAddressRangeStream = new MemoryStream()
            };
            dwarf.Write(outputContext);

            dwarf.AbbreviationTable.Print(Console.Out);
            dwarf.InfoSection.Print(Console.Out);
            dwarf.InfoSection.PrintRelocations(Console.Out);
            dwarf.AddressRangeTable.Print(Console.Out);
            dwarf.AddressRangeTable.PrintRelocations(Console.Out);

            dwarf.WriteToElf(elfContext);

            var cppObj2 = $"{cppName}_debug2.o";
            using (var outStream = new FileStream(cppObj2, FileMode.Create))
            {
                elf.Write(outStream);
            }

            PrintStreamLength(outputContext);
        }

        Dictionary<String,DATATYPE_TYPE> baseDataTypes = new () {
            // {"uint8",DATATYPE_TYPE.UINT8},
            // {"uint16",DATATYPE_TYPE.UINT16},
            // {"uint32",DATATYPE_TYPE.UINT32},
            // {"uint64",DATATYPE_TYPE.UINT64},
            // {"sint8",DATATYPE_TYPE.SINT8},
            // {"sint16",DATATYPE_TYPE.SINT16},
            // {"sint32",DATATYPE_TYPE.SINT32},
            // {"sint64",DATATYPE_TYPE.SINT64},
            {"unsigned char",DATATYPE_TYPE.UINT8},
            {"unsigned short int",DATATYPE_TYPE.UINT16},
            {"unsigned short",DATATYPE_TYPE.UINT16},
            {"unsigned long int",DATATYPE_TYPE.UINT32},
            {"unsigned int",DATATYPE_TYPE.UINT32},
            {"unsigned long long",DATATYPE_TYPE.UINT64},
            {"unsigned long",DATATYPE_TYPE.UINT64},
            {"signed char",DATATYPE_TYPE.SINT8},
            {"char",DATATYPE_TYPE.SINT8},
            {"signed short int",DATATYPE_TYPE.SINT16},
            {"signed short",DATATYPE_TYPE.SINT16},
            {"short",DATATYPE_TYPE.SINT16},
            {"signed long int",DATATYPE_TYPE.SINT32},
            {"signed int",DATATYPE_TYPE.SINT32},
            {"int",DATATYPE_TYPE.SINT32},
            {"signed long long",DATATYPE_TYPE.SINT64},
            {"signed long",DATATYPE_TYPE.SINT64},
            {"long",DATATYPE_TYPE.SINT64},
            {"float",DATATYPE_TYPE.FLOAT32},
            {"double",DATATYPE_TYPE.FLOAT64},
            {"void",DATATYPE_TYPE.INVALID},
            {"BOOL_TYPE",DATATYPE_TYPE.UINT8},
            {"ON_OFF_TYPE",DATATYPE_TYPE.UINT8},
            {"PASS_FAIL_TYPE",DATATYPE_TYPE.UINT8},
            {"COMPLETE_TYPE",DATATYPE_TYPE.UINT8},
            {"ACTIVE_TYPE",DATATYPE_TYPE.UINT8},
        };
        
        // unsigned char       uint8;
        // typedef unsigned short int  uint16;
        // typedef unsigned long int   uint32;
        // typedef unsigned long long  uint64;

        // typedef signed char         sint8;
        // typedef signed short int    sint16;
        // typedef signed long int     sint32;
        // typedef signed long long    sint64;

        // typedef float               float32;
        // typedef double              float64;

        enum DATATYPE_TYPE
        {
            INVALID = 0,   //!< DATATYPE_INVALID
            UINT8,         //!< DATATYPE_UINT8
            UINT16,        //!< DATATYPE_UINT16
            UINT32,        //!< DATATYPE_UINT32
            UINT64,        //!< DATATYPE_UINT64
            SINT8,         //!< DATATYPE_SINT8
            SINT16,        //!< DATATYPE_SINT16
            SINT32,        //!< DATATYPE_SINT32
            SINT64,        //!< DATATYPE_SINT64
            FLOAT32,       //!< DATATYPE_FLOAT32
            FLOAT64,       //!< DATATYPE_FLOAT64
            IS_ARRAY,      //!< DATATYPE_IS_ARRAY Use this type if you want to extern an array. Receiver will need to understand what this array is.
            IS_REGULATION,  //!< DATATYPE_IS_REGULATION Use this type to extern the Regulation array.
            IS_Q15_CELSIUS //!< DATATYPE_IS_Q15_CELSIUS is the special type of SINT16 to represent temperatures in celsius on the Q15 format.
        };

        record VariableEntry(string FileName, string Name, string TagType, string TypeName, ulong Offset) 
        {
            public override string ToString() => $"{FileName},{Name},{TagType},{TypeName},{Offset:X8}";
        }

        DwarfDIE NavigateToBaseType(DwarfDIE die, int level = 0) {            
            if(die.Tag.Equals(DwarfTag.PointerType) ||
                die.Tag.Equals(DwarfTag.UnionType) ||
                die.Tag.Equals(DwarfTag.SubroutineType) ||
                die.Tag.Equals(DwarfTag.BaseType) ||
                die.Tag.Equals(DwarfTag.StructureType))
                 return die;

            if (die.FindAttributeByKey(DwarfAttributeKind.Type)?.ValueAsObject is not DwarfDIE typeRef) 
                return die; //just return the current die as the found type and let the client decide the resolution.
                //throw new NullReferenceException($"{die.Tag} does not contain Type attribute");
            return NavigateToBaseType(typeRef, level + 1);
        }

        /// <summary>
        /// Find the first reference type that contains byte size.
        /// </summary>
        /// <param name="die"></param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        DwarfDIE NavigateToByteSizeableType(DwarfDIE die) 
        {
            if(die.Tag.Equals(DwarfTag.PointerType) || //if pointer return itself so user can decide the size.
               die.Tag.Equals(DwarfTag.SubroutineType) || 
               die.Tag.Equals(DwarfTag.Subprogram) ||
               die.FindAttributeByKey(DwarfAttributeKind.ByteSize) is not null ||
                //found the last type in the chain that does not reference another type.
               die.FindAttributeByKey(DwarfAttributeKind.Type)?.ValueAsObject is not DwarfDIE typeRef) 
            {
               return die; 
            }
            
            return NavigateToByteSizeableType(typeRef);
        }

        uint GetByteSize(DwarfDIE die, uint pointerSize = 4) 
        {
            var typeWithByteSize = NavigateToByteSizeableType(die);
            //this should not happen, but the lib can't parse some enumerations correctly, return size 1 for this cases (typedef types).
            var typeSize = typeWithByteSize.FindAttributeByKey(DwarfAttributeKind.ByteSize)?.ValueAsU32 ?? (typeWithByteSize.Tag.Equals(DwarfTag.Typedef) ? 1u : pointerSize); //subrotine or pointer would not contain bytesize, and we default to 4 as size of 32bits address.
            if(die is DwarfDIEArrayType)
                return (die.Children[0].FindAttributeByKey(DwarfAttributeKind.UpperBound).ValueAsU32 + 1) * typeSize;
            
            return typeSize;
        }

        List<(string, DwarfDIE, ulong)> GetStructureMembers(DwarfDIE die, string parentMemberName, ulong offset, List<(string, DwarfDIE, ulong)> members = null) {            
            if(!die.Tag.Equals(DwarfTag.StructureType) && !die.Tag.Equals(DwarfTag.UnionType)) 
                throw new ArgumentException($"{die.Tag} is not a Structure or Union Type", nameof(die));
            members ??= new List<(string,DwarfDIE, ulong)>();
            uint memberBitSize = 0;
            uint byteSize = 0;
            uint? previousBitsize = null;
            foreach(var member in die.Children) 
            {
                if(!member.Tag.Equals(DwarfTag.Member)) throw new Exception("Struct have invalid Children. All Children must be Members Tag Types.");
                uint? bitsize = member.FindAttributeByKey(DwarfAttributeKind.BitSize)?.ValueAsU32;
                //Fix bad structured bitmap.
                if(memberBitSize > 0 && memberBitSize < (byteSize*8)) 
                {                    
                    if (
                        //previous bitmap never reached type size;
                        (!bitsize.HasValue && previousBitsize.HasValue) ||
                        //new bitmap overflow type size, it will be allocated in a new position.
                       (bitsize.HasValue && (bitsize.Value + memberBitSize > byteSize*8))
                    )
                    {
                        memberBitSize = 0;
                        offset += byteSize;
                    }
                }
                previousBitsize = bitsize;
                var rootType = member.FindAttributeByKey(DwarfAttributeKind.Type).ValueAsObject as DwarfDIE;
                var typeRef = NavigateToBaseType(rootType);
                var isPointerType = typeRef.Tag.Equals(DwarfTag.PointerType);
                var isArrayType = rootType.Tag.Equals(DwarfTag.ArrayType);
                uint upperBound = 0;
                if(isArrayType) upperBound = rootType.Children[0].FindAttributeByKey(DwarfAttributeKind.UpperBound).ValueAsU32 + 1;
                var memberName = $"{parentMemberName}.{member.FindAttributeByKey(DwarfAttributeKind.Name)?.ValueAsObject??"unnamed"}{(bitsize.HasValue?(":"+bitsize):"")}{(isPointerType ? "*":"")}{(isArrayType ? "["+upperBound+"]":"")}";
                if(typeRef != die && (typeRef.Tag.Equals(DwarfTag.StructureType) || typeRef.Tag.Equals(DwarfTag.UnionType)))
                {
                    GetStructureMembers(typeRef, memberName, offset, members);
                }
                else 
                {
                    members.Add((memberName, typeRef, offset));
                }
                bool updateOffset = member.Parent is not DwarfDIEUnionType;
                byteSize = GetByteSize(rootType); //update the bytesize of the current member.

                if(bitsize.HasValue)
                {
                    updateOffset = false;
                    memberBitSize += bitsize.Value;
                    if(memberBitSize >= (byteSize*8))
                        updateOffset = true;
                }
                
                if(updateOffset) 
                {
                    memberBitSize = 0;
                    offset += byteSize;
                }
            }
            return members;    
        }

        [Test]
        public void FlatAllVariablesWithExtensions()
        {
            using var inStream = File.OpenRead("TestFiles/Hill_ACU_D.out");
            ElfObjectFile.TryRead(inStream, out ElfObjectFile elf, out DiagnosticBag bag);
            var variableEntries = elf.GetVariableEntries();
            using TextWriter textWriter2 = new StreamWriter("varDefsExt.csv");
            textWriter2.WriteLine($"File Name, Variable Name, Tag Type, Type Name, Offset");
            foreach(var variableEntry in variableEntries){
                textWriter2.WriteLine(variableEntry);
            }
        }

        [Test]
        public void FlatAllVariables()
        {
            using var inStream = File.OpenRead("TestFiles/Hill_ACU_D.out");
            ElfObjectFile.TryRead(inStream, out ElfObjectFile elf, out DiagnosticBag bag);

            var symbolTable = elf.Sections.FirstOrDefault(s => s is ElfSymbolTable) as ElfSymbolTable;
            var objectSymbols = symbolTable.Entries.Where(e => e.Type == ElfSymbolType.Object);
            
            using TextWriter textWriter1 = new StreamWriter("varSymbols.txt");
            foreach (var symbol in objectSymbols.OrderBy(i => i.Name.Value)){
                textWriter1.WriteLine($"{symbol.Value:x16} {symbol.Size,5} {symbol.Name.Value}");
            }
            var dwarf = DwarfFile.ReadFromElf(elf, out DiagnosticBag _);
            var compilationDIES = dwarf.InfoSection.Units
                .Where(unit => unit.Root != null && unit.Root.Children.Count > 0)
                .Select(unit => unit.Root);

            using TextWriter textWriter2 = new StreamWriter("varDefs.csv");
            textWriter2.WriteLine($"File Name, Variable Name, Tag Type, Type Name, Offset");
            foreach (var compilationDIE in compilationDIES) {
                foreach(var variableDIE in compilationDIE.Children.Where(die => die.Tag.Equals(DwarfTagEx.Variable)))    
                {
                    List<VariableEntry> variableList = new();                    
                    var fileName = Path.GetFileName(compilationDIE.FindAttributeByKey(DwarfAttributeKind.Name).ValueAsObject.ToString());

                    var originalName = variableDIE.FindAttributeByKey(DwarfAttributeKind.Name).ValueAsObject.ToString();
                    var typeRef = variableDIE.FindAttributeByKey(DwarfAttributeKind.Type).ValueAsObject as DwarfDIE;
                    typeRef = NavigateToBaseType(typeRef);
                    var isPointerType = typeRef.Tag.Equals(DwarfTag.PointerType);
                    var isArrayType = typeRef.Tag.Equals(DwarfTag.ArrayType);
                    uint upperBound = 0;
                    if(isArrayType) upperBound = typeRef.Children[0].FindAttributeByKey(DwarfAttributeKind.UpperBound).ValueAsU32 + 1;
                    var name = $"{originalName}{(isPointerType ? "*":"")}{(isArrayType ? "["+upperBound+"]" : "")}";
                    var tagType = typeRef.Tag;
                    var typeName = (typeRef.FindAttributeByKey(DwarfAttributeKind.Name)?.ValueAsObject.ToString()) ?? (isPointerType ? "unsigned long" : typeRef.Tag.ToString());
                    
                    if(typeRef.Tag.Equals(DwarfTag.StructureType) || typeRef.Tag.Equals(DwarfTag.UnionType)) {
                       var members = GetStructureMembers(typeRef, name, objectSymbols.FirstOrDefault(i => i.Name.Value.TrimStart('_') == originalName).Value);
                       foreach(var (memberName,memTypeRef, offset) in members) {
                            isPointerType = memTypeRef.Tag.Equals(DwarfTag.PointerType);
                            typeName = (memTypeRef.FindAttributeByKey(DwarfAttributeKind.Name)?.ValueAsObject.ToString()) ?? (isPointerType ? "unsigned long" : typeRef.Tag.ToString());
                            textWriter2.WriteLine($"{fileName},{memberName},{tagType},{typeName},\"{offset:X8}\"");
                       }
                    }
                    else {
                        textWriter2.WriteLine($"{fileName},{name},{tagType},{typeName},\"{objectSymbols.FirstOrDefault(i => i.Name.Value.TrimStart('_') == originalName).Value:X8}\"");
                    }
                }
            }
            var variableDIES = dwarf.InfoSection.Units
                .Where(unit => unit.Root != null && unit.Root.Children.Count > 0)
                .Select(unit => unit.Root.Children[0]) //get first children as variables are at the first level in the DIE tree
                .Where(die => die.Tag.Equals(DwarfTagEx.Variable));

            var typedefDIES = dwarf.InfoSection.Units
                .Where(unit => unit.Root != null)
                .Select(unit => unit.Root)
                .SelectMany(root => root.Children)
                .Where(root => root.Tag.Equals(DwarfTagEx.Variable));

            using TextWriter textWriter = new StreamWriter("variables.txt");
            foreach (var unit in variableDIES) {
                unit.Print(textWriter);
            }

        }

        [Test]
        public void ReadOutFile() {
            using var inStream = File.OpenRead("TestFiles/SaturnIII_ACU_A.out");
            ElfObjectFile.TryRead(inStream, out ElfObjectFile elf, out DiagnosticBag bag);
            var dwarf = DwarfFile.ReadFromElf(elf, out DiagnosticBag dbag);
            
            using (TextWriter textWriter = new StreamWriter("elf.txt"))
                elf.Print(textWriter);
            //dwarf.AbbreviationTable.Print(textWriter);
            using (TextWriter textWriter = new StreamWriter("dwarf.txt"))
                dwarf.InfoSection.Print(textWriter);

            using (TextWriter textWriter = new StreamWriter("bag.txt",false))
                foreach(var message in bag.Messages)
                    textWriter.WriteLine(message);
            using (TextWriter textWriter = new StreamWriter("dbag.txt",false))
                foreach(var message in dbag.Messages)
                    textWriter.WriteLine(message);

            //dwarf.InfoSection.PrintRelocations(textWriter);
            //dwarf.AddressRangeTable.Print(textWriter);
        }


        [Test]
        public void CreateDwarf()
        {
            // Create ELF object
            var elf = new ElfObjectFile(ElfArch.X86_64);

            var codeSection = new ElfBinarySection(new MemoryStream(new byte[0x64])).ConfigureAs(ElfSectionSpecialType.Text);
            elf.AddSection(codeSection);
            var stringSection = new ElfStringTable();
            elf.AddSection(stringSection);
            elf.AddSection(new ElfSymbolTable() { Link = stringSection });
            elf.AddSection(new ElfSectionHeaderStringTable());

            var elfDiagnostics = new DiagnosticBag();
            elf.UpdateLayout(elfDiagnostics);
            Assert.False(elfDiagnostics.HasErrors);

            // Create DWARF Object
            var dwarfFile = new DwarfFile();
            
            // Create .debug_line information
            var fileName = new DwarfFileName()
            {
                Name = "check1.cpp",
                Directory = Environment.CurrentDirectory,
            };
            var fileName2 = new DwarfFileName()
            {
                Name = "check2.cpp",
                Directory = Environment.CurrentDirectory,
            };

            // First line table
            for (int i = 0; i < 2; i++)
            {
                var lineTable = new DwarfLineProgramTable();
                dwarfFile.LineSection.AddLineProgramTable(lineTable);

                lineTable.AddressSize = DwarfAddressSize.Bit64;
                lineTable.FileNames.Add(fileName);
                lineTable.FileNames.Add(fileName2);
                lineTable.AddLineSequence(new DwarfLineSequence()
                    {

                        new DwarfLine()
                        {
                            File = fileName,
                            Address = 0,
                            Column = 1,
                            Line = 1,
                        },
                        new DwarfLine()
                        {
                            File = fileName,
                            Address = 1,
                            Column = 1,
                            Line = 2,
                        }
                    }
                );
                // NOTE: doesn't seem to be generated by regular GCC
                // (it seems that only one line sequence is usually used)
                lineTable.AddLineSequence(new DwarfLineSequence()
                    {

                        new DwarfLine()
                        {
                            File = fileName2,
                            Address = 0,
                            Column = 1,
                            Line = 1,
                        },
                    }
                );
            }

            // Create .debug_info
            var rootDIE = new DwarfDIECompileUnit()
            {
                Name = fileName.Name,
                LowPC = 0, // 0 relative to base virtual address
                HighPC = (int)codeSection.Size, // default is offset/length after LowPC
                CompDir = fileName.Directory,
                StmtList = dwarfFile.LineSection.LineTables[0],
            };
            var subProgram = new DwarfDIESubprogram()
            {
                Name = "MyFunction",
            };
            rootDIE.AddChild(subProgram);

            var locationList = new DwarfLocationList();
            var regExpression = new DwarfExpression();
            regExpression.AddOperation(new DwarfOperation { Kind = DwarfOperationKindEx.Reg0 });            
            var regExpression2 = new DwarfExpression();
            regExpression2.AddOperation(new DwarfOperation { Kind = DwarfOperationKindEx.Reg2 });            
            locationList.AddLocationListEntry(new DwarfLocationListEntry
            {
                Start = 0,
                End = 0x10,
                Expression = regExpression,
            });
            locationList.AddLocationListEntry(new DwarfLocationListEntry
            {
                Start = 0x10,
                End = 0x20,
                Expression = regExpression2,
            });
            var variable = new DwarfDIEVariable()
            {
                Name = "a",
                Location = locationList,
            };
            dwarfFile.LocationSection.AddLocationList(locationList);
            subProgram.AddChild(variable);

            var cu = new DwarfCompilationUnit()
            {
                AddressSize = DwarfAddressSize.Bit64,
                Root = rootDIE
            };
            dwarfFile.InfoSection.AddUnit(cu);
            
            // AddressRange table
            dwarfFile.AddressRangeTable.AddressSize = DwarfAddressSize.Bit64;
            dwarfFile.AddressRangeTable.Unit = cu;
            dwarfFile.AddressRangeTable.Ranges.Add(new DwarfAddressRange(0, 0, codeSection.Size));
            
            // Transfer DWARF To ELF
            var dwarfElfContext = new DwarfElfContext(elf);
            dwarfFile.WriteToElf(dwarfElfContext);

            var outputFileName = "create_dwarf.o";
            using (var output = new FileStream(outputFileName, FileMode.Create))
            {
                elf.Write(output);
            }

            elf.Print(Console.Out);
            Console.WriteLine();
            dwarfFile.AbbreviationTable.Print(Console.Out);
            Console.WriteLine();
            dwarfFile.AddressRangeTable.Print(Console.Out);
            Console.WriteLine();
            dwarfFile.InfoSection.Print(Console.Out);

            Console.WriteLine("ReadBack --debug-dump=rawline");
            var readelf = LinuxUtil.ReadElf(outputFileName, "--debug-dump=rawline").TrimEnd();
            Console.WriteLine(readelf);
        }

        private static void PrintStreamLength(DwarfReaderWriterContext context)
        {
            if (context.DebugInfoStream != null)
            {
                Console.WriteLine($".debug_info {context.DebugInfoStream.Length}");
            }
            if (context.DebugAbbrevStream != null)
            {
                Console.WriteLine($".debug_abbrev {context.DebugAbbrevStream.Length}");
            }
            if (context.DebugAddressRangeStream != null)
            {
                Console.WriteLine($".debug_aranges {context.DebugAddressRangeStream.Length}");
            }
            if (context.DebugStringStream != null)
            {
                Console.WriteLine($".debug_str {context.DebugStringStream.Length}");
            }
            if (context.DebugLineStream != null)
            {
                Console.WriteLine($".debug_line {context.DebugLineStream.Length}");
            }
        }
    }
}