using AeroForge.Core;
using System.Diagnostics;
using System.IO;
using F = System.Windows.Forms;
using D = System.Drawing;

namespace AeroForge.App;

// No owner drawing, custom palette, control templates or theme previews:
// Windows renders the controls using the active system visual style.
public sealed class NativeMainForm : F.Form
{
    private readonly ContentStore store = new(MainWindow.FindContent());
    private readonly Catalog catalog;
    private readonly ShellHost host;
    private readonly TransactionManager transactions;
    private readonly DesktopController desktop = new();
    private readonly SevenSetup seven = new(MainWindow.FindContent());
    private readonly F.TabControl tabs = new() { Dock = F.DockStyle.Fill };
    private readonly F.ToolStripStatusLabel status = new() { Spring=true, TextAlign=D.ContentAlignment.MiddleLeft };
    private readonly F.ListView targets=List("Оформление",260,"Семейство",180,"Состояние",270);
    private readonly F.Label targetDetail=Label("Выберите оформление. Windows 95 доступна на тестовом стенде; остальные пункты — заглушки.");
    private readonly F.ListView modules=List("Модуль",280,"Категория",150,"Версия",90,"Статус",200);
    private readonly F.ListView planItems=List("Действие",380,"Назначение",430);
    private readonly F.ListView history=List("Время",175,"Профиль",250,"Состояние",190,"Журнал",230);
    private readonly F.TextBox detail=new(){Multiline=true,ReadOnly=true,ScrollBars=F.ScrollBars.Vertical,Dock=F.DockStyle.Bottom,Height=90};
    private readonly F.CheckBox autostart=Check("Запускать движок при входе в Windows");
    private readonly F.CheckBox consent=Check("Я ознакомился с планом экспериментальной сборки");
    private readonly F.CheckBox restart=Check("Перезапустить Проводник (окна папок будут закрыты)",true);
    private readonly F.Button apply=Button("Подготовить профиль");
    private readonly F.Button applySeven=Button("Применить Windows 7 в тестовой ВМ");
    private readonly F.ListView checks=List("Проверка",180,"Состояние",95,"Подробности",540);
    private readonly Dictionary<string,F.CheckBox> extras=[];
    private readonly Dictionary<string,string> downloads=[];
    private readonly F.ComboBox profiles=new(){DropDownStyle=F.ComboBoxStyle.DropDownList,Width=240};
    private readonly F.Button cancel=Button("Отменить операцию");
    private Profile profile;
    private Plan? plan;
    private CancellationTokenSource? operation;
    private bool busy;
    private string query="";
    private readonly HashSet<string> selected=[];

