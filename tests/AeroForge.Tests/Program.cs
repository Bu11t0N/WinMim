using AeroForge.Core;
using System.Text;
using System.Diagnostics;

string scratch = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scratch);
int passed = 0, failed = 0;
void Assert(bool condition, string reason = "Assertion failed") { if (!condition) throw new Exception(reason); }
void Test(string name, Action body)
{ try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); } }
void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected an exception"); }
Change FileChange(string name, string value) => new() { Kind = ChangeKind.File, Target = name, Content = Encoding.UTF8.GetBytes(value), Description = name };
Plan PlanOf(params Change[] changes) => new() { ProfileId = "test", Changes = changes.ToList() };
string NewState() => Path.Combine(scratch, Guid.NewGuid().ToString("N"));

Test("Classic colors and assets build a native profile without critical process modules", () =>
{
    Assert(ClassicAppearance.ColorIndices.Length==ClassicAppearance.Colors95.Length);
    Assert(ClassicAppearance.ColorNames.Length==ClassicAppearance.Colors95.Length);
    Assert(ClassicAppearance.RgbText(ClassicAppearance.Colors95[1])=="0 128 128");
    Assert(ClassicAppearance.RgbText(ClassicAppearance.Colors95[2])=="0 0 128");
    string content=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content"));
    var store=new ContentStore(content);var catalog=store.Load();
    var shell=new ProfilePlanner(store,catalog).Build("windows95");
    Assert(shell.Blockers.Count==0,string.Join("; ",shell.Blockers));
    Assert(!shell.Modules.Contains("classic-theme-enable")&&!shell.Modules.Contains("basic-themer"));
    var plan=Classic95Profile.Build(content,NewState(),ClassicAppearance.Read());
    Assert(plan.Exclusive && plan.Changes.Count(c=>c.Kind==ChangeKind.ClassicAppearance)==1);
    Assert(plan.Changes.Any(c=>c.Kind==ChangeKind.Wallpaper&&c.Value==""));
    Assert(plan.Changes.Any(c=>c.ValueName=="MenuStyle"&&c.Value=="Classic1"));
    Assert(plan.Changes.Select(c=>c.Identity).Distinct().Count()==plan.Changes.Count);
    var backend=new DesktopBackend(NewState());foreach(var change in plan.Changes)backend.Validate(change);
    Assert(!Classic95Profile.Allows(new(){Kind=ChangeKind.Registry,Target=Classic95Profile.ColorsKey,ValueName="Foreign"}));
});
Test("classic native snapshot ignores only theme-derived caption width", () =>
{
    var original=ClassicAppearance.Read();var bytes=Convert.FromBase64String(original.Metrics);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16),30);
    var derived=original with {Metrics=Convert.ToBase64String(bytes)};
    Assert(ClassicAppearance.StableValue(ClassicAppearance.Serialize(original))==ClassicAppearance.StableValue(ClassicAppearance.Serialize(derived)));
    derived=derived with {Colors=(int[])derived.Colors.Clone()};derived.Colors[1]^=1;
    Assert(ClassicAppearance.StableValue(ClassicAppearance.Serialize(original))!=ClassicAppearance.StableValue(ClassicAppearance.Serialize(derived)));
    var preference=new Change{Kind=ChangeKind.Registry,Target=Classic95Profile.MetricsKey,ValueName="CaptionWidth",ValueKind="String",Value="-270"};
    Assert(Classic95Profile.Allows(preference));
    Assert(ValueState.Desired(preference).Fingerprint!=new ValueState{Exists=true,Kind="String",Value="-450"}.Fingerprint);
});

Test("Classic ACL journals distinguish new logons, not just reused session numbers", () =>
{
    string a="O:BAG:SYD:(A;;CCLCRC;;;S-1-5-5-0-100)(A;;CCDCLCSWRPSDRCWDWO;;;SY)";
    string b=a.Replace("0-100","0-200");
    Assert(ClassicSession.SessionIdentity(a)!=ClassicSession.SessionIdentity(b));
    Assert(ClassicSession.SessionIdentity(a)==ClassicSession.SessionIdentity(ClassicSession.LockReadAccess(a)));
});

Test("Classic session ACL keeps original grants and never runs on the development host", () =>
{
    string original="O:BAG:SYD:(A;;CCLCRC;;;IU)(A;;CCDCLCSWRPSDRCWDWO;;;SY)";
    var locked=ClassicSession.LockReadAccess(original);
    Assert(locked.Contains("(D;;LC;;;IU)") && locked.Contains("(A;;CCLCRC;;;IU)"));
    if(Environment.MachineName!="AEROFORGE-LAB"){
        Throws(()=>new ClassicSession(1).Apply());Throws(()=>new ClassicSession(1).Restore());Throws(()=>Classic95Setup.Guard());
    }
});

Test("selection of all targets remains side-effect free", () =>
{
    string[] ids=["windows-95","windows-nt4","windows-98","windows-98se","windows-me","windows-2000","windows-xp","windows-vista","windows-7","windows-8","windows-81","windows-10","windows-11"];
    Assert(AppearanceCatalog.Targets.Select(t=>t.Id).SequenceEqual(ids));
    foreach(var id in ids)
    {
        var first=AppearanceCatalog.Select(id);var second=AppearanceCatalog.Select(id);
        Assert(first==second && !first.ChangesSystem && first.Changes.Count==0);
    }
    Throws(()=>AppearanceCatalog.Select("seven")); // executable profiles cannot enter this selection API
    Throws(()=>AppearanceCatalog.Select("../windows-7"));
});

