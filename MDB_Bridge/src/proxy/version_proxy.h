// ==============================
// Version.dll Proxy - Header
// ==============================
// When MDB_Bridge.dll is renamed to version.dll, these exports forward
// all 17 version.dll API calls to the real system version.dll so the
// game functions normally while our DLL is loaded via proxy.

#pragma once

/// Call during DLL_PROCESS_DETACH to free the real version.dll handle.
void VersionProxy_Cleanup();
