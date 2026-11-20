// ==WindhawkMod==
// @id              winmim-classic-frames
// @name            WinMim: classic window frames
// @description     Use native non-client frames in the Classic95 lab session.
// @version         0.3.1
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
static bool basicThemed;
static bool explorerBasic;
static std::atomic_bool unloading{false};
static decltype(&DwmIsCompositionEnabled) compositionOriginal;
static decltype(&IsCompositionActive) themeCompositionOriginal;
static decltype(&DwmExtendFrameIntoClientArea) extendOriginal;
static decltype(&SetWindowTheme) setWindowThemeOriginal;
struct MarginState {DWORD process;MARGINS requested;};
static SRWLOCK marginLock=SRWLOCK_INIT;
static std::unordered_map<HWND,MarginState> margins;
static bool WindowClassIs(HWND window, LPCWSTR expected) {
    WCHAR name[64];
    return GetClassNameW(window,name,ARRAYSIZE(name))&&_wcsicmp(name,expected)==0;
}
static bool ExplorerWindow(HWND window) {
    return WindowClassIs(window,L"CabinetWClass");
}
static HRESULT WINAPI SetNavigationTheme(HWND window,LPCWSTR subApp,LPCWSTR subIds) {
    // The Luna port collapses the themed navigation rebar to zero height.
    // Only the rebar directly under Explorer's frame loses its theme class.
    // Its child bands, the menu rebar and every navigation command stay native.
    if(!unloading && subApp && *subApp && !subIds && WindowClassIs(window,L"ReBarWindow32")) {
        HWND parent=GetParent(window);
        if(WindowClassIs(parent,L"WorkerW") && ExplorerWindow(GetParent(parent)))
            return setWindowThemeOriginal(window,L"",L"");
    }
    return setWindowThemeOriginal(window,subApp,subIds);
}
static HRESULT WINAPI Composition(BOOL* enabled) {
    if(!unloading&&enabled){*enabled=FALSE;return S_OK;}
    return compositionOriginal(enabled);
}
static BOOL WINAPI ThemeComposition(){return unloading?themeCompositionOriginal():FALSE;}
static HRESULT WINAPI Extend(HWND window,const MARGINS* requested) {
    if(unloading||!requested||!ExplorerWindow(window))return extendOriginal(window,requested);
    MARGINS saved=*requested,zero{};HRESULT result=extendOriginal(window,&zero);
    if(SUCCEEDED(result)) {
        DWORD process=0;GetWindowThreadProcessId(window,&process);
        AcquireSRWLockExclusive(&marginLock);margins[window]={process,saved};ReleaseSRWLockExclusive(&marginLock);
    }
    // A success code tells Explorer to keep its custom client caption even
    // with zero margins. Request its basic fallback after clearing the glass.
    return SUCCEEDED(result)?static_cast<HRESULT>(0x80263001L):result;
}
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
        // NCRENDERING_POLICY is write-only. Preserve windows already using
        // basic rendering; restore only our enabled -> disabled changes.
        changed[window]={process,false};
    }
    if(!enabled)return;
    DWMNCRENDERINGPOLICY policy=DWMNCRP_DISABLED;
    if(SUCCEEDED(DwmSetWindowAttribute(window,DWMWA_NCRENDERING_POLICY,&policy,sizeof(policy)))) {
        changed[window].policyChanged=true;
        if(explorerBasic&&ExplorerWindow(window)) {
            PostMessageW(window,WM_DWMCOMPOSITIONCHANGED,0,0);
            SetWindowPos(window,nullptr,0,0,0,0,SWP_NOMOVE|SWP_NOSIZE|SWP_NOZORDER|SWP_NOACTIVATE|SWP_FRAMECHANGED|SWP_ASYNCWINDOWPOS);
        }
        RedrawWindow(window,nullptr,nullptr,RDW_INVALIDATE|RDW_FRAME);
    }
}
static BOOL CALLBACK Visit(HWND window,LPARAM){ApplyFrame(window);if(changed.count(window))Notify(window);return TRUE;}
static void CALLBACK Event(HWINEVENTHOOK,DWORD event,HWND window,LONG object,LONG child,DWORD,DWORD) {
    if(event==EVENT_SYSTEM_FOREGROUND){ApplyFrame(window);return;}
    if(object!=OBJID_WINDOW||child!=CHILDID_SELF)return;
    if(event==EVENT_OBJECT_DESTROY){
        changed.erase(window);
        if(explorerBasic){AcquireSRWLockExclusive(&marginLock);margins.erase(window);ReleaseSRWLockExclusive(&marginLock);}
        return;
    }
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
    if(!basicThemed && GetThemeAppProperties()==0)SetThemeAppProperties(originalThemeFlags);
    for(const auto& entry:changed) {
        DWORD process=0;GetWindowThreadProcessId(entry.first,&process);
        if(process!=entry.second.process)continue;
        Notify(entry.first);
        if(!entry.second.policyChanged)continue;
        BOOL enabled=TRUE;
        if(FAILED(DwmGetWindowAttribute(entry.first,DWMWA_NCRENDERING_ENABLED,&enabled,sizeof(enabled)))||enabled)continue;
        DWMNCRENDERINGPOLICY policy=DWMNCRP_USEWINDOWSTYLE;
        DwmSetWindowAttribute(entry.first,DWMWA_NCRENDERING_POLICY,&policy,sizeof(policy));
        if(explorerBasic&&ExplorerWindow(entry.first))SetWindowPos(entry.first,nullptr,0,0,0,0,SWP_NOMOVE|SWP_NOSIZE|SWP_NOZORDER|SWP_NOACTIVATE|SWP_FRAMECHANGED|SWP_ASYNCWINDOWPOS);
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
    basicThemed=Wh_GetIntSetting(L"basicThemed")!=0;
    WCHAR image[MAX_PATH];DWORD length=GetModuleFileNameW(nullptr,image,ARRAYSIZE(image));
    const WCHAR* file=length?wcsrchr(image,L'\\'):nullptr;
    explorerBasic=basicThemed&&file&&_wcsicmp(file+1,L"explorer.exe")==0;
    if(explorerBasic) {
        // Explorer otherwise keeps an extended transparent client frame even
        // when non-client DWM rendering is disabled. Restrict this compatibility
        // view to Explorer's process; DWM itself and other apps stay composed.
        if(!Wh_SetFunctionHook((void*)DwmIsCompositionEnabled,(void*)Composition,(void**)&compositionOriginal)
            ||!Wh_SetFunctionHook((void*)IsCompositionActive,(void*)ThemeComposition,(void**)&themeCompositionOriginal)
            ||!Wh_SetFunctionHook((void*)DwmExtendFrameIntoClientArea,(void*)Extend,(void**)&extendOriginal)
            ||!Wh_SetFunctionHook((void*)SetWindowTheme,(void*)SetNavigationTheme,(void**)&setWindowThemeOriginal))return FALSE;
    }
    // XP needs UxTheme for both controls and basic non-client painting. Do not
    // change process theme flags or SetWindowTheme in this mode.
    if(!basicThemed)SetThemeAppProperties(0);
    ready=CreateEventW(nullptr,TRUE,FALSE,nullptr);if(!ready){if(!basicThemed)SetThemeAppProperties(originalThemeFlags);return FALSE;}
    worker=CreateThread(nullptr,0,Watch,nullptr,0,&workerId);
    if(!worker){CloseHandle(ready);ready=nullptr;if(!basicThemed)SetThemeAppProperties(originalThemeFlags);return FALSE;}
    WaitForSingleObject(ready,INFINITE);CloseHandle(ready);ready=nullptr;
    if(!initialized){WaitForSingleObject(worker,INFINITE);CloseHandle(worker);worker=nullptr;return FALSE;}
    return TRUE;
}
void Wh_ModUninit() {
    unloading=true;
    if(worker){PostThreadMessageW(workerId,WM_QUIT,0,0);WaitForSingleObject(worker,INFINITE);CloseHandle(worker);worker=nullptr;}
    // Replay the last margins requested by each surviving Explorer window.
    // Never hold a map lock while calling an application-facing API.
    AcquireSRWLockExclusive(&marginLock);auto saved=std::move(margins);ReleaseSRWLockExclusive(&marginLock);
    for(const auto& entry:saved) {
        DWORD process=0;GetWindowThreadProcessId(entry.first,&process);
        if(process!=entry.second.process||!ExplorerWindow(entry.first))continue;
        extendOriginal(entry.first,&entry.second.requested);
        PostMessageW(entry.first,WM_DWMCOMPOSITIONCHANGED,0,0);
    }
}
