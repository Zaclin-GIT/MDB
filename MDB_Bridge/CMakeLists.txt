# ==============================================================================
# MDB_Bridge - Unified IL2CPP Modding Bridge (DLL)
# ==============================================================================
# Single-DLL solution containing:
#   - IL2CPP runtime dumper
#   - C# wrapper generator
#   - MSBuild automation trigger
#   - CLR hosting & mod loading
#   - ImGui overlay (DX11 + Win32)
#   - MinHook for API hooking
# ==============================================================================

# ------------------------------------------------------------------------------
# MinHook (vendored, compiled from source)
# ------------------------------------------------------------------------------
add_library(MinHook STATIC
    minhook/src/buffer.c
    minhook/src/hook.c
    minhook/src/trampoline.c
    minhook/src/hde/hde64.c    # x64 only — no 32-bit support
)

target_include_directories(MinHook PUBLIC
    minhook/include
)

set_target_properties(MinHook PROPERTIES
    FOLDER "ThirdParty"
    POSITION_INDEPENDENT_CODE ON
)

# Disable warnings for third-party code
target_compile_options(MinHook PRIVATE /W0)

# ------------------------------------------------------------------------------
# Dear ImGui (vendored, compiled from source)
# ------------------------------------------------------------------------------
add_library(ImGui STATIC
    # Core
    imgui/imgui.cpp
    imgui/imgui_draw.cpp
    imgui/imgui_tables.cpp
    imgui/imgui_widgets.cpp
    imgui/imgui_demo.cpp

    # Platform/Renderer backends (DX11 + Win32)
    imgui/backends/imgui_impl_win32.cpp
    imgui/backends/imgui_impl_dx11.cpp
)

target_include_directories(ImGui PUBLIC
    imgui
    imgui/backends
)

set_target_properties(ImGui PROPERTIES
    FOLDER "ThirdParty"
    POSITION_INDEPENDENT_CODE ON
)

# Disable warnings for third-party code
target_compile_options(ImGui PRIVATE /W0)

# ------------------------------------------------------------------------------
# MDB_Bridge DLL
# ------------------------------------------------------------------------------
add_library(MDB_Bridge SHARED
    # DLL entry point & CLR hosting
    dllmain.cpp

    # P/Invoke bridge exports
    bridge_exports.cpp

    # IL2CPP dumper (integrated from MDB_Dumper)
    il2cpp_dumper.cpp

    # C# wrapper generator (C++ port of wrapper_generator.py)
    wrapper_generator.cpp

    # MSBuild automation
    build_trigger.cpp

    # ImGui integration layer
    imgui_integration.cpp
    imgui_exports.cpp
)

# Public headers (for reference / IDE indexing)
target_sources(MDB_Bridge PRIVATE
    bridge_exports.h
    il2cpp_resolver.hpp
    il2cpp_dumper.hpp
    wrapper_generator.hpp
    build_trigger.hpp
    imgui_integration.h
    imgui_exports.h
)

# MDB_BRIDGE_EXPORTS drives __declspec(dllexport) in bridge_exports.h
target_compile_definitions(MDB_Bridge PRIVATE
    MDB_BRIDGE_EXPORTS
    _WINDOWS
    _USRDLL
    $<$<CONFIG:Debug>:_DEBUG>
    $<$<CONFIG:Release>:NDEBUG>
)

# Include directories
target_include_directories(MDB_Bridge PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}                          # Project root (bridge_exports.h, etc.)
    ${CMAKE_CURRENT_SOURCE_DIR}/../Archive/MDB_Dumper    # Legacy IL2CPP API headers
)

# Link internal libraries
target_link_libraries(MDB_Bridge PRIVATE
    MDB_Common   # Shared IL2CPP type definitions
    MinHook      # API hooking
    ImGui        # Dear ImGui overlay
)

# System libraries
target_link_libraries(MDB_Bridge PRIVATE
    mscoree      # .NET CLR hosting (ICLRRuntimeHost)
    d3d11        # DirectX 11 (ImGui backend)
    d3d12        # DirectX 12 (future backend support)
    dxgi         # DXGI (swap chain hooking)
    d3dcompiler  # Shader compilation
    psapi        # Process status API (module enumeration)
)

# MSVC-specific options
if(MSVC)
    target_compile_options(MDB_Bridge PRIVATE
        /W3                                              # Warning level 3
        /permissive-                                     # Strict conformance mode
        $<$<CONFIG:Release>:/O2>                         # Full optimization
        $<$<CONFIG:Release>:/GL>                         # Whole program optimization
        $<$<CONFIG:Release>:/Oi>                         # Intrinsic functions
        $<$<CONFIG:Release>:/Gy>                         # Function-level linking
    )

    target_link_options(MDB_Bridge PRIVATE
        $<$<CONFIG:Release>:/LTCG>                       # Link-time code generation
        $<$<CONFIG:Release>:/OPT:REF>                    # Eliminate unreferenced data
        $<$<CONFIG:Release>:/OPT:ICF>                    # Identical COMDAT folding
    )
endif()

# Generate debug info for both configurations
set_target_properties(MDB_Bridge PROPERTIES
    # Output naming
    OUTPUT_NAME "MDB_Bridge"

    # Debug info
    MSVC_DEBUG_INFORMATION_FORMAT "$<$<CONFIG:Debug>:ProgramDatabase>$<$<CONFIG:Release>:ProgramDatabase>"

    # Runtime library (/MDd for Debug, /MD for Release)
    MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL"

    # Windows subsystem
    WIN32_EXECUTABLE OFF
)