    public NativeMainForm()
    {
        Text="WinMim — Windows Mimicry";Icon=D.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)??D.SystemIcons.Application;AutoScaleMode=F.AutoScaleMode.Font;
        Font=D.SystemFonts.MessageBoxFont;StartPosition=F.FormStartPosition.CenterScreen;
        Size=new(960,720);MinimumSize=new(720,540);
        var area=F.Screen.PrimaryScreen!.WorkingArea;Size=new(Math.Min(Width,area.Width),Math.Min(Height,area.Height));
        catalog=store.Load();profile=catalog.Profiles.First(p=>p.Id=="seven");
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForge");
        host=new(Path.Combine(root,"Host"),catalog);
        transactions=new(new WindowsBackend(Path.Combine(root,"Host")),Path.Combine(root,"State"));
        var strip=new F.StatusStrip();strip.Items.Add(status);Controls.Add(tabs);Controls.Add(cancel);Controls.Add(strip);cancel.Dock=F.DockStyle.Bottom;cancel.Visible=false;
        status.Text="Готово. Выбор оформления в каталоге не меняет систему.";
        TargetPage();SevenPage();ModulePage();PlanPage();RecoveryPage();ComponentPage();ToolPage();AboutPage();
        SelectProfile(profile);RefreshHistory();
        FormClosing+=(_,e)=>{if(busy){e.Cancel=true;status.Text="Дождитесь завершения операции.";}};
        Microsoft.Win32.SystemEvents.UserPreferenceChanged+=PreferencesChanged;
        FormClosed+=(_,_)=>Microsoft.Win32.SystemEvents.UserPreferenceChanged-=PreferencesChanged;
    }
    private void PreferencesChanged(object sender,Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {if(!IsDisposed && IsHandleCreated)BeginInvoke(()=>{Font=D.SystemFonts.MessageBoxFont;PerformLayout();Refresh();});}
    public void SelectPage(int index)=>tabs.SelectedIndex=Math.Clamp(index,0,tabs.TabCount-1);
    private F.TabPage Page(string caption)
    {var page=new F.TabPage(caption){Padding=new F.Padding(12),UseVisualStyleBackColor=true};tabs.TabPages.Add(page);return page;}
    private static F.Label Label(string text)=>new(){Text=text,AutoSize=true,MaximumSize=new D.Size(780,0),Margin=new F.Padding(3,6,3,9),BackColor=D.Color.Transparent};
    private static F.CheckBox Check(string text,bool value=false)=>new(){Text=text,AutoSize=true,Checked=value,FlatStyle=F.FlatStyle.System,Margin=new F.Padding(3,7,3,7)};
    private static F.Button Button(string text)=>new(){Text=text,AutoSize=true,AutoSizeMode=F.AutoSizeMode.GrowAndShrink,Padding=new F.Padding(8,3,8,3),Margin=new F.Padding(3,5,6,5),FlatStyle=F.FlatStyle.System};
    private static F.FlowLayoutPanel Flow(bool vertical=false)=>new(){AutoSize=true,AutoSizeMode=F.AutoSizeMode.GrowAndShrink,BackColor=D.Color.Transparent,Dock=F.DockStyle.Top,FlowDirection=vertical?F.FlowDirection.TopDown:F.FlowDirection.LeftToRight,WrapContents=!vertical};
    private static F.ListView List(params object[] columns)
    {var list=new F.ListView{Dock=F.DockStyle.Fill,View=F.View.Details,FullRowSelect=true,HideSelection=false,MultiSelect=false};for(int i=0;i<columns.Length;i+=2)list.Columns.Add((string)columns[i],(int)columns[i+1]);return list;}
    private static void Row(F.ListView list,object? tag,params string[] values)=>list.Items.Add(new F.ListViewItem(values){Tag=tag});
    private void TargetPage()
    {
        var page=Page("Оформления");var top=Flow(true);
        top.Controls.Add(Label("Во что превратить Windows"));
        top.Controls.Add(Label("Классические оформления и XP доступны для применения в подготовленной Windows 10 LTSC 2021. Это первые рабочие версии: часть ресурсов и системных окон ещё отличается от оригинала."));
        top.Controls.Add(Label("Рабочий профиль Windows 7 для подготовленной ВМ вынесен на отдельную вкладку «Windows 7 (стенд)»."));
        top.Controls.Add(Label("XP: панель и меню Luna, пока классические рамки. Классические профили используют общий набор значков; оригинальные ресурсы каждой версии будут уточняться отдельно."));
        var bottom=Flow(true);bottom.Dock=F.DockStyle.Bottom;bottom.Controls.Add(targetDetail);
        var action=Button("Выбрать оформление");bottom.Controls.Add(action);
        page.Controls.Add(targets);page.Controls.Add(bottom);page.Controls.Add(top);
        var apply95=Button("Применить Windows 95");var restore95=Button("Вернуть предыдущее оформление");bottom.Controls.Add(apply95);bottom.Controls.Add(restore95);
        apply95.Enabled=false;restore95.Enabled=Environment.MachineName=="AEROFORGE-LAB"&&Classic95Setup.IsActive;
        apply95.Click+=async(_,_)=>{if(targets.SelectedItems.Count==0)return;var target=(AppearanceTarget)targets.SelectedItems[0].Tag!;await Run(async(token,p)=>{await Task.Run(()=>new Classic95Setup(MainWindow.FindContent()).ApplyThemeAsync(target.Id,p,token));status.Text=target.Name+" применена. Для обновления уже открытых окон перезапустите их.";});restore95.Enabled=Environment.MachineName=="AEROFORGE-LAB"&&Classic95Setup.IsActive;};
        restore95.Click+=async(_,_)=>{await Run(async(_,p)=>{await Task.Run(()=>new Classic95Setup(MainWindow.FindContent()).RestoreAsync(p));status.Text="Предыдущее оформление возвращено.";});restore95.Enabled=Environment.MachineName=="AEROFORGE-LAB"&&Classic95Setup.IsActive;};
        foreach(var target in AppearanceCatalog.Targets)Row(targets,target,target.Name,target.Family,target.Available?"Экспериментальный профиль":"Заглушка — не применяется");
        void SelectTarget()
        {if(targets.SelectedItems.Count==0)return;var choice=AppearanceCatalog.Select(((AppearanceTarget)targets.SelectedItems[0].Tag!).Id);targetDetail.Text=choice.Target.Name+": "+choice.Status+". "+(choice.Target.Available?ClassicThemes.Get(choice.Target.Id).Detail:"");apply95.Text="Применить "+choice.Target.Name;apply95.Enabled=choice.Target.Available&&Environment.MachineName=="AEROFORGE-LAB";status.Text=targetDetail.Text;}
        targets.SelectedIndexChanged+=(_,_)=>SelectTarget();action.Click+=(_,_)=>SelectTarget();
        targets.DoubleClick+=(_,_)=>SelectTarget();var active=Classic95Setup.ActiveTarget??"windows-7";foreach(F.ListViewItem row in targets.Items)if(((AppearanceTarget)row.Tag!).Id==active)row.Selected=true;
        action.AccessibleDescription="Только выбирает оформление. Для изменения системы нажмите отдельную кнопку применения.";
    }
    private void SevenPage()
    {
        var page=Page("Windows 7 (стенд)");var top=Flow(true);
        top.Controls.Add(Label("Рабочее оформление Windows 7 — экспериментальный стенд Windows 10 LTSC 2021"));
        top.Controls.Add(Label("Это отдельное действие с реальными изменениями. Применение доступно только в подготовленной ВМ AEROFORGE-LAB. Каталог оформлений систему не меняет."));
        var bottom=Flow(true);bottom.Dock=F.DockStyle.Bottom;bottom.Controls.Add(restart);
        var options=Flow();foreach(var component in PrivateExtras.Components)
        {
            var box=Check(component.Name);box.Enabled=Environment.MachineName=="AEROFORGE-LAB"&&(PrivateExtras.InstallerAvailable(component.Id)||PrivateExtras.IsInstalled(component.Id));
            box.Checked=box.Enabled;extras[component.Id]=box;options.Controls.Add(box);
        }
        bottom.Controls.Add(options);var actions=Flow();var check=Button("Проверить компоненты");applySeven.Enabled=false;
        check.Click+=async(_,_)=>await Run(async(_,progress)=>{var result=await Task.Run(seven.Check);checks.Items.Clear();foreach(var item in result)Row(checks,item,item.Name,item.Ready?"Готово":"Не готово",item.Detail);applySeven.Enabled=result.All(c=>c.Ready);status.Text=applySeven.Enabled?"Проверка пройдена. Можно применить Windows 7.":"Подготовьте недостающие компоненты и повторите проверку.";});
        applySeven.Click+=async(_,_)=>
        {
            // Capture UI input on the UI thread before entering the worker.
            bool refresh=restart.Checked;string[] requested=extras.Where(p=>p.Value.Checked&&p.Value.Enabled).Select(p=>p.Key).ToArray();
            await Run(async(token,progress)=>{await PrivateExtras.EnsureInstalledAsync(requested,progress);var result=await Task.Run(()=>seven.ApplyAsync(refresh,progress,token));status.Text="Windows 7 применена. Журнал: "+result.DesktopReceipt;});
        };
        actions.Controls.Add(check);actions.Controls.Add(applySeven);bottom.Controls.Add(actions);
        page.Controls.Add(checks);page.Controls.Add(bottom);page.Controls.Add(top);
    }
    private void ModulePage()
    {
        var page=Page("Модули");modules.CheckBoxes=true;var top=Flow();
        profiles.DisplayMember=nameof(Profile.Name);foreach(var item in catalog.Profiles)profiles.Items.Add(item);profiles.SelectedItem=profile;
        profiles.SelectedIndexChanged+=(_,_)=>{if(profiles.SelectedItem is Profile p)SelectProfile(p);};top.Controls.Add(profiles);
        var search=new F.TextBox{Width=190,AccessibleName="Поиск модулей"};search.TextChanged+=(_,_)=>{query=search.Text;RefreshModules();};top.Controls.Add(search);
        var settings=Button("Параметры модуля…");settings.Click+=(_,_)=>EditModule();top.Controls.Add(settings);
        var preview=Button("План изменений");preview.Click+=(_,_)=>BuildPlan();top.Controls.Add(preview);
        var export=Button("Экспорт…");export.Click+=(_,_)=>ExportProfile();top.Controls.Add(export);
        var import=Button("Импорт…");import.Click+=(_,_)=>ImportProfile();top.Controls.Add(import);
        modules.ItemChecked+=(_,e)=>{if(e.Item.Tag is not Core.Module m)return;if(e.Item.Checked)selected.Add(m.Id);else selected.Remove(m.Id);InvalidatePlan();};
        modules.DoubleClick+=(_,_)=>EditModule();page.Controls.Add(modules);page.Controls.Add(top);
    }
    private void SelectProfile(Profile value)
    {profile=value;selected.Clear();selected.UnionWith(value.Modules);RefreshModules();InvalidatePlan();}
    private void RefreshModules()
    {
        // ItemChecked fires while populating: preserve the selection of filtered-out rows.
        var snapshot=selected.ToHashSet();modules.BeginUpdate();modules.Items.Clear();
        foreach(var m in catalog.Modules.Where(m=>(m.Name+" "+m.Id+" "+m.Category+" "+m.Description).Contains(query,StringComparison.OrdinalIgnoreCase)))
        {var row=new F.ListViewItem(new[]{m.Name,m.Category,m.Version,m.RequiresResources?"Внешние ресурсы":m.CriticalProcess?"Проверка ВМ":m.Status}){Tag=m};modules.Items.Add(row);row.Checked=snapshot.Contains(m.Id);}
        selected.Clear();selected.UnionWith(snapshot);modules.EndUpdate();
    }
    private void InvalidatePlan(){plan=null;apply.Enabled=false;}
    private void PlanPage()
    {
        var page=Page("План");var bottom=Flow(true);bottom.Dock=F.DockStyle.Bottom;
        bottom.Controls.Add(autostart);autostart.CheckedChanged+=(_,_)=>InvalidatePlan();bottom.Controls.Add(consent);
        var actions=Flow();var recalc=Button("Пересчитать план");recalc.Click+=(_,_)=>BuildPlan();actions.Controls.Add(recalc);actions.Controls.Add(apply);
        apply.Click+=async(_,_)=>{if(plan==null)return;if(!consent.Checked){status.Text="Прочитайте план и отметьте согласие.";return;}var request=plan;await Run(async(token,progress)=>{host.EnsureStopped();var receipt=await Task.Run(()=>transactions.Apply(request,progress,token));InvalidatePlan();status.Text="Профиль подготовлен; запуск движка выполняется отдельно. Журнал: "+receipt.Id;});};
        var start=Button("Запустить движок");start.Click+=(_,_)=>Try(()=>{host.Start();status.Text="Запуск движка запрошен.";});actions.Controls.Add(start);
        var stop=Button("Остановить движок");stop.Click+=async(_,_)=>await Run(async(_,_)=>{await host.StopAsync();status.Text="Движок остановлен.";});actions.Controls.Add(stop);
        cancel.Click+=(_,_)=>operation?.Cancel();cancel.Enabled=false;bottom.Controls.Add(actions);
        page.Controls.Add(planItems);page.Controls.Add(bottom);
    }
    private void BuildPlan()=>Try(()=>
    {
        plan=new ProfilePlanner(store,catalog).Build(profile.Id,selected,autostart.Checked,host.Executable,privateIconPack:selected.Contains("icon-resource-redirect")?seven.IconPack:null,privateDisplayProvider:seven.DisplayProvider);
        planItems.Items.Clear();foreach(var change in plan.Changes)Row(planItems,change,change.Description,change.Target);
        foreach(var blocker in plan.Blockers)Row(planItems,null,"Блокировка",blocker);foreach(var warning in plan.Warnings)Row(planItems,null,"Замечание",warning);
        consent.Checked=false;apply.Enabled=plan.Blockers.Count==0;SelectPage(3);status.Text=$"{plan.Modules.Length} модулей; {plan.Changes.Count} операций; {plan.Blockers.Count} блокировок.";
    });
    private void RecoveryPage()
    {
        var page=Page("Восстановление");var actions=Flow();actions.Dock=F.DockStyle.Bottom;
        var refresh=Button("Обновить");refresh.Click+=(_,_)=>RefreshHistory();actions.Controls.Add(refresh);
        var undo=Button("Откатить выбранный журнал");undo.Click+=async(_,_)=>{if(history.SelectedItems.Count==0||history.SelectedItems[0].Tag is not Receipt receipt)return;await Run(async(_,progress)=>{host.EnsureStopped();var result=await Task.Run(()=>transactions.Rollback(receipt.FilePath,progress));status.Text="Откат: "+result.Status;});};actions.Controls.Add(undo);
        var desktopUndo=Button("Вернуть оформление");desktopUndo.Click+=async(_,_)=>await Run(async(_,progress)=>{await host.StopAsync();var result=await Task.Run(()=>desktop.Restore(progress));status.Text="Возврат оформления: "+result.Status+". Внешние компоненты остаются установленными.";});actions.Controls.Add(desktopUndo);
        var machineUndo=Button("Вернуть системные настройки Seven");machineUndo.Click+=async(_,_)=>await Run(async(_,_)=>{await new MachineSeven().RestoreElevatedAsync();status.Text="Системные настройки Seven возвращены.";});actions.Controls.Add(machineUndo);
        history.SelectedIndexChanged+=(_,_)=>Try(()=>{if(history.SelectedItems.Count>0 && history.SelectedItems[0].Tag is Receipt r)detail.Text=r.FilePath+Environment.NewLine+r.Error+Environment.NewLine+string.Join(Environment.NewLine,transactions.ReadErrors(r.FilePath));});
        page.Controls.Add(history);page.Controls.Add(detail);page.Controls.Add(actions);
    }
    private void RefreshHistory()=>Try(()=>{history.BeginUpdate();try{history.Items.Clear();foreach(var receipt in transactions.History(false))Row(history,receipt,receipt.Created.ToLocalTime().ToString("g"),receipt.ProfileName,receipt.Status,receipt.Id);}finally{history.EndUpdate();}});
    private void ComponentPage()
    {
        var page=Page("Компоненты");var stack=Flow(true);stack.Dock=F.DockStyle.Fill;stack.AutoScroll=true;stack.WrapContents=false;stack.AutoSize=false;page.Controls.Add(stack);
        stack.Controls.Add(Label("Установщики имеют собственные журналы и средства удаления. Загружаемые файлы проверяются по SHA-256."));
        foreach(var component in PrivateExtras.Components)
        {
            var line=Flow();var label=Label("");var install=Button("Установить");var remove=Button("Удалить");
            void Refresh(){bool installed=PrivateExtras.IsInstalled(component.Id),owned=PrivateExtras.IsOwned(component.Id);label.Text=component.Name+" — "+(installed?owned?"установлено WinMim":"установлено отдельно":"не установлено");install.Enabled=Environment.MachineName=="AEROFORGE-LAB"&&!installed&&PrivateExtras.InstallerAvailable(component.Id);remove.Enabled=Environment.MachineName=="AEROFORGE-LAB"&&installed&&owned;}
            Refresh();install.Click+=async(_,_)=>{await Run(async(_,p)=>await PrivateExtras.EnsureInstalledAsync([component.Id],p),false);Refresh();};remove.Click+=async(_,_)=>{await Run(async(_,_)=>await PrivateExtras.RemoveElevatedAsync(component.Id),false);Refresh();};
            line.Controls.Add(label);line.Controls.Add(install);line.Controls.Add(remove);stack.Controls.Add(line);
        }
        var sounds=Flow();sounds.Controls.Add(Label("Звуки Windows 7 (стенд)"));
        var soundApply=Button("Применить звуки");var soundRestore=Button("Вернуть звуки");
        soundApply.Enabled=Environment.MachineName=="AEROFORGE-LAB" && Directory.Exists(SevenSounds.PrivateAssets);
        soundRestore.Enabled=Environment.MachineName=="AEROFORGE-LAB";
        soundApply.Click+=async(_,_)=>await Run(async(token,p)=>{var result=await Task.Run(()=>new SevenSoundsController().Apply(p,token));status.Text="Звуки Windows 7: "+result.Status;});
        soundRestore.Click+=async(_,_)=>await Run(async(_,p)=>{var result=await Task.Run(()=>new SevenSoundsController().Restore(p));status.Text="Возврат звуков: "+result.Status;});
        sounds.Controls.Add(soundApply);sounds.Controls.Add(soundRestore);stack.Controls.Add(sounds);
        foreach(var dependency in catalog.Dependencies)
        {
            var group=new F.GroupBox{Text=dependency.Name+" "+dependency.Version,AutoSize=true,AutoSizeMode=F.AutoSizeMode.GrowAndShrink,Width=820,Padding=new F.Padding(10)};
            var body=Flow(true);body.Dock=F.DockStyle.None;body.Location=new D.Point(8,22);body.Controls.Add(Label(dependency.Description+" · "+dependency.License));
            var actions=Flow();var download=Button("Загрузить и проверить");var install=Button("Запустить установщик");install.Enabled=false;
            download.Click+=async(_,_)=>await Run(async(token,p)=>{string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForge","Downloads");downloads[dependency.Id]=await new PackageCache(root).DownloadAsync(dependency,p,token);install.Enabled=true;status.Text=dependency.Name+": SHA-256 проверен.";});
            install.Click+=(_,_)=>Try(()=>{string file=downloads[dependency.Id];if(!Hashing.File(file).Equals(dependency.Sha256,StringComparison.OrdinalIgnoreCase))throw new IOException("Файл изменён");Process.Start(new ProcessStartInfo(file){UseShellExecute=true});});
            actions.Controls.Add(download);actions.Controls.Add(install);body.Controls.Add(actions);group.Controls.Add(body);stack.Controls.Add(group);
        }
    }
    private void ToolPage()
    {
        var page=Page("Инструменты");var actions=Flow(true);actions.Controls.Add(Label("При наличии установленных классических программ запускаются они. Иначе доступны резервные инструменты WinMim."));
        foreach(var (caption,argument) in new[]{("Гаджеты","--gadgets"),("Media Center / медиатека","--media"),("Сапёр","--mines"),("Переключатель окон","--switcher"),("Свойства панели задач","--taskbar-properties")})
        {var button=Button(caption);button.Click+=(_,_)=>Try(()=>{var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true};start.ArgumentList.Add(argument);Process.Start(start);});actions.Controls.Add(button);}
        page.Controls.Add(actions);
    }
    private void AboutPage()
    {
        var page=Page("О программе");var body=Flow(true);
        body.Controls.Add(Label("WinMim — Windows Mimicry"));body.Controls.Add(Label("Цель: воспроизвести вид и поведение выбранной версии Windows, включая оболочку и системные диалоги. Это долгосрочная цель; полное соответствие пока не достигнуто."));
        body.Controls.Add(Label("Собственный код — GPL-3.0. Сторонние компоненты сохраняют лицензии и авторство. Личные пакеты тем и ресурсов распространяются отдельно."));
        body.Controls.Add(Label("Интерфейс использует стандартные элементы Windows и текущие системные цвета и шрифт. Собственная тема интерфейса отсутствует."));
        body.Controls.Add(Label(WindowsBackend.Inspect().Summary));
        var verify=Button("Проверить файлы");verify.Click+=async(_,_)=>await Run(async(_,_)=>{var errors=await Task.Run(()=>store.VerifyAll(catalog));status.Text=errors.Length==0?"Все контрольные суммы совпадают.":string.Join("; ",errors);});body.Controls.Add(verify);
        var diagnostics=Button("Сохранить диагностику…");diagnostics.Click+=(_,_)=>Try(()=>{using var save=new F.SaveFileDialog{Filter="JSON|*.json",FileName="winmim-diagnostics.json"};if(save.ShowDialog(this)==F.DialogResult.OK)JsonFile.Write(save.FileName,new{version=catalog.Version,host=WindowsBackend.Inspect(),contentErrors=store.VerifyAll(catalog),receipts=transactions.History(false).Select(r=>new{r.Id,r.Status,r.Created})});});body.Controls.Add(diagnostics);
        var sources=Button("Исходники модулей");sources.Click+=(_,_)=>Try(()=>Process.Start(new ProcessStartInfo("https://github.com/ramensoftware/windhawk-mods/tree/"+catalog.SourceCommit){UseShellExecute=true}));body.Controls.Add(sources);page.Controls.Add(body);
    }
    private void EditModule()
    {
        if(modules.SelectedItems.Count==0 || modules.SelectedItems[0].Tag is not Core.Module module)return;
        using var dialog=new NativeModuleSettings(module,profile.Settings.GetValueOrDefault(module.Id));
        if(dialog.ShowDialog(this)==F.DialogResult.OK){profile.Settings[module.Id]=dialog.Result;InvalidatePlan();}
    }
    private void ExportProfile()=>Try(()=>
    {
        using var save=new F.SaveFileDialog{Filter="WinMim profile|*.afprofile.json",FileName=profile.Id+".afprofile.json"};if(save.ShowDialog(this)!=F.DialogResult.OK)return;
        JsonFile.Write(save.FileName,new Profile{Id=profile.Id,Name=profile.Name,Description=profile.Description,Accent=profile.Accent,Modules=selected.ToArray(),Settings=profile.Settings});
    });
    private void ImportProfile()=>Try(()=>
    {
        using var open=new F.OpenFileDialog{Filter="WinMim profile|*.afprofile.json|JSON|*.json"};if(open.ShowDialog(this)!=F.DialogResult.OK)return;
        if(new FileInfo(open.FileName).Length>1024*1024)throw new IOException("Профиль больше 1 МБ");
        var imported=JsonFile.Read<Profile>(open.FileName);imported.Id="custom-"+Guid.NewGuid().ToString("N")[..8];
        if(imported.Name.Length>60||imported.Description.Length>1000||imported.Modules.Any(id=>!catalog.Modules.Any(m=>m.Id==id)))throw new InvalidDataException("Некорректный профиль");
        catalog.Profiles.Add(imported);profiles.Items.Add(imported);profiles.SelectedItem=imported;
    });
    private async Task Run(Func<CancellationToken,IProgress<string>,Task> action,bool cancellable=true)
    {
        if(busy)return;busy=true;using var token=new CancellationTokenSource();operation=token;
        tabs.Enabled=false;cancel.Enabled=cancellable;cancel.Visible=cancellable;
        var progress=new Progress<string>(text=>status.Text=text);
        try{await action(token.Token,progress);}
        catch(Exception error){Error(error);}
        finally{busy=false;operation=null;tabs.Enabled=true;cancel.Enabled=false;cancel.Visible=false;RefreshHistory();}
    }
    private void Try(Action action){try{action();}catch(Exception error){Error(error);}}
    private void Error(Exception error){status.Text=error.Message;F.MessageBox.Show(this,error.Message,"WinMim",F.MessageBoxButtons.OK,F.MessageBoxIcon.Error);}
    public void Render(string file,int page=0)
    {
        SelectPage(page);Show();F.Application.DoEvents();
        using var bitmap=new D.Bitmap(Width,Height);DrawToBitmap(bitmap,new D.Rectangle(0,0,Width,Height));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);bitmap.Save(file,D.Imaging.ImageFormat.Png);Hide();
    }
    public void Smoke(string file)
    {
        Show();F.Application.DoEvents();var results=new List<object>();
        foreach(F.ListViewItem row in targets.Items)
        {
            targets.SelectedItems.Clear();row.Selected=true;F.Application.DoEvents();
            var choice=AppearanceCatalog.Select(((AppearanceTarget)row.Tag!).Id);
            if(choice.ChangesSystem||choice.Changes.Count!=0||!targetDetail.Text.Contains(choice.Target.Name))throw new InvalidOperationException("Placeholder changed behavior");
            results.Add(new{id=choice.Target.Id,name=choice.Target.Name,choice.ChangesSystem,changes=choice.Changes.Count});
        }
        JsonFile.Write(file,new{framework="Windows Forms",visualStyles=F.Application.RenderWithVisualStyles,ownerDraw=targets.OwnerDraw,systemButtons=apply.FlatStyle.ToString(),pages=tabs.TabCount,targets=results});Hide();
    }
}

