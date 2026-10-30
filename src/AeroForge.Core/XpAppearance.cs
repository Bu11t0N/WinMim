using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AeroForge.Core;

[SupportedOSPlatform("windows")]
public static class XpAppearance
{
    public static int SystemDpi=>AtSystemDpi(()=>checked((int)GetDpiForSystem()));
    public static ClassicAppearanceState ReadPhysical()=>AtSystemDpi(ClassicAppearance.Read);
    public static void WritePhysical(ClassicAppearanceState state)=>AtSystemDpi(()=>{ClassicAppearance.Write(state);return true;});
    private static T AtSystemDpi<T>(Func<T> action)
    {
        // CLI callers can be DPI-unaware. SPI metrics must use the same physical
        // coordinate system as the system-aware GUI. Keep the thread override
        // inside this synchronous native call; never carry it across an await.
        IntPtr previous=SetThreadDpiAwarenessContext(new IntPtr(-2));
        if(previous==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
        try{return action();}finally{SetThreadDpiAwarenessContext(previous);}
    }
    public static ClassicAppearanceState Desired(ClassicAppearanceState current,int dpi)
    {
        if(dpi is <96 or >480)throw new ArgumentOutOfRangeException(nameof(dpi));
        int Scale(int value)=>(int)Math.Round(value*dpi/96.0,MidpointRounding.AwayFromZero);
        var m=ClassicAppearance.Decode<ClassicAppearance.Ncm>(current.Metrics);
        // Windows normalizes persisted LOGFONT quality to DEFAULT_QUALITY at
        // logon. Use that value from the start so the native undo fingerprint
        // stays exact after reboot without ignoring user font changes.
        var font=new ClassicAppearance.LogFont{Height=-Scale(11),Weight=400,CharSet=1,Quality=0,Face="Tahoma"};
        m.Border=Scale(1);m.PaddedBorder=0;m.ScrollWidth=Scale(17);m.ScrollHeight=Scale(17);
        m.CaptionWidth=Scale(25);m.CaptionHeight=Scale(25);m.SmallCaptionWidth=Scale(17);m.SmallCaptionHeight=Scale(17);m.MenuWidth=Scale(18);m.MenuHeight=Scale(18);
        m.CaptionFont=font;m.CaptionFont.Face="Trebuchet MS";m.CaptionFont.Height=-Scale(13);m.CaptionFont.Weight=700;
        m.SmallCaptionFont=font;m.MenuFont=font;m.StatusFont=font;m.MessageFont=font;
        var colors=(int[])ClassicAppearance.Colors95.Clone();
        void Color(string name,int r,int g,int b)=>colors[Array.IndexOf(ClassicAppearance.ColorNames,name)]=ClassicAppearance.Rgb(r,g,b);
        foreach(string name in new[]{"ButtonFace","Menu","MenuBar","ActiveBorder","InactiveBorder"})Color(name,236,233,216);
        Color("Background",58,110,165);Color("ActiveTitle",0,84,227);Color("InactiveTitle",122,150,223);
        Color("GradientActiveTitle",61,149,255);Color("GradientInactiveTitle",157,185,235);
        Color("Hilight",49,106,197);Color("MenuHilight",49,106,197);Color("HotTrackingColor",0,0,128);
        Color("ButtonShadow",172,168,153);Color("ButtonLight",241,239,226);Color("ButtonDkShadow",113,111,100);
        Color("Scrollbar",212,208,200);Color("InactiveTitleText",216,228,248);
        return new(colors,ClassicAppearance.Encode(m),ClassicAppearance.Encode(font),true,true);
    }
    public static IEnumerable<Change> PersistentMetrics(ClassicAppearanceState state,int dpi)
    {
        var m=ClassicAppearance.Decode<ClassicAppearance.Ncm>(state.Metrics);
        foreach(var (name,value) in new[]{("CaptionFont",ClassicAppearance.Encode(m.CaptionFont)),("SmCaptionFont",ClassicAppearance.Encode(m.SmallCaptionFont)),("MenuFont",ClassicAppearance.Encode(m.MenuFont)),("StatusFont",ClassicAppearance.Encode(m.StatusFont)),("MessageFont",ClassicAppearance.Encode(m.MessageFont)),("IconFont",state.IconFont)})
            yield return Metric(name,value,"Binary");
        foreach(var (name,value) in new[]{("BorderWidth",m.Border),("PaddedBorderWidth",m.PaddedBorder),("ScrollWidth",m.ScrollWidth),("ScrollHeight",m.ScrollHeight),("CaptionWidth",m.CaptionWidth),("CaptionHeight",m.CaptionHeight),("SmCaptionWidth",m.SmallCaptionWidth),("SmCaptionHeight",m.SmallCaptionHeight),("MenuWidth",m.MenuWidth),("MenuHeight",m.MenuHeight)})
            yield return Metric(name,(-(int)Math.Round(value*1440.0/dpi)).ToString(CultureInfo.InvariantCulture),"String");
    }
    private static Change Metric(string name,string value,string kind)=>new(){Kind=ChangeKind.Registry,Target=Classic95Profile.MetricsKey,ValueName=name,ValueKind=kind,Value=value,Description="Шрифт/метрика XP: "+name};
    [DllImport("user32.dll")]private static extern uint GetDpiForSystem();
    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
