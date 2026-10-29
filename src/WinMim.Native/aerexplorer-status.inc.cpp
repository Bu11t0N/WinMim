// Original WinMim diagnostics, GPL-3.0. Appended to pinned upstream at build time.
// Read-only; never changes Explorer settings or windows.
extern "C" __declspec(dllexport) unsigned WinMim_AerexplorerFlags() {
    unsigned value=(settings.noribbon?1u:0u) | (settings.nopins?2u:0u) | (settings.hidetitle?8u:0u);
#ifdef _WIN64
    value|=settings.detailspane?4u:0u;
#endif
    return value;
}
