using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace AeroForge.Core;

[SupportedOSPlatform("windows")]
public static class WindowsProcessInfo
{
    // Query the process image through the kernel, without enumerating target DLLs.
    // Module enumeration can wait on a loader that is initializing injected code.
    public static string? ImagePath(int processId)
    {
        if(processId<=0)return null;
        using var handle=OpenProcess(0x1000,false,processId); // PROCESS_QUERY_LIMITED_INFORMATION
        if(handle.IsInvalid)return null;
        var path=new StringBuilder(32768);int length=path.Capacity;
        return QueryFullProcessImageName(handle,0,path,ref length)?path.ToString():null;
    }
    [DllImport("kernel32.dll",SetLastError=true)]
    private static extern SafeProcessHandle OpenProcess(uint access,[MarshalAs(UnmanagedType.Bool)] bool inherit,int processId);
    [DllImport("kernel32.dll",EntryPoint="QueryFullProcessImageNameW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process,uint flags,StringBuilder path,ref int length);
}
