// Original WinMim code, GPL-3.0. Windows 10 LTSC 2021 lab profile.
// No r8p implementation is used here.
#include <windows.h>
#include <shellapi.h>
#include <string>
#include <cwctype>

using ExecuteEx = BOOL(WINAPI*)(SHELLEXECUTEINFOW*);
using Execute = HINSTANCE(WINAPI*)(HWND,LPCWSTR,LPCWSTR,LPCWSTR,LPCWSTR,INT);
ExecuteEx originalEx=nullptr;
Execute original=nullptr;
using LoadMenuFn=HMENU(WINAPI*)(HINSTANCE,LPCWSTR);
LoadMenuFn originalLoadMenu=nullptr;
std::wstring controller;
bool useOwnTaskbarDialog=true;

void CleanTaskbarMenu(HMENU menu) {
    for(int position=GetMenuItemCount(menu)-1;position>=0;--position) {
        wchar_t label[256]{};MENUITEMINFOW item{sizeof(item)};
        item.fMask=MIIM_STRING|MIIM_SUBMENU;item.dwTypeData=label;item.cch=256;
        if(!GetMenuItemInfoW(menu,position,TRUE,&item))continue;
        std::wstring text;
        for(wchar_t c:std::wstring(label))if(c!=L'&')text+=std::towlower(c);
        bool modern=text==L"search" || text==L"поиск" || text.find(L"cortana")!=std::wstring::npos
            || text.find(L"кортана")!=std::wstring::npos || text.find(L"task view")!=std::wstring::npos
            || text.find(L"просмотра задач")!=std::wstring::npos || text.find(L"windows ink")!=std::wstring::npos
            || text.find(L"people")!=std::wstring::npos || text.find(L"люди")!=std::wstring::npos
            || text.find(L"news and interests")!=std::wstring::npos || text.find(L"новости и интересы")!=std::wstring::npos;
        if(modern){DeleteMenu(menu,position,MF_BYPOSITION);continue;}
        if(text.find(L"taskbar settings")!=std::wstring::npos || text.find(L"параметры панели задач")!=std::wstring::npos) {
            wchar_t russian[]=L"Свойства",english[]=L"Properties";
            MENUITEMINFOW replacement{sizeof(replacement)};replacement.fMask=MIIM_STRING;
            replacement.dwTypeData=text.find(L"taskbar settings")!=std::wstring::npos?english:russian;
            SetMenuItemInfoW(menu,position,TRUE,&replacement);
        }
        if(item.hSubMenu)CleanTaskbarMenu(item.hSubMenu);
    }
    bool previousSeparator=true;
    for(int position=0;position<GetMenuItemCount(menu);) {
        MENUITEMINFOW item{sizeof(item)};item.fMask=MIIM_FTYPE;
        if(!GetMenuItemInfoW(menu,position,TRUE,&item))break;
        bool separator=(item.fType&MFT_SEPARATOR)!=0;
        if(separator && (previousSeparator || position==GetMenuItemCount(menu)-1)){DeleteMenu(menu,position,MF_BYPOSITION);continue;}
        previousSeparator=separator;++position;
    }
}
HMENU WINAPI LoadMenuHook(HINSTANCE instance,LPCWSTR name) {
    HMENU menu=originalLoadMenu(instance,name);
    if(menu && instance==GetModuleHandleW(L"explorer.exe") && IS_INTRESOURCE(name) && reinterpret_cast<ULONG_PTR>(name)==205)
        CleanTaskbarMenu(menu);
    return menu;
}

struct Destination { LPCWSTR file=nullptr; LPCWSTR arguments=nullptr; };
Destination Route(LPCWSTR file) {
    if(!file)return {};
    std::wstring uri(file);
    for(auto& c:uri)c=std::towlower(c);
    auto query=uri.find(L'?');if(query!=std::wstring::npos)uri.resize(query);
    // Exact routes only. Unknown settings pages retain their Windows behavior.
    if(uri==L"ms-settings:taskbar") {
        if(!useOwnTaskbarDialog)return {};
        if(controller.empty() || GetFileAttributesW(controller.c_str())==INVALID_FILE_ATTRIBUTES)return {};
        return {controller.c_str(),L"--taskbar-properties"};
    }
    if(uri==L"ms-settings:sound")return {L"control.exe",L"mmsys.cpl"};
    if(uri==L"ms-settings:dateandtime")return {L"control.exe",L"timedate.cpl"};
    if(uri==L"ms-settings:network" || uri==L"ms-settings:network-status" || uri==L"ms-settings:network-ethernet" || uri==L"ms-settings:network-wifi")
        return {L"control.exe",L"/name Microsoft.NetworkAndSharingCenter"};
    if(uri==L"ms-settings:appsfeatures")return {L"control.exe",L"appwiz.cpl"};
    if(uri==L"ms-settings:notifications")return {L"explorer.exe",L"shell:::{05D7B0F4-2121-4EFF-BF6B-ED3F69B894D9}"};
    return {};
}
BOOL WINAPI ExecuteExHook(SHELLEXECUTEINFOW* info) {
    if(info && info->lpFile && (!info->lpVerb || !_wcsicmp(info->lpVerb,L"open"))) {
        auto route=Route(info->lpFile);
        if(route.file) {
            auto copy=*info;
            copy.lpFile=route.file;copy.lpParameters=route.arguments;
            // The replacement is a file path, never a caller-supplied PIDL.
            copy.fMask&=~(SEE_MASK_INVOKEIDLIST|SEE_MASK_IDLIST);
            copy.lpIDList=nullptr;
            BOOL result=originalEx(&copy);
            if(result){info->hInstApp=copy.hInstApp;info->hProcess=copy.hProcess;return TRUE;}
        }
    }
    return originalEx(info);
}
HINSTANCE WINAPI ExecuteHook(HWND window,LPCWSTR verb,LPCWSTR file,LPCWSTR arguments,LPCWSTR directory,INT show) {
    auto route=(!verb || !_wcsicmp(verb,L"open"))?Route(file):Destination{};
    if(route.file) {
        HINSTANCE result=original(window,verb,route.file,route.arguments,directory,show);
        if(reinterpret_cast<INT_PTR>(result)>32)return result;
    }
    return original(window,verb,file,arguments,directory,show);
}
BOOL Wh_ModInit() {
    useOwnTaskbarDialog=Wh_GetIntSetting(L"useOwnTaskbarDialog")!=0;
    auto setting=Wh_GetStringSetting(L"controller");controller=setting;Wh_FreeStringSetting(setting);
    // A missing controller only disables the taskbar route; all other exact routes remain useful.
    auto shell=GetModuleHandleW(L"shell32.dll");
    if(!shell)shell=LoadLibraryW(L"shell32.dll");
    if(!shell)return FALSE;
    return Wh_SetFunctionHook(reinterpret_cast<void*>(GetProcAddress(shell,"ShellExecuteExW")),reinterpret_cast<void*>(ExecuteExHook),reinterpret_cast<void**>(&originalEx))
        && Wh_SetFunctionHook(reinterpret_cast<void*>(GetProcAddress(shell,"ShellExecuteW")),reinterpret_cast<void*>(ExecuteHook),reinterpret_cast<void**>(&original))
        && Wh_SetFunctionHook(reinterpret_cast<void*>(LoadMenuW),reinterpret_cast<void*>(LoadMenuHook),reinterpret_cast<void**>(&originalLoadMenu));
}
