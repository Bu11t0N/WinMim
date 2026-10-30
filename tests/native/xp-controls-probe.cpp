// Visual acceptance fixture, launched only in the dedicated test VM.
// Both architectures use the same native controls and explicit comctl32 v6.
#include <windows.h>
#include <commctrl.h>
#include <uxtheme.h>
#include <dwmapi.h>
#include <cstdio>

static HWND button;
static void Report(HWND window) {
    BOOL enabled=TRUE;int policy=0;
    HRESULT nc=DwmGetWindowAttribute(window,DWMWA_NCRENDERING_ENABLED,&enabled,sizeof(enabled));
    HRESULT hr=DwmGetWindowAttribute(window,DWMWA_NCRENDERING_POLICY,&policy,sizeof(policy));
    wchar_t path[MAX_PATH];GetModuleFileNameW(nullptr,path,MAX_PATH);wcscat_s(path,L".json");
    char data[1024];int length=snprintf(data,sizeof(data),"{\"bits\":%d,\"flags\":%lu,\"themeActive\":%d,\"appThemed\":%d,\"buttonThemed\":%d,\"ncEnabled\":%d,\"ncHr\":%ld,\"policy\":%d,\"policyHr\":%ld}",int(sizeof(void*)*8),GetThemeAppProperties(),IsThemeActive(),IsAppThemed(),GetWindowTheme(button)!=nullptr,enabled,nc,policy,hr);
    HANDLE file=CreateFileW(path,GENERIC_WRITE,FILE_SHARE_READ,nullptr,CREATE_ALWAYS,0,nullptr);
    if(file!=INVALID_HANDLE_VALUE){DWORD written;WriteFile(file,data,length,&written,nullptr);CloseHandle(file);}
}
static LRESULT CALLBACK WindowProc(HWND window,UINT message,WPARAM wParam,LPARAM lParam) {
    if(message==WM_TIMER){Report(window);KillTimer(window,1);return 0;}
    if(message==WM_DESTROY){PostQuitMessage(0);return 0;}
    return DefWindowProcW(window,message,wParam,lParam);
}
int WINAPI wWinMain(HINSTANCE instance,HINSTANCE,PWSTR,int) {
    wchar_t computer[64];DWORD size=64;
    if(!GetComputerNameW(computer,&size)||wcscmp(computer,L"AEROFORGE-LAB"))return 2;
    SetProcessDPIAware();
    HDC screen=GetDC(nullptr);int dpi=GetDeviceCaps(screen,LOGPIXELSX);ReleaseDC(nullptr,screen);
    auto Scale=[&](int value){return MulDiv(value,dpi,96);};
    wchar_t manifest[MAX_PATH];GetModuleFileNameW(nullptr,manifest,MAX_PATH);wcscat_s(manifest,L".manifest");
    ACTCTXW context{sizeof(context)};context.lpSource=manifest;HANDLE activation=CreateActCtxW(&context);ULONG_PTR cookie=0;
    if(activation==INVALID_HANDLE_VALUE||!ActivateActCtx(activation,&cookie))return 3;
    INITCOMMONCONTROLSEX controls{sizeof(controls),ICC_WIN95_CLASSES|ICC_PROGRESS_CLASS};InitCommonControlsEx(&controls);
    WNDCLASSW type{};type.lpfnWndProc=WindowProc;type.hInstance=instance;type.lpszClassName=L"WinMimXpControls";type.hCursor=LoadCursorW(nullptr,IDC_ARROW);type.hbrBackground=(HBRUSH)(COLOR_BTNFACE+1);RegisterClassW(&type);
    HWND window=CreateWindowW(type.lpszClassName,sizeof(void*)==8?L"Проверка Luna — x64":L"Проверка Luna — x86",WS_OVERLAPPEDWINDOW,Scale(sizeof(void*)==8?50:600),Scale(100),Scale(520),Scale(440),nullptr,nullptr,instance,nullptr);
    HFONT font=CreateFontW(-Scale(11),0,0,0,FW_NORMAL,FALSE,FALSE,FALSE,DEFAULT_CHARSET,OUT_DEFAULT_PRECIS,CLIP_DEFAULT_PRECIS,NONANTIALIASED_QUALITY,DEFAULT_PITCH,L"Tahoma");
    auto Child=[&](const wchar_t* cls,const wchar_t* text,DWORD style,int x,int y,int width,int height){
        HWND child=CreateWindowW(cls,text,WS_CHILD|WS_VISIBLE|style,Scale(x),Scale(y),Scale(width),Scale(height),window,nullptr,instance,nullptr);
        SendMessageW(child,WM_SETFONT,(WPARAM)font,TRUE);return child;
    };
    button=Child(L"BUTTON",L"Кнопка",BS_PUSHBUTTON,20,20,110,28);
    EnableWindow(Child(L"BUTTON",L"Недоступно",BS_PUSHBUTTON,150,20,110,28),FALSE);
    Child(L"BUTTON",L"Флажок",BS_AUTOCHECKBOX,20,65,130,24);
    Child(L"BUTTON",L"Вариант",BS_AUTORADIOBUTTON,170,65,130,24);
    Child(L"EDIT",L"Поле ввода",WS_BORDER|ES_AUTOHSCROLL,20,105,260,25);
    HWND tab=Child(WC_TABCONTROLW,L"",0,20,150,460,100);TCITEMW item{};item.mask=TCIF_TEXT;item.pszText=(PWSTR)L"Общие";TabCtrl_InsertItem(tab,0,&item);item.pszText=(PWSTR)L"Настройка";TabCtrl_InsertItem(tab,1,&item);
    HWND progress=Child(PROGRESS_CLASSW,L"",0,20,275,270,24);SendMessageW(progress,PBM_SETPOS,65,0);
    HWND list=Child(L"LISTBOX",L"",WS_BORDER|WS_VSCROLL,310,265,170,85);SendMessageW(list,LB_ADDSTRING,0,(LPARAM)L"Первый элемент");SendMessageW(list,LB_ADDSTRING,0,(LPARAM)L"Второй элемент");
    Child(L"SCROLLBAR",L"",SBS_HORZ,20,325,270,18);
    // A backend launcher can supply SW_HIDE for the first ShowWindow call.
    SetTimer(window,1,6000,nullptr);ShowWindow(window,SW_SHOWNORMAL);ShowWindow(window,SW_SHOWNORMAL);UpdateWindow(window);
    MSG message;while(GetMessageW(&message,nullptr,0,0)>0){TranslateMessage(&message);DispatchMessageW(&message);}
    DeleteObject(font);DeactivateActCtx(0,cookie);ReleaseActCtx(activation);return 0;
}
