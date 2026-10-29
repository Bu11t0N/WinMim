namespace AeroForge.Core;

// Presentation targets are deliberately separate from executable shell profiles.
// Selecting any target, including Seven, is currently a side-effect-free placeholder.
public sealed record AppearanceTarget(string Id, string Name, string Family, bool Available=false);
public sealed record AppearanceSelection(AppearanceTarget Target)
{
    public string Status => Target.Available?"Экспериментальный профиль — применение отдельной кнопкой":"Заглушка — оформление не применяется";
    public bool ChangesSystem => false;
    public IReadOnlyList<Change> Changes => Array.Empty<Change>();
}
public static class AppearanceCatalog
{
    public static IReadOnlyList<AppearanceTarget> Targets { get; } = Array.AsReadOnly(new[] {
        new AppearanceTarget("windows-95","Windows 95","Классические Windows",true),
        new AppearanceTarget("windows-98","Windows 98","Классические Windows"),
        new AppearanceTarget("windows-me","Windows Me","Классические Windows"),
        new AppearanceTarget("windows-2000","Windows 2000","Классические Windows"),
        new AppearanceTarget("windows-xp","Windows XP","Luna"),
        new AppearanceTarget("windows-vista","Windows Vista","Aero"),
        new AppearanceTarget("windows-7","Windows 7","Aero"),
        new AppearanceTarget("windows-8","Windows 8","Modern"),
        new AppearanceTarget("windows-81","Windows 8.1","Modern"),
        new AppearanceTarget("windows-10","Windows 10","Windows 10"),
        new AppearanceTarget("windows-11","Windows 11","Windows 11") });
    public static AppearanceSelection Select(string id) => new(Targets.SingleOrDefault(t=>t.Id==id)
        ?? throw new ArgumentException("Неизвестное оформление: "+id,nameof(id)));
}
