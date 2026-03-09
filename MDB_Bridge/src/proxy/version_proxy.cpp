// ==============================
// Version.dll Proxy - Implementation
// ==============================
// Forwards all 17 version.dll API calls to the real system version.dll.
// The .def file (version.def) maps each public export name to the _impl
// function defined here, so callers see the original API surface.

#include "version_proxy.h"
#include <windows.h>

static HMODULE g_hRealVersion = nullptr;

static void EnsureRealVersionLoaded()
{
    if (g_hRealVersion) return;

    char sysDir[MAX_PATH];
    GetSystemDirectoryA(sysDir, MAX_PATH);
    strcat_s(sysDir, "\\version.dll");
    g_hRealVersion = LoadLibraryA(sysDir);
}

// ---------------------------------------------------------------------------
// Function-pointer typedefs
// ---------------------------------------------------------------------------
typedef BOOL  (WINAPI* fn_GetFileVersionInfoA)(LPCSTR, DWORD, DWORD, LPVOID);
typedef BOOL  (WINAPI* fn_GetFileVersionInfoW)(LPCWSTR, DWORD, DWORD, LPVOID);
typedef BOOL  (WINAPI* fn_GetFileVersionInfoExA)(DWORD, LPCSTR, DWORD, DWORD, LPVOID);
typedef BOOL  (WINAPI* fn_GetFileVersionInfoExW)(DWORD, LPCWSTR, DWORD, DWORD, LPVOID);
typedef DWORD (WINAPI* fn_GetFileVersionInfoSizeA)(LPCSTR, LPDWORD);
typedef DWORD (WINAPI* fn_GetFileVersionInfoSizeW)(LPCWSTR, LPDWORD);
typedef DWORD (WINAPI* fn_GetFileVersionInfoSizeExA)(DWORD, LPCSTR, LPDWORD);
typedef DWORD (WINAPI* fn_GetFileVersionInfoSizeExW)(DWORD, LPCWSTR, LPDWORD);
typedef int   (WINAPI* fn_GetFileVersionInfoByHandle)(DWORD, HANDLE);
typedef DWORD (WINAPI* fn_VerFindFileA)(DWORD, LPCSTR, LPCSTR, LPCSTR, LPSTR, PUINT, LPSTR, PUINT);
typedef DWORD (WINAPI* fn_VerFindFileW)(DWORD, LPCWSTR, LPCWSTR, LPCWSTR, LPWSTR, PUINT, LPWSTR, PUINT);
typedef DWORD (WINAPI* fn_VerInstallFileA)(DWORD, LPCSTR, LPCSTR, LPCSTR, LPCSTR, LPCSTR, LPSTR, PUINT);
typedef DWORD (WINAPI* fn_VerInstallFileW)(DWORD, LPCWSTR, LPCWSTR, LPCWSTR, LPCWSTR, LPCWSTR, LPWSTR, PUINT);
typedef DWORD (WINAPI* fn_VerLanguageNameA)(DWORD, LPSTR, DWORD);
typedef DWORD (WINAPI* fn_VerLanguageNameW)(DWORD, LPWSTR, DWORD);
typedef BOOL  (WINAPI* fn_VerQueryValueA)(LPCVOID, LPCSTR, LPVOID*, PUINT);
typedef BOOL  (WINAPI* fn_VerQueryValueW)(LPCVOID, LPCWSTR, LPVOID*, PUINT);

// ---------------------------------------------------------------------------
// Cached pointers (lazy-resolved on first call)
// ---------------------------------------------------------------------------
static fn_GetFileVersionInfoA         p_GetFileVersionInfoA         = nullptr;
static fn_GetFileVersionInfoW         p_GetFileVersionInfoW         = nullptr;
static fn_GetFileVersionInfoExA       p_GetFileVersionInfoExA       = nullptr;
static fn_GetFileVersionInfoExW       p_GetFileVersionInfoExW       = nullptr;
static fn_GetFileVersionInfoSizeA     p_GetFileVersionInfoSizeA     = nullptr;
static fn_GetFileVersionInfoSizeW     p_GetFileVersionInfoSizeW     = nullptr;
static fn_GetFileVersionInfoSizeExA   p_GetFileVersionInfoSizeExA   = nullptr;
static fn_GetFileVersionInfoSizeExW   p_GetFileVersionInfoSizeExW   = nullptr;
static fn_GetFileVersionInfoByHandle  p_GetFileVersionInfoByHandle  = nullptr;
static fn_VerFindFileA                p_VerFindFileA                = nullptr;
static fn_VerFindFileW                p_VerFindFileW                = nullptr;
static fn_VerInstallFileA             p_VerInstallFileA             = nullptr;
static fn_VerInstallFileW             p_VerInstallFileW             = nullptr;
static fn_VerLanguageNameA            p_VerLanguageNameA            = nullptr;
static fn_VerLanguageNameW            p_VerLanguageNameW            = nullptr;
static fn_VerQueryValueA              p_VerQueryValueA              = nullptr;
static fn_VerQueryValueW              p_VerQueryValueW              = nullptr;

