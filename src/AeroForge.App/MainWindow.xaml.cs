using AeroForge.Core;
using System.IO;
namespace AeroForge.App;
public static class MainWindow
{
    public static string FindContent()
    {
        string? current=AppContext.BaseDirectory;
        for(int i=0;i<8 && current!=null;i++,current=Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar)))
            if(File.Exists(Path.Combine(current,"content","catalog.json")))return Path.Combine(current,"content");
        throw new FileNotFoundException(UiText.Get("MainWindow.xaml.ContentCatalogJsonIsMissingBuildThe"));
    }
}
