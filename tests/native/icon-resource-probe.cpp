// WinMim integration test: real extraction APIs, never injects or changes settings itself.
#include <windows.h>
#include <shellapi.h>
#include <cstdio>
#include <string>
#include <vector>
#include <cstdint>
static BOOL CALLBACK FirstGroup(HMODULE,LPCWSTR,LPWSTR name,LONG_PTR value) {
    if(IS_INTRESOURCE(name)){*reinterpret_cast<unsigned*>(value)=LOWORD(name);return FALSE;}return TRUE;
}
static uint64_t Pixels(HICON icon) {
    BITMAPINFO info{};info.bmiHeader.biSize=sizeof(BITMAPINFOHEADER);info.bmiHeader.biWidth=32;info.bmiHeader.biHeight=-32;
    info.bmiHeader.biPlanes=1;info.bmiHeader.biBitCount=32;info.bmiHeader.biCompression=BI_RGB;
    void* pixels=nullptr;HDC dc=CreateCompatibleDC(nullptr);HBITMAP bitmap=CreateDIBSection(dc,&info,DIB_RGB_COLORS,&pixels,nullptr,0);
    if(!dc || !bitmap){if(bitmap)DeleteObject(bitmap);if(dc)DeleteDC(dc);return 0;}
    HGDIOBJ previous=SelectObject(dc,bitmap);memset(pixels,0,32*32*4);DrawIconEx(dc,0,0,icon,32,32,0,nullptr,DI_NORMAL);GdiFlush();
    uint64_t hash=14695981039346656037ull;for(unsigned i=0;i<32*32*4;i++){hash^=static_cast<unsigned char*>(pixels)[i];hash*=1099511628211ull;}
    SelectObject(dc,previous);DeleteObject(bitmap);DeleteDC(dc);return hash;
}
int wmain(int argc,wchar_t** argv) {
    wchar_t computer[64];DWORD length=64;
    if(argc!=2 || !GetComputerNameW(computer,&length) || wcscmp(computer,L"AEROFORGE-LAB"))return 2;
    ULONGLONG deadline=GetTickCount64()+30000;
    while(!GetModuleHandleW(L"aeroforge-icon-resource-redirect_1.3-winmim1.dll")) {if(GetTickCount64()>deadline)return 3;Sleep(100);}
    Sleep(2000);
    wchar_t windows[MAX_PATH];GetWindowsDirectoryW(windows,MAX_PATH);
    std::wstring folder=argv[1];WIN32_FIND_DATAW file{};HANDLE find=FindFirstFileW((folder+L"\\*.dll").c_str(),&file);
    if(find==INVALID_HANDLE_VALUE)return 4;
    auto aer=GetModuleHandleW(L"aeroforge-aerexplorer_1.8.2-winmim1.dll");
    auto flags=aer?reinterpret_cast<unsigned(__cdecl*)()>(GetProcAddress(aer,"WinMim_AerexplorerFlags")):nullptr;
    auto frame=reinterpret_cast<const BYTE*>(GetModuleHandleW(L"ExplorerFrame.dll"));unsigned long long head=0;
    // RVA from this lab's pinned 19044.1288 SymbolCache, never searched over the network.
    if(frame){auto dos=reinterpret_cast<const IMAGE_DOS_HEADER*>(frame);auto nt=reinterpret_cast<const IMAGE_NT_HEADERS*>(frame+dos->e_lfanew);if(nt->OptionalHeader.SizeOfImage>338192)memcpy(&head,frame+338184,8);}
    int checked=0,failed=0,skipped=0;printf("{\"aerLoaded\":%s,\"aerFlags\":%u,\"ribbonHead\":\"%016llx\",\"icons\":[",aer?"true":"false",flags?flags():0,head);bool comma=false;
    do {
        std::wstring resource=folder+L"\\"+file.cFileName,target=file.cFileName;
        if(target.ends_with(L".exe.dll")||target.ends_with(L".cpl.dll"))target.resize(target.size()-4);
        std::wstring original=std::wstring(windows)+L"\\System32\\"+target;
        if(target==L"explorer.exe"||target==L"regedit.exe")original=std::wstring(windows)+L"\\"+target;
        if(target==L"basebrd.dll")original=std::wstring(windows)+L"\\Branding\\Basebrd\\basebrd.dll";
        if(GetFileAttributesW(original.c_str())==INVALID_FILE_ATTRIBUTES){skipped++;continue;}
        HMODULE data=LoadLibraryExW(resource.c_str(),nullptr,LOAD_LIBRARY_AS_DATAFILE|LOAD_LIBRARY_AS_IMAGE_RESOURCE);
        unsigned id=0;if(data){EnumResourceNamesW(data,RT_GROUP_ICON,FirstGroup,reinterpret_cast<LONG_PTR>(&id));FreeLibrary(data);}
        if(!id){skipped++;continue;}
        HICON actual=nullptr,expected=nullptr;UINT actualId=0,expectedId=0;
        PrivateExtractIconsW(original.c_str(),-static_cast<int>(id),32,32,&actual,&actualId,1,0);
        PrivateExtractIconsW(resource.c_str(),-static_cast<int>(id),32,32,&expected,&expectedId,1,0);
        uint64_t a=actual?Pixels(actual):0,e=expected?Pixels(expected):0;bool match=a&&e&&a==e;
        if(actual)DestroyIcon(actual);if(expected)DestroyIcon(expected);
        if(comma)printf(",");comma=true;
        printf("{\"library\":\"%ls\",\"id\":%u,\"actual\":\"%016llx\",\"expected\":\"%016llx\",\"match\":%s}",file.cFileName,id,a,e,match?"true":"false");
        checked++;if(!match)failed++;
    }while(FindNextFileW(find,&file));FindClose(find);
    printf("],\"checked\":%d,\"failed\":%d,\"skipped\":%d}\n",checked,failed,skipped);
    return checked<15 || failed?1:0;
}
