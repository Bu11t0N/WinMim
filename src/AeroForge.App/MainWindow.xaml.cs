using System.IO;
namespace AeroForge.App;
public static class MainWindow
{
    public static string FindContent()
    {
        string? current=AppContext.BaseDirectory;
        for(int i=0;i<8 && current!=null;i++,current=Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar)))
            if(File.Exists(Path.Combine(current,"content","catalog.json")))return Path.Combine(current,"content");
        throw new FileNotFoundException("Не найден content/catalog.json. Соберите проект командой tools/build.ps1.");
    }
}
