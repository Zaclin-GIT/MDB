# ==============================================================================
# MDB_Common - Shared IL2CPP Headers (Interface Library)
# ==============================================================================
# Header-only library providing common IL2CPP type definitions, signatures,
# and pattern scanning used by both MDB_Bridge and (legacy) MDB_Dumper.
# ==============================================================================

add_library(MDB_Common INTERFACE)

target_include_directories(MDB_Common INTERFACE
    ${CMAKE_CURRENT_SOURCE_DIR}/IL2CPP
)
