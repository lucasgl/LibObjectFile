namespace LibObjectFile.Tests.Dwarf;

public record VariableEntry(string FileName, string Name, string TagType, string TypeName, ulong Offset){
    public override string ToString() => $"{FileName},{Name},{TagType},{TypeName},\"{Offset:X8}\"";
}