#define RESOLVE(name) \
    do { if (!p_##name) { EnsureRealVersionLoaded(); p_##name = reinterpret_cast<fn_##name>(GetProcAddress(g_hRealVersion, #name)); } } while(0)

// ---------------------------------------------------------------------------
// Proxy implementations  (exported via version.def as the real API names)
// ---------------------------------------------------------------------------

extern "C" BOOL WINAPI GetFileVersionInfoA_impl(LPCSTR f, DWORD h, DWORD len, LPVOID d) {
    RESOLVE(GetFileVersionInfoA);
    return p_GetFileVersionInfoA(f, h, len, d);
}
extern "C" BOOL WINAPI GetFileVersionInfoW_impl(LPCWSTR f, DWORD h, DWORD len, LPVOID d) {
    RESOLVE(GetFileVersionInfoW);
    return p_GetFileVersionInfoW(f, h, len, d);
}
extern "C" BOOL WINAPI GetFileVersionInfoExA_impl(DWORD fl, LPCSTR f, DWORD h, DWORD len, LPVOID d) {
    RESOLVE(GetFileVersionInfoExA);
    return p_GetFileVersionInfoExA(fl, f, h, len, d);
}
extern "C" BOOL WINAPI GetFileVersionInfoExW_impl(DWORD fl, LPCWSTR f, DWORD h, DWORD len, LPVOID d) {
    RESOLVE(GetFileVersionInfoExW);
    return p_GetFileVersionInfoExW(fl, f, h, len, d);
}
extern "C" DWORD WINAPI GetFileVersionInfoSizeA_impl(LPCSTR f, LPDWORD h) {
    RESOLVE(GetFileVersionInfoSizeA);
    return p_GetFileVersionInfoSizeA(f, h);
}
extern "C" DWORD WINAPI GetFileVersionInfoSizeW_impl(LPCWSTR f, LPDWORD h) {
    RESOLVE(GetFileVersionInfoSizeW);
    return p_GetFileVersionInfoSizeW(f, h);
}
extern "C" DWORD WINAPI GetFileVersionInfoSizeExA_impl(DWORD fl, LPCSTR f, LPDWORD h) {
    RESOLVE(GetFileVersionInfoSizeExA);
    return p_GetFileVersionInfoSizeExA(fl, f, h);
}
extern "C" DWORD WINAPI GetFileVersionInfoSizeExW_impl(DWORD fl, LPCWSTR f, LPDWORD h) {
    RESOLVE(GetFileVersionInfoSizeExW);
    return p_GetFileVersionInfoSizeExW(fl, f, h);
}
extern "C" int WINAPI GetFileVersionInfoByHandle_impl(DWORD fl, HANDLE hf) {
    RESOLVE(GetFileVersionInfoByHandle);
    return p_GetFileVersionInfoByHandle(fl, hf);
}
extern "C" DWORD WINAPI VerFindFileA_impl(DWORD fl, LPCSTR fn, LPCSTR wd, LPCSTR ad, LPSTR cd, PUINT cl, LPSTR dd, PUINT dl) {
    RESOLVE(VerFindFileA);
    return p_VerFindFileA(fl, fn, wd, ad, cd, cl, dd, dl);
}
extern "C" DWORD WINAPI VerFindFileW_impl(DWORD fl, LPCWSTR fn, LPCWSTR wd, LPCWSTR ad, LPWSTR cd, PUINT cl, LPWSTR dd, PUINT dl) {
    RESOLVE(VerFindFileW);
    return p_VerFindFileW(fl, fn, wd, ad, cd, cl, dd, dl);
}
extern "C" DWORD WINAPI VerInstallFileA_impl(DWORD fl, LPCSTR sf, LPCSTR df, LPCSTR sd, LPCSTR dd, LPCSTR cd, LPSTR tf, PUINT tl) {
    RESOLVE(VerInstallFileA);
    return p_VerInstallFileA(fl, sf, df, sd, dd, cd, tf, tl);
}
extern "C" DWORD WINAPI VerInstallFileW_impl(DWORD fl, LPCWSTR sf, LPCWSTR df, LPCWSTR sd, LPCWSTR dd, LPCWSTR cd, LPWSTR tf, PUINT tl) {
    RESOLVE(VerInstallFileW);
    return p_VerInstallFileW(fl, sf, df, sd, dd, cd, tf, tl);
}
extern "C" DWORD WINAPI VerLanguageNameA_impl(DWORD lang, LPSTR buf, DWORD sz) {
    RESOLVE(VerLanguageNameA);
    return p_VerLanguageNameA(lang, buf, sz);
}
extern "C" DWORD WINAPI VerLanguageNameW_impl(DWORD lang, LPWSTR buf, DWORD sz) {
    RESOLVE(VerLanguageNameW);
    return p_VerLanguageNameW(lang, buf, sz);
}
extern "C" BOOL WINAPI VerQueryValueA_impl(LPCVOID blk, LPCSTR sub, LPVOID* buf, PUINT len) {
    RESOLVE(VerQueryValueA);
    return p_VerQueryValueA(blk, sub, buf, len);
}
extern "C" BOOL WINAPI VerQueryValueW_impl(LPCVOID blk, LPCWSTR sub, LPVOID* buf, PUINT len) {
    RESOLVE(VerQueryValueW);
    return p_VerQueryValueW(blk, sub, buf, len);
}

// ---------------------------------------------------------------------------
// Cleanup
// ---------------------------------------------------------------------------
void VersionProxy_Cleanup()
{
    if (g_hRealVersion) {
        FreeLibrary(g_hRealVersion);
        g_hRealVersion = nullptr;
    }
}
