using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AeroForge.Core;

namespace AeroForge.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        DispatcherUnhandledException += (_, error) => { MessageBox.Show(error.Exception.Message, "WinMim", MessageBoxButton.OK, MessageBoxImage.Error); error.Handled = true; };
        try
        {
            if(e.Args.FirstOrDefault()=="--resume-shell")
            {
                if(Environment.MachineName!="AEROFORGE-LAB"){Shutdown(2);return;}
                string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AeroForge");
                try
                {
                    DesktopController.EnsureLab();
                    var catalog=new ContentStore(AeroForge.App.MainWindow.FindContent()).Load();
                    var runtime=new ShellHost(Path.Combine(root,"Host"),catalog);
                    if(Classic95Setup.IsActive)new Classic95Setup(AeroForge.App.MainWindow.FindContent()).ResumeAsync().GetAwaiter().GetResult();
                    runtime.Start();
                    File.Delete(Path.Combine(root,"resume-error.txt"));
                    JsonFile.Write(Path.Combine(root,"resume-status.json"),new{requestedAt=DateTimeOffset.UtcNow,state="StartRequested",executable=runtime.Executable});
                }
                catch(Exception error)
                {Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"resume-error.txt"),error.ToString());Shutdown(1);return;}
                Shutdown();return;
            }
            if(e.Args.FirstOrDefault()=="--taskbar-properties")
            {ShutdownMode=ShutdownMode.OnExplicitShutdown;using var properties=new NativeTaskbarProperties();System.Windows.Forms.Application.Run(properties);Shutdown();return;}
            if (e.Args.Length == 3 && e.Args[0] == "--render-tool")
            {
                Window tool = e.Args[1] == "--switcher" ? new WindowSwitcher() : new ExtrasWindow(e.Args[1]);
                var content = (FrameworkElement)tool.Content;
                if (content is System.Windows.Controls.Panel panel) panel.Background = tool.Background;
                content.Measure(new Size(tool.Width, tool.Height)); content.Arrange(new Rect(0, 0, tool.Width, tool.Height)); content.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)tool.Width, (int)tool.Height, 96, 96, PixelFormats.Pbgra32);
                var background = new DrawingVisual();
                using (var context = background.RenderOpen()) context.DrawRectangle(tool.Background, null, new Rect(0, 0, tool.Width, tool.Height));
                bitmap.Render(background); bitmap.Render(content);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var target = File.Create(e.Args[2]); encoder.Save(target); Shutdown(); return;
            }
            if (e.Args.FirstOrDefault() == "--switcher") { new WindowSwitcher().Show(); return; }
            if (e.Args.FirstOrDefault() is "--gadgets" or "--media" or "--mines")
            {
                string? native=PrivateExtras.NativeApplication(e.Args[0][2..]);
                if(native!=null){
                    var start=new System.Diagnostics.ProcessStartInfo(native){UseShellExecute=true};
                    if(e.Args[0]=="--gadgets")start.ArgumentList.Add("/showGadgets");
                    System.Diagnostics.Process.Start(start);Shutdown();return;
                }
                new ExtrasWindow(e.Args[0]).Show(); return;
            }
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            using var window=new NativeMainForm();
            bool render=e.Args.FirstOrDefault() is "--render" or "--render-wizard" or "--render-native";
            if(render && e.Args.Length>=2)
            {
                window.Render(e.Args[1],e.Args.Length>=3?int.Parse(e.Args[2]):0);Shutdown();return;
            }
            if(e.Args.FirstOrDefault()=="--native-ui-smoke" && e.Args.Length==2)
            {window.Smoke(e.Args[1]);Shutdown();return;}
            if(e.Args.FirstOrDefault()=="--advanced")window.SelectPage(2);
            System.Windows.Forms.Application.Run(window);
            Shutdown();
        }
        catch (Exception ex) { MessageBox.Show(ex.ToString(), "WinMim startup failed", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
}

