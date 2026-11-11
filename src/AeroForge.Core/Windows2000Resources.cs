using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace AeroForge.Core;

public static class Windows2000Resources
{
    public const string Cursors=@"HKCU\Control Panel\Cursors";
    private static readonly string[] CursorNames=["Arrow","Help","Wait","AppStarting"];
    private static readonly string[] SoundEvents=[".Default/.Default",".Default/SystemAsterisk",".Default/SystemExclamation",".Default/SystemHand",".Default/SystemNotification",".Default/EmptyRecycleBin","Explorer/Navigating"];
    public static bool Allows(Change c)=>c.Kind==ChangeKind.Registry &&
        (c.Target==Cursors&&CursorNames.Contains(c.ValueName) || c.ValueName==""&&SoundEvents.Any(e=>c.Target==@"HKCU\AppEvents\Schemes\Apps\"+e.Replace('/', '\\')+@"\.Current"));
    public static void Add(Plan plan,string directory,string root)
    {
        var manifest=JsonFile.Read<ResourceManifest>(SafePath.Under(directory,"manifest.json"));
        foreach(string role in CursorNames.Select(n=>"cursor/"+n).Concat(SoundEvents.Select(n=>"sound/"+n)))
            if(!manifest.Resources.ContainsKey(role))throw new InvalidDataException("Missing Windows 2000 resource role: "+role);
        var reference=manifest.Files.ToDictionary(f=>f.Name,StringComparer.Ordinal);
        foreach(var (role,name) in manifest.Resources)
        {
            bool cursor=role.StartsWith("cursor/",StringComparison.Ordinal)&&CursorNames.Contains(role[7..]);
            bool sound=role.StartsWith("sound/",StringComparison.Ordinal)&&SoundEvents.Contains(role[6..]);
            if(!cursor&&!sound)throw new InvalidDataException("Unsupported Windows 2000 resource role: "+role);
            if(!reference.TryGetValue(name,out var file))throw new InvalidDataException("Missing resource manifest entry");
            byte[] data=File.ReadAllBytes(SafePath.Under(directory,name));
            if(data.LongLength!=file.Size||Hashing.Bytes(data)!=file.Sha256)throw new InvalidDataException("Windows 2000 resource hash/size mismatch: "+name);
            if(sound&&(data.Length<44||!data.AsSpan(0,4).SequenceEqual("RIFF"u8)||!data.AsSpan(8,4).SequenceEqual("WAVE"u8)||BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4))+8L!=data.LongLength))throw new InvalidDataException("Invalid WAVE data");
            if(cursor&&(data.Length<22||!data.AsSpan(0,4).SequenceEqual(new byte[]{0,0,2,0})))throw new InvalidDataException("Invalid cursor data");
            string target="Assets2000/"+name;
            if(!plan.Changes.Any(c=>c.Kind==ChangeKind.File&&c.Target==target))plan.Changes.Add(new(){Kind=ChangeKind.File,Target=target,Content=data,Description="Windows 2000 resource: "+name});
            plan.Changes.Add(new(){Kind=ChangeKind.Registry,Target=cursor?Cursors:@"HKCU\AppEvents\Schemes\Apps\"+role[6..].Replace('/', '\\')+@"\.Current",ValueName=cursor?role[7..]:"",ValueKind="ExpandString",Value=SafePath.Under(root,target),Description="Windows 2000: "+role});
        }
    }
    public static void RefreshCursors(){if(!SystemParametersInfoW(0x57,0,IntPtr.Zero,0))throw new System.ComponentModel.Win32Exception();}
    [DllImport("user32.dll",ExactSpelling=true,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SystemParametersInfoW(uint action,uint parameter,IntPtr value,uint flags);
    private sealed class ResourceManifest{public Dictionary<string,string> Resources{get;set;}=[];public ResourceFile[] Files{get;set;}=[];}
    private sealed class ResourceFile{public string Name{get;set;}="";public string Sha256{get;set;}="";public long Size{get;set;}}
}
