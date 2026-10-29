using System.Globalization;
using System.Text.Json;

namespace AeroForge.Core;

public sealed record TaskbarPreferences(bool Locked=true,bool AutoHide=false,bool SmallIcons=false,int Combine=0,bool Peek=true)
{
    public const string SavedPath="Taskbar/preferences.json";
    public static IReadOnlySet<string> UserSettingIdentities { get; }=new TaskbarPreferences().Changes().Where(c=>c.Kind!=ChangeKind.File).Select(c=>c.Identity).ToHashSet();
    public void Validate()
    {
        if(Combine is <0 or >2)throw new InvalidDataException("Недопустимый режим группировки панели задач.");
    }
    public static TaskbarPreferences LoadSaved(string root)
    {
        string path=SafePath.Under(root,SavedPath);
        var preferences=File.Exists(path)?JsonFile.Read<TaskbarPreferences>(path):new();
        preferences.Validate();return preferences;
    }
    public IEnumerable<Change> Changes()
    {
        Validate();
        yield return new(){Kind=ChangeKind.File,Target=SavedPath,Content=JsonSerializer.SerializeToUtf8Bytes(this,JsonFile.Options),Description="Настройки панели задач WinMim"};
        foreach(var (name,value) in new[]{("TaskbarSizeMove",Locked?0:1),("TaskbarSmallIcons",SmallIcons?1:0),
            ("TaskbarGlomLevel",Combine),("MMTaskbarGlomLevel",Combine),("DisablePreviewDesktop",Peek?0:1)})
            yield return new(){Kind=ChangeKind.Registry,Target=DesktopRegistryPolicy.Advanced,ValueName=name,ValueKind="DWord",Value=value.ToString(CultureInfo.InvariantCulture),Description=name};
        yield return new(){Kind=ChangeKind.TaskbarState,Target="Windows.TaskbarState",ValueKind="TaskbarState",Value=AutoHide?"1":"0",Description="Автоматическое скрытие панели задач"};
    }
}
