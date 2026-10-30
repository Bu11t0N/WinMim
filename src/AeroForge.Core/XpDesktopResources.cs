using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AeroForge.Core;

public static class XpDesktopResources
{
    public const string CursorKey=@"HKCU\Control Panel\Cursors";
    public static readonly string[] CursorNames=["Arrow","Help","Hand","AppStarting","Wait","NWPen","No","SizeNS","SizeWE","Crosshair","IBeam","SizeNWSE","SizeNESW","SizeAll","UpArrow"];
    public static readonly string[] SoundEvents=[".Default/.Default",".Default/SystemAsterisk",".Default/SystemExclamation",".Default/SystemHand",".Default/SystemNotification",".Default/DeviceConnect",".Default/DeviceDisconnect",".Default/DeviceFail",".Default/LowBatteryAlarm",".Default/CriticalBatteryAlarm",".Default/MailBeep",".Default/WindowsLogon",".Default/WindowsLogoff",".Default/SystemExit",".Default/MenuCommand",".Default/Minimize",".Default/RestoreUp",".Default/RestoreDown",".Default/PrintComplete",".Default/WindowsUAC","Explorer/EmptyRecycleBin","Explorer/Navigating","Explorer/BlockedPopup","Explorer/SecurityBand","Explorer/FeedDiscovered"];
    public static bool Allows(Change change)=>change.Kind==ChangeKind.Registry&&change.Target==CursorKey&&CursorNames.Contains(change.ValueName);
    public static IEnumerable<Change> Build(XpPackManifest manifest,string root)
    {
        foreach(var pair in manifest.Cursors)
            yield return Setting(CursorKey,pair.Key,SafePath.Under(root,"XP/"+pair.Value),"Указатель XP: "+pair.Key);
        foreach(var pair in manifest.Sounds)
            yield return Setting(@"HKCU\AppEvents\Schemes\Apps\"+pair.Key.Replace('/', '\\')+@"\.Current","",SafePath.Under(root,"XP/"+pair.Value),"Звук XP: "+pair.Key);
    }
    private static Change Setting(string key,string name,string path,string description)=>new(){Kind=ChangeKind.Registry,Target=key,ValueName=name,ValueKind="String",Value=path,Description=description};
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static void RefreshCursors(){if(!SystemParametersInfoW(0x57,0,IntPtr.Zero,0))throw new Win32Exception();}
    [DllImport("user32.dll",SetLastError=true)]private static extern bool SystemParametersInfoW(uint action,uint parameter,IntPtr value,uint flags);
}
