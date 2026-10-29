using System.Text;

namespace AeroForge.Core;

public sealed class ProfilePlanner(ContentStore store, Catalog catalog)
{
    public static string? FindController()=>new[]{Path.Combine(AppContext.BaseDirectory,"WinMim.exe"),Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","WinMim.exe"))}.FirstOrDefault(File.Exists);
    public Plan Build(string profileId, IEnumerable<string>? selected = null, bool? autostart = null,
        string? hostExecutable = null, bool allowCritical = false, string? privateIconPack = null, string? privateDisplayProvider = null,string? resumeLauncher=null)
    {
        var profile = catalog.Profiles.Single(p => p.Id == profileId);
        var ids = (selected ?? profile.Modules).ToHashSet(StringComparer.Ordinal);
        byte[]? displayProvider=ids.Contains("win7-display-control-panel-restorer")&&privateDisplayProvider!=null?ExternalDisplayProvider.Read(privateDisplayProvider):null;
        Dictionary<string,string>? iconSettings=null;
        if(ids.Contains("winmim-shell-behavior"))
        {
            profile=new Profile{Id=profile.Id,Name=profile.Name,Description=profile.Description,Accent=profile.Accent,
                Modules=profile.Modules,Settings=profile.Settings.ToDictionary(p=>p.Key,p=>new Dictionary<string,string>(p.Value))};
            string controller=FindController()??"";
            profile.Settings["winmim-shell-behavior"]=new(){{"controller",controller},{"useOwnTaskbarDialog",ids.Contains("classic-taskbar-properties")?"0":"1"}};
        }
        if(privateIconPack!=null)
        {
            iconSettings=PrivateIconPack.Settings(privateIconPack);
            ids.Add("icon-resource-redirect");
            profile=new Profile{Id=profile.Id,Name=profile.Name,Description=profile.Description,Accent=profile.Accent,
                Modules=profile.Modules,Settings=profile.Settings.ToDictionary(p=>p.Key,p=>new Dictionary<string,string>(p.Value))};
            profile.Settings["icon-resource-redirect"]=iconSettings;
        }
        var plan = new Plan { ProfileId = profile.Id, ProfileName = profile.Name, Modules = ids.Order().ToArray() };
        var known = catalog.Modules.ToDictionary(m => m.Id);
        foreach (string id in ids)
        {
            if (!known.TryGetValue(id, out var module)) { plan.Blockers.Add("Неизвестный модуль: " + id); continue; }
            if (module.Status != "built" || module.Binaries.Count == 0) plan.Blockers.Add(module.Name + ": бинарник не собран");
            if (module.CriticalProcess && !allowCritical) plan.Blockers.Add(module.Name + ": требуется отдельная проверка системных процессов в VM");
            if (module.RequiresResources && !(module.Id=="icon-resource-redirect"&&iconSettings!=null) && !(module.Id=="win7-display-control-panel-restorer"&&displayProvider!=null)) plan.Blockers.Add(module.Name + ": не установлен внешний пакет ресурсов");
            foreach (string conflict in module.Conflicts.Where(ids.Contains))
                plan.Blockers.Add(module.Name + " конфликтует с " + conflict);
        }
        plan.Warnings.Add("Совместимость модулей зависит от конкретного обновления Windows. Испытания одной сборки не гарантируют работу на всех версиях.");
        if(iconSettings!=null)plan.Warnings.Add("Личный внешний пакет значков: не включается в дистрибутив WinMim. Системные DLL не заменяются.");
        plan.Warnings.Add("При первом запуске модулям могут понадобиться символы с сервера Microsoft. Для работы без сети нужен заранее заполненный кеш символов.");
        if (catalog.RuntimeFiles.Count == 0) plan.Blockers.Add("Движок отсутствует. Выполните tools/prepare.py и tools/build_modules.py.");
        if (ids.Count == 0) plan.Warnings.Add("Не выбран ни один модуль: будет подготовлен только движок.");
        // Deployment must run with the managed host stopped; the UI and CLI enforce that boundary.
        foreach (var f in catalog.RuntimeFiles) AddFile(plan, f.Target, store.ReadVerified(f), "Движок: " + f.Target);
        if(displayProvider!=null)AddFile(plan,ExternalDisplayProvider.Target,displayProvider,"Проверенный внешний Microsoft Display provider");
        foreach (var module in catalog.Modules)
        {
            if (ids.Contains(module.Id))
                foreach (var f in module.Binaries) AddFile(plan, f.Target, store.ReadVerified(f), module.Name);
            AddFile(plan, "Data/Engine/Mods/" + module.ManagedId + ".ini",
                Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Ini(module, ids.Contains(module.Id), profile))).ToArray(),
                (ids.Contains(module.Id) ? "Включить: " : "Отключить: ") + module.Name);
        }
        AddFile(plan, "windhawk.ini", Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(
            "[Storage]\r\nPortable=1\r\nEnginePath=Engine\\1.7.3\r\nCompilerPath=Compiler\r\nUIPath=UI\r\nAppDataPath=Data\r\n")).ToArray(), "Изолированная конфигурация Windhawk");
        AddFile(plan, "Engine/1.7.3/engine.ini", Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(
            "[Storage]\r\nPortable=1\r\nAppDataPath=../../Data/Engine\r\n")).ToArray(), "Конфигурация движка");
        AddFile(plan, "active-profile.json", Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new
        { profile = profile.Id, modules = plan.Modules, version = catalog.Version,
            displayProvider = displayProvider!=null?Path.GetFullPath(privateDisplayProvider!):null }, JsonFile.Options)), "Активный профиль");
        if (autostart.HasValue)
        {
            string? startup=resumeLauncher??hostExecutable;
            if (autostart.Value && (string.IsNullOrWhiteSpace(startup) || !Path.IsPathFullyQualified(startup) || startup.IndexOfAny(['"','\r','\n','\0'])>=0))
                throw new ArgumentException("An absolute host executable path is required for autostart");
            plan.Changes.Add(new Change { Kind = ChangeKind.Registry,
                Target = "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run", ValueName = "AeroForge",
                Value = autostart.Value ? "\"" + startup + (resumeLauncher!=null?"\" --resume-shell":"\" -tray-only") : null,
                Description = autostart.Value ? "Включить автозапуск движка" : "Отключить автозапуск движка" });
        }
        if (plan.Changes.Select(c => c.Identity).Distinct().Count() != plan.Changes.Count)
            throw new InvalidDataException("Plan contains duplicate targets");
        return plan;
    }
    private static void AddFile(Plan plan, string target, byte[] content, string description) => plan.Changes.Add(new Change
    { Kind = ChangeKind.File, Target = target, Content = content, Description = description });
    public static string Ini(Module module, bool enabled, Profile profile)
    {
        string? dll = module.Binaries.FirstOrDefault()?.Target.Split('/').Last();
        var settings = new Dictionary<string, string>(module.DefaultSettings);
        if (profile.Settings.TryGetValue(module.Id, out var overrides))
            foreach (var pair in overrides) settings[pair.Key] = pair.Value;
        // Controllers and guest integration must remain available for recovery.
        var excludes=module.Excludes.Concat(new[]{"WinMim.exe","AeroForge.exe","AeroForge.Cli.exe","AeroForge.VmSmoke.exe",
            "vmtoolsd.exe","vm3dservice.exe","VGAuthService.exe","vmwaretray.exe","vmwareuser.exe"}).Distinct(StringComparer.OrdinalIgnoreCase);
        var includes=module.Includes;
        var lines = new List<string> { "[Mod]", "LibraryFileName=" + (dll ?? "missing.dll"),
            "Disabled=" + (enabled ? "0" : "1"), "LoggingEnabled=0", "DebugLoggingEnabled=0",
            "Include=" + string.Join('|', includes), "Exclude=" + string.Join('|', excludes),
            "Architecture=" + string.Join('|', module.Architecture), "Version=" + module.Version,
            "SettingsChangeTime=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "", "[Settings]" };
        foreach (var kv in settings.OrderBy(p => p.Key))
        {
            if (kv.Key.ContainsAny('\r', '\n', '=') || kv.Value.ContainsAny('\r', '\n'))
                throw new InvalidDataException("Unsafe INI setting");
            lines.Add(kv.Key + "=" + kv.Value);
        }
        return string.Join("\r\n", lines) + "\r\n";
    }
}
internal static class StringExtensions
{ public static bool ContainsAny(this string s, params char[] chars) => s.IndexOfAny(chars) >= 0; }
