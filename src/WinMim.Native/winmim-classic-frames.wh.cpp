// ==WindhawkMod==
// @id              winmim-classic-frames
// @name            WinMim: classic window frames
// @description     Use native non-client frames in the Classic95 lab session.
// @version         0.1.0
// @author          WinMim contributors
// @license         GPL-3.0
// @include         *
// @exclude         dwm.exe
// @exclude         winlogon.exe
// @exclude         consent.exe
// @exclude         LogonUI.exe
// @architecture    x86
// @architecture    x86-64
// @compilerOptions -ldwmapi -luxtheme
// ==/WindhawkMod==

#include <windows.h>
#include <windhawk_utils.h>
#include <dwmapi.h>
#include <uxtheme.h>
#include <unordered_map>
#include <atomic>

static HANDLE worker,ready;
static DWORD workerId,session;
static std::atomic_bool initialized{false};
static DWORD originalThemeFlags;
// The map is owned exclusively by the event thread.
struct FrameState {DWORD process;bool policyChanged;};
static std::unordered_map<HWND,FrameState> changed;
static BOOL CALLBACK NotifyControl(HWND window,LPARAM){PostMessageW(window,WM_THEMECHANGED,0,0);return TRUE;}
static void Notify(HWND window){PostMessageW(window,WM_THEMECHANGED,0,0);EnumChildWindows(window,NotifyControl,0);}

static void ApplyFrame(HWND window) {
    if(!IsWindowVisible(window)||GetAncestor(window,GA_ROOT)!=window)return;
    if((GetWindowLongPtrW(window,GWL_STYLE)&WS_CAPTION)!=WS_CAPTION)return;
    DWORD process=0,targetSession=0;GetWindowThreadProcessId(window,&process);
    if(process!=GetCurrentProcessId()||!ProcessIdToSessionId(process,&targetSession)||targetSession!=session)return;
    BOOL enabled=FALSE;
    if(FAILED(DwmGetWindowAttribute(window,DWMWA_NCRENDERING_ENABLED,&enabled,sizeof(enabled))))return;
    if(!changed.count(window)) {
        if(IsHungAppWindow(window))return;
        changed[window]={process,false};
    }
    if(!enabled)return;
    DWMNCRENDERINGPOLICY policy=DWMNCRP_DISABLED;
    if(SUCCEEDED(DwmSetWindowAttribute(window,DWMWA_NCRENDERING_POLICY,&policy,sizeof(policy)))) {
        changed[window].policyChanged=true;
        RedrawWindow(window,nullptr,nullptr,RDW_INVALIDATE|RDW_FRAME);
    }
}
static BOOL CALLBACK Visit(HWND window,LPARAM){ApplyFrame(window);if(changed.count(window))Notify(window);return TRUE;}
static void CALLBACK Event(HWINEVENTHOOK,DWORD event,HWND window,LONG object,LONG child,DWORD,DWORD) {
    if(event==EVENT_SYSTEM_FOREGROUND){ApplyFrame(window);return;}
    if(object!=OBJID_WINDOW||child!=CHILDID_SELF)return;
    if(event==EVENT_OBJECT_DESTROY){changed.erase(window);return;}
    if(event==EVENT_OBJECT_SHOW||event==EVENT_OBJECT_CREATE||event==EVENT_OBJECT_LOCATIONCHANGE) {
        if(GetAncestor(window,GA_ROOT)!=window)return;
        ApplyFrame(window);
    }
}
static DWORD WINAPI Watch(void*) {
    MSG message;PeekMessageW(&message,nullptr,WM_USER,WM_USER,PM_NOREMOVE);
    HWINEVENTHOOK windows=SetWinEventHook(EVENT_OBJECT_CREATE,EVENT_OBJECT_LOCATIONCHANGE,nullptr,Event,GetCurrentProcessId(),0,WINEVENT_OUTOFCONTEXT);
    HWINEVENTHOOK foreground=SetWinEventHook(EVENT_SYSTEM_FOREGROUND,EVENT_SYSTEM_FOREGROUND,nullptr,Event,GetCurrentProcessId(),0,WINEVENT_OUTOFCONTEXT);
    initialized=windows&&foreground;SetEvent(ready);
    if(initialized){EnumWindows(Visit,0);while(GetMessageW(&message,nullptr,0,0)>0){TranslateMessage(&message);DispatchMessageW(&message);}}
    if(windows)UnhookWinEvent(windows);
    if(foreground)UnhookWinEvent(foreground);
    SetThemeAppProperties(originalThemeFlags);
    for(const auto& entry:changed) {
        DWORD process=0;GetWindowThreadProcessId(entry.first,&process);
        if(process!=entry.second.process)continue;
        Notify(entry.first);
        if(!entry.second.policyChanged)continue;
        BOOL enabled=TRUE;
        if(FAILED(DwmGetWindowAttribute(entry.first,DWMWA_NCRENDERING_ENABLED,&enabled,sizeof(enabled)))||enabled)continue;
        DWMNCRENDERINGPOLICY policy=DWMNCRP_USEWINDOWSTYLE;
        DwmSetWindowAttribute(entry.first,DWMWA_NCRENDERING_POLICY,&policy,sizeof(policy));
        RedrawWindow(entry.first,nullptr,nullptr,RDW_INVALIDATE|RDW_FRAME);
    }
    changed.clear();return 0;
}
BOOL Wh_ModInit() {
    Wh_Log(L"Starting the lab classic-frame observer");
    WCHAR computer[MAX_COMPUTERNAME_LENGTH+1];DWORD size=ARRAYSIZE(computer);
    if(!GetComputerNameW(computer,&size)||_wcsicmp(computer,L"AEROFORGE-LAB")!=0)return FALSE;
    if(!ProcessIdToSessionId(GetCurrentProcessId(),&session)||session==0)return FALSE;
    const WCHAR* command=GetCommandLineW();
    if(wcsstr(command,L" --type=renderer")||wcsstr(command,L" --type=gpu-process")||wcsstr(command,L" --type=utility"))return FALSE;
    originalThemeFlags=GetThemeAppProperties();
    SetThemeAppProperties(0);
    ready=CreateEventW(nullptr,TRUE,FALSE,nullptr);if(!ready){SetThemeAppProperties(originalThemeFlags);return FALSE;}
    worker=CreateThread(nullptr,0,Watch,nullptr,0,&workerId);
    if(!worker){CloseHandle(ready);ready=nullptr;SetThemeAppProperties(originalThemeFlags);return FALSE;}
    WaitForSingleObject(ready,INFINITE);CloseHandle(ready);ready=nullptr;
    if(!initialized){WaitForSingleObject(worker,INFINITE);CloseHandle(worker);worker=nullptr;return FALSE;}
    return TRUE;
}
void Wh_ModUninit() {
    if(worker){PostThreadMessageW(workerId,WM_QUIT,0,0);WaitForSingleObject(worker,INFINITE);CloseHandle(worker);worker=nullptr;}
}
