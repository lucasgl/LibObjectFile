using LibObjectFile;
using LibObjectFile.Dwarf;
using LibObjectFile.Elf;

using var inStream = File.OpenRead("Hill_ACU_D.out");
ElfObjectFile.TryRead(inStream, out ElfObjectFile elf, out DiagnosticBag bag);
Console.WriteLine("Not Exploded");

// var elfContext = new DwarfElfContext(elf);
// var inputContext = new DwarfReaderContext(elfContext);
// var dwarf = DwarfFile.Read(inputContext);
var dwarf = DwarfFile.ReadFromElf(elf);
TextWriter textWriter= new StreamWriter("out.txt");
//dwarf.AbbreviationTable.Print(textWriter);
dwarf.InfoSection.Print(textWriter);
//dwarf.AddressRangeTable.Print(textWriter);


foreach(var section in elf.Sections)//.Where(s => ((LibObjectFile.Elf.ElfSymbolTable)s).Entries.Count > 0))//.Where(s => s.Name.Value == ".symtab"))
{
    //Console.WriteLine($"{section.GetType()}:{section.Name}");
    if(section is ElfSymbolTable) {
        foreach(var entry in ((LibObjectFile.Elf.ElfSymbolTable)section).Entries.Where(t => t.Type == ElfSymbolType.Object)) {
           // Console.WriteLine(entry);
        }
    }
    if(section.Name.Equals(".debug_info")) {
        DwarfInfoSection dwarfObject = new();
        
    }
}