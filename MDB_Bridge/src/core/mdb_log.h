// ==============================
// MDB Bridge - Shared Logging
// ==============================
// Provides LOG_INFO, LOG_WARN, LOG_ERROR (always on) and
// LOG_DEBUG, LOG_TRACE (debug builds only) across all translation units.
//
// Implementation: header-only with inline statics. The first call to
// mdb_log_message() opens the log file and allocates a console window.

#pragma once

#include <Windows.h>
#include <cstdio>
#include <cstdarg>
#include <cstring>
#include <filesystem>

// ========== Debug/Release Logging Macros ==========

#ifdef MDB_DEBUG
    #define LOG_DEBUG(fmt, ...) mdb_log_message("[DEBUG] " fmt, ##__VA_ARGS__)
    #define LOG_TRACE(fmt, ...) mdb_log_message("[TRACE] " fmt, ##__VA_ARGS__)
#else
    #define LOG_DEBUG(fmt, ...) ((void)0)
    #define LOG_TRACE(fmt, ...) ((void)0)
#endif

#define LOG_ERROR(fmt, ...) mdb_log_message("[ERROR] " fmt, ##__VA_ARGS__)
#define LOG_WARN(fmt, ...)  mdb_log_message("[WARN] " fmt, ##__VA_ARGS__)
#define LOG_INFO(fmt, ...)  mdb_log_message("[INFO] " fmt, ##__VA_ARGS__)

// ========== Implementation ==========

namespace mdb_log_detail {

inline FILE*& log_file() { static FILE* f = nullptr; return f; }
inline bool& console_allocated() { static bool v = false; return v; }

inline void allocate_console() {
    if (console_allocated()) return;

    if (AllocConsole()) {
        FILE* fp;
        freopen_s(&fp, "CONOUT$", "w", stdout);
        freopen_s(&fp, "CONOUT$", "w", stderr);
        freopen_s(&fp, "CONIN$", "r", stdin);

        SetConsoleTitleA("MDB Framework Console");

        HANDLE hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
        // Header in purple/magenta
        SetConsoleTextAttribute(hConsole, FOREGROUND_RED | FOREGROUND_BLUE | FOREGROUND_INTENSITY);
        printf("=== MDB Framework Console ===\n\n");
        // Reset to default gray
        SetConsoleTextAttribute(hConsole, FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);

        console_allocated() = true;
    }
}

} // namespace mdb_log_detail

inline void mdb_log_message(const char* format, ...) {
    mdb_log_detail::allocate_console();

    if (!mdb_log_detail::log_file()) {
        char path[MAX_PATH];
        GetModuleFileNameA(nullptr, path, MAX_PATH);
        std::filesystem::path exe_path(path);
        auto log_path = exe_path.parent_path() / "MDB" / "Logs" / "MDB.log";
        std::filesystem::create_directories(log_path.parent_path());
        mdb_log_detail::log_file() = fopen(log_path.string().c_str(), "a");
    }

    va_list args;
    va_start(args, format);

    // Timestamp
    SYSTEMTIME st;
    GetLocalTime(&st);
    char timestamp[32];
    snprintf(timestamp, sizeof(timestamp), "[%02d:%02d:%02d.%03d] ",
             st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);

    // Log to file
    if (mdb_log_detail::log_file()) {
        va_list file_args;
        va_copy(file_args, args);
        fprintf(mdb_log_detail::log_file(), "%s", timestamp);
        vfprintf(mdb_log_detail::log_file(), format, file_args);
        fprintf(mdb_log_detail::log_file(), "\n");
        fflush(mdb_log_detail::log_file());
        va_end(file_args);
    }

    // Also print to console if available
    if (mdb_log_detail::console_allocated()) {
        HANDLE hConsole = GetStdHandle(STD_OUTPUT_HANDLE);

        // Set color based on log level
        if (strstr(format, "[ERROR]")) {
            SetConsoleTextAttribute(hConsole, FOREGROUND_RED | FOREGROUND_INTENSITY);
        } else if (strstr(format, "[WARN]")) {
            SetConsoleTextAttribute(hConsole, FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_INTENSITY);
        } else {
            SetConsoleTextAttribute(hConsole, FOREGROUND_BLUE | FOREGROUND_INTENSITY);
        }

        va_list con_args;
        va_copy(con_args, args);
        printf("%s", timestamp);
        vprintf(format, con_args);
        printf("\n");
        va_end(con_args);

        // Reset to default gray
        SetConsoleTextAttribute(hConsole, FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
    }

    va_end(args);
}
