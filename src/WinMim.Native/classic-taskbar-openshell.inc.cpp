// Original WinMim integration, GPL-3.0. Applied to the upstream MIT dialog at build time.
#include <windows.h>
#include <shellapi.h>
#include <string>
static std::wstring WinMimOpenShellPath() {
    wchar_t root[32768];
    DWORD size=GetEnvironmentVariableW(L"ProgramW6432",root,ARRAYSIZE(root));
    if(!size)size=GetEnvironmentVariableW(L"ProgramFiles",root,ARRAYSIZE(root));
    if(!size || size>=ARRAYSIZE(root))return {};
    auto file=std::wstring(root)+L"\\Open-Shell\\StartMenu.exe";
    return GetFileAttributesW(file.c_str())==INVALID_FILE_ATTRIBUTES?std::wstring():file;
}
static bool WinMimOpenShellSettings(HWND parent) {
    auto file=WinMimOpenShellPath();
    return !file.empty() && reinterpret_cast<INT_PTR>(ShellExecuteW(parent,L"open",file.c_str(),L"-settings",nullptr,SW_SHOWNORMAL))>32;
}
static constexpr LPCWSTR WinMimCommands[]={L"CommandShutdown",L"CommandRestart",L"CommandSleep",L"CommandHibernate",L"CommandLogOff",L"CommandLock",L"CommandSwitch"};
static constexpr DWORD WinMimPowerValues[]={2,4,16,64,256,512,1024};
static constexpr LPCWSTR WinMimStartKey=L"Software\\OpenShell\\StartMenu\\Settings";
static DWORD WinMimGetOpenShellPower(DWORD fallback) {
    if(WinMimOpenShellPath().empty())return fallback;
    wchar_t command[128]=L"CommandShutdown";DWORD size=sizeof(command);
    RegGetValueW(HKEY_CURRENT_USER,WinMimStartKey,L"ShutdownCommand",RRF_RT_REG_SZ,nullptr,command,&size);
    for(size_t i=0;i<ARRAYSIZE(WinMimCommands);++i)if(!_wcsicmp(command,WinMimCommands[i]))return WinMimPowerValues[i];
    return fallback;
}
static void WinMimSetOpenShellPower(DWORD value) {
    auto file=WinMimOpenShellPath();if(file.empty() || WinMimGetOpenShellPower(MAXDWORD)==value)return;
    for(size_t i=0;i<ARRAYSIZE(WinMimCommands);++i)if(value==WinMimPowerValues[i]) {
        HKEY key=nullptr;
        if(RegCreateKeyExW(HKEY_CURRENT_USER,WinMimStartKey,0,nullptr,0,KEY_SET_VALUE,nullptr,&key,nullptr)==ERROR_SUCCESS) {
            auto command=WinMimCommands[i];
            RegSetValueExW(key,L"ShutdownCommand",0,REG_SZ,reinterpret_cast<const BYTE*>(command),static_cast<DWORD>((wcslen(command)+1)*sizeof(wchar_t)));
            RegCloseKey(key);
            ShellExecuteW(nullptr,L"open",file.c_str(),L"-reloadsettings",nullptr,SW_HIDE);
        }
        break;
    }
}
