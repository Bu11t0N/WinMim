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

// Narrow Windows 2000 counterpart of the archive's AHK Edit/Console fix.
// Off for every other profile. WinEvents drive it; no polling or resizing.
static bool clientEdges;
struct EdgeState { HANDLE token; };
static std::unordered_map<HWND,EdgeState> edges;
static ULONG_PTR nextEdgeToken=1;
static const WCHAR edgeProperty[]=L"WinMim.Classic.ClientEdge";
static bool ClassIs(HWND window,const WCHAR* expected) {
    WCHAR name[128];return GetClassNameW(window,name,ARRAYSIZE(name))&&wcscmp(name,expected)==0;
}
static void ApplyEdge(HWND window) {
    if(!clientEdges||!IsWindowVisible(window))return;
    DWORD process=0;GetWindowThreadProcessId(window,&process);if(process!=GetCurrentProcessId())return;
    HWND root=GetAncestor(window,GA_ROOT);
    bool target=ClassIs(window,L"ConsoleWindowClass") ||
        (ClassIs(root,L"Notepad")&&ClassIs(window,L"Edit")) ||
        (ClassIs(root,L"NotebookFrame")&&ClassIs(window,L"NotebookContent"));
    if(!target)return;
    LONG_PTR style=GetWindowLongPtrW(window,GWL_EXSTYLE);
    if(style&WS_EX_CLIENTEDGE)return;
    auto previous=edges.find(window);
    if(previous!=edges.end()&&GetPropW(window,edgeProperty)==previous->second.token)return;
    HANDLE token=reinterpret_cast<HANDLE>(nextEdgeToken++);
    if(!SetPropW(window,edgeProperty,token))return;
    SetLastError(0);
    if(!SetWindowLongPtrW(window,GWL_EXSTYLE,style|WS_EX_CLIENTEDGE)&&GetLastError()) {RemovePropW(window,edgeProperty);return;}
    edges[window]={token};
    SetWindowPos(window,nullptr,0,0,0,0,SWP_FRAMECHANGED|SWP_NOMOVE|SWP_NOSIZE|SWP_NOZORDER|SWP_NOACTIVATE);
}
static BOOL CALLBACK VisitEdge(HWND window,LPARAM){ApplyEdge(window);return TRUE;}

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
static BOOL CALLBACK Visit(HWND window,LPARAM){ApplyFrame(window);ApplyEdge(window);if(clientEdges)EnumChildWindows(window,VisitEdge,0);if(changed.count(window))Notify(window);return TRUE;}
static void CALLBACK Event(HWINEVENTHOOK,DWORD event,HWND window,LONG object,LONG child,DWORD,DWORD) {
    if(event==EVENT_SYSTEM_FOREGROUND){ApplyFrame(window);return;}
    if(object!=OBJID_WINDOW||child!=CHILDID_SELF)return;
    if(event==EVENT_OBJECT_DESTROY){changed.erase(window);edges.erase(window);return;}
    if(event==EVENT_OBJECT_SHOW||event==EVENT_OBJECT_CREATE||event==EVENT_OBJECT_LOCATIONCHANGE) {
        if(event!=EVENT_OBJECT_LOCATIONCHANGE){ApplyEdge(window);if(clientEdges&&GetAncestor(window,GA_ROOT)==window)EnumChildWindows(window,VisitEdge,0);}
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
    for(const auto& entry:edges) {
        HWND window=entry.first;DWORD process=0;GetWindowThreadProcessId(window,&process);
        if(process!=GetCurrentProcessId()||GetPropW(window,edgeProperty)!=entry.second.token)continue;
        RemovePropW(window,edgeProperty);
        LONG_PTR style=GetWindowLongPtrW(window,GWL_EXSTYLE);
        if(!(style&WS_EX_CLIENTEDGE))continue;
        SetWindowLongPtrW(window,GWL_EXSTYLE,style&~WS_EX_CLIENTEDGE);
        SetWindowPos(window,nullptr,0,0,0,0,SWP_FRAMECHANGED|SWP_NOMOVE|SWP_NOSIZE|SWP_NOZORDER|SWP_NOACTIVATE);
    }
    edges.clear();
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
    clientEdges=Wh_GetIntSetting(L"clientEdges")!=0;
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
