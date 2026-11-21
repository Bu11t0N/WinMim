using AeroForge.Core;
using System.Diagnostics;
using System.IO;
using F=System.Windows.Forms;
using D=System.Drawing;
namespace AeroForge.App;

internal sealed class NativeTaskbarProperties : F.Form
{
    private readonly DesktopController desktop=new();
    private readonly F.CheckBox locked=Check(UiText.Get("NativeTaskbarProperties.LockTheTaskbar")),autoHide=Check(UiText.Get("NativeTaskbarProperties.AutoHideTheTaskbar")),small=Check(UiText.Get("NativeTaskbarProperties.UseSmallIcons")),peek=Check(UiText.Get("NativeTaskbarProperties.UseAeroPeekToPreviewTheDesktop"));
    private readonly F.ComboBox combine=new(){DropDownStyle=F.ComboBoxStyle.DropDownList,Width=350};
    private readonly F.Label status=new(){AutoSize=true,MaximumSize=new D.Size(400,0)};
    public NativeTaskbarProperties()
    {
        Text=UiText.Get("NativeTaskbarProperties.TaskbarAndStartMenuProperties");Font=D.SystemFonts.MessageBoxFont;AutoScaleMode=F.AutoScaleMode.Font;
        ClientSize=new(450,430);FormBorderStyle=F.FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=F.FormStartPosition.CenterScreen;
        if(Classic95Setup.IsActive)peek.Visible=false;
        var tabs=new F.TabControl{Dock=F.DockStyle.Fill};var taskbar=new F.TabPage(UiText.Get("NativeTaskbarProperties.Taskbar")){UseVisualStyleBackColor=true,Padding=new F.Padding(12)};var start=new F.TabPage(UiText.Get("NativeTaskbarProperties.StartMenu")){UseVisualStyleBackColor=true,Padding=new F.Padding(12)};tabs.TabPages.Add(taskbar);tabs.TabPages.Add(start);
        var layout=Flow();layout.Dock=F.DockStyle.Fill;taskbar.Controls.Add(layout);
        layout.Controls.Add(locked);layout.Controls.Add(autoHide);layout.Controls.Add(small);layout.Controls.Add(new F.Label{Text=UiText.Get("NativeTaskbarProperties.TaskbarButtons"),AutoSize=true,Margin=new F.Padding(3,12,3,4)});
        combine.Items.AddRange(new object[]{UiText.Get("NativeTaskbarProperties.AlwaysCombineHideLabels"),UiText.Get("NativeTaskbarProperties.CombineWhenTaskbarIsFull"),UiText.Get("NativeTaskbarProperties.NeverCombine")});layout.Controls.Add(combine);
        var notifications=Button(UiText.Get("NativeTaskbarProperties.CustomizeIconsAndNotifications"));notifications.Click+=(_,_)=>Launch("explorer.exe","shell:::{05D7B0F4-2121-4EFF-BF6B-ED3F69B894D9}");layout.Controls.Add(notifications);layout.Controls.Add(peek);
        var startLayout=Flow();startLayout.Dock=F.DockStyle.Fill;var settings=Button(UiText.Get("NativeTaskbarProperties.CustomizeTheStartMenu"));settings.Click+=(_,_)=>Launch(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Open-Shell","StartMenu.exe"),"-settings");startLayout.Controls.Add(settings);start.Controls.Add(startLayout);
        var bottom=Flow();bottom.Dock=F.DockStyle.Bottom;bottom.Controls.Add(status);var actions=new F.FlowLayoutPanel{AutoSize=true,FlowDirection=F.FlowDirection.LeftToRight};
        var ok=Button("OK");var cancel=Button(UiText.Get("NativeTaskbarProperties.Cancel"));var apply=Button(UiText.Get("NativeTaskbarProperties.Apply"));ok.Click+=(_,_)=>{if(Save())Close();};cancel.Click+=(_,_)=>Close();apply.Click+=(_,_)=>Save();actions.Controls.Add(ok);actions.Controls.Add(cancel);actions.Controls.Add(apply);bottom.Controls.Add(actions);
        Controls.Add(tabs);Controls.Add(bottom);AcceptButton=ok;CancelButton=cancel;
        try{var value=desktop.TaskbarSettings();locked.Checked=value.Locked;autoHide.Checked=value.AutoHide;small.Checked=value.SmallIcons;peek.Checked=value.Peek;combine.SelectedIndex=value.Combine;}catch(Exception error){status.Text=error.Message;}
    }
    private static F.FlowLayoutPanel Flow()=>new(){AutoSize=true,FlowDirection=F.FlowDirection.TopDown,WrapContents=false,BackColor=D.Color.Transparent,Padding=new F.Padding(8)};
    private static F.CheckBox Check(string text)=>new(){Text=text,AutoSize=true,FlatStyle=F.FlatStyle.System,Margin=new F.Padding(3,8,3,5)};
    private static F.Button Button(string text)=>new(){Text=text,AutoSize=true,FlatStyle=F.FlatStyle.System,Margin=new F.Padding(3,8,3,5)};
    private bool Save(){try{desktop.SetTaskbar(new(locked.Checked,autoHide.Checked,small.Checked,combine.SelectedIndex,peek.Checked));status.Text="";return true;}catch(Exception error){status.Text=error.Message;return false;}}
    private void Launch(string file,string arguments){try{Process.Start(new ProcessStartInfo(file,arguments){UseShellExecute=true})?.Dispose();}catch(Exception error){status.Text=error.Message;}}
}
