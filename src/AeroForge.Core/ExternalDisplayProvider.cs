namespace AeroForge.Core;

public static class ExternalDisplayProvider
{
    public const string Sha256="a2624a5e908fc9b99edf89a1aaeee0c70dd10ed3142d1f922f308ad35993d30c";
    public const string Target="Data/Engine/ModsWritable/mod-storage/local@aeroforge-win7-display-control-panel-restorer/display.dll";
    public static byte[] Read(string path)
    {
        if(new FileInfo(path).Length>2*1024*1024)throw new InvalidDataException("Display provider exceeds size limit");
        byte[] data=File.ReadAllBytes(path);
        if(Hashing.Bytes(data)!=Sha256)throw new InvalidDataException("Microsoft Display provider SHA-256 mismatch");
        return data;
    }
}
