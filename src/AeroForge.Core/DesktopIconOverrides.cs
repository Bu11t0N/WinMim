using Microsoft.Win32;
using System.Globalization;
using System.Runtime.InteropServices;

namespace AeroForge.Core;

public static class DesktopIconOverrides
{
    public static void Refresh()=>SHChangeNotify(0x08000000,0x2000,IntPtr.Zero,IntPtr.Zero);
    private static readonly string[] Classes=["Folder","Directory","Drive",
        @"CLSID\{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
        @"CLSID\{645FF040-5081-101B-9F08-00AA002F954E}",
        @"CLSID\{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}",
        @"CLSID\{26EE0668-A00A-44D7-9371-BEB064C98683}",
        @"CLSID\{21EC2020-3AEA-1069-A2DD-08002B30309D}",
        @"CLSID\{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}",
        @"CLSID\{031E4825-7B94-4DC3-B131-E946B44C8DD5}",
        @"CLSID\{59031A47-3F72-44A7-89C5-5595FE6B30EE}",
        @"CLSID\{323CA680-C24D-4099-B94D-446DD2D7249E}",
        @"CLSID\{7007ACC7-3202-11D1-AAD2-00805FC1270E}",
        @"CLSID\{B4FB3F98-C1EA-428D-A78A-D1F5659CBA93}"];
    public static bool Allows(Change change)=>change.Kind==ChangeKind.Registry
        && Classes.Any(c=>change.Target.Equals(@"HKCU\Software\Classes\"+c+@"\DefaultIcon",StringComparison.OrdinalIgnoreCase)
            || c.StartsWith(@"CLSID\",StringComparison.Ordinal) && change.Target.Equals(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\"+c+@"\DefaultIcon",StringComparison.OrdinalIgnoreCase))
        && change.ValueName is "" or "empty" or "full";
    public static Change[] Build(string directory,Func<string,string,string?>? read=null)
    {
        PrivateIconPack.Settings(directory); // Validate every external resource before recording a path.
        var pack=JsonFile.Read<PrivateIconPack>(SafePath.Under(directory,"manifest.json"));
        var resources=pack.Files.ToDictionary(f=>f.FileName,StringComparer.OrdinalIgnoreCase);
        read??=ReadClassIcon;
        var result=new List<Change>();
        foreach(string item in Classes)
            foreach(string name in new[]{"","empty","full"})
            {
                string? original=read(item+@"\DefaultIcon",name);
                if(string.IsNullOrWhiteSpace(original))continue;
                int comma=original.LastIndexOf(',');
                if(comma<0 || !int.TryParse(original[(comma+1)..],NumberStyles.Integer,CultureInfo.InvariantCulture,out int index))continue;
                string source=Environment.ExpandEnvironmentVariables(original[..comma].Trim().Trim('"'));
                string file=Path.GetFileName(source);
                if(!resources.TryGetValue(file,out var resource))continue;
                if(index>=0)
                {
                    // Positive indices depend on resource ordering; persist the actual group ID.
                    uint count=PrivateExtractIconsW(source,index,32,32,out IntPtr icon,out uint resourceId,1,0);
                    if(icon!=IntPtr.Zero)DestroyIcon(icon);
                    if(count!=1 || resourceId==uint.MaxValue || resourceId>int.MaxValue)continue;
                    index=-(int)resourceId;
                }
                result.Add(new(){Kind=ChangeKind.Registry,Target=@"HKCU\Software\Classes\"+item+@"\DefaultIcon",ValueName=name,ValueKind="String",
                    Value=SafePath.Under(directory,resource.FileName)+","+index.ToString(CultureInfo.InvariantCulture),Description="Значок Windows 7: "+item+" "+name});
                if(item.StartsWith(@"CLSID\",StringComparison.Ordinal))
                    result.Add(new(){Kind=ChangeKind.Registry,Target=@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\"+item+@"\DefaultIcon",ValueName=name,ValueKind="String",
                        Value=SafePath.Under(directory,resource.FileName)+","+index.ToString(CultureInfo.InvariantCulture),Description="Значок рабочего стола Windows 7: "+item+" "+name});
            }
        return result.ToArray();
    }
    private static string? ReadClassIcon(string path,string name)
    {using var key=Registry.ClassesRoot.OpenSubKey(path);return key?.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern uint PrivateExtractIconsW(string file,int index,int width,int height,out IntPtr icon,out uint resourceId,uint count,uint flags);
    [DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("shell32.dll")]private static extern void SHChangeNotify(uint eventId,uint flags,IntPtr first,IntPtr second);
}
