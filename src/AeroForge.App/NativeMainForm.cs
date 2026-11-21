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
    private static string ProductTitle => $"WinMim {typeof(NativeMainForm).Assembly.GetName().Version!.ToString(3)} Preview";
    private readonly ContentStore store = new(MainWindow.FindContent());
    private readonly Catalog catalog;
    private readonly ShellHost host;
    private readonly TransactionManager transactions;
    private readonly DesktopController desktop = new();
    private readonly SevenSetup seven = new(MainWindow.FindContent());
    private readonly F.TabControl tabs = new() { Dock = F.DockStyle.Fill };
    private readonly F.ToolStripStatusLabel status = new() { Spring=true, TextAlign=D.ContentAlignment.MiddleLeft };
    private readonly F.ListView targets = List(UiText.Get("NativeMainForm.Appearance"), 200, UiText.Get("NativeMainForm.Family"), 165, UiText.Get("NativeMainForm.Status"), 225);
    private static string NoSelection => UiText.Get("NativeMainForm.SelectAnAppearanceToSeeItsRequirements");
    private readonly F.TextBox targetDetail = new()
    {
        AccessibleName = UiText.Get("NativeMainForm.RequirementsForTheSelectedAppearance"), Dock = F.DockStyle.Fill,
        Multiline = true, ReadOnly = true, ScrollBars = F.ScrollBars.Vertical, Text = NoSelection
    };
    private readonly F.Button applyTheme = Button(UiText.Get("NativeMainForm.Apply"));
    private readonly F.Button restoreTheme = Button(UiText.Get("NativeMainForm.RestorePreviousAppearance"));
    private readonly List<F.TabPage> extraPages = [];
    private F.Button advanced = null!;
    private readonly F.ListView modules=List(UiText.Get("NativeMainForm.Module"),280,UiText.Get("NativeMainForm.Category"),150,UiText.Get("NativeMainForm.Version"),90,UiText.Get("NativeMainForm.Status2"),200);
    private readonly F.ListView planItems=List(UiText.Get("NativeMainForm.Action"),380,UiText.Get("NativeMainForm.Target"),430);
    private readonly F.ListView history=List(UiText.Get("NativeMainForm.Time"),175,UiText.Get("NativeMainForm.Profile"),250,UiText.Get("NativeMainForm.Status"),190,UiText.Get("NativeMainForm.Journal"),230);
    private readonly F.TextBox detail=new(){Multiline=true,ReadOnly=true,ScrollBars=F.ScrollBars.Vertical,Dock=F.DockStyle.Bottom,Height=90};
    private readonly F.CheckBox autostart=Check(UiText.Get("NativeMainForm.StartTheEngineWhenSigningInTo"));
    private readonly F.CheckBox consent=Check(UiText.Get("NativeMainForm.IHaveReviewedTheExperimentalPlan"));
    private readonly F.CheckBox restart=Check(UiText.Get("NativeMainForm.RestartExplorerFolderWindowsWillClose"),true);
    private readonly F.Button apply=Button(UiText.Get("NativeMainForm.PrepareProfile"));
    private readonly Dictionary<string,string> downloads=[];
    private readonly F.ComboBox profiles=new(){DropDownStyle=F.ComboBoxStyle.DropDownList,Width=240};
    private readonly F.Button cancel=Button(UiText.Get("NativeMainForm.CancelOperation"));
    private Profile profile;
    private Plan? plan;
    private CancellationTokenSource? operation;
    private bool busy;
    private string query="";
    private readonly HashSet<string> selected=[];

    public NativeMainForm()
    {
        Text=ProductTitle;Icon=D.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)??D.SystemIcons.Application;AutoScaleMode=F.AutoScaleMode.Font;
        Font=D.SystemFonts.MessageBoxFont;StartPosition=F.FormStartPosition.CenterScreen;
        Size=new(960,720);MinimumSize=new(720,540);
        var area=F.Screen.PrimaryScreen!.WorkingArea;Size=new(Math.Min(Width,area.Width),Math.Min(Height,area.Height));
        catalog=store.Load();profile=catalog.Profiles.First(p=>p.Id=="seven");
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForge");
        host=new(Path.Combine(root,"Host"),catalog);
        transactions=new(new WindowsBackend(Path.Combine(root,"Host")),Path.Combine(root,"State"));
        var strip=new F.StatusStrip();strip.Items.Add(status);Controls.Add(tabs);Controls.Add(cancel);Controls.Add(strip);cancel.Dock=F.DockStyle.Bottom;cancel.Visible=false;
        status.Text=UiText.Get("NativeMainForm.Ready");
        TargetPage();ModulePage();PlanPage();RecoveryPage();ComponentPage();ToolPage();AboutPage();LanguagePage();
        extraPages.AddRange(tabs.TabPages.Cast<F.TabPage>().Skip(1));
        SetAdvanced(false);
        SelectProfile(profile);RefreshHistory();
        FormClosing+=(_,e)=>{if(busy){e.Cancel=true;status.Text=UiText.Get("NativeMainForm.WaitForTheOperationToFinish");}};
        Microsoft.Win32.SystemEvents.UserPreferenceChanged+=PreferencesChanged;
        FormClosed+=(_,_)=>Microsoft.Win32.SystemEvents.UserPreferenceChanged-=PreferencesChanged;
    }
    private void PreferencesChanged(object sender,Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {if(!IsDisposed && IsHandleCreated)BeginInvoke(()=>{Font=D.SystemFonts.MessageBoxFont;PerformLayout();Refresh();});}
    public void SelectPage(int index)
    {
        if (index > 0) SetAdvanced(true);
        tabs.SelectedIndex = Math.Clamp(index, 0, tabs.TabCount - 1);
    }

    private void SetAdvanced(bool show)
    {
        if (show && tabs.TabCount == 1) tabs.TabPages.AddRange(extraPages.ToArray());
        if (!show)
        {
            tabs.SelectedIndex = 0;
            foreach (var page in extraPages) tabs.TabPages.Remove(page);
        }
        advanced.Text = show ? UiText.Get("NativeMainForm.HideAdvancedTabs") : UiText.Get("NativeMainForm.AdVanced");
    }
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
        var page = Page(UiText.Get("NativeMainForm.Appearances"));
        var layout = new F.TableLayoutPanel { ColumnCount = 1, RowCount = 4, Dock = F.DockStyle.Fill };
        layout.ColumnStyles.Add(new(F.SizeType.Percent, 100));
        layout.RowStyles.Add(new(F.SizeType.Percent, 100));
        layout.RowStyles.Add(new(F.SizeType.Absolute, 112));
        layout.RowStyles.Add(new(F.SizeType.AutoSize));
        layout.RowStyles.Add(new(F.SizeType.AutoSize));
        page.Controls.Add(layout);

        targets.AccessibleName = UiText.Get("NativeMainForm.AppearanceList");
        layout.Controls.Add(targets, 0, 0);
        layout.Controls.Add(targetDetail, 0, 1);
        var actions = Flow();
        var check = Button(UiText.Get("NativeMainForm.CheckRequirements"));
        var prepare = Button(UiText.Get("NativeMainForm.PrepareTheme"));
        prepare.Enabled = false;
        advanced = Button(UiText.Get("NativeMainForm.AdVanced"));
        advanced.Click += (_, _) => SetAdvanced(tabs.TabCount == 1);
        var settings = Button(UiText.Get("Language.Tab"));
        settings.Click += (_, _) => SelectPage(7);
        actions.Controls.AddRange([prepare, applyTheme, restoreTheme, check, settings, advanced]);
        layout.Controls.Add(actions, 0, 2);

        var options = Flow();
        var sevenTools = Button(UiText.Get("NativeMainForm.Windows7Tools"));
        sevenTools.Click += (_, _) => SelectPage(5);
        var sevenComponents = Button(UiText.Get("NativeMainForm.Windows7COmponents"));
        sevenComponents.Click += (_, _) => SelectPage(4);
        var retroFiles = Button(UiText.Get("NativeMainForm.WinMimFiles"));
        retroFiles.Click += (_, _) =>
        {
            using var browser = new RetroFileBrowser(null);
            browser.ShowDialog(this);
        };
        var xpHelp = Button(UiText.Get("NativeMainForm.XPPack"));
        xpHelp.Click += (_, _) => F.MessageBox.Show(this,
            UiText.Get("NativeMainForm.UseOnlyResourcesYouHavePermissionTo")
            + Environment.NewLine + Environment.NewLine + XpThemePack.UserAssets
            + Environment.NewLine + Environment.NewLine + UiText.Get("NativeMainForm.ThenClickCheckRequirementsWinMimVerifiesThe"),
            UiText.Get("NativeMainForm.PersonalWindowsXPPack"), F.MessageBoxButtons.OK, F.MessageBoxIcon.Information);
        var importResources = Button(UiText.Get("ResourceImport.Action"));
        importResources.Click += async (_, _) =>
        {
            using var dialog = new NativeResourceImport();
            if (dialog.ShowDialog(this) != F.DialogResult.OK) return;
            await Run(async (token, progress) =>
            {
                status.Text = UiText.Get("ResourceImport.Verifying");
                string folder = await Task.Run(() => ResourcePackageCache.ForCurrentUser()
                    .PrepareAsync(dialog.Package!, dialog.Archive, token, progress));
                status.Text = UiText.Get("ResourceImport.Prepared");
                F.MessageBox.Show(this, UiText.Format("ResourceImport.PreparedIn", folder),
                    UiText.Get("ResourceImport.Action"), F.MessageBoxButtons.OK, F.MessageBoxIcon.Information);
            });
        };
        options.Controls.AddRange([restart, sevenTools, sevenComponents, retroFiles, xpHelp, importResources]);
        layout.Controls.Add(options, 0, 3);

        foreach (var target in AppearanceCatalog.Targets)
            Row(targets, target, target.Name, target.Family, new AppearanceSelection(target).Status);
        AppearanceTarget? SelectedTarget() => targets.SelectedItems.Count == 0 ? null : (AppearanceTarget)targets.SelectedItems[0].Tag!;
        int checkVersion = 0;
        async Task RefreshTargetAsync()
        {
            int version = ++checkVersion;
            var target = SelectedTarget();
            applyTheme.Enabled = false;
            prepare.Enabled = false;
            check.Enabled = target?.Available == true;
            options.Visible = target?.Mode == AppearanceMode.Seven || target?.Id == "windows-xp";
            restart.Visible = sevenTools.Visible = sevenComponents.Visible = target?.Mode == AppearanceMode.Seven;
            retroFiles.Visible = xpHelp.Visible = importResources.Visible = target?.Id == "windows-xp";
            targetDetail.Text = target == null ? NoSelection : UiText.Format("NativeMainForm.0CheckingRequirements", target.Name);
            string recovery = "";
            try
            {
                var mode = RecoveryMode();
                restoreTheme.Enabled = mode != AppearanceMode.Unavailable;
                if (restoreTheme.Enabled)
                    recovery = UiText.Get("NativeMainForm.ThePreviousAppearanceCanBeRestored") + Environment.NewLine;
            }
            catch (Exception error)
            {
                restoreTheme.Enabled = false;
                recovery = UiText.Get("NativeMainForm.CouldNotReadRecoveryJournalsOpenAdvanced") + Environment.NewLine;
                status.Text = error.Message;
            }
            if (target == null)
            {
                targetDetail.Text = recovery + NoSelection;
                return;
            }
            if (!target.Available)
            {
                targetDetail.Text = recovery + target.Name + UiText.Get("NativeMainForm.AppearanceIsCurrentlyUnavailable");
                return;
            }

            string requirements;
            bool ready = false;
            try
            {
                requirements = await Task.Run(() => CheckTarget(target));
                ready = requirements.Length == 0;
            }
            catch (Exception error) { requirements = error.Message; }
            if (IsDisposed || version != checkVersion) return;
            string preparation = ThemePreparation.DisabledReason();
            string components = UiText.Get("NativeMainForm.Components") + string.Join(", ", ThemePreparation.Components(target)) + ".";
            targetDetail.Text = recovery + target.Name + Environment.NewLine
                + (ready ? UiText.Get("NativeMainForm.RequirementsMetTheAppearanceIsReadyTo") : requirements)
                + Environment.NewLine + components;
            if (target.Id == "windows-xp")
                targetDetail.AppendText(Environment.NewLine + UiText.Get("Xp.SourcePending"));
            else if (ready && target.Mode == AppearanceMode.Classic)
                targetDetail.AppendText(Environment.NewLine + UiText.Get("NativeMainForm.ClassicApplyApproval"));
            applyTheme.Enabled = ready;
            prepare.Enabled = !ready && preparation.Length == 0;
        }

        targets.SelectedIndexChanged += async (_, _) => await RefreshTargetAsync();
        check.Click += async (_, _) => await RefreshTargetAsync();
        prepare.Click += async (_, _) =>
        {
            if (SelectedTarget() is not { Available: true } target) return;
            string items = string.Join(Environment.NewLine, ThemePreparation.Components(target));
            string licenses = UiText.Get("NativeMainForm.VendorLicensesAndTerms") + Environment.NewLine +
                "Open-Shell (MIT) — github.com/Open-Shell/Open-Shell-Menu";
            if (target.Mode == AppearanceMode.Seven || target.Id == "windows-xp")
                licenses += Environment.NewLine + "SecureUxTheme (LGPL-2.1) — github.com/namazso/SecureUxTheme";
            if (F.MessageBox.Show(this, items + Environment.NewLine + Environment.NewLine +
                licenses + Environment.NewLine + Environment.NewLine +
                UiText.Get("NativeMainForm.ClickOKToPrepareTheComponentsWindows"),
                UiText.Get("NativeMainForm.Prepare") + target.Name, F.MessageBoxButtons.OKCancel, F.MessageBoxIcon.Information) != F.DialogResult.OK)
                return;
            await Run(async (token, progress) =>
            {
                await Task.Run(() => new ThemePreparation(MainWindow.FindContent()).PrepareAsync(target.Id, progress, token));
                status.Text = UiText.Get("NativeMainForm.AutomaticPreparationFinishedCheckRemainingRequirements");
            });
            await RefreshTargetAsync();
        };
        applyTheme.Click += async (_, _) =>
        {
            if (SelectedTarget() is not { } target) return;
            bool refreshExplorer = restart.Checked;
            await Run(async (token, progress) =>
            {
                await Task.Run(async () =>
                {
                    if (target.Mode == AppearanceMode.Seven)
                        await new SevenSetup(MainWindow.FindContent()).ApplyAsync(refreshExplorer, progress, token);
                    else
                        await new Classic95Setup(MainWindow.FindContent()).ApplyThemeAsync(target.Id, progress, token);
                });
                status.Text = UiText.Get("NativeMainForm.Applied") + target.Name + UiText.Get("NativeMainForm.");
            });
            await RefreshTargetAsync();
        };
        restoreTheme.Click += async (_, _) =>
        {
            await Run(async (_, progress) =>
            {
                await Task.Run(async () =>
                {
                    switch (RecoveryMode())
                    {
                        case AppearanceMode.Classic: await Classic95Setup.ForRecovery(MainWindow.FindContent()).RestoreAsync(progress); break;
                        case AppearanceMode.Seven: await seven.RestoreAsync(progress); break;
                        default: throw new InvalidOperationException(UiText.Get("NativeMainForm.ThereIsNoActiveAppearanceToRestore"));
                    }
                });
                status.Text = UiText.Get("NativeMainForm.ThePreviousAppearanceWasRestored");
            }, cancellable: false);
            await RefreshTargetAsync();
        };
        page.Enter += async (_, _) => await RefreshTargetAsync();
        _ = RefreshTargetAsync();
    }

    private AppearanceMode RecoveryMode()
    {
        if (Classic95Setup.HasRecoveryWork) return AppearanceMode.Classic;
        return seven.HasRecoveryWork ? AppearanceMode.Seven : AppearanceMode.Unavailable;
    }

    private string CheckTarget(AppearanceTarget target)
    {
        var checks = target.Mode == AppearanceMode.Seven
            ? new SevenSetup(MainWindow.FindContent()).Check()
            : new Classic95Setup(MainWindow.FindContent()).Check(target.Id);
        return string.Join(Environment.NewLine, checks.Where(check => !check.Ready)
            .Select(check => check.Name + " — " + check.Detail));
    }

    private void ModulePage()
    {
        var page=Page(UiText.Get("NativeMainForm.Modules"));modules.CheckBoxes=true;var top=Flow();
        profiles.DisplayMember=nameof(Profile.Name);foreach(var item in catalog.Profiles)profiles.Items.Add(item);profiles.SelectedItem=profile;
        profiles.SelectedIndexChanged+=(_,_)=>{if(profiles.SelectedItem is Profile p)SelectProfile(p);};top.Controls.Add(profiles);
        var search=new F.TextBox{Width=190,AccessibleName=UiText.Get("NativeMainForm.SearchModules")};search.TextChanged+=(_,_)=>{query=search.Text;RefreshModules();};top.Controls.Add(search);
        var settings=Button(UiText.Get("NativeMainForm.ModuleSettings"));settings.Click+=(_,_)=>EditModule();top.Controls.Add(settings);
        var preview=Button(UiText.Get("NativeMainForm.ChangePlan"));preview.Click+=(_,_)=>BuildPlan();top.Controls.Add(preview);
        var export=Button(UiText.Get("NativeMainForm.Export"));export.Click+=(_,_)=>ExportProfile();top.Controls.Add(export);
        var import=Button(UiText.Get("NativeMainForm.Import"));import.Click+=(_,_)=>ImportProfile();top.Controls.Add(import);
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
        {var row=new F.ListViewItem(new[]{m.Name,m.Category,m.Version,m.RequiresResources?UiText.Get("NativeMainForm.ExternalResources"):m.CriticalProcess?UiText.Get("NativeMainForm.VMTesting"):m.Status}){Tag=m};modules.Items.Add(row);row.Checked=snapshot.Contains(m.Id);}
        selected.Clear();selected.UnionWith(snapshot);modules.EndUpdate();
    }
    private void InvalidatePlan(){plan=null;apply.Enabled=false;}
    private void PlanPage()
    {
        var page=Page(UiText.Get("NativeMainForm.Plan"));var bottom=Flow(true);bottom.Dock=F.DockStyle.Bottom;
        bottom.Controls.Add(autostart);autostart.CheckedChanged+=(_,_)=>InvalidatePlan();bottom.Controls.Add(consent);
        var actions=Flow();var recalc=Button(UiText.Get("NativeMainForm.RebuildPlan"));recalc.Click+=(_,_)=>BuildPlan();actions.Controls.Add(recalc);actions.Controls.Add(apply);
        apply.Click+=async(_,_)=>{if(plan==null)return;if(!consent.Checked){status.Text=UiText.Get("NativeMainForm.ReadThePlanAndConfirmThatYou");return;}var request=plan;await Run(async(token,progress)=>{host.EnsureStopped();var receipt=await Task.Run(()=>transactions.Apply(request,progress,token));InvalidatePlan();status.Text=UiText.Get("NativeMainForm.ProfilePreparedStartTheEngineSeparatelyJournal")+receipt.Id;});};
        var start=Button(UiText.Get("NativeMainForm.StartEngine"));start.Click+=(_,_)=>Try(()=>{host.Start();status.Text=UiText.Get("NativeMainForm.EngineStartRequested");});actions.Controls.Add(start);
        var stop=Button(UiText.Get("NativeMainForm.StopEngine"));stop.Click+=async(_,_)=>await Run(async(_,_)=>{await host.StopAsync();status.Text=UiText.Get("NativeMainForm.EngineStopped");});actions.Controls.Add(stop);
        cancel.Click+=(_,_)=>operation?.Cancel();cancel.Enabled=false;bottom.Controls.Add(actions);
        page.Controls.Add(planItems);page.Controls.Add(bottom);
    }
    private void BuildPlan()=>Try(()=>
    {
        plan=new ProfilePlanner(store,catalog).Build(profile.Id,selected,autostart.Checked,host.Executable,privateIconPack:selected.Contains("icon-resource-redirect")?seven.IconPack:null,privateDisplayProvider:seven.DisplayProvider);
        planItems.Items.Clear();foreach(var change in plan.Changes)Row(planItems,change,change.Description,change.Target);
        foreach(var blocker in plan.Blockers)Row(planItems,null,UiText.Get("NativeMainForm.Blocker"),blocker);foreach(var warning in plan.Warnings)Row(planItems,null,UiText.Get("NativeMainForm.Note"),warning);
        consent.Checked=false;apply.Enabled=plan.Blockers.Count==0;SelectPage(2);status.Text=UiText.Format("NativeMainForm.0Modules1Operations2Blockers", plan.Modules.Length, plan.Changes.Count, plan.Blockers.Count);
    });
    private void RecoveryPage()
    {
        var page=Page(UiText.Get("NativeMainForm.Recovery"));var actions=Flow();actions.Dock=F.DockStyle.Bottom;
        var refresh=Button(UiText.Get("NativeMainForm.Refresh"));refresh.Click+=(_,_)=>RefreshHistory();actions.Controls.Add(refresh);
        var undo=Button(UiText.Get("NativeMainForm.RollBackSelectedJournal"));undo.Click+=async(_,_)=>{if(history.SelectedItems.Count==0||history.SelectedItems[0].Tag is not Receipt receipt)return;await Run(async(_,progress)=>{host.EnsureStopped();var result=await Task.Run(()=>transactions.Rollback(receipt.FilePath,progress));status.Text=UiText.Get("NativeMainForm.Rollback")+result.Status;});};actions.Controls.Add(undo);
        var classicUndo = Button(UiText.Get("NativeMainForm.RestoreXPAndClassicThemes"));
        classicUndo.Click += async (_, _) => await Run(async (_, progress) =>
        {
            await Task.Run(() => Classic95Setup.ForRecovery(MainWindow.FindContent()).RestoreAsync(progress));
            status.Text = UiText.Get("NativeMainForm.RecoveryForXPAndClassicThemesCompleted");
        }, cancellable: false);
        actions.Controls.Add(classicUndo);
        var desktopUndo=Button(UiText.Get("NativeMainForm.RestoreAppearance"));desktopUndo.Click+=async(_,_)=>await Run(async(_,progress)=>{await host.StopAsync();var result=await Task.Run(()=>desktop.Restore(progress));status.Text=UiText.Get("NativeMainForm.AppearanceRecovery")+result.Status+UiText.Get("NativeMainForm.ExternalComponentsAreStillInstalled");});actions.Controls.Add(desktopUndo);
        var machineUndo=Button(UiText.Get("NativeMainForm.RestoreSevenSystemSettings"));machineUndo.Click+=async(_,_)=>await Run(async(_,_)=>{await new MachineSeven().RestoreElevatedAsync();status.Text=UiText.Get("NativeMainForm.SevenSystemSettingsRestored");});actions.Controls.Add(machineUndo);
        history.SelectedIndexChanged+=(_,_)=>Try(()=>{if(history.SelectedItems.Count>0 && history.SelectedItems[0].Tag is Receipt r)detail.Text=r.FilePath+Environment.NewLine+r.Error+Environment.NewLine+string.Join(Environment.NewLine,transactions.ReadErrors(r.FilePath));});
        page.Controls.Add(history);page.Controls.Add(detail);page.Controls.Add(actions);
    }
    private void RefreshHistory()=>Try(()=>{history.BeginUpdate();try{history.Items.Clear();foreach(var receipt in transactions.History(false))Row(history,receipt,receipt.Created.ToLocalTime().ToString("g"),receipt.ProfileName,receipt.Status,receipt.Id);}finally{history.EndUpdate();}});
    private void ComponentPage()
    {
        var page=Page(UiText.Get("NativeMainForm.Components2"));var stack=Flow(true);stack.Dock=F.DockStyle.Fill;stack.AutoScroll=true;stack.WrapContents=false;stack.AutoSize=false;page.Controls.Add(stack);
        stack.Controls.Add(Label(UiText.Get("NativeMainForm.InstallersHaveTheirOwnLogsAndUninstall")));
        foreach(var component in PrivateExtras.Components)
        {
            var line=Flow();var label=Label("");var install=Button(UiText.Get("NativeMainForm.Install"));var remove=Button(UiText.Get("NativeMainForm.Uninstall"));
            void Refresh(){bool installed=PrivateExtras.IsInstalled(component.Id),owned=PrivateExtras.IsOwned(component.Id);label.Text=component.Name+" — "+(installed?owned?UiText.Get("NativeMainForm.InstalledByWinMim"):UiText.Get("NativeMainForm.InstalledSeparately"):UiText.Get("NativeMainForm.Missing"));install.Enabled=Environment.MachineName=="AEROFORGE-LAB"&&!installed&&PrivateExtras.InstallerAvailable(component.Id);remove.Enabled=Environment.MachineName=="AEROFORGE-LAB"&&installed&&owned;}
            Refresh();install.Click+=async(_,_)=>{await Run(async(_,p)=>await PrivateExtras.EnsureInstalledAsync([component.Id],p),false);Refresh();};remove.Click+=async(_,_)=>{await Run(async(_,_)=>await PrivateExtras.RemoveElevatedAsync(component.Id),false);Refresh();};
            line.Controls.Add(label);line.Controls.Add(install);line.Controls.Add(remove);stack.Controls.Add(line);
        }
        var sounds=Flow();sounds.Controls.Add(Label(UiText.Get("NativeMainForm.Windows7SoundsLab")));
        var soundApply=Button(UiText.Get("NativeMainForm.ApplySounds"));var soundRestore=Button(UiText.Get("NativeMainForm.RestoreSounds"));
        soundApply.Enabled=Environment.MachineName=="AEROFORGE-LAB" && Directory.Exists(SevenSounds.PrivateAssets);
        soundRestore.Enabled=Environment.MachineName=="AEROFORGE-LAB";
        soundApply.Click+=async(_,_)=>await Run(async(token,p)=>{var result=await Task.Run(()=>new SevenSoundsController().Apply(p,token));status.Text=UiText.Get("NativeMainForm.Windows7Sounds")+result.Status;});
        soundRestore.Click+=async(_,_)=>await Run(async(_,p)=>{var result=await Task.Run(()=>new SevenSoundsController().Restore(p));status.Text=UiText.Get("NativeMainForm.SoundRecovery")+result.Status;});
        sounds.Controls.Add(soundApply);sounds.Controls.Add(soundRestore);stack.Controls.Add(sounds);
        foreach(var dependency in catalog.Dependencies)
        {
            var group=new F.GroupBox{Text=dependency.Name+" "+dependency.Version,AutoSize=true,AutoSizeMode=F.AutoSizeMode.GrowAndShrink,Width=820,Padding=new F.Padding(10)};
            var body=Flow(true);body.Dock=F.DockStyle.None;body.Location=new D.Point(8,22);body.Controls.Add(Label(dependency.Description+" · "+dependency.License));
            var actions=Flow();var download=Button(UiText.Get("NativeMainForm.DownloadAndVerify"));var install=Button(UiText.Get("NativeMainForm.RunInstaller"));install.Enabled=false;
            download.Click+=async(_,_)=>await Run(async(token,p)=>{string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForge","Downloads");downloads[dependency.Id]=await new PackageCache(root).DownloadAsync(dependency,p,token);install.Enabled=true;status.Text=dependency.Name+UiText.Get("NativeMainForm.SHA256Verified");});
            install.Click+=(_,_)=>Try(()=>{string file=downloads[dependency.Id];if(!Hashing.File(file).Equals(dependency.Sha256,StringComparison.OrdinalIgnoreCase))throw new IOException(UiText.Get("NativeMainForm.TheFileWasChanged"));Process.Start(new ProcessStartInfo(file){UseShellExecute=true});});
            actions.Controls.Add(download);actions.Controls.Add(install);body.Controls.Add(actions);group.Controls.Add(body);stack.Controls.Add(group);
        }
    }
    private void ToolPage()
    {
        var page=Page(UiText.Get("NativeMainForm.Tools"));var actions=Flow(true);actions.Controls.Add(Label(UiText.Get("NativeMainForm.WinMimOpensInstalledClassicProgramsWhenAvailable")));
        foreach(var (caption,argument) in new[]{(UiText.Get("NativeMainForm.Gadgets"),"--gadgets"),(UiText.Get("NativeMainForm.MediaCenterMediaLibrary"),"--media"),(UiText.Get("NativeMainForm.Minesweeper"),"--mines"),(UiText.Get("NativeMainForm.WindowSwitcher"),"--switcher"),(UiText.Get("NativeMainForm.TaskbarProperties"),"--taskbar-properties")})
        {var button=Button(caption);button.Click+=(_,_)=>Try(()=>{var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true};start.ArgumentList.Add(argument);Process.Start(start);});actions.Controls.Add(button);}
        page.Controls.Add(actions);
    }
    private void LanguagePage()
    {
        var page = Page(UiText.Get("Language.Tab"));
        var settings = Flow(true);
        settings.Controls.Add(Label(UiText.Get("Language.Label")));
        var choice = new F.ComboBox { DropDownStyle = F.ComboBoxStyle.DropDownList, Width = 180 };
        choice.AccessibleName = UiText.Get("Language.Label");
        choice.Items.AddRange(["Русский", "English"]);
        choice.SelectedIndex = UiText.Language == "en" ? 1 : 0;
        settings.Controls.Add(choice);
        settings.Controls.Add(Label(UiText.Get("Language.Restart")));
        var save = Button(UiText.Get("Language.Save"));
        save.Click += (_, _) => Try(() =>
        {
            UiText.SaveLanguage(choice.SelectedIndex == 1 ? "en" : "ru");
            status.Text = UiText.Get("Language.Saved");
        });
        settings.Controls.Add(save);
        page.Controls.Add(settings);
    }

    private void AboutPage()
    {
        var page=Page(UiText.Get("NativeMainForm.About"));var body=Flow(true);
        body.Controls.Add(Label(ProductTitle));body.Controls.Add(Label(UiText.Get("NativeMainForm.WinMimRecreatesTheLookAndBehaviorOf")));
        body.Controls.Add(Label(UiText.Get("NativeMainForm.WinMimCodeIsGPL30Third")));
        body.Controls.Add(Label(UiText.Get("NativeMainForm.WinMimUsesStandardWindowsControlsAndThe")));
        body.Controls.Add(Label(WindowsBackend.Inspect().Summary));
        var verify=Button(UiText.Get("NativeMainForm.VerifyFiles"));verify.Click+=async(_,_)=>await Run(async(_,_)=>{var errors=await Task.Run(()=>store.VerifyAll(catalog));status.Text=errors.Length==0?UiText.Get("NativeMainForm.AllChecksumsMatch"):string.Join("; ",errors);});body.Controls.Add(verify);
        var diagnostics=Button(UiText.Get("NativeMainForm.SaveDiagnostics"));diagnostics.Click+=(_,_)=>Try(()=>{using var save=new F.SaveFileDialog{Filter="JSON|*.json",FileName="winmim-diagnostics.json"};if(save.ShowDialog(this)==F.DialogResult.OK)JsonFile.Write(save.FileName,new{version=catalog.Version,host=WindowsBackend.Inspect(),contentErrors=store.VerifyAll(catalog),receipts=transactions.History(false).Select(r=>new{r.Id,r.Status,r.Created})});});body.Controls.Add(diagnostics);
        var sources=Button(UiText.Get("NativeMainForm.ModuleSourceCode"));sources.Click+=(_,_)=>Try(()=>Process.Start(new ProcessStartInfo("https://github.com/ramensoftware/windhawk-mods/tree/"+catalog.SourceCommit){UseShellExecute=true}));body.Controls.Add(sources);page.Controls.Add(body);
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
        if(new FileInfo(open.FileName).Length>1024*1024)throw new IOException(UiText.Get("NativeMainForm.TheProfileExceeds1MB"));
        var imported=JsonFile.Read<Profile>(open.FileName);imported.Id="custom-"+Guid.NewGuid().ToString("N")[..8];
        if(imported.Name.Length>60||imported.Description.Length>1000||imported.Modules.Any(id=>!catalog.Modules.Any(m=>m.Id==id)))throw new InvalidDataException(UiText.Get("NativeMainForm.InvalidProfile"));
        catalog.Profiles.Add(imported);profiles.Items.Add(imported);profiles.SelectedItem=imported;
    });
    private async Task Run(Func<CancellationToken,IProgress<string>,Task> action,bool cancellable=true)
    {
        if(busy)return;busy=true;using var token=new CancellationTokenSource();operation=token;
        tabs.Enabled=false;cancel.Enabled=cancellable;cancel.Visible=cancellable;
        var progress=new Progress<string>(text=>status.Text=text);
        try{await action(token.Token,progress);}
        catch(OperationCanceledException error){status.Text=token.IsCancellationRequested?UiText.Get("NativeMainForm.OperationCanceled"):error.Message;}
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
        Show(); F.Application.DoEvents();
        if (tabs.TabCount != 1 || targets.SelectedItems.Count != 0 || applyTheme.Enabled || !targetDetail.Text.Contains(NoSelection))
            throw new InvalidOperationException(UiText.Get("NativeMainForm.TheStartScreenShouldOpenWithoutA"));
        var results = new List<object>();
        foreach (F.ListViewItem row in targets.Items)
        {
            targets.SelectedItems.Clear();
            row.Selected = true;
            F.Application.DoEvents();
            var choice = AppearanceCatalog.Select(((AppearanceTarget)row.Tag!).Id);
            if (choice.ChangesSystem || choice.Changes.Count != 0 || !targetDetail.Text.Contains(choice.Target.Name))
                throw new InvalidOperationException(UiText.Get("NativeMainForm.SelectingAnAppearanceShouldOnlyUpdateIts"));
            results.Add(new { id = choice.Target.Id, name = choice.Target.Name, choice.ChangesSystem, changes = choice.Changes.Count });
        }
        targets.SelectedItems.Clear();
        F.Application.DoEvents();
        if (applyTheme.Enabled || !targetDetail.Text.Contains(NoSelection))
            throw new InvalidOperationException(UiText.Get("NativeMainForm.ClearingTheSelectionShouldDisableApply"));
        SetAdvanced(true);
        if (tabs.TabCount != extraPages.Count + 1) throw new InvalidOperationException(UiText.Get("NativeMainForm.AdvancedTabsAreMissing"));
        SetAdvanced(false);
        JsonFile.Write(file, new { framework = "Windows Forms", visualStyles = F.Application.RenderWithVisualStyles, ownerDraw = targets.OwnerDraw, systemButtons = applyTheme.FlatStyle.ToString(), pages = tabs.TabCount, targets = results });
        Hide();
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
        grid.Columns.Add(new F.DataGridViewTextBoxColumn{HeaderText=UiText.Get("NativeMainForm.Setting"),ReadOnly=true,AutoSizeMode=F.DataGridViewAutoSizeColumnMode.Fill});grid.Columns.Add(new F.DataGridViewTextBoxColumn{HeaderText=UiText.Get("NativeMainForm.Value"),AutoSizeMode=F.DataGridViewAutoSizeColumnMode.Fill});
        foreach(var pair in values)grid.Rows.Add(pair.Key,pair.Value);
        var footer=new F.FlowLayoutPanel{Dock=F.DockStyle.Bottom,AutoSize=true,FlowDirection=F.FlowDirection.RightToLeft};
        var save=new F.Button{Text=UiText.Get("NativeMainForm.Save"),AutoSize=true,FlatStyle=F.FlatStyle.System};var cancel=new F.Button{Text=UiText.Get("NativeMainForm.Cancel"),AutoSize=true,DialogResult=F.DialogResult.Cancel,FlatStyle=F.FlatStyle.System};
        save.Click+=(_,_)=>{grid.EndEdit();Result=grid.Rows.Cast<F.DataGridViewRow>().ToDictionary(r=>(string)r.Cells[0].Value!,r=>r.Cells[1].Value?.ToString()??"");DialogResult=F.DialogResult.OK;};
        footer.Controls.Add(save);footer.Controls.Add(cancel);Controls.Add(grid);Controls.Add(footer);AcceptButton=save;CancelButton=cancel;
    }
}
