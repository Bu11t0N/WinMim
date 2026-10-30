using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
namespace AeroForge.Core;

public sealed record ClassicAppearanceState(int[] Colors,string Metrics,string IconFont,bool Gradient,bool FlatMenus);
[SupportedOSPlatform("windows")]
public static class ClassicAppearance
{
    public static readonly int[] ColorIndices=Enumerable.Range(0,31).Where(i=>i!=25).ToArray();
    public static readonly string[] ColorNames=["Scrollbar","Background","ActiveTitle","InactiveTitle","Menu","Window","WindowFrame","MenuText","WindowText","TitleText","ActiveBorder","InactiveBorder","AppWorkspace","Hilight","HilightText","ButtonFace","ButtonShadow","GrayText","ButtonText","InactiveTitleText","ButtonHilight","ButtonDkShadow","ButtonLight","InfoText","InfoWindow","HotTrackingColor","GradientActiveTitle","GradientInactiveTitle","MenuHilight","MenuBar"];
    public static readonly int[] Colors95=[Rgb(192,192,192),Rgb(0,128,128),Rgb(0,0,128),Rgb(128,128,128),Rgb(192,192,192),Rgb(255,255,255),0,0,0,Rgb(255,255,255),Rgb(192,192,192),Rgb(192,192,192),Rgb(128,128,128),Rgb(0,0,128),Rgb(255,255,255),Rgb(192,192,192),Rgb(128,128,128),Rgb(128,128,128),0,Rgb(192,192,192),Rgb(255,255,255),0,Rgb(223,223,223),0,Rgb(255,255,225),Rgb(0,0,128),Rgb(0,0,128),Rgb(128,128,128),Rgb(0,0,128),Rgb(192,192,192)];
    public static int Rgb(int r,int g,int b)=>r|(g<<8)|(b<<16);
    public static string RgbText(int color)=>$"{color&255} {(color>>8)&255} {(color>>16)&255}";
    public static ClassicAppearanceState Read()
    {
        var metrics=new Ncm{Size=(uint)Marshal.SizeOf<Ncm>()};if(!Metrics(0x29,metrics.Size,ref metrics,0))throw new Win32Exception();
        var icon=new LogFont();if(!Font(0x1F,(uint)Marshal.SizeOf<LogFont>(),ref icon,0))throw new Win32Exception();
        return new(ColorIndices.Select(GetSysColor).ToArray(),Encode(metrics),Encode(icon),Bool(0x1008),Bool(0x1022));
    }
    public static ClassicAppearanceState Desired(ClassicAppearanceState current,string target="windows-95")
    {
        var theme=ClassicThemes.Get(target);
        var m=Decode<Ncm>(current.Metrics);var font=new LogFont{Height=-11,Weight=400,CharSet=1,Quality=3,Pitch=0,Face=theme.Font};
        m.Border=1;m.ScrollWidth=16;m.ScrollHeight=16;m.CaptionWidth=18;m.CaptionHeight=18;m.SmallCaptionWidth=15;m.SmallCaptionHeight=15;m.MenuWidth=18;m.MenuHeight=18;m.PaddedBorder=0;
        m.CaptionFont=font;m.CaptionFont.Weight=700;m.SmallCaptionFont=font;m.SmallCaptionFont.Weight=700;m.MenuFont=font;m.StatusFont=font;m.MessageFont=font;
        var colors=(int[])Colors95.Clone();
        void Color(string name,int color)=>colors[Array.IndexOf(ColorNames,name)]=color;
        Color("Background",theme.Background);
        foreach(string name in new[]{"ButtonFace","Menu","MenuBar","ActiveBorder","InactiveBorder","Scrollbar"})Color(name,theme.Face);
        Color("Hilight",theme.Highlight);Color("MenuHilight",theme.Highlight);
        if(theme.Gradient){Color("GradientActiveTitle",Rgb(16,132,208));Color("GradientInactiveTitle",Rgb(181,181,181));}
        if(theme.Xp){Color("ActiveTitle",Rgb(0,84,227));Color("GradientActiveTitle",Rgb(61,149,255));Color("InactiveTitle",Rgb(122,150,223));}
        return new(colors,Encode(m),Encode(font),theme.Gradient,false);
    }
    // Windows may derive caption-button width from the calling process's active
    // visual style. The persistent CaptionWidth preference has its own registry
    // journal entry, so compare native state without this single derived field.
    public static string StableValue(string value)
    {
        var state=Parse(value);var metrics=Decode<Ncm>(state.Metrics);metrics.CaptionWidth=0;
        return Serialize(state with {Metrics=Encode(metrics)});
    }
    public static string Serialize(ClassicAppearanceState state)=>JsonSerializer.Serialize(state);
    public static void NotifyThemeChanged()=>PostMessage(new IntPtr(0xffff),0x031A,IntPtr.Zero,IntPtr.Zero);
    public static ClassicAppearanceState Parse(string json)=>JsonSerializer.Deserialize<ClassicAppearanceState>(json)??throw new InvalidDataException("Нет состояния Classic");
    public static void Write(ClassicAppearanceState value)
    {
        if(value.Colors.Length!=ColorIndices.Length)throw new InvalidDataException("Неверная палитра Classic");
        var m=Decode<Ncm>(value.Metrics);var icon=Decode<LogFont>(value.IconFont);
        if(m.Size!=Marshal.SizeOf<Ncm>())throw new InvalidDataException("Неверный снимок метрик");
        if(!Metrics(0x2A,m.Size,ref m,0)||!Font(0x22,(uint)Marshal.SizeOf<LogFont>(),ref icon,0)||!SetBool(0x1009,0,new IntPtr(value.Gradient?1:0),0)||!SetBool(0x1023,0,new IntPtr(value.FlatMenus?1:0),0)||!SetSysColors(ColorIndices.Length,ColorIndices,value.Colors))throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public static IEnumerable<Change> PersistentMetrics(ClassicAppearanceState value)
    {
        var m=Decode<Ncm>(value.Metrics);
        foreach(var (name,data) in new[]{("CaptionFont",Encode(m.CaptionFont)),("SmCaptionFont",Encode(m.SmallCaptionFont)),("MenuFont",Encode(m.MenuFont)),("StatusFont",Encode(m.StatusFont)),("MessageFont",Encode(m.MessageFont)),("IconFont",value.IconFont)})
            yield return new(){Kind=ChangeKind.Registry,Target=@"HKCU\Control Panel\Desktop\WindowMetrics",ValueName=name,ValueKind="Binary",Value=data,Description="Шрифт Windows 95: "+name};
        foreach(var (name,data) in new[]{("BorderWidth","-15"),("PaddedBorderWidth","0"),("ScrollWidth","-240"),("ScrollHeight","-240"),("CaptionWidth","-270"),("CaptionHeight","-270"),("SmCaptionWidth","-225"),("SmCaptionHeight","-225"),("MenuWidth","-270"),("MenuHeight","-270")})
            yield return new(){Kind=ChangeKind.Registry,Target=@"HKCU\Control Panel\Desktop\WindowMetrics",ValueName=name,ValueKind="String",Value=data,Description="Метрика Windows 95: "+name};
    }
    private static bool Bool(uint action){if(!GetBool(action,0,out int value,0))throw new Win32Exception();return value!=0;}
    private static string Encode<T>(T value)where T:struct
    {int size=Marshal.SizeOf<T>();IntPtr p=Marshal.AllocHGlobal(size);try{Marshal.Copy(new byte[size],0,p,size);Marshal.StructureToPtr(value,p,false);var bytes=new byte[size];Marshal.Copy(p,bytes,0,size);return Convert.ToBase64String(bytes);}finally{Marshal.FreeHGlobal(p);}}
    private static T Decode<T>(string text)where T:struct
    {var bytes=Convert.FromBase64String(text);if(bytes.Length!=Marshal.SizeOf<T>())throw new InvalidDataException("Неверный размер снимка");IntPtr p=Marshal.AllocHGlobal(bytes.Length);try{Marshal.Copy(bytes,0,p,bytes.Length);return Marshal.PtrToStructure<T>(p);}finally{Marshal.FreeHGlobal(p);}}
    [StructLayout(LayoutKind.Sequential,CharSet=System.Runtime.InteropServices.CharSet.Unicode)]private struct LogFont{public int Height,Width,Escapement,Orientation,Weight;public byte Italic,Underline,StrikeOut,CharSet,OutPrecision,ClipPrecision,Quality,Pitch;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Face;}
    [StructLayout(LayoutKind.Sequential,CharSet=System.Runtime.InteropServices.CharSet.Unicode)]private struct Ncm{public uint Size;public int Border,ScrollWidth,ScrollHeight,CaptionWidth,CaptionHeight;public LogFont CaptionFont;public int SmallCaptionWidth,SmallCaptionHeight;public LogFont SmallCaptionFont;public int MenuWidth,MenuHeight;public LogFont MenuFont,StatusFont,MessageFont;public int PaddedBorder;}
    [DllImport("user32.dll")]private static extern int GetSysColor(int index);
    [DllImport("user32.dll",EntryPoint="PostMessageW")]private static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool SetSysColors(int count,int[] indices,int[] colors);
    [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)]private static extern bool Metrics(uint action,uint size,ref Ncm metrics,uint flags);
    [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)]private static extern bool Font(uint action,uint size,ref LogFont font,uint flags);
    [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)]private static extern bool GetBool(uint action,uint size,out int value,uint flags);
    [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)]private static extern bool SetBool(uint action,uint size,IntPtr value,uint flags);
}
