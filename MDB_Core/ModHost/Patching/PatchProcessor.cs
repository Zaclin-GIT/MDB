// ==============================
// PatchProcessor - Harmony-like Patch Discovery and Application
// ==============================
// Discovers [Patch], [Prefix], [Postfix] attributes and applies hooks
// Uses the correct IL2CPP calling convention for detours
//
// This file contains the main entry point and shared state.
// Implementation is split across partial classes:
// - PatchDiscovery.cs: Patch attribute discovery and scanning
// - PatchApplication.cs: Hook application and execution
// - PatchSignatureAnalyzer.cs: Method signature analysis
// - PatchDebugger.cs: Debugging utilities

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GameSDK.ModHost.Patching
{
    /// <summary>
    /// Information about an applied patch.
    /// </summary>
    public class PatchInfo
    {
        public Type PatchClass { get; set; }
        public string TargetNamespace { get; set; }
        public string TargetTypeName { get; set; }
        public string TargetMethodName { get; set; }
        public ulong? TargetRva { get; set; }
        public int ParameterCount { get; set; } = -1;
        public bool IsStatic { get; set; }
        
        /// <summary>
        /// Signature string indicating parameter types: P = pointer/int, F = float, D = double
        /// Example: "PFP" = this (P), float param (F), pointer param (P)
        /// </summary>
        public string ParameterSignature { get; set; } = "";
        
        /// <summary>
        /// True if the method returns a float (R4) or double (R8)
        /// </summary>
        public bool ReturnsFloat { get; set; }
        
        /// <summary>
        /// The IL2CPP type enum for the return type (IL2CPP_TYPE_VOID, IL2CPP_TYPE_BOOLEAN, etc.)
        /// </summary>
        public int ReturnTypeEnum { get; set; } = Il2CppBridge.IL2CPP_TYPE_VOID;
        
        /// <summary>
        /// True if the method returns void
        /// </summary>
        public bool ReturnsVoid => ReturnTypeEnum == Il2CppBridge.IL2CPP_TYPE_VOID;
        
        /// <summary>
        /// The IL2CPP MethodInfo pointer for querying parameter types
        /// </summary>
        public IntPtr Il2CppMethod { get; set; }
        
        public MethodInfo PrefixMethod { get; set; }
        public MethodInfo PostfixMethod { get; set; }
        public MethodInfo FinalizerMethod { get; set; }
        
        public IntPtr OriginalMethodPtr { get; set; }
        public Delegate OriginalDelegate { get; set; }
        public long HookHandle { get; set; }
        public bool IsActive => HookHandle > 0;
    }

    /// <summary>
    /// Processes and applies Harmony-style patches from mod assemblies.
    /// Split into partial classes for better organization:
    /// - PatchDiscovery: Discovers and processes patch attributes
    /// - PatchApplication: Applies hooks and manages execution
    /// - PatchSignatureAnalyzer: Analyzes method signatures
    /// - PatchDebugger: Provides debugging utilities
    /// </summary>
    public static partial class PatchProcessor
    {
        // Shared state across all partial classes
        private static readonly Dictionary<Type, List<PatchInfo>> _patchesByClass = new Dictionary<Type, List<PatchInfo>>();
        private static readonly Dictionary<Assembly, List<PatchInfo>> _patchesByAssembly = new Dictionary<Assembly, List<PatchInfo>>();
        private static readonly ModLogger _logger = new ModLogger("PatchProcessor");

        // Keep delegates alive to prevent GC
        private static readonly List<object> _keepAlive = new List<object>();
        
        // Map from hook handle to patch info for detour lookup
        private static readonly Dictionary<long, PatchInfo> _hookToPatch = new Dictionary<long, PatchInfo>();
        
        // Debug flag for verbose logging
        private static bool _debugEnabled = false;
        
        // Track if hooks section header has been shown
        private static bool _hooksHeaderShown = false;

        #region Delegate Types for Different Parameter Counts
        
        // IL2CPP calling convention: args..., MethodInfo*
        // Static 0 params: RetType Method(MethodInfo*)
        // Static 1 param:  RetType Method(arg0, MethodInfo*)
        // Instance 0 params: RetType Method(this, MethodInfo*)
        // Instance 1 param:  RetType Method(this, arg0, MethodInfo*)
        
        // === Standard IntPtr-only delegates ===
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour0(IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour1(IntPtr arg0, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour2(IntPtr arg0, IntPtr arg1, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour3(IntPtr arg0, IntPtr arg1, IntPtr arg2, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour4(IntPtr arg0, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour5(IntPtr arg0, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour6(IntPtr arg0, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4, IntPtr arg5, IntPtr methodInfo);

        // === Float-aware delegates (F = float position, P = pointer/int position) ===
        // Naming: Detour{argCount}_{signature} where signature uses F for float, P for pointer
        // Position 0 is 'this' for instance methods, so floats typically start at position 1+
        
        // 2 args total (instance + 1 float param)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour2_PF(IntPtr arg0, float arg1, IntPtr methodInfo);
        
        // 3 args total (instance + 2 params, various float positions)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour3_PFP(IntPtr arg0, float arg1, IntPtr arg2, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour3_PPF(IntPtr arg0, IntPtr arg1, float arg2, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour3_PFF(IntPtr arg0, float arg1, float arg2, IntPtr methodInfo);
        
        // 4 args total (instance + 3 params)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour4_PFPP(IntPtr arg0, float arg1, IntPtr arg2, IntPtr arg3, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour4_PPFP(IntPtr arg0, IntPtr arg1, float arg2, IntPtr arg3, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour4_PPPF(IntPtr arg0, IntPtr arg1, IntPtr arg2, float arg3, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour4_PFFP(IntPtr arg0, float arg1, float arg2, IntPtr arg3, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour4_PFPF(IntPtr arg0, float arg1, IntPtr arg2, float arg3, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour4_PPFF(IntPtr arg0, IntPtr arg1, float arg2, float arg3, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr Detour4_PFFF(IntPtr arg0, float arg1, float arg2, float arg3, IntPtr methodInfo);

        // === Float return type delegates ===
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate float DetourFloat1(IntPtr arg0, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate float DetourFloat2(IntPtr arg0, IntPtr arg1, IntPtr methodInfo);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate float DetourFloat2_PF(IntPtr arg0, float arg1, IntPtr methodInfo);

        #endregion
    }
}
