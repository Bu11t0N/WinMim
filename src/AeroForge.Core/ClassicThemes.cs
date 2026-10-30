namespace AeroForge.Core;

public sealed record ClassicTheme(string Id,string Name,string Taskbar,string Font,int Background,int Face,int Highlight,bool Gradient,bool QuickLaunch,bool Xp=false)
{
    public string Detail=>Xp?"Панель и меню Luna; пока классические рамки и общий набор значков":"Классические окна, палитра, меню и панель; ресурсы ещё уточняются";
}
public static class ClassicThemes
{
    public static IReadOnlyList<ClassicTheme> All {get;}=Array.AsReadOnly(new[]{
        new ClassicTheme("windows-95","Windows 95","Windows 95-98","MS Sans Serif",0x808000,0xC0C0C0,0x800000,false,false),
        new ClassicTheme("windows-nt4","Windows NT 4.0","Windows 95-98","MS Sans Serif",0x808000,0xC0C0C0,0x800000,false,false),
        new ClassicTheme("windows-98","Windows 98","Windows 95-98","MS Sans Serif",0x808000,0xC0C0C0,0x800000,true,true),
        new ClassicTheme("windows-98se","Windows 98 SE","Windows 95-98","MS Sans Serif",0x808000,0xC0C0C0,0x800000,true,true),
        new ClassicTheme("windows-me","Windows Me","Windows Me","Tahoma",0x808000,0xC8D0D4,0x800000,true,true),
        new ClassicTheme("windows-2000","Windows 2000","Windows 2000","Tahoma",0xA66E3A,0xC8D0D4,0x800000,true,true),
        new ClassicTheme("windows-xp","Windows XP","Windows XP Blue","Tahoma",0xB06E3A,0xD8E9EC,0xC56A31,true,true,true)
    });
    public static ClassicTheme Get(string id)=>All.SingleOrDefault(t=>t.Id==id)??throw new ArgumentException("Неизвестный классический профиль: "+id);
    public static string FromReceipt(string profile)=>profile=="desktop-windows95"?"windows-95":Get(profile.Replace("desktop-","",StringComparison.Ordinal)).Id;
}