internal sealed class NativeModuleSettings : F.Form
{
    public Dictionary<string,string> Result {get;private set;}=[];
    public NativeModuleSettings(Core.Module module,Dictionary<string,string>? settings)
    {
        Text=module.Name;Font=D.SystemFonts.MessageBoxFont;AutoScaleMode=F.AutoScaleMode.Font;Size=new(720,500);StartPosition=F.FormStartPosition.CenterParent;
        var values=new Dictionary<string,string>(module.DefaultSettings);if(settings!=null)foreach(var pair in settings)values[pair.Key]=pair.Value;
        var grid=new F.DataGridView{Dock=F.DockStyle.Fill,AutoGenerateColumns=false,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false};
        grid.Columns.Add(new F.DataGridViewTextBoxColumn{HeaderText="Параметр",ReadOnly=true,AutoSizeMode=F.DataGridViewAutoSizeColumnMode.Fill});grid.Columns.Add(new F.DataGridViewTextBoxColumn{HeaderText="Значение",AutoSizeMode=F.DataGridViewAutoSizeColumnMode.Fill});
        foreach(var pair in values)grid.Rows.Add(pair.Key,pair.Value);
        var footer=new F.FlowLayoutPanel{Dock=F.DockStyle.Bottom,AutoSize=true,FlowDirection=F.FlowDirection.RightToLeft};
        var save=new F.Button{Text="Сохранить",AutoSize=true,FlatStyle=F.FlatStyle.System};var cancel=new F.Button{Text="Отмена",AutoSize=true,DialogResult=F.DialogResult.Cancel,FlatStyle=F.FlatStyle.System};
        save.Click+=(_,_)=>{grid.EndEdit();Result=grid.Rows.Cast<F.DataGridViewRow>().ToDictionary(r=>(string)r.Cells[0].Value!,r=>r.Cells[1].Value?.ToString()??"");DialogResult=F.DialogResult.OK;};
        footer.Controls.Add(save);footer.Controls.Add(cancel);Controls.Add(grid);Controls.Add(footer);AcceptButton=save;CancelButton=cancel;
    }
}
