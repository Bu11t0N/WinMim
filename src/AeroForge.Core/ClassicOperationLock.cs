namespace AeroForge.Core;

internal static class ClassicOperationLock
{
    public static async Task<FileStream> AcquireAsync(string data,CancellationToken token=default)
    {
        Directory.CreateDirectory(data);string path=Path.Combine(data,"classic-desktop.lock");
        var deadline=DateTime.UtcNow.AddSeconds(20);
        while(true){
            token.ThrowIfCancellationRequested();
            try{return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
            catch(IOException)when(DateTime.UtcNow<deadline){await Task.Delay(200,token);}
        }
    }
}
