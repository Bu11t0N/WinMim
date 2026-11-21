namespace AeroForge.Core;

// Presentation targets are deliberately separate from executable shell profiles.
// Selection is side-effect free; available targets are applied explicitly.
public enum AppearanceMode { Unavailable, Classic, Seven }

public sealed record AppearanceTarget(string Id, string Name, string Family, AppearanceMode Mode = AppearanceMode.Unavailable)
{
    public bool Available => Mode != AppearanceMode.Unavailable;
}
public sealed record AppearanceSelection(AppearanceTarget Target)
{
    public string Status => Target.Available?UiText.Get("AppearanceCatalog.ExperimentalAppearance"):UiText.Get("AppearanceCatalog.AppearanceCurrentlyUnavailable");
    public bool ChangesSystem => false;
    public IReadOnlyList<Change> Changes => Array.Empty<Change>();
}
public static class AppearanceCatalog
{
    public static IReadOnlyList<AppearanceTarget> Targets { get; } = Array.AsReadOnly(new[] {
        new AppearanceTarget("windows-95","Windows 95",UiText.Get("AppearanceCatalog.ClassicWindows"),AppearanceMode.Classic),
        new AppearanceTarget("windows-nt4","Windows NT 4.0",UiText.Get("AppearanceCatalog.ClassicWindows"),AppearanceMode.Classic),
        new AppearanceTarget("windows-98","Windows 98",UiText.Get("AppearanceCatalog.ClassicWindows"),AppearanceMode.Classic),
        new AppearanceTarget("windows-98se","Windows 98 SE",UiText.Get("AppearanceCatalog.ClassicWindows"),AppearanceMode.Classic),
        new AppearanceTarget("windows-me","Windows Me",UiText.Get("AppearanceCatalog.ClassicWindows"),AppearanceMode.Classic),
        new AppearanceTarget("windows-2000","Windows 2000",UiText.Get("AppearanceCatalog.ClassicWindows"),AppearanceMode.Classic),
        new AppearanceTarget("windows-xp","Windows XP",UiText.Get("AppearanceCatalog.LunaPartial"),AppearanceMode.Classic),
        new AppearanceTarget("windows-vista","Windows Vista","Aero"),
        new AppearanceTarget("windows-7","Windows 7","Aero",AppearanceMode.Seven),
        new AppearanceTarget("windows-8","Windows 8","Modern"),
        new AppearanceTarget("windows-81","Windows 8.1","Modern"),
        new AppearanceTarget("windows-10","Windows 10","Windows 10"),
        new AppearanceTarget("windows-11","Windows 11","Windows 11") });
    public static AppearanceSelection Select(string id) => new(Targets.SingleOrDefault(t=>t.Id==id)
        ?? throw new ArgumentException(UiText.Get("AppearanceCatalog.UnknownAppearance")+id,nameof(id)));
}