Test("all classic and XP targets build distinct real plans", () =>
{
    string content=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content"));
    var before=ClassicAppearance.Read();var snapshots=new Dictionary<string,ClassicAppearanceState>();
    foreach(var theme in ClassicThemes.All){
        Assert(AppearanceCatalog.Select(theme.Id).Target.Available);
        var plan=Classic95Profile.Build(content,NewState(),before,target:theme.Id);
        Assert(ClassicThemes.FromReceipt(plan.ProfileId)==theme.Id);
        var backend=new DesktopBackend(NewState());foreach(var c in plan.Changes)backend.Validate(c);
        Assert(plan.Changes.Select(c=>c.Identity).Distinct().Count()==plan.Changes.Count);
        Assert(plan.Changes.Any(c=>c.ValueName=="MenuCaption"&&c.Value==theme.Name));
        snapshots[theme.Id]=ClassicAppearance.Parse(plan.Changes.Single(c=>c.Kind==ChangeKind.ClassicAppearance).Value!);
        Assert((theme.Id!="windows-95")==plan.Changes.Any(c=>c.Kind==ChangeKind.File&&c.Target.EndsWith("RetroBar.exe")));
        if(theme.Id!="windows-95")Assert(plan.Changes.Any(c=>c.Target=="QuickLaunch/Проводник.lnk"));
    }
    Assert(!snapshots["windows-95"].Gradient && snapshots["windows-98"].Gradient);
    Assert(!snapshots["windows-95"].Colors.SequenceEqual(snapshots["windows-2000"].Colors));
    Assert(snapshots["windows-95"].Metrics!=snapshots["windows-me"].Metrics);
    var xp=Classic95Profile.Build(content,NewState(),before,target:"windows-xp");
    Assert(xp.Changes.Any(c=>c.ValueName=="SkinC2"&&c.Value=="Windows XP Luna"));
    Assert(xp.Changes.Any(c=>c.ValueName=="MenuStyle"&&c.Value=="Classic2"));
    Throws(()=>ClassicThemes.Get("windows-31"));Throws(()=>ClassicThemes.Get("../windows-xp"));
});
Test("RetroBar JSON field journal preserves upstream defaults and restores original fields", () =>
{
    string root=NewState();Directory.CreateDirectory(root);string path=Path.Combine(root,"settings.json");
    File.WriteAllText(path,"{\"Theme\":\"Original\",\"Unrelated\":42}");
    var backend=new RetroBarSettingsBackend(path);var manager=new TransactionManager(backend,NewState());
    var plan=RetroBarSettingsBackend.Plan(ClassicThemes.Get("windows-xp"),Path.Combine(root,"QuickLaunch"));
    var receipt=manager.Apply(plan);var node=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
    node["RuntimeDefault"]=true;File.WriteAllText(path,node.ToJsonString());
    Assert(manager.Rollback(receipt.FilePath).Status=="RolledBack");
    node=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
    Assert(node["Theme"]!.GetValue<string>()=="Original"&&node["Unrelated"]!.GetValue<int>()==42&&node["RuntimeDefault"]!.GetValue<bool>());
    Assert(node["CheckForUpdates"]==null);
    Throws(()=>backend.Validate(FileChange("../Theme","true")));
});
Test("RetroBar modified user preference is retained on rollback", () =>
{
    string path=Path.Combine(NewState(),"settings.json");var backend=new RetroBarSettingsBackend(path);var manager=new TransactionManager(backend,NewState());
    var receipt=manager.Apply(RetroBarSettingsBackend.Plan(ClassicThemes.Get("windows-98"),NewState()));
    var node=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;node["Theme"]="User theme";File.WriteAllText(path,node.ToJsonString());
    var preserve=RetroBarSettingsBackend.Fields.Select(key=>new Change{Kind=ChangeKind.File,Target=key}.Identity).ToHashSet();
    Assert(manager.Rollback(receipt.FilePath,preserveUserChanges:preserve).Status=="RolledBack");
    Assert(System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!["Theme"]!.GetValue<string>()=="User theme");
});
Test("atomic JSON writes tolerate a short external reader lock", () =>
{
    string path=Path.Combine(NewState(),"state.json");JsonFile.Write(path,new{value=1});
    var read=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
    var release=Task.Run(async()=>{await Task.Delay(100);read.Dispose();});
    JsonFile.Write(path,new{value=2});release.GetAwaiter().GetResult();
    Assert(System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("value").GetInt32()==2);
});
Test("sound scheme allows only its named event values", () =>
{
    foreach(var path in SevenSounds.EventPaths)
    {
        Assert(SevenSounds.Allows(new(){Kind=ChangeKind.Registry,Target=path,ValueName=""}));
        Assert(!SevenSounds.Allows(new(){Kind=ChangeKind.Registry,Target=path,ValueName="foreign"}));
    }
    Assert(!SevenSounds.Allows(new(){Kind=ChangeKind.Registry,Target=@"HKCU\AppEvents\Schemes\Apps\Malware\SystemHand\.Current"}));
    Throws(()=>SevenSounds.Build(NewState()));
});
Test("sound application refuses the development host before creating state", () =>
{
    if(Environment.MachineName=="AEROFORGE-LAB")return;
    Throws(()=>new SevenSoundsController().Apply());Throws(()=>new SevenSoundsController().Restore());
});

Test("private extra installers refuse the host before creating state", () =>
{
    if(Environment.MachineName=="AEROFORGE-LAB")return;
    foreach(var component in PrivateExtras.Components)
    {
        Throws(()=>PrivateExtras.InstallAsync(component.Id).GetAwaiter().GetResult());
        Throws(()=>PrivateExtras.RemoveAsync(component.Id).GetAwaiter().GetResult());
    }
});

Test("process image inspection uses a kernel query without reading target modules", () =>
{
    Assert(string.Equals(WindowsProcessInfo.ImagePath(Environment.ProcessId),Environment.ProcessPath,StringComparison.OrdinalIgnoreCase));
    Assert(WindowsProcessInfo.ImagePath(0)==null);
    Assert(WindowsProcessInfo.ImagePath(-1)==null);
});

