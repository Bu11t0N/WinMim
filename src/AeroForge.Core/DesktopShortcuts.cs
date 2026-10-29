using System.Text;

namespace AeroForge.Core;

public static class DesktopShortcuts
{
    // [MS-SHLLINK]: Unicode LinkInfo and StringData, with no volatile tracker,
    // timestamps, environment expansion, or script execution dependencies.
    public static byte[] Create(string executable,string arguments="")
    {
        if(!Path.IsPathFullyQualified(executable)||!File.Exists(executable))throw new FileNotFoundException("Shortcut target is unavailable",executable);
        executable=Path.GetFullPath(executable);
        if(executable.Length>=260)throw new InvalidDataException("Shortcut target exceeds MAX_PATH");
        string directory=Path.GetDirectoryName(executable)!;
        byte[] ansi=Encoding.ASCII.GetBytes(executable+'\0');
        byte[] unicode=Encoding.Unicode.GetBytes(executable+'\0');
        const uint volumeOffset=36,volumeSize=17;
        uint baseOffset=volumeOffset+volumeSize,suffixOffset=baseOffset+(uint)ansi.Length;
        uint unicodeOffset=suffixOffset+1,unicodeSuffixOffset=unicodeOffset+(uint)unicode.Length;
        using var output=new MemoryStream();using var writer=new BinaryWriter(output,Encoding.Unicode,true);
        writer.Write(76u);writer.Write(new Guid("00021401-0000-0000-C000-000000000046").ToByteArray());
        writer.Write(0x23402D2u|(arguments.Length>0?0x20u:0u)); // Prefer fixed Unicode target block; disable mutable tracking.
        writer.Write(0u);writer.Write(0L);writer.Write(0L);writer.Write(0L);
        writer.Write(0u);writer.Write(0);writer.Write(1u);writer.Write((ushort)0);writer.Write((ushort)0);writer.Write(0u);writer.Write(0u);
        writer.Write(unicodeSuffixOffset+2);writer.Write(36u);writer.Write(1u);
        writer.Write(volumeOffset);writer.Write(baseOffset);writer.Write(0u);writer.Write(suffixOffset);writer.Write(unicodeOffset);writer.Write(unicodeSuffixOffset);
        writer.Write(volumeSize);writer.Write(3u);writer.Write(0u);writer.Write(16u);writer.Write((byte)0);
        writer.Write(ansi);writer.Write((byte)0);writer.Write(unicode);writer.Write((ushort)0);
        void Text(string value)
        {
            if(value.Length>ushort.MaxValue||value.Contains('\0'))throw new InvalidDataException("Invalid shortcut string");
            writer.Write((ushort)value.Length);writer.Write(Encoding.Unicode.GetBytes(value));
        }
        Text(directory);if(arguments.Length>0)Text(arguments);Text(executable);
        // EnvironmentVariableDataBlock also accepts an absolute path without variables.
        // Unlike filesystem ID lists, it contains no access times or machine-specific object IDs.
        writer.Write(0x314u);writer.Write(0xA0000001u);
        writer.Write(ansi);writer.Write(new byte[260-ansi.Length]);
        writer.Write(unicode);writer.Write(new byte[520-unicode.Length]);
        writer.Write(0u);writer.Flush();return output.ToArray();
    }
}
