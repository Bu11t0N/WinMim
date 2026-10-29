using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace AeroForge.Core;

[SupportedOSPlatform("windows")]
public sealed class DesktopBackend(string root) : IChangeBackend
{
    private readonly WindowsBackend filesAndRegistry = new(root, allowDesktopSettings: true);
    public void Validate(Change change)
    {
        switch (change.Kind)
        {
            case ChangeKind.ClassicAppearance:
                if(change.Target!="Windows.ClassicAppearance")throw new InvalidDataException("Invalid Classic appearance target");
                if(change.Value!=null)_=ClassicAppearance.Parse(change.Value);
                break;
            case ChangeKind.RegistryKey:
                if(change.Target!=DesktopRegistryPolicy.FavoritesNamespace || change.ValueName!="" || change.ValueKind!="RegistryKey" || change.Value is not (null or "present"))
                    throw new InvalidDataException("Only the owned Favorites namespace key can be registered");
                break;
            case ChangeKind.File: filesAndRegistry.Validate(change); break;
            case ChangeKind.Registry:
                if (!DesktopRegistryPolicy.Allows(change)) throw new InvalidDataException("Setting is outside the desktop profile: " + change.Target + "\\" + change.ValueName);
                filesAndRegistry.Validate(change); break;
            case ChangeKind.VisualStyle:
                if (change.Target != "Windows.VisualStyle") throw new InvalidDataException("Invalid visual style target");
                if (change.Value != null) ValidateStyle(VisualStyleState.Decode(change.Value));
                break;
            case ChangeKind.Wallpaper:
                if (change.Target != "Windows.Wallpaper") throw new InvalidDataException("Invalid wallpaper target");
                if (!string.IsNullOrEmpty(change.Value) && !Path.IsPathFullyQualified(change.Value)) throw new InvalidDataException("Wallpaper needs an absolute path");
                break;
            case ChangeKind.TaskbarState:
                if(change.Target!="Windows.TaskbarState" || change.Value!=null && (!uint.TryParse(change.Value,out uint bits) || bits>1))
                    throw new InvalidDataException("Invalid taskbar state");
                break;
            default: throw new InvalidDataException("Unsupported desktop change");
        }
    }
    private static void ValidateStyle(VisualStyleState style)
    {
        if (!Path.IsPathFullyQualified(style.Path) || !style.Path.EndsWith(".msstyles", StringComparison.OrdinalIgnoreCase)
            || style.Path.IndexOfAny(['\0','\r','\n']) >= 0 || style.Color.IndexOfAny(['\0','\r','\n']) >= 0 || style.Size.IndexOfAny(['\0','\r','\n']) >= 0)
            throw new InvalidDataException("Invalid visual style state");
    }
    public ValueState Read(Change change)
    {
        Validate(change);
        if(change.Kind==ChangeKind.ClassicAppearance)return new(){Exists=true,Kind="ClassicAppearance",Value=ClassicAppearance.Serialize(ClassicAppearance.Read())};
        if(change.Kind==ChangeKind.RegistryKey)
        {
            using var key=Registry.CurrentUser.OpenSubKey(change.Target[5..]);
            return new(){Exists=key!=null,Kind="RegistryKey",Value=key!=null?"present":null};
        }
        if(change.Kind==ChangeKind.TaskbarState)
        {
            var data=new AppBarData{Size=(uint)Marshal.SizeOf<AppBarData>()};
            return new(){Exists=true,Kind="TaskbarState",Value=(SHAppBarMessage(4,ref data).ToUInt64()&1).ToString(System.Globalization.CultureInfo.InvariantCulture)};
        }
        if (change.Kind == ChangeKind.VisualStyle)
        {
            var path = new StringBuilder(32768); var color = new StringBuilder(256); var size = new StringBuilder(256);
            Marshal.ThrowExceptionForHR(GetCurrentThemeName(path,path.Capacity,color,color.Capacity,size,size.Capacity));
            return new() { Exists=true,Kind="VisualStyle",Value=new VisualStyleState(path.ToString(),color.ToString(),size.ToString()).Encode() };
        }
        if (change.Kind == ChangeKind.Wallpaper)
        {
            var path = new StringBuilder(32768);
            if (!GetWallpaper(0x73,(uint)path.Capacity,path,0)) throw new Win32Exception();
            return new() { Exists=true,Kind="Wallpaper",Value=path.Length==0 ? "" : Path.GetFullPath(path.ToString()).ToUpperInvariant() };
        }
        return filesAndRegistry.Read(change);
    }
    public void Write(Change change, ValueState state)
    {
        Validate(change);
        if(change.Kind==ChangeKind.ClassicAppearance)
        {if(!state.Exists||state.Kind!="ClassicAppearance")throw new InvalidDataException("Invalid Classic snapshot");ClassicAppearance.Write(ClassicAppearance.Parse(state.Value!));return;}
        if(change.Kind==ChangeKind.RegistryKey)
        {
            if(state.Exists)
            {
                if(state.Kind!="RegistryKey" || state.Value!="present")throw new InvalidDataException("Invalid namespace key snapshot");
                using var created=Registry.CurrentUser.CreateSubKey(change.Target[5..],true);
            }
            else
            {
                using(var existing=Registry.CurrentUser.OpenSubKey(change.Target[5..]))
                    if(existing!=null && (existing.ValueCount!=0 || existing.SubKeyCount!=0))
                        throw new IOException("Namespace key contains later changes; it was preserved.");
                Registry.CurrentUser.DeleteSubKey(change.Target[5..],false);
            }
            return;
        }
        if(change.Kind==ChangeKind.TaskbarState)
        {
            if(!state.Exists || state.Kind!="TaskbarState" || !uint.TryParse(state.Value,out uint bits) || bits>1)throw new InvalidDataException("Invalid taskbar snapshot");
            var data=new AppBarData{Size=(uint)Marshal.SizeOf<AppBarData>(),Window=FindWindowW("Shell_TrayWnd",null)};
            // Only own ABS_AUTOHIDE. Modern Windows does not retain ABS_ALWAYSONTOP.
            ulong previous=SHAppBarMessage(4,ref data).ToUInt64();
            data.Parameter=new IntPtr((long)((previous&~1UL)|bits));
            if(data.Window==IntPtr.Zero)throw new InvalidOperationException("Панель задач не найдена.");
            SHAppBarMessage(10,ref data);return;
        }
        if (change.Kind == ChangeKind.VisualStyle)
        {
            if (!state.Exists || state.Kind != "VisualStyle") throw new InvalidDataException("Invalid style snapshot");
            var style=VisualStyleState.Decode(state.Value!);ValidateStyle(style);
            if (!File.Exists(style.Path)) throw new FileNotFoundException("Визуальный стиль отсутствует",style.Path);
            // UxTheme resets this DWM preference even when reapplying the same style.
            // Keep the side effect inside the logical style operation so reverse-order
            // rollback and repeat application retain the independently journalled value.
            var colorization=new Change { Kind=ChangeKind.Registry,Target=DesktopRegistryPolicy.Dwm,ValueName="EnableWindowColorization" };
            var beforeColorization=filesAndRegistry.Read(colorization);
            int result;
            try { result=SetSystemVisualStyle(style.Path,style.Color,style.Size,0); }
            finally { filesAndRegistry.Write(colorization,beforeColorization); }
            if (result < 0) throw new InvalidOperationException($"Windows отклонила визуальный стиль (0x{result:X8}). Проверьте установленный SecureUxTheme и перезагрузите Windows.");
            return;
        }
        if (change.Kind == ChangeKind.Wallpaper)
        {
            if (!state.Exists || state.Kind != "Wallpaper" || state.Value == null) throw new InvalidDataException("Invalid wallpaper snapshot");
            if (state.Value.Length>0 && !File.Exists(state.Value)) throw new FileNotFoundException("Файл обоев отсутствует",state.Value);
            if (!SetWallpaper(0x14,0,state.Value,3)) throw new Win32Exception();
            return;
        }
        filesAndRegistry.Write(change,state);
    }
    public static void NotifySettingsChanged()
    {
        // Broadcast application configuration changes; no mouse/keyboard automation.
        SendMessageTimeout(new IntPtr(0xFFFF),0x1A,UIntPtr.Zero,"ImmersiveColorSet",2,1000,out _);
        SendMessageTimeout(new IntPtr(0xFFFF),0x1A,UIntPtr.Zero,"TraySettings",2,1000,out _);
        SendMessageTimeout(new IntPtr(0xFFFF),0x31A,UIntPtr.Zero,null,2,1000,out _);
        SendMessageTimeout(new IntPtr(0xFFFF),0x320,UIntPtr.Zero,null,2,1000,out _);
    }
    [StructLayout(LayoutKind.Sequential)] private struct AppBarData
    { public uint Size; public IntPtr Window; public uint Callback; public uint Edge; public int Left,Top,Right,Bottom; public IntPtr Parameter; }
    [DllImport("shell32.dll",ExactSpelling=true)] private static extern UIntPtr SHAppBarMessage(uint message,ref AppBarData data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] private static extern IntPtr FindWindowW(string className,string? title);
    [DllImport("uxtheme.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] private static extern int GetCurrentThemeName(StringBuilder path,int pathLength,StringBuilder color,int colorLength,StringBuilder size,int sizeLength);
    // Undocumented Windows export; deliberately restricted to the tested lab by DesktopController.
    [DllImport("uxtheme.dll",EntryPoint="#65",CharSet=CharSet.Unicode,ExactSpelling=true)] private static extern int SetSystemVisualStyle(string path,string color,string size,uint reserved);
    [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetWallpaper(uint action,uint parameter,StringBuilder path,uint flags);
    [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetWallpaper(uint action,uint parameter,string path,uint flags);
    [DllImport("user32.dll",EntryPoint="SendMessageTimeoutW",CharSet=CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr window,uint message,UIntPtr wParam,string? lParam,uint flags,uint timeout,out UIntPtr result);
}
