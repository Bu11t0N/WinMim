using AeroForge.Core;
using System.Text.Json;

try
{
    string Get(string flag, string fallback = "") => Array.IndexOf(args, flag) is var i && i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    string defaultContent = Path.Combine(AppContext.BaseDirectory, "content");
    if (!Directory.Exists(defaultContent)) defaultContent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "content"));
    string content = Get("--content", defaultContent);
    string root = Get("--root", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AeroForge", "Host"));
    string state = Get("--state", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AeroForge", "State"));
    string command = args.FirstOrDefault() ?? "help";
    if(command is "windows95-apply" or "windows95-restore" or "windows95-inspect")
    {
        if(command=="windows95-inspect"){Console.WriteLine(JsonSerializer.Serialize(new{active=Classic95Setup.IsActive,appearance=ClassicAppearance.Read()},JsonFile.Options));return 0;}
        if(!args.Contains("--accept-experimental"))throw new InvalidOperationException("Windows 95 requires --accept-experimental");
        var setup=new Classic95Setup(content);
        if(command=="windows95-apply")Console.WriteLine(JsonSerializer.Serialize(await setup.ApplyAsync(new Progress<string>(Console.Error.WriteLine)),JsonFile.Options));
        else{await setup.RestoreAsync(new Progress<string>(Console.Error.WriteLine));Console.WriteLine("Restored");}return 0;
    }
    if(command.StartsWith("classic-session-",StringComparison.Ordinal))
    {
        var session=new ClassicSession(int.Parse(Get("--session",ClassicSession.ActiveSession.ToString())));
        if(command=="classic-session-inspect"){Console.WriteLine(JsonSerializer.Serialize(session.Inspect(),JsonFile.Options));return 0;}
        if(!args.Contains("--accept-experimental"))throw new InvalidOperationException("Classic requires --accept-experimental");
        var receipt=command=="classic-session-apply"?session.Apply():command=="classic-session-restore"?session.Restore():throw new ArgumentException("Unknown Classic operation");
        Console.WriteLine(JsonSerializer.Serialize(receipt,JsonFile.Options));return receipt==null||receipt.Status is "Applied" or "RolledBack"?0:3;
    }
    if(command is "sounds-apply" or "sounds-restore" or "sounds-history")
    {
        var sounds=new SevenSoundsController();
        if(command=="sounds-history"){Console.WriteLine(JsonSerializer.Serialize(sounds.History(),JsonFile.Options));return 0;}
        if(!args.Contains("--accept-experimental"))throw new InvalidOperationException(command+" requires --accept-experimental");
        var receipt=command=="sounds-apply"?sounds.Apply():sounds.Restore();Console.WriteLine(JsonSerializer.Serialize(receipt,JsonFile.Options));
        return receipt.Status is "Applied" or "RolledBack"?0:3;
    }
    if(command=="private-extras-inspect")
    {Console.WriteLine(JsonSerializer.Serialize(PrivateExtras.Components.Select(c=>new{c.Id,c.Name,installed=PrivateExtras.IsInstalled(c.Id),available=PrivateExtras.InstallerAvailable(c.Id),owned=PrivateExtras.IsOwned(c.Id)}),JsonFile.Options));return 0;}
    if(command is "private-extras-install" or "private-extras-remove")
    {
        if(!args.Contains("--accept-experimental"))throw new InvalidOperationException(command+" requires --accept-experimental");
        var ids=Get("--ids",Get("--id")).Split(',',StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
        if(ids.Length==0)throw new InvalidDataException("Choose an explicit component ID");
        foreach(string id in ids)PrivateExtras.Find(id);
        var receipts=new List<ExtraReceipt>();
        foreach(string id in ids)receipts.Add(command=="private-extras-install"?await PrivateExtras.InstallAsync(id):await PrivateExtras.RemoveAsync(id));
        Console.WriteLine(JsonSerializer.Serialize(receipts,JsonFile.Options));return 0;
    }
    if(command=="shell-status")
    {var host=new ShellHost(root,new ContentStore(content).Load());Console.WriteLine(JsonSerializer.Serialize(new{running=host.IsRunning(),modules=host.ExplorerStates()},JsonFile.Options));return 0;}
    if(command=="machine-seven-inspect")
    {Console.WriteLine(JsonSerializer.Serialize(MachineSeven.Inspect(),JsonFile.Options));return 0;}
    if(command is "machine-seven-apply" or "machine-seven-restore")
    {
        if(!args.Contains("--accept-experimental"))throw new InvalidOperationException(command+" requires --accept-experimental");
        var machine=new MachineSeven();var receipt=command=="machine-seven-apply"?machine.Apply():machine.Restore();
        Console.WriteLine(JsonSerializer.Serialize(receipt,JsonFile.Options));return receipt.Status is "Applied" or "RolledBack"?0:3;
    }
    if(command=="taskbar-get")
    {Console.WriteLine(JsonSerializer.Serialize(new DesktopController().TaskbarSettings(),JsonFile.Options));return 0;}
    if(command=="taskbar-set")
    {
        if(!args.Contains("--accept-experimental"))throw new InvalidOperationException("taskbar-set requires --accept-experimental");
        var preferences=JsonFile.Read<TaskbarPreferences>(Path.GetFullPath(Get("--preferences")));
        Console.WriteLine(JsonSerializer.Serialize(new DesktopController().SetTaskbar(preferences),JsonFile.Options));return 0;
    }
    if (command == "diagnose")
    { Console.WriteLine(JsonSerializer.Serialize(WindowsBackend.Inspect(), JsonFile.Options)); return 0; }
    if (command == "help")
    {
        Console.WriteLine("WinMim CLI 0.1.0\nCommands: seven-check, seven-apply, diagnose, verify, plan, stage, history, rollback\nOptions: --content DIR --root DIR --state DIR --profile seven|vista|basic|classic\n" +
            "stage requires --accept-experimental. It prepares files; it does not start the shell host.\nrollback requires --receipt ABSOLUTE_PATH. --modules id,id overrides the profile selection.\n" +
            "Desktop lab: desktop-inspect, desktop-plan, desktop-history, desktop-apply, desktop-restore, desktop-refresh\n" +
            "Shell: shell-status (reads the engine's actual Loaded/Loading/Unloaded metadata)\nTaskbar: taskbar-get, taskbar-set --accept-experimental --preferences FILE\nMachine lab: machine-seven-inspect, machine-seven-apply, machine-seven-restore (apply/restore require elevation and --accept-experimental)\n" +
            "desktop-inspect reads the active native visual style and wallpaper without applying a profile.\n" +
            "desktop-apply requires --accept-experimental. Options: --desktop-root DIR --desktop-state DIR --assets DIR --style FILE --wallpaper FILE --button FILE.\n" +
            "desktop-refresh closes Explorer folder windows. Desktop changes are restricted to AEROFORGE-LAB.");
        Console.WriteLine("Sound lab: sounds-apply / sounds-restore --accept-experimental; sounds-history");
        Console.WriteLine("Private lab components: private-extras-inspect, private-extras-install / private-extras-remove --ids games,gadgets,media-center --accept-experimental (elevated)");
        return 0;
    }
    if(command is "seven-check" or "seven-apply")
    {
        var setup=new SevenSetup(content,root,state,args.Contains("--assets")?Get("--assets"):null,args.Contains("--button")?Get("--button"):null);
        if(command=="seven-check")
        {
            var checks=setup.Check();Console.WriteLine(JsonSerializer.Serialize(checks,JsonFile.Options));
            return checks.All(c=>c.Ready)?0:2;
        }
        if(!args.Contains("--accept-experimental"))throw new InvalidOperationException("seven-apply requires --accept-experimental");
        var applied=await setup.ApplyAsync(!args.Contains("--no-restart-explorer"),new Progress<string>(Console.Error.WriteLine));
        Console.WriteLine(JsonSerializer.Serialize(applied,JsonFile.Options));return 0;
    }
    if (command.StartsWith("desktop-",StringComparison.Ordinal))
    {
        if(command=="desktop-inspect")
        {
            var backend=new DesktopBackend(root);
            var currentStyle=backend.Read(new Change{Kind=ChangeKind.VisualStyle,Target="Windows.VisualStyle"});
            var currentWallpaper=backend.Read(new Change{Kind=ChangeKind.Wallpaper,Target="Windows.Wallpaper"});
            Console.WriteLine(JsonSerializer.Serialize(new{visualStyle=VisualStyleState.Decode(currentStyle.Value!),wallpaper=currentWallpaper.Value},JsonFile.Options));
            return 0;
        }
        var desktop=new DesktopController(args.Contains("--desktop-root")?Get("--desktop-root"):null,args.Contains("--desktop-state")?Get("--desktop-state"):null);
        string assets=Get("--assets",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Resources","Themes","Aero10"));
        string style=Get("--style",Path.Combine(assets,"Seven","aero10_seven.msstyles"));
        string wallpaper=Get("--wallpaper",Path.Combine(assets,"Wallpapers","img0.png"));
        string? button=args.Contains("--button")?Get("--button"):null;
        switch(command)
        {
            case "desktop-refresh": await DesktopController.RestartExplorerAsync();break;
            case "desktop-plan": Console.WriteLine(JsonSerializer.Serialize(DesktopProfile.BuildSeven(desktop.Root,style,wallpaper,button),JsonFile.Options));break;
            case "desktop-history": Console.WriteLine(JsonSerializer.Serialize(desktop.History(),JsonFile.Options));break;
            case "desktop-apply":
                if(!args.Contains("--accept-experimental"))throw new InvalidOperationException("desktop-apply requires --accept-experimental");
                Console.WriteLine(JsonSerializer.Serialize(desktop.Apply(style,wallpaper,button,new Progress<string>(Console.Error.WriteLine)),JsonFile.Options));break;
            case "desktop-restore":
                var restored=desktop.Restore(new Progress<string>(Console.Error.WriteLine));
                Console.WriteLine(JsonSerializer.Serialize(restored,JsonFile.Options));return restored.Status=="RolledBack"?0:3;
            default: throw new ArgumentException("Unknown desktop command");
        }
        return 0;
    }
    var store = new ContentStore(content); var catalog = store.Load();
    var transactions = new TransactionManager(new WindowsBackend(root), state);
    if (command == "verify")
    { var errors = store.VerifyAll(catalog); Console.WriteLine(JsonSerializer.Serialize(errors, JsonFile.Options)); return errors.Length == 0 ? 0 : 2; }
    if (command == "history")
    { Console.WriteLine(JsonSerializer.Serialize(transactions.History(), JsonFile.Options)); return 0; }
    if (command == "rollback")
    {
        new ShellHost(root, catalog).EnsureStopped();
        var receipt = transactions.Rollback(Get("--receipt"));
        Console.WriteLine(JsonSerializer.Serialize(receipt, JsonFile.Options));
        return receipt.Status == "RolledBack" ? 0 : 3;
    }
    if (command is "plan" or "stage")
    {
        string? selected = args.Contains("--modules") ? Get("--modules") : null;
        var plan = new ProfilePlanner(store, catalog).Build(Get("--profile", "seven"),
            selected?.Split(',', StringSplitOptions.RemoveEmptyEntries), allowCritical: args.Contains("--allow-critical"));
        if (command == "plan")
        {
            Console.WriteLine(JsonSerializer.Serialize(new { plan.ProfileName, plan.Modules, plan.Blockers, plan.Warnings,
                files = plan.Changes.Count, plan.Bytes, plan.Fingerprint, changes = plan.Changes.Select(c => new { c.Target, c.Description }) }, JsonFile.Options));
            return plan.Blockers.Count == 0 ? 0 : 2;
        }
        if (!args.Contains("--accept-experimental")) throw new InvalidOperationException("stage requires --accept-experimental");
        new ShellHost(root, catalog).EnsureStopped();
        var receipt = transactions.Apply(plan, new Progress<string>(Console.Error.WriteLine));
        Console.WriteLine(receipt.FilePath); return 0;
    }
    throw new ArgumentException("Unknown command: " + command);
}
catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }

