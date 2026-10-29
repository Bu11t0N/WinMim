using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;

namespace AeroForge.Core;

// Changes only the ThemeSection mapping access in the active lab session.
// No injection into winlogon, no service removal and no hard-coded restore ACL.
[SupportedOSPlatform("windows")]
public sealed class ClassicSession
{
    public static int ActiveSession=>checked((int)WTSGetActiveConsoleSessionId());
    private readonly int session;
    private TransactionManager? store;
    private TransactionManager transactions
    {
        get
        {
            if(store!=null)return store;
            string current=backend.Read(backend.Change).Value!;
            string parent=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"WinMim","ClassicSession",session.ToString());
            string identity=SessionIdentity(current),directory=Path.Combine(parent,identity);
            // Upgrade only records for this same logon object. A new logon creates
            // a new ThemeSection and must never reuse the old object's ACL backup.
            if(Directory.Exists(parent))foreach(string file in Directory.EnumerateFiles(parent,"*.json"))
            {
                var receipt=JsonFile.Read<Receipt>(file);
                if(receipt.Entries.Count!=1 || SessionIdentity(receipt.Entries[0].Before.Value!)!=identity)continue;
                Directory.CreateDirectory(directory);string target=SafePath.Under(directory,Path.GetFileName(file));
                if(!File.Exists(target))try{File.Copy(file,target);}catch(IOException)when(File.Exists(target)){}
            }
            return store=new(new SectionBackend(session),directory);
        }
    }
    public static string SessionIdentity(string descriptor)
    {
        string logon=System.Text.RegularExpressions.Regex.Match(descriptor,@"S-1-5-5-\d+-\d+").Value;
        if(logon.Length==0)throw new InvalidDataException("ThemeSection does not identify its logon session");
        return Hashing.Text(logon)[..16];
    }
    private readonly SectionBackend backend;
    public ClassicSession(int session)
    {
        this.session=session;backend=new(session);

    }
    private void Guard(bool elevated)
    {
        if(Environment.MachineName!="AEROFORGE-LAB" || WindowsBackend.Inspect() is not {Build:19044,Architecture:"X64"} info || elevated&&!info.IsAdministrator || session!=ActiveSession || session<1)
            throw new InvalidOperationException("Classic переключается только в активном сеансе тестовой Windows 10 LTSC 2021.");
    }
    public ValueState Inspect(){Guard(false);return backend.Read(backend.Change);}
    public static string LockReadAccess(string original)
    {
        var descriptor=new RawSecurityDescriptor(original);
        var old=descriptor.DiscretionaryAcl??throw new InvalidDataException("Нет исходного DACL темы.");
        var acl=new RawAcl(old.Revision,old.Count+1);
        acl.InsertAce(0,new CommonAce(AceFlags.None,AceQualifier.AccessDenied,4,new SecurityIdentifier(WellKnownSidType.InteractiveSid,null),false,null));
        for(int i=0;i<old.Count;i++)acl.InsertAce(i+1,old[i]);
        descriptor.DiscretionaryAcl=acl;
        return descriptor.GetSddlForm(AccessControlSections.All);
    }
    public Receipt Apply()
    {
        Guard(true);
        foreach(var active in transactions.History().Where(r=>r.Status!="RolledBack"))
        {
            var current=backend.Read(backend.Change);
            if(active.Entries.Count==1 && current.Fingerprint==active.Entries[0].Before.Fingerprint)transactions.Rollback(active.FilePath);
        }
        var existing=transactions.History().FirstOrDefault(r=>r.Status!="RolledBack");
        if(existing!=null)
        {
            if(existing.Status=="Applied" && backend.Read(backend.Change).Fingerprint==existing.Entries.Single().ExpectedAfter)return existing;
            throw new IOException("Сначала восстановите незавершённое переключение Classic.");
        }
        string desired=LockReadAccess(backend.Read(backend.Change).Value!);
        return transactions.Apply(new Plan{ProfileId="classic-session",ProfileName="Классическая отрисовка",Exclusive=true,Changes=[new(){Kind=ChangeKind.ThemeSection,Target=backend.Change.Target,ValueKind="ThemeSection",Value=desired,Description="Включить классическую отрисовку в текущем сеансе"}]});
    }
    public Receipt? Restore()
    {
        Guard(true);var active=transactions.History(false).FirstOrDefault(r=>r.Status!="RolledBack");
        return active==null?null:transactions.Rollback(active.FilePath);
    }
    public async Task ElevatedAsync(bool restore)
    {
        Guard(false);
        var active=transactions.History().FirstOrDefault(r=>r.Status!="RolledBack");
        if(restore&&active==null)return;
        if(!restore&&active?.Status=="Applied"&&backend.Read(backend.Change).Fingerprint==active.Entries.Single().ExpectedAfter)return;
        string cli=new[]{Path.Combine(AppContext.BaseDirectory,"AeroForge.Cli.exe"),Path.Combine(AppContext.BaseDirectory,"cli","AeroForge.Cli.exe")}.FirstOrDefault(File.Exists)??throw new FileNotFoundException("CLI не найден");
        var start=new ProcessStartInfo(cli){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
        foreach(var argument in new[]{restore?"classic-session-restore":"classic-session-apply","--session",session.ToString(),"--accept-experimental"})start.ArgumentList.Add(argument);
        using var child=Process.Start(start)??throw new IOException("Помощник Classic не запущен");await child.WaitForExitAsync();
        if(child.ExitCode!=0)throw new IOException("Не удалось переключить классическую отрисовку.");
    }
    private sealed class SectionBackend(int session):IChangeBackend
    {
        public Change Change=>new(){Kind=ChangeKind.ThemeSection,Target="Windows.ThemeSection."+session};
        public void Validate(Change change)
        {
            if(change.Kind!=ChangeKind.ThemeSection||change.Target!=Change.Target)throw new InvalidDataException("Неожиданный объект темы");
            if(change.Value!=null)_=new RawSecurityDescriptor(change.Value);
        }
        public ValueState Read(Change change)
        {
            Validate(change);using var section=Open(session,0x20000);
            GetKernelObjectSecurity(section,7,null,0,out uint length);
            if(length is 0 or >65536)throw new Win32Exception(Marshal.GetLastWin32Error());
            var bytes=new byte[length];if(!GetKernelObjectSecurity(section,7,bytes,length,out _))throw new Win32Exception(Marshal.GetLastWin32Error());
            return new(){Exists=true,Kind="ThemeSection",Value=new RawSecurityDescriptor(bytes,0).GetSddlForm(AccessControlSections.All)};
        }
        public void Write(Change change,ValueState state)
        {
            Validate(change);if(!state.Exists||state.Kind!="ThemeSection")throw new InvalidDataException("Нет резервной копии DACL темы");
            var descriptor=new RawSecurityDescriptor(state.Value!);var bytes=new byte[descriptor.BinaryLength];descriptor.GetBinaryForm(bytes,0);
            using var section=Open(session,0x40000);
            if(!SetKernelObjectSecurity(section,4,bytes))throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
    private static SafeFileHandle Open(int session,uint access)
    {
        string name=$@"\Sessions\{session}\Windows\ThemeSection";
        IntPtr buffer=Marshal.StringToHGlobalUni(name),unicode=IntPtr.Zero;
        try
        {
            var str=new UnicodeString{Length=(ushort)(name.Length*2),MaximumLength=(ushort)((name.Length+1)*2),Buffer=buffer};
            unicode=Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());Marshal.StructureToPtr(str,unicode,false);
            var attrs=new ObjectAttributes{Length=Marshal.SizeOf<ObjectAttributes>(),Name=unicode,Attributes=0x40};
            int status=NtOpenSection(out var handle,access,ref attrs);if(status<0)throw new Win32Exception((int)RtlNtStatusToDosError(status));
            return new SafeFileHandle(handle,true);
        }
        finally{if(unicode!=IntPtr.Zero)Marshal.FreeHGlobal(unicode);Marshal.FreeHGlobal(buffer);}
    }
    [StructLayout(LayoutKind.Sequential)]private struct UnicodeString{public ushort Length,MaximumLength;public IntPtr Buffer;}
    [StructLayout(LayoutKind.Sequential)]private struct ObjectAttributes{public int Length;public IntPtr Root,Name;public uint Attributes;public IntPtr SecurityDescriptor,Quality;}
    [DllImport("kernel32.dll")]private static extern uint WTSGetActiveConsoleSessionId();
    [DllImport("ntdll.dll")]private static extern int NtOpenSection(out IntPtr handle,uint access,ref ObjectAttributes attrs);
    [DllImport("ntdll.dll")]private static extern uint RtlNtStatusToDosError(int status);
    [DllImport("advapi32.dll",SetLastError=true)]private static extern bool GetKernelObjectSecurity(SafeFileHandle handle,uint information,byte[]? data,uint length,out uint needed);
    [DllImport("advapi32.dll",SetLastError=true)]private static extern bool SetKernelObjectSecurity(SafeFileHandle handle,uint information,byte[] descriptor);
}
