// Backend integration probe: run only inside the WinMim lab with its host active.
#include <windows.h>
#include <shellapi.h>
#include <cstdio>
#include <string>
int wmain(int argc,wchar_t** argv) {
    if(argc!=3 && argc!=4)return 2;
    wchar_t computer[64];DWORD length=64;
    if(!GetComputerNameW(computer,&length) || wcscmp(computer,L"AEROFORGE-LAB"))return 3;
    auto deadline=GetTickCount64()+30000;
    bool loaded=false;
    // Windhawk scans newly started user processes asynchronously.
    while(GetTickCount64()<deadline) {
        if(GetModuleHandleW(argv[1])){loaded=true;break;}
        Sleep(250);
    }
    if(!loaded)return 4;
    Sleep(1000);
    if(wcscmp(argv[2],L"--settings")==0) {
        auto module=GetModuleHandleW(argv[1]);
        auto read=reinterpret_cast<int(__cdecl*)(const wchar_t*,...)>(GetProcAddress(module,"_Z16Wh_GetIntSettingPKwz"));
        auto original=reinterpret_cast<void**>(GetProcAddress(module,"CShellBrowser__ShouldShowRibbon_orig"));
        auto flags=reinterpret_cast<unsigned(__cdecl*)()>(GetProcAddress(module,"WinMim_AerexplorerFlags"));
        if(!read)return 6;
        printf("noribbon=%d nopins=%d detailspane=%d ribbonHookResolved=%d\n",read(L"noribbon"),read(L"nopins"),read(L"detailspane"),original&&*original?1:0);
        printf("effectiveFlags=%u\n",flags?flags():~0u);
        if(argc==4) {
            unsigned long rva=wcstoul(argv[3],nullptr,10);
            auto image=reinterpret_cast<const BYTE*>(GetModuleHandleW(L"ExplorerFrame.dll"));
            if(image) {
                auto dos=reinterpret_cast<const IMAGE_DOS_HEADER*>(image);
                auto nt=reinterpret_cast<const IMAGE_NT_HEADERS*>(image+dos->e_lfanew);
                if(rva>0 && rva+8<nt->OptionalHeader.SizeOfImage){unsigned long long head=0;memcpy(&head,image+rva,8);printf("ribbonTargetHead=%016llx\n",head);}
            }
        }
        return 0;
    }
    SHELLEXECUTEINFOW info{sizeof(info)};
    info.fMask=SEE_MASK_NOCLOSEPROCESS;info.lpVerb=L"open";info.lpFile=argv[2];info.nShow=SW_SHOWNORMAL;
    if(!ShellExecuteExW(&info))return 5;
    if(info.hProcess)CloseHandle(info.hProcess);
    if(argc==4 && wcscmp(argv[3],L"--hold")==0)Sleep(45000);
    return 0;
}