Test("Seven setup with missing assets refuses before creating transaction state", () =>
{
    string content=NewState();Directory.CreateDirectory(content);
    JsonFile.Write(Path.Combine(content,"catalog.json"),new Catalog{Profiles=[new Profile{Id="seven"}]});
    string root=NewState(),state=NewState();
    var setup=new SevenSetup(content,root,state,NewState());
    Assert(setup.Check().Any(c=>c.Name=="Тема Windows 7"&&!c.Ready));
    Throws(()=>setup.ApplyAsync(true).GetAwaiter().GetResult());
    Assert(!Directory.Exists(root)&&!Directory.Exists(state),"Blocked setup created transaction state");
});

Test("unsafe paths cannot escape managed root", () =>
{
    foreach (string path in new[] { "../outside", "a/../../outside", "C:\\Windows\\file", "file:stream", "a//b", "a/./b", "a./b", "a /b", "CON", "NUL.txt", "path/COM1.log", "LPT².txt", "star*file", "a?b" })
        Throws(() => SafePath.Under(scratch, path));
});
Test("directory reparse target and ancestor are rejected", () =>
{
    string path = NewState(); Directory.CreateDirectory(path);
    string target = NewState(); Directory.CreateDirectory(target);
    string link = Path.Combine(path, "link");
    try { Directory.CreateSymbolicLink(link, target); }
    catch (Exception error) when (error is UnauthorizedAccessException || error is IOException && error.HResult == unchecked((int)0x80070522))
    {
        // A directory junction exercises the same reparse-point boundary without
        // requiring Developer Mode or SeCreateSymbolicLinkPrivilege on Windows.
        if ((link + target).IndexOfAny(['"', '%', '!', '\r', '\n']) >= 0)
            throw new InvalidOperationException("Unsupported test fixture path");
        using var command = Process.Start(new ProcessStartInfo("cmd.exe", "/d /s /c \"mklink /J \"" + link + "\" \"" + target + "\"\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        string result = command.StandardOutput.ReadToEnd() + command.StandardError.ReadToEnd();
        command.WaitForExit();
        Assert(command.ExitCode == 0, "Could not create the junction fixture: " + result);
    }
    Throws(() => SafePath.Under(path, "link/file"));
    Throws(() => SafePath.Under(Path.Combine(link, "child"), "file"));
});
Test("file deployment preserves and restores original bytes", () =>
{
    string host = NewState(), state = NewState(); Directory.CreateDirectory(host);
    File.WriteAllText(Path.Combine(host, "existing"), "original");
    var manager = new TransactionManager(new WindowsBackend(host), state);
    var receipt = manager.Apply(PlanOf(FileChange("existing", "new"), FileChange("nested/new", "created")));
    Assert(File.ReadAllText(Path.Combine(host, "existing")) == "new");
    var result = manager.Rollback(receipt.FilePath);
    Assert(result.Status == "RolledBack"); Assert(File.ReadAllText(Path.Combine(host, "existing")) == "original");
    Assert(!File.Exists(Path.Combine(host, "nested/new"))); Assert(!Directory.Exists(Path.Combine(host, "nested")));
});
Test("failure after mutation rolls back the failing step too", () =>
{
    var backend = new MemoryBackend { FailAfterWrite = "b" }; string state = NewState();
    var manager = new TransactionManager(backend, state);
    try { manager.Apply(PlanOf(FileChange("a", "1"), FileChange("b", "2"))); throw new Exception("Expected failure"); }
    catch (ApplyException ex) { Assert(ex.Receipt.Status == "RolledBack"); }
    Assert(backend.Values.Count == 0);
});
Test("rollback preserves third-party changes and records conflicts", () =>
{
    var backend = new MemoryBackend(); var manager = new TransactionManager(backend, NewState());
    var receipt = manager.Apply(PlanOf(FileChange("a", "managed")));
    backend.Write(FileChange("a", ""), new ValueState { Exists = true, Kind = "File", Bytes = Encoding.UTF8.GetBytes("external") });
    var result = manager.Rollback(receipt.FilePath);
    Assert(result.Status == "RollbackIncomplete"); Assert(Encoding.UTF8.GetString(backend.Values["a"].Bytes!) == "external");
    Throws(() => manager.Apply(PlanOf(FileChange("b", "new"))));
});
Test("crash between write and journal acknowledgement is recoverable", () =>
{
    var backend = new MemoryBackend(); string state = NewState(); Directory.CreateDirectory(state);
    var change = FileChange("a", "written"); backend.Write(change, ValueState.Desired(change));
    var receipt = new Receipt { Status = "Applying", Entries = [new JournalEntry { Change = change, Before = new(), ExpectedAfter = ValueState.Desired(change).Fingerprint }] };
    string path = Path.Combine(state, receipt.Id + ".json"); JsonFile.Write(path, receipt);
    var manager = new TransactionManager(backend, state);
    Assert(manager.Rollback(path).Status == "RolledBack"); Assert(backend.Values.Count == 0);
});
Test("cancellation after first change restores state", () =>
{
    var token = new CancellationTokenSource(); var backend = new MemoryBackend { OnWrite = () => token.Cancel() };
    var manager = new TransactionManager(backend, NewState());
    try { manager.Apply(PlanOf(FileChange("a", "a"), FileChange("b", "b")), cancellationToken: token.Token); throw new Exception("Expected cancellation"); }
    catch (ApplyException ex) { Assert(ex.Receipt.Status == "RolledBack"); }
    Assert(backend.Values.Count == 0);
});
Test("layered profiles must be rolled back newest first", () =>
{
    var backend = new MemoryBackend(); var manager = new TransactionManager(backend, NewState());
    var first = manager.Apply(PlanOf(FileChange("a", "first")));
    Thread.Sleep(3);
    var second = manager.Apply(PlanOf(FileChange("a", "second")));
    Throws(() => manager.Rollback(first.FilePath)); manager.Rollback(second.FilePath);
    Assert(Encoding.UTF8.GetString(backend.Values["a"].Bytes!) == "first");
    manager.Rollback(first.FilePath); Assert(backend.Values.Count == 0);
});
Test("duplicate writes and blocked plans cannot be applied", () =>
{
    var manager = new TransactionManager(new MemoryBackend(), NewState());
    Throws(() => manager.Apply(PlanOf(FileChange("a", "1"), FileChange("A", "2"))));
    var plan = PlanOf(FileChange("a", "1")); plan.Blockers.Add("blocked"); Throws(() => manager.Apply(plan));
});
Test("unknown HKLM and foreign startup registry targets are rejected", () =>
{
    var backend = new WindowsBackend(NewState());
    Throws(() => backend.Validate(new Change { Kind = ChangeKind.Registry, Target = "HKLM\\SOFTWARE\\Windows" }));
    Throws(() => backend.Validate(new Change { Kind = ChangeKind.Registry, Target = "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run", ValueName = "OtherApp" }));
});
Test("corrupted content cannot enter deployment plan", () =>
{
    string content = NewState(); Directory.CreateDirectory(content); File.WriteAllText(Path.Combine(content, "file"), "tampered");
    var store = new ContentStore(content);
    Throws(() => store.ReadVerified(new ContentFile { Path = "file", Sha256 = Hashing.Text("original") }));
    Throws(() => store.ReadVerified(new ContentFile { Path = "../file", Sha256 = new string('a', 64) }));
});
Test("plan rejects conflicting and critical modules", () =>
{
    var catalog = new Catalog { Profiles = [new Profile { Id = "test", Modules = ["a", "b"] }], Modules =
        [new Module { Id = "a", Name = "a", Status = "built", CriticalProcess = true, Conflicts = ["b"] }, new Module { Id = "b", Name = "b" }] };
    var plan = new ProfilePlanner(new ContentStore(NewState()), catalog).Build("test");
    Assert(plan.Blockers.Any(s => s.Contains("VM"))); Assert(plan.Blockers.Any(s => s.Contains("конфликтует")));
});
Test("registry types and absent values are restored exactly", () =>
{
    var backend = new MemoryBackend(); var change = new Change { Kind = ChangeKind.Registry, Target = "key", ValueName = "name", ValueKind = "String", Value = "new" };
    backend.Values["key|name"] = new() { Exists = true, Kind = "ExpandString", Value = "%USERPROFILE%" };
    var manager = new TransactionManager(backend, NewState()); var receipt = manager.Apply(PlanOf(change)); manager.Rollback(receipt.FilePath);
    Assert(backend.Values["key|name"].Kind == "ExpandString" && backend.Values["key|name"].Value == "%USERPROFILE%");
});
Test("concurrent writer cannot enter the transaction directory", () =>
{
    string state = NewState(); Directory.CreateDirectory(state);
    using var file = new FileStream(Path.Combine(state, "transaction.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    Throws(() => new TransactionManager(new MemoryBackend(), state).Apply(PlanOf(FileChange("a", "a"))));
});
Test("mines first click and its neighbors are safe across seeds", () =>
{
    for (int seed = 0; seed < 100; seed++)
    {
        var game = new MinesGame(seed: seed); game.Reveal(4, 4);
        Assert(!game.Lost); Assert(game.Cells.Cast<MineCell>().Count(c => c.Mine) == 10);
        for (int r = 3; r <= 5; r++) for (int c = 3; c <= 5; c++) Assert(!game.Cells[r, c].Mine);
    }
});
Test("mines flags protect cells and victory is detected", () =>
{
    var game = new MinesGame(seed: 42); game.Flag(4, 4); game.Reveal(4, 4); Assert(!game.Started);
    game.Flag(4, 4); game.Reveal(4, 4);
    for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++) if (!game.Cells[r, c].Mine && !game.Cells[r, c].Revealed) game.Reveal(r, c);
    Assert(game.Won && !game.Lost);
});
Test("mines loss ends the board and blocks later reveals", () =>
{
    var game = new MinesGame(seed: 3); game.Reveal(0, 0);
    var mine = Enumerable.Range(0, 81).First(i => game.Cells[i / 9, i % 9].Mine);
    game.Reveal(mine / 9, mine % 9); Assert(game.Lost);
    int before = game.Cells.Cast<MineCell>().Count(c => c.Revealed);
    game.Reveal(8, 8); Assert(game.Cells.Cast<MineCell>().Count(c => c.Revealed) == before);
});
Test("INI line injection is rejected", () =>
{
    var module = new Module { Id = "test", DefaultSettings = new() { ["key"] = "value\r\n[Mod]\r\nDisabled=0" } };
    Throws(() => ProfilePlanner.Ini(module, false, new Profile()));
});
Test("classic frame styling reaches the UI but excludes recovery and critical processes",()=>
{
    string ini=ProfilePlanner.Ini(new Module{Id="winmim-classic-frames",Includes=["*"]},true,new Profile());
    var excluded=ini.Split("\r\n").Single(s=>s.StartsWith("Exclude=")).Substring(8).Split('|');
    Assert(!excluded.Contains("WinMim.exe")&&!excluded.Contains("WinMim.UiProbe.exe"));
    foreach(string name in new[]{"AeroForge.Cli.exe","AeroForge.VmSmoke.exe","dwm.exe","winlogon.exe","consent.exe","vmtoolsd.exe"})Assert(excluded.Contains(name),name);
});
Test("unverified original icon packs cannot produce a desktop plan",()=>
{
    string content=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../content"));
    string icons=NewState();Directory.CreateDirectory(icons);byte[] fake=new byte[22];fake[2]=1;fake[4]=1;
    File.WriteAllBytes(Path.Combine(icons,"computer.ico"),fake);
    Throws(()=>Classic95Profile.Build(content,NewState(),ClassicAppearance.Read(),icons));
});
Test("journal error details skip binary backups and reject outside paths",()=>
{
    string state=NewState();Directory.CreateDirectory(state);string file=Path.Combine(state,"fixture.json");
    File.WriteAllText(file,"{\"Entries\":[{\"Before\":{\"Bytes\":\"deliberately invalid base64\"},\"Error\":\"locked module\"},{\"Error\":null}]}");
    var manager=new TransactionManager(new MemoryBackend(),state);
    Assert(manager.ReadErrors(file).SequenceEqual(new[]{"locked module"}));
    Throws(()=>manager.ReadErrors(Path.Combine(state,"..","fixture.json")));
});
Test("rollback retires a mapped native DLL and preserves later edits during cleanup", () =>
{
    string root=NewState();var backend=new WindowsBackend(root);
    var manager=new TransactionManager(backend,NewState());
    var change=new Change{Kind=ChangeKind.File,Target="Data/Engine/Mods/64/mapped-fixture.dll",Content=File.ReadAllBytes(Path.Combine(Environment.SystemDirectory,"version.dll"))};
    var receipt=manager.Apply(PlanOf(change));
    nint loaded=System.Runtime.InteropServices.NativeLibrary.Load(SafePath.Under(root,change.Target));
    string retired="",foreign="";
    try
    {
        Assert(manager.Rollback(receipt.FilePath).Status=="RolledBack");
        Assert(!backend.Read(change).Exists);
        retired=Directory.GetFiles(Path.Combine(root,"Data/Engine/Retired"),"*.dll").Single();
        backend.CleanupRetiredModules();Assert(File.Exists(retired));
        foreign=Path.Combine(Path.GetDirectoryName(retired)!,Path.GetFileName(retired)[..65]+Guid.NewGuid().ToString("N")+".dll");
        File.WriteAllBytes(foreign,[1,2,3]);
    }
    finally{System.Runtime.InteropServices.NativeLibrary.Free(loaded);}
    backend.CleanupRetiredModules();
    Assert(!File.Exists(retired)&&File.Exists(foreign));
});
Test("wildcard theme modules cannot target guest recovery services", () =>
{
    var module=new Module{Id="classic-theme-transparency-fix",Includes=["*"],Excludes=["foreign.exe","VMTOOLSD.EXE"]};
    string ini=ProfilePlanner.Ini(module,true,new Profile());
    string[] excluded=ini.Split("\r\n").Single(line=>line.StartsWith("Exclude=")).Substring(8).Split('|');
    foreach(string name in new[]{"vmtoolsd.exe","vm3dservice.exe","VGAuthService.exe","vmwaretray.exe","vmwareuser.exe","WinMim.exe","foreign.exe"})
        Assert(excluded.Contains(name,StringComparer.OrdinalIgnoreCase),name);
    Assert(excluded.Count(name=>name.Equals("vmtoolsd.exe",StringComparison.OrdinalIgnoreCase))==1);
    Assert(ini.Contains("Include=*\r\n")&&!excluded.Contains("explorer.exe"));
});
Test("autostart can be explicitly disabled without affecting other values", () =>
{
    var catalog = new Catalog { Profiles = [new Profile { Id = "test" }] };
    var plan = new ProfilePlanner(new ContentStore(NewState()), catalog).Build("test", autostart: false);
    var startup = plan.Changes.Single(c => c.Kind == ChangeKind.Registry);
    Assert(startup.ValueName == "AeroForge" && startup.Value == null);
});
Test("default developer plans do not mutate the user's registry", () =>
{
    var catalog = new Catalog { Profiles = [new Profile { Id = "test" }] };
    var plan = new ProfilePlanner(new ContentStore(NewState()), catalog).Build("test");
    Assert(plan.Changes.All(c => c.Kind == ChangeKind.File));
});
Test("exclusive desktop application is idempotent and preserves its original undo snapshot", () =>
{
    var backend=new MemoryBackend();var manager=new TransactionManager(backend,NewState());
    var plan=PlanOf(FileChange("owned","seven"));plan.Exclusive=true;
    backend.Write(FileChange("owned",""),new(){Exists=true,Kind="File",Bytes=Encoding.UTF8.GetBytes("before")});
    var first=manager.Apply(plan);var again=manager.Apply(plan);
    Assert(first.Id==again.Id);Assert(manager.History().Count==1);
    manager.Rollback(first.FilePath);
    Assert(Encoding.UTF8.GetString(backend.Values["owned"].Bytes!)=="before");
    var next=manager.Apply(plan);Assert(next.Id!=first.Id);
    manager.Rollback(next.FilePath);Assert(Encoding.UTF8.GetString(backend.Values["owned"].Bytes!)=="before");
});
Test("shortcut resources are reproducible across repeat plans", () =>
{
    string target=Path.Combine(scratch,"shortcut-target.exe");File.WriteAllBytes(target,[1,2,3]);
    var first=DesktopShortcuts.Create(target,"--mines");var second=DesktopShortcuts.Create(target,"--mines");
    Assert(first.SequenceEqual(second),"Shortcut metadata changes between identical plans");
});
Test("owned profile upgrades keep the original backup and can undo just the update", () =>
{
    var backend=new MemoryBackend();var manager=new TransactionManager(backend,NewState());
    backend.Write(FileChange("owned",""),new(){Exists=true,Kind="File",Bytes=Encoding.UTF8.GetBytes("original")});
    var firstPlan=PlanOf(FileChange("owned","old look"));firstPlan.Exclusive=true;
    var first=manager.Apply(firstPlan);
    var update=PlanOf(FileChange("owned","new look"),FileChange("new asset","new"));update.Exclusive=true;
    var second=manager.Apply(update,allowProfileUpdate:true);
    Assert(second.Id!=first.Id&&manager.History().Count==2);
    Assert(manager.Apply(update,allowProfileUpdate:true).Id==second.Id);
    Assert(manager.Rollback(second.FilePath).Status=="RolledBack");
    Assert(Encoding.UTF8.GetString(backend.Values["owned"].Bytes!)=="old look");
    Assert(manager.Rollback(first.FilePath).Status=="RolledBack");
    Assert(Encoding.UTF8.GetString(backend.Values["owned"].Bytes!)=="original");
});
Test("owned profile upgrades refuse foreign modifications", () =>
{
    var backend=new MemoryBackend();var manager=new TransactionManager(backend,NewState());
    var first=PlanOf(FileChange("owned","old"));first.Exclusive=true;manager.Apply(first);
    backend.Write(FileChange("owned",""),new(){Exists=true,Kind="File",Bytes=Encoding.UTF8.GetBytes("external")});
    var update=PlanOf(FileChange("owned","new"));update.Exclusive=true;
    Throws(()=>manager.Apply(update,allowProfileUpdate:true));
    Assert(manager.History().Count==1);
    Assert(Encoding.UTF8.GetString(backend.Values["owned"].Bytes!)=="external");
});
Test("unchanged resources are verified without rewriting locked files", () =>
{
    var backend=new MemoryBackend();var manager=new TransactionManager(backend,NewState());
    var file=FileChange("loaded.dll","same bytes");
    backend.Write(file,ValueState.Desired(file));
    backend.OnWrite=()=>throw new IOException("Loaded DLL cannot be replaced");
    var receipt=manager.Apply(PlanOf(file));
    Assert(receipt.Status=="Applied");
    Assert(receipt.Entries.Count==0,"Unchanged non-exclusive files must not duplicate DLL backups");
    Assert(manager.Rollback(receipt.FilePath).Status=="RolledBack");
});
Test("private icon resources require their pinned hash and no PE entry point", () =>
{
    string root=NewState();Directory.CreateDirectory(root);
    byte[] pe=new byte[128];pe[0]=(byte)'M';pe[1]=(byte)'Z';pe[60]=64;pe[64]=(byte)'P';pe[65]=(byte)'E';
    string file=Path.Combine(root,"imageres.dll");File.WriteAllBytes(file,pe);
    var pack=new PrivateIconPack{Source="test",Files=[new PrivateIconFile{FileName="imageres.dll",Sha256=Hashing.Bytes(pe)}]};
    JsonFile.Write(Path.Combine(root,"manifest.json"),pack);
    var settings=PrivateIconPack.Settings(root);
    Assert(settings["redirectionResourcePaths[0].redirect"]==file);
    Assert(settings["allResourceRedirect"]=="0");
    pe[104]=1;File.WriteAllBytes(file,pe);
    Throws(()=>PrivateIconPack.Settings(root));
    pack.Files[0].Sha256=Hashing.Bytes(pe);JsonFile.Write(Path.Combine(root,"manifest.json"),pack);
    Throws(()=>PrivateIconPack.Settings(root));
});
Test("private icon packs reject unsupported libraries and traversal", () =>
{
    string root=NewState();Directory.CreateDirectory(root);
    foreach(string name in new[]{"../imageres.dll","user32.dll"})
    {
        JsonFile.Write(Path.Combine(root,"manifest.json"),new PrivateIconPack{Files=[new PrivateIconFile{FileName=name}]});
        Throws(()=>PrivateIconPack.Settings(root));
    }
});
Test("expanded private icon packs map executable, CPL, branding and 32-bit resource requests",()=>
{
    string root=NewState();Directory.CreateDirectory(root);
    byte[] pe=new byte[128];pe[0]=(byte)'M';pe[1]=(byte)'Z';pe[60]=64;pe[64]=(byte)'P';pe[65]=(byte)'E';
    var pack=new PrivateIconPack();
    foreach(string name in new[]{"explorer.exe.dll","appwiz.cpl.dll","basebrd.dll","imageres.dll"})
    {File.WriteAllBytes(Path.Combine(root,name),pe);pack.Files.Add(new(){FileName=name,Sha256=Hashing.Bytes(pe)});}
    JsonFile.Write(Path.Combine(root,"manifest.json"),pack);
    var settings=PrivateIconPack.Settings(root);
    var originals=settings.Where(p=>p.Key.EndsWith(".original")).Select(p=>p.Value).ToArray();
    Assert(originals.Contains(@"%SystemRoot%\explorer.exe"));
    Assert(originals.Contains(@"%SystemRoot%\System32\appwiz.cpl"));
    Assert(originals.Contains(@"%SystemRoot%\Branding\Basebrd\basebrd.dll"));
    Assert(originals.Contains(@"%SystemRoot%\SysWOW64\imageres.dll"));
    Assert(!originals.Any(p=>p.EndsWith(".exe.dll")||p.EndsWith(".cpl.dll")));
    string ini=ProfilePlanner.Ini(new Module{Id="icon-resource-redirect",Includes=["*"]},true,new Profile{Settings=new(){{"icon-resource-redirect",settings}}});
    Assert(ini.Contains("Include=*\r\n"),"Resources must also reach other user applications");
});
Test("desktop icon paths use validated private resources and restore the user's original registry value",()=>
{
    string root=NewState();Directory.CreateDirectory(root);
    byte[] pe=new byte[128];pe[0]=(byte)'M';pe[1]=(byte)'Z';pe[60]=64;pe[64]=(byte)'P';pe[65]=(byte)'E';
    File.WriteAllBytes(Path.Combine(root,"imageres.dll"),pe);
    JsonFile.Write(Path.Combine(root,"manifest.json"),new PrivateIconPack{Files=[new(){FileName="imageres.dll",Sha256=Hashing.Bytes(pe)}]});
    string computer=@"CLSID\{20D04FE0-3AEA-1069-A2D8-08002B30309D}\DefaultIcon";
    var changes=DesktopIconOverrides.Build(root,(path,name)=>path==computer && name==""?@"%SystemRoot%\System32\imageres.dll,-109":null);
    Assert(changes.Length==2 && changes.All(c=>c.Value!.EndsWith("imageres.dll,-109")));
    Assert(changes.All(DesktopRegistryPolicy.Allows));
    Assert(changes.Any(c=>c.Target.StartsWith(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\")));
    Assert(!DesktopRegistryPolicy.Allows(new(){Kind=ChangeKind.Registry,Target=@"HKLM\Software\Classes\"+computer}));
    var backend=new MemoryBackend();backend.Write(changes[0],new(){Exists=true,Kind="ExpandString",Value="user-icon-before"});
    var manager=new TransactionManager(backend,NewState());var receipt=manager.Apply(PlanOf(changes));manager.Rollback(receipt.FilePath);
    Assert(backend.Read(changes[0]).Kind=="ExpandString" && backend.Read(changes[0]).Value=="user-icon-before");
});
Test("exclusive desktop profile refuses to overwrite later external changes", () =>
{
    var backend=new MemoryBackend();var manager=new TransactionManager(backend,NewState());
    var plan=PlanOf(FileChange("owned","seven"));plan.Exclusive=true;var receipt=manager.Apply(plan);
    backend.Write(FileChange("owned",""),new(){Exists=true,Kind="File",Bytes=Encoding.UTF8.GetBytes("external")});
    Throws(()=>manager.Apply(plan));Assert(manager.History().Count==1);
    Assert(manager.Rollback(receipt.FilePath).Status=="RollbackIncomplete");
    Assert(Encoding.UTF8.GetString(backend.Values["owned"].Bytes!)=="external");
});
Test("desktop registry permission is an explicit value allowlist", () =>
{
    var good=new Change{Kind=ChangeKind.Registry,Target=DesktopRegistryPolicy.Dwm,ValueName="CaptionButtons",ValueKind="DWord",Value="2"};
    Throws(()=>new WindowsBackend(NewState()).Validate(good));
    new DesktopBackend(NewState()).Validate(good);
    foreach(var bad in new[]{new Change{Kind=ChangeKind.Registry,Target=DesktopRegistryPolicy.Dwm,ValueName="Foreign"},new Change{Kind=ChangeKind.Registry,Target=DesktopRegistryPolicy.Shell,ValueName="Unrelated"},new Change{Kind=ChangeKind.Registry,Target=@"HKLM\Software\Microsoft\Windows\DWM",ValueName="CaptionButtons"}})
        Throws(()=>new DesktopBackend(NewState()).Validate(bad));
});
Test("native appearance operations participate in failed apply rollback", () =>
{
    var backend=new MemoryBackend();var manager=new TransactionManager(backend,NewState());
    var style=new Change{Kind=ChangeKind.VisualStyle,Target="Windows.VisualStyle",ValueKind="VisualStyle",Value="seven"};
    backend.Write(style,new(){Exists=true,Kind="VisualStyle",Value="previous"});
    backend.FailAfterWrite="Windows.Wallpaper";
    Throws(()=>manager.Apply(PlanOf(style,new Change{Kind=ChangeKind.Wallpaper,Target="Windows.Wallpaper",ValueKind="Wallpaper",Value="new-wallpaper"})));
    Assert(backend.Values["Windows.VisualStyle|"].Value=="previous");
    Assert(!backend.Values.ContainsKey("Windows.Wallpaper|"));
});
Test("taskbar preferences reject invalid grouping before a journal is created",()=>
{
    Throws(()=>new TaskbarPreferences(Combine:-1).Changes().ToArray());
    Throws(()=>new TaskbarPreferences(Combine:3).Changes().ToArray());
});
Test("native taskbar customization is adopted while theme conflicts stay protected",()=>
{
    var backend=new MemoryBackend();var manager=new TransactionManager(backend,NewState());
    var setting=new TaskbarPreferences().Changes().Single(c=>c.ValueName=="TaskbarSmallIcons");
    var original=PlanOf(setting,FileChange("theme","seven"));original.Exclusive=true;
    var first=manager.Apply(original);
    backend.Write(setting,new(){Exists=true,Kind="DWord",Value="1"});
    var changed=new TaskbarPreferences(SmallIcons:true).Changes().Single(c=>c.ValueName=="TaskbarSmallIcons");
    var update=PlanOf(changed,FileChange("theme","updated"));update.Exclusive=true;
    update.AdoptUserPreferences=TaskbarPreferences.UserSettingIdentities.ToHashSet();
    var second=manager.Apply(update,allowProfileUpdate:true);
    manager.Rollback(second.FilePath,preserveUserChanges:TaskbarPreferences.UserSettingIdentities);
    var undo=manager.Rollback(first.FilePath,preserveUserChanges:TaskbarPreferences.UserSettingIdentities);
    Assert(undo.Status=="RolledBack"&&undo.Entries.Any(e=>e.State=="Preserved"));
    Assert(backend.Read(setting).Value=="1");Assert(!backend.Values.ContainsKey("theme"));
});
Test("untrusted display providers are rejected without staging files",()=>
{
    string file=Path.Combine(NewState(),"display.dll");Directory.CreateDirectory(Path.GetDirectoryName(file)!);
    File.WriteAllBytes(file,[77,90,0,0]);Throws(()=>ExternalDisplayProvider.Read(file));
});
Test("machine settings adapter refuses the development host before touching HKLM",()=>
{
    if(Environment.MachineName=="AEROFORGE-LAB")return;
    Throws(()=>new MachineSeven().Apply());
    Throws(()=>new MachineSeven().Restore());
    Throws(()=>MachineSeven.Inspect());
});
Test("metadata history preserves ordering and status while full receipts still restore bytes",()=>
{
    var backend=new MemoryBackend();var manager=new TransactionManager(backend,NewState());
    var first=manager.Apply(PlanOf(FileChange("a","original")));
    var second=manager.Apply(PlanOf(FileChange("a","updated")));
    var headers=manager.History(false);var full=manager.History();
    Assert(headers.Select(r=>r.Id).SequenceEqual(full.Select(r=>r.Id)));
    Assert(headers.All(r=>r.Entries.Count==0));
    manager.Rollback(second.FilePath);
    Assert(Encoding.UTF8.GetString(backend.Values["a"].Bytes!)=="original");
    Assert(manager.History(false).Single(r=>r.Id==second.Id).Status=="RolledBack");
    manager.Rollback(first.FilePath);Assert(!backend.Values.ContainsKey("a"));
});
Test("a partial preferences update retains conflict protection for older appearance settings",()=>
{
    var backend=new MemoryBackend();var manager=new TransactionManager(backend,NewState());
    var original=PlanOf(FileChange("taskbar","default"),FileChange("menu","default"));original.Exclusive=true;
    manager.Apply(original);
    var preferences=PlanOf(FileChange("taskbar","custom"));preferences.Exclusive=true;
    manager.Apply(preferences,allowProfileUpdate:true);
    backend.Write(FileChange("menu",""),new(){Exists=true,Kind="File",Bytes=Encoding.UTF8.GetBytes("foreign")});
    Throws(()=>manager.Apply(original,allowProfileUpdate:true));
    Assert(Encoding.UTF8.GetString(backend.Values["menu"].Bytes!)=="foreign");
});
Test("taskbar changes restore both appbar state and saved preferences after failure",()=>
{
    var backend=new MemoryBackend();var manager=new TransactionManager(backend,NewState());
    var state=new Change{Kind=ChangeKind.TaskbarState,Target="Windows.TaskbarState"};
    backend.Write(state,new(){Exists=true,Kind="TaskbarState",Value="0"});
    backend.FailAfterWrite="Windows.TaskbarState";
    Throws(()=>manager.Apply(PlanOf(new TaskbarPreferences(AutoHide:true,Combine:2).Changes().ToArray())));
    Assert(backend.Read(state).Value=="0");
    Assert(!backend.Values.ContainsKey(TaskbarPreferences.SavedPath));
    Assert(manager.History().Single().Status=="RolledBack");
});
Test("saved taskbar customization survives rebuilding the Seven desktop plan",()=>
{
    string root=NewState();Directory.CreateDirectory(root);
    string style=Path.Combine(root,"test.msstyles"),wallpaper=Path.Combine(root,"test.png");
    File.WriteAllBytes(style,[0]);File.WriteAllBytes(wallpaper,[0]);
    var preferences=new TaskbarPreferences(Locked:false,AutoHide:true,SmallIcons:true,Combine:2,Peek:false);
    JsonFile.Write(SafePath.Under(root,TaskbarPreferences.SavedPath),preferences);
    var plan=DesktopProfile.BuildSeven(root,style,wallpaper);
    Assert(plan.Changes.Single(c=>c.ValueName=="TaskbarGlomLevel").Value=="2");
    Assert(plan.Changes.Single(c=>c.Kind==ChangeKind.TaskbarState).Value=="1");
    Assert(plan.Changes.Select(c=>c.Identity).Distinct().Count()==plan.Changes.Count);
});
Test("runtime status reads live delete-on-close UTF16 metadata and distinguishes loading from loaded",()=>
{
    string root=NewState(),folder=Path.Combine(root,"Data","Engine","ModsWritable","mod-status");Directory.CreateDirectory(folder);
    string path=Path.Combine(folder,"100_123456_200_local@aeroforge-aerexplorer");
    using(var writer=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.Read,4096,FileOptions.DeleteOnClose))
    {
        writer.Write(Encoding.Unicode.GetBytes("explorer.exe|Loading..."));writer.Flush();
        var host=new ShellHost(root,new Catalog());
        Assert(host.ExplorerStates() is [{ProcessId:200,Status:"Loading...",ModuleId:"local@aeroforge-aerexplorer"}]);
        writer.SetLength(0);writer.Position=0;writer.Write(Encoding.Unicode.GetBytes("Explorer.EXE|Loaded"));writer.Flush();
        Assert(host.ExplorerStates().Single().Status=="Loaded");
    }
    Assert(new ShellHost(root,new Catalog()).ExplorerStates().Length==0);
});
Console.WriteLine($"{passed} passed, {failed} failed. Scratch: {scratch}");
return failed == 0 ? 0 : 1;

sealed class MemoryBackend : IChangeBackend
{
    public Dictionary<string, ValueState> Values { get; } = [];
    public string? FailAfterWrite { get; set; }
    public Action? OnWrite { get; set; }
    private string Key(Change c) => c.Kind == ChangeKind.File ? c.Target : c.Kind==ChangeKind.RegistryKey?"key:"+c.Target:c.Target + "|" + c.ValueName;
    public void Validate(Change change) { }
    public ValueState Read(Change c) => Values.GetValueOrDefault(Key(c)) ?? new();
    public void Write(Change c, ValueState s)
    {
        if (s.Exists) Values[Key(c)] = s; else Values.Remove(Key(c));
        OnWrite?.Invoke();
        if (c.Target == FailAfterWrite) { FailAfterWrite = null; throw new IOException("Injected failure after mutation"); }
    }
}
