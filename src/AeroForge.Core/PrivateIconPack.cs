using System.Buffers.Binary;

namespace AeroForge.Core;

public sealed class PrivateIconPack
{
    private static readonly HashSet<string> SupportedLibraries=new(("aclui.dll appwiz.cpl.dll basebrd.dll batmeter.dll changepk.exe.dll cleanmgr.exe.dll cscui.dll DDORes.dll DeviceCenter.dll DevicePairing.dll DevicePairingFolder.dll DevicePairingWizard.exe.dll devmgr.dll Display.dll dmdskres.dll dsui.dll explorer.exe.dll explorerframe.dll FileHistory.exe.dll fontext.dll hdwwiz.cpl.dll hdwwiz.exe.dll HelpPane.exe.dll hotplug.dll imageres.dll imagesp1.dll joy.cpl.dll magnify.exe.dll main.cpl.dll mmres.dll mmsys.cpl.dll modemui.dll ndadmin.exe.dll networkexplorer.dll newdev.dll newdev.exe.dll pnidui.dll regedit.exe.dll SensorsCpl.dll sethc.exe.dll setupapi.dll shell32.dll shellbrd.dll SmartScreenSettings.exe.dll SndVolSSO.dll stobject.dll SyncCenter.dll taskmgr.exe.dll themeui.dll UserAccountControlSettings.exe.dll winver.exe.dll WorkFolders.exe.dll WorkFoldersControl.dll WorkFoldersRes.dll wuapi.dll zipfldr.dll").Split(' '),StringComparer.OrdinalIgnoreCase);
    public string Source { get; set; } = "";
    public List<PrivateIconFile> Files { get; set; } = [];
    public static Dictionary<string,string> Settings(string directory)
    {
        directory=Path.GetFullPath(directory);
        var pack=JsonFile.Read<PrivateIconPack>(SafePath.Under(directory,"manifest.json"));
        if(pack.Files.Count<1 || pack.Files.Count>SupportedLibraries.Count || pack.Files.Select(f=>f.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=pack.Files.Count)
            throw new InvalidDataException("Invalid private icon pack file list");
        var settings=new Dictionary<string,string>{{"iconTheme",""},{"allResourceRedirect","0"},{"themePaths[0]",""}};
        int mapping=0;long total=0;
        void Redirect(string original,string path)
        {
            settings[$"redirectionResourcePaths[{mapping}].original"]=original;
            settings[$"redirectionResourcePaths[{mapping++}].redirect"]=path;
        }
        foreach(var file in pack.Files)
        {
            if(!SupportedLibraries.Contains(file.FileName))throw new InvalidDataException("Unsupported icon resource library");
            string path=SafePath.Under(directory,file.FileName);
            long size=new FileInfo(path).Length;total+=size;
            if(size>64*1024*1024 || total>256*1024*1024)throw new InvalidDataException("Icon resource size limit exceeded");
            byte[] data=File.ReadAllBytes(path);
            if(!Hashing.Bytes(data).Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Icon library SHA-256 mismatch: "+file.FileName);
            if(data.Length<64 || data[0]!='M' || data[1]!='Z')throw new InvalidDataException("Invalid icon resource PE");
            int pe=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(60,4)));
            if(pe<64 || pe>data.Length-44 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pe,4))!=0x4550
                || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pe+40,4))!=0)
                throw new InvalidDataException("Only resource-only PE libraries with no entry point are accepted");
            string target=file.FileName.EndsWith(".exe.dll",StringComparison.OrdinalIgnoreCase)||file.FileName.EndsWith(".cpl.dll",StringComparison.OrdinalIgnoreCase)?file.FileName[..^4]:file.FileName;
            Redirect(@"%SystemRoot%\System32\"+target,path);
            Redirect(@"%SystemRoot%\SysWOW64\"+target,path);
            if(target.Equals("explorer.exe",StringComparison.OrdinalIgnoreCase)||target.Equals("regedit.exe",StringComparison.OrdinalIgnoreCase))Redirect(@"%SystemRoot%\"+target,path);
            if(target.Equals("basebrd.dll",StringComparison.OrdinalIgnoreCase))Redirect(@"%SystemRoot%\Branding\Basebrd\basebrd.dll",path);
        }
        settings[$"redirectionResourcePaths[{mapping}].original"]="";
        settings[$"redirectionResourcePaths[{mapping}].redirect"]="";
        return settings;
    }
}
public sealed class PrivateIconFile
{
    public string FileName { get; set; } = "";
    public string Sha256 { get; set; } = "";
}
