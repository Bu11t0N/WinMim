using System.Buffers.Binary;
using System.Runtime.Versioning;

namespace AeroForge.Core;

public static class SevenSounds
{
    public const string PrivateAssets=@"C:\AeroForgeLab\PrivateAssets\SevenSounds";
    // Hashes of the unchanged Windows sound resources supplied in the private r8p reference.
    public static IReadOnlyDictionary<string,string> Hashes {get;} = new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(new Dictionary<string,string>{
        ["Windows Balloon.wav"]="1e646954174841eb594f8573dc761276d3132b2389554709ad7d44e6772d7a7d",
        ["Windows Battery Critical.wav"]="873f59c7c115bdbefd4e3f650160a72e4aea2f9af486ec95d6dd2c0be430cf79",
        ["Windows Battery Low.wav"]="61eb4fbc96d79324053dcce4a70dc2d403e61259ae6bd959fa9e47008147d186",
        ["Windows Critical Stop.wav"]="27dc9782ffaa29c830158374afb9370f4df32d03b592e83251b55e0a8e972b98",
        ["Windows Default.wav"]="6fc1edd17679bb2af8a844aa9b7da03fc72c55cf55eded8c3a5b99e52d96d11e",
        ["Windows Ding.wav"]="7020a1c64207ad8e60062d5400e1332bd07d95bb59cc82af2dd4b39347c64a76",
        ["Windows Exclamation.wav"]="c362d950204cc8327016209a3246216efed7167cd92e02f754b963f49f793707",
        ["Windows Feed Discovered.wav"]="1ce44ef43d39d5aefd8b25d96926848c569c1b7c0bfa029dfc8310c2e1f7d50f",
        ["Windows Hardware Fail.wav"]="0c4f60760d9b80f3f053832d6ae1fcb90576cb118a3e0a1574e234eaf7cbf4ee",
        ["Windows Hardware Insert.wav"]="2536d34c366b343cc07b53850ae800fadc608149fe4d04357fefa80b447a1c1e",
        ["Windows Hardware Remove.wav"]="fe1ad6c8b2fdbb60b7c3b5f5303165f713745f05aaf85e0bd358eb3b81700e98",
        ["Windows Information Bar.wav"]="fd1b4b2472b731842dda8c29f7f82c7199294d75f9f606602bc6ea737790ab6f",
        ["Windows Logoff Sound.wav"]="5be225a34e03182028c69ae0cdda13e3beb491d1c10674e043b5cb7329326a7b",
        ["Windows Logon Sound.wav"]="3d9908bad270909fc063e4cf26e9da35cf5c36bc3074fe6fc9a50ebc7777227e",
        ["Windows Menu Command.wav"]="66bcd3c2c60a62709131233d4eea48f7d53f7fdfe22dd5dcb0d03b3f94c48ad1",
        ["Windows Minimize.wav"]="3b71ca5a67432991ffa159d082cbf96c7948ad2db7486e6d491063d68863e3c2",
        ["Windows Navigation Start.wav"]="682c8a7cf410de17c249775aee568ec1f9e3341bd4409e96ccbba4882f1d8670",
        ["Windows Notify.wav"]="e134873ca4fd9140e5a90ed5d57814115b2f283dacde68de138b25a5c80ec264",
        ["Windows Pop-up Blocked.wav"]="18ed883bd848af6ffd1121baf332d021c5b4ea91bff4a8819b9dca14fc080a9a",
        ["Windows Print complete.wav"]="c0460683a5f500ad4a497c7ce1bfbb6d5571f4b0affaf036409c91701bac4bdb",
        ["Windows Recycle.wav"]="f1eb9d205799a5b803b1df4de4e8feb54acd1a9ac9f3732b1a17a0cff02d71a7",
        ["Windows Restore.wav"]="e201003b7edd20e3e8de9921e358668a5fd84bc650bcd7ded06fad5f3bff0a49",
        ["Windows Shutdown.wav"]="76a53d12ace9bb89b424da5e419fe25a06725d80d13901b12d190b24d9a4edbd",
        ["Windows User Account Control.wav"]="cf6c22452ff7e392cfdfb09a2736c40bc0ef47dc8285df2401d7a4c84d084804",
    });
    private static readonly IReadOnlyDictionary<string,string> Events=new Dictionary<string,string>{
        [".Default/.Default"]="Windows Default.wav",
        [".Default/SystemAsterisk"]="Windows Ding.wav",
        [".Default/SystemExclamation"]="Windows Exclamation.wav",
        [".Default/SystemHand"]="Windows Critical Stop.wav",
        [".Default/SystemNotification"]="Windows Balloon.wav",
        [".Default/DeviceConnect"]="Windows Hardware Insert.wav",
        [".Default/DeviceDisconnect"]="Windows Hardware Remove.wav",
        [".Default/DeviceFail"]="Windows Hardware Fail.wav",
        [".Default/LowBatteryAlarm"]="Windows Battery Low.wav",
        [".Default/CriticalBatteryAlarm"]="Windows Battery Critical.wav",
        [".Default/MailBeep"]="Windows Notify.wav",
        [".Default/WindowsLogon"]="Windows Logon Sound.wav",
        [".Default/WindowsLogoff"]="Windows Logoff Sound.wav",
        [".Default/SystemExit"]="Windows Shutdown.wav",
        [".Default/MenuCommand"]="Windows Menu Command.wav",
        [".Default/Minimize"]="Windows Minimize.wav",
        [".Default/RestoreUp"]="Windows Restore.wav",
        [".Default/RestoreDown"]="Windows Restore.wav",
        [".Default/PrintComplete"]="Windows Print complete.wav",
        [".Default/WindowsUAC"]="Windows User Account Control.wav",
        ["Explorer/EmptyRecycleBin"]="Windows Recycle.wav",
        ["Explorer/Navigating"]="Windows Navigation Start.wav",
        ["Explorer/BlockedPopup"]="Windows Pop-up Blocked.wav",
        ["Explorer/SecurityBand"]="Windows Information Bar.wav",
        ["Explorer/FeedDiscovered"]="Windows Feed Discovered.wav",
    };
    public static IEnumerable<string> EventPaths=>Events.Keys.Select(e=>@"HKCU\AppEvents\Schemes\Apps\"+e.Replace('/', '\\')+@"\.Current");
    public static bool Allows(Change change)=>change.Kind==ChangeKind.Registry && change.ValueName==""
        && EventPaths.Contains(change.Target,StringComparer.OrdinalIgnoreCase);
    public static Plan Build(string source)
    {
        // Validate the complete input before creating any transaction or registry value.
        
        foreach(var pair in Hashes)
        {
            string path=SafePath.Under(source,pair.Key);
            if(!File.Exists(path)||new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Нет звукового ресурса: "+pair.Key);
            var bytes=File.ReadAllBytes(path);
            if(Hashing.Bytes(bytes)!=pair.Value || bytes.Length<12 || !bytes.AsSpan(0,4).SequenceEqual("RIFF"u8)
                || !bytes.AsSpan(8,4).SequenceEqual("WAVE"u8) || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4))!=bytes.Length-8)
                throw new InvalidDataException("Звуковой ресурс изменён: "+pair.Key);

        }
        var plan=new Plan{ProfileId="seven-sounds",ProfileName="Звуки Windows 7",Exclusive=true};
        foreach(var pair in Events)plan.Changes.Add(new(){Kind=ChangeKind.Registry,Target=@"HKCU\AppEvents\Schemes\Apps\"+pair.Key.Replace('/', '\\')+@"\.Current",ValueKind="String",ValueName="",Value=SafePath.Under(source,pair.Value),Description="Звук события: "+pair.Key});
        return plan;
    }
}

[SupportedOSPlatform("windows")]
public sealed class SevenSoundsController
{
    private readonly string root,state;
    private TransactionManager Manager=>new(new SoundBackend(root),state);
    public SevenSoundsController(string? root=null,string? state=null)
    {
        string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForge");
        this.root=Path.GetFullPath(root??Path.Combine(data,"SevenSounds"));this.state=Path.GetFullPath(state??Path.Combine(data,"SoundState"));
    }
    public Receipt Apply(IProgress<string>? progress=null,CancellationToken token=default)
    {
        DesktopController.EnsureLab();
        var receipt=Manager.Apply(SevenSounds.Build(SevenSounds.PrivateAssets),progress,token,allowProfileUpdate:true);
        NotifyChanged();return receipt;
    }
    public Receipt Restore(IProgress<string>? progress=null)
    {
        DesktopController.EnsureLab();
        var receipt=Manager.History(false).FirstOrDefault(r=>r.Status!="RolledBack")??throw new InvalidOperationException("Нет звуков WinMim для возврата.");
        var result=Manager.Rollback(receipt.FilePath,progress);NotifyChanged();return result;
    }
    // Sounds do not require theme/tray broadcasts or an Explorer restart.
    private static void NotifyChanged()=>SendMessageTimeout(new IntPtr(0xFFFF),0x1A,UIntPtr.Zero,"AppEvents",2,50,out _);
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr window,uint message,UIntPtr parameter,string section,uint flags,uint timeout,out UIntPtr result);
    public IReadOnlyList<Receipt> History()=>Manager.History(false);
    private sealed class SoundBackend(string root):IChangeBackend
    {
        private readonly WindowsBackend backend=new(root,allowDesktopSettings:true);
        public void Validate(Change change)
        {
            if(change.Kind==ChangeKind.File){if(!change.Target.StartsWith("Media/",StringComparison.Ordinal)||!SevenSounds.Hashes.ContainsKey(change.Target[6..]))throw new InvalidDataException("Unexpected sound file");}
            else if(!SevenSounds.Allows(change))throw new InvalidDataException("Unexpected sound setting");
            backend.Validate(change);
        }
        public ValueState Read(Change change){Validate(change);return backend.Read(change);}
        public void Write(Change change,ValueState value){Validate(change);backend.Write(change,value);}
    }
}
