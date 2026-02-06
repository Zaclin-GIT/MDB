// ==============================
// PatchApplication - Hook Application and Execution
// ==============================
// Applies hooks and manages the hook lifecycle including detour creation and patch execution

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GameSDK.ModHost.Patching
{
    /// <summary>
    /// Handles application of hooks and patch execution.
    /// </summary>
    public static partial class PatchProcessor
    {
        /// <summary>
        /// Result from invoking a patch method, including modified __result value.
        /// Used to support HarmonyX-style __result ref parameters.
        /// </summary>
        private struct PatchInvocationResult
        {
            /// <summary>
            /// The return value from the patch method (e.g., bool for Prefix)
            /// </summary>
            public object ReturnValue;
            
            /// <summary>
            /// The modified __result value if the patch had a ref __result parameter
            /// </summary>
            public IntPtr ModifiedResult;
            
            /// <summary>
            /// True if the patch method had a ref __result parameter
            /// </summary>
            public bool HasModifiedResult;
        }

        /// <summary>
        /// Apply a patch to a target method.
        /// </summary>
        private static bool ApplyPatch(PatchInfo patchInfo, string assembly)
        {
            string target = $"{patchInfo.TargetNamespace}.{patchInfo.TargetTypeName}.{patchInfo.TargetMethodName}";
            // Clean up target name - remove leading dots
            target = target.TrimStart('.');

            IntPtr methodPtr = IntPtr.Zero;

            if (patchInfo.TargetRva.HasValue)
            {
                methodPtr = Il2CppBridge.mdb_get_method_pointer_from_rva(patchInfo.TargetRva.Value);
                if (methodPtr == IntPtr.Zero)
                {
                    _logger.Error($"  Failed to resolve RVA 0x{patchInfo.TargetRva.Value:X}");
                    return false;
                }
                _logger.Warning($"  RVA-based hook - cannot detect parameter types, assuming all IntPtr");
            }
            else
            {
                IntPtr klass = FindClass(assembly, patchInfo.TargetNamespace, patchInfo.TargetTypeName);
                if (klass == IntPtr.Zero)
                {
                    _logger.Error($"  Target class not found: {patchInfo.TargetNamespace}.{patchInfo.TargetTypeName}");
                    return false;
                }

                IntPtr method = Il2CppBridge.mdb_get_method(klass, patchInfo.TargetMethodName, patchInfo.ParameterCount);
                if (method == IntPtr.Zero)
                {
                    _logger.Error($"  Target method not found: {patchInfo.TargetMethodName}");
                    return false;
                }

                // Store the IL2CPP method for signature detection
                patchInfo.Il2CppMethod = method;
                
                // Build parameter signature for float-aware detour selection
                _logger.Info($"  Building parameter signature for {patchInfo.TargetMethodName} (paramCount={patchInfo.ParameterCount})...");
                BuildParameterSignature(patchInfo, method);
                _logger.Info($"  Detected signature: '{patchInfo.ParameterSignature}'");

                methodPtr = Il2CppBridge.mdb_get_method_pointer(method);
                if (methodPtr == IntPtr.Zero)
                {
                    _logger.Error($"  Failed to get method pointer for: {patchInfo.TargetMethodName}");
                    return false;
                }
            }

            // Determine total argument count for the detour
            int detourArgCount = patchInfo.ParameterCount;
            if (detourArgCount < 0) detourArgCount = 1; // Default to 1 arg
            
            // Check if there's an __instance parameter to determine if it's an instance method
            bool hasInstance = false;
            if (patchInfo.PrefixMethod != null)
                hasInstance = patchInfo.PrefixMethod.GetParameters().Any(p => p.Name == "__instance");
            else if (patchInfo.PostfixMethod != null)
                hasInstance = patchInfo.PostfixMethod.GetParameters().Any(p => p.Name == "__instance");
            
            patchInfo.IsStatic = !hasInstance;
            
            // Total args to detour: instance (if any) + params
            int totalArgs = (hasInstance ? 1 : 0) + detourArgCount;
            
            // Create and install the hook
            if (!InstallHook(patchInfo, methodPtr, totalArgs, target))
            {
                return false;
            }

            // Show hooks section header on first hook
            if (!_hooksHeaderShown)
            {
                ModLogger.Section("Hooks", ConsoleColor.Magenta);
                _hooksHeaderShown = true;
            }
            
            _logger.Info($"+ {target}", ConsoleColor.Blue);
            return true;
        }

        /// <summary>
        /// Install a hook for a patch.
        /// </summary>
        private static bool InstallHook(PatchInfo patchInfo, IntPtr methodPtr, int totalArgs, string description)
        {
            Delegate detour;
            IntPtr detourPtr;
            
            // Create the appropriate detour based on argument count and signature
            string sig = patchInfo.ParameterSignature;
            
            // Try to use float-aware delegate if signature contains floats
            if (!string.IsNullOrEmpty(sig) && sig.Contains("F"))
                
            {
                var floatDetour = CreateFloatAwareDetour(patchInfo, sig);
                if (floatDetour != null)
                {
                    detour = floatDetour;
                    detourPtr = Marshal.GetFunctionPointerForDelegate(floatDetour);
                    // Continue to hook finalization below
                    goto finalize;
                }
                // Fall through to standard detour if no matching float delegate
            }
            
            // Standard IntPtr-based detour selection
            switch (totalArgs)
            {
                case 0:
                    var d0 = CreateDetour0(patchInfo);
                    detour = d0;
                    detourPtr = Marshal.GetFunctionPointerForDelegate(d0);
                    break;
                case 1:
                    var d1 = CreateDetour1(patchInfo);
                    detour = d1;
                    detourPtr = Marshal.GetFunctionPointerForDelegate(d1);
                    break;
                case 2:
                    var d2 = CreateDetour2(patchInfo);
                    detour = d2;
                    detourPtr = Marshal.GetFunctionPointerForDelegate(d2);
                    break;
                case 3:
                    var d3 = CreateDetour3(patchInfo);
                    detour = d3;
                    detourPtr = Marshal.GetFunctionPointerForDelegate(d3);
                    break;
                case 4:
                    var d4 = CreateDetour4(patchInfo);
                    detour = d4;
                    detourPtr = Marshal.GetFunctionPointerForDelegate(d4);
                    break;
                case 5:
                    var d5 = CreateDetour5(patchInfo);
                    detour = d5;
                    detourPtr = Marshal.GetFunctionPointerForDelegate(d5);
                    break;
                default:
                    var d6 = CreateDetour6(patchInfo);
                    detour = d6;
                    detourPtr = Marshal.GetFunctionPointerForDelegate(d6);
                    break;
            }

        finalize:
            _keepAlive.Add(detour);
            GCHandle handle = GCHandle.Alloc(detour);
            _keepAlive.Add(handle);

            IntPtr originalPtr;
            long hookHandle;
            
            // Use debug hook creation if debugging is enabled
            if (Il2CppBridge.mdb_hook_is_debug_enabled())
            {
                _logger.Info($"  Creating debug hook for {description} with signature: {sig ?? "N/A"}");
                hookHandle = Il2CppBridge.mdb_create_hook_debug(methodPtr, detourPtr, out originalPtr, sig, description);
            }
            else
            {
                // Use mdb_create_hook_ptr since methodPtr is already the function pointer
                hookHandle = Il2CppBridge.mdb_create_hook_ptr(methodPtr, detourPtr, out originalPtr);
            }
            
            if (hookHandle <= 0)
            {
                _logger.Error($"  Failed to create hook: {Il2CppBridge.GetLastError()}");
                handle.Free();
                return false;
            }

            patchInfo.OriginalMethodPtr = originalPtr;
            patchInfo.HookHandle = hookHandle;
            
            // Create a delegate for the original method with matching signature
            if (!string.IsNullOrEmpty(sig) && sig.Contains("F"))
            {
                patchInfo.OriginalDelegate = CreateOriginalDelegateForSignature(sig, originalPtr);
            }
            else
            {
                switch (totalArgs)
                {
                    case 0:
                        patchInfo.OriginalDelegate = Marshal.GetDelegateForFunctionPointer<Detour0>(originalPtr);
                        break;
                    case 1:
                        patchInfo.OriginalDelegate = Marshal.GetDelegateForFunctionPointer<Detour1>(originalPtr);
                        break;
                    case 2:
                        patchInfo.OriginalDelegate = Marshal.GetDelegateForFunctionPointer<Detour2>(originalPtr);
                        break;
                    case 3:
                        patchInfo.OriginalDelegate = Marshal.GetDelegateForFunctionPointer<Detour3>(originalPtr);
                        break;
                    case 4:
                        patchInfo.OriginalDelegate = Marshal.GetDelegateForFunctionPointer<Detour4>(originalPtr);
                        break;
                    case 5:
                        patchInfo.OriginalDelegate = Marshal.GetDelegateForFunctionPointer<Detour5>(originalPtr);
                        break;
                    default:
                        patchInfo.OriginalDelegate = Marshal.GetDelegateForFunctionPointer<Detour6>(originalPtr);
                        break;
                }
            }

            _hookToPatch[hookHandle] = patchInfo;
            return true;
        }

        /// <summary>
        /// Create a float-aware detour delegate based on the parameter signature.
        /// Returns null if no matching delegate type exists.
        /// </summary>
        private static Delegate CreateFloatAwareDetour(PatchInfo patch, string sig)
        {
            switch (sig)
            {
                case "PF": // Instance method with 1 float param
                    return CreateDetour2_PF(patch);
                case "PFP": // Instance method with float, then pointer
                    return CreateDetour3_PFP(patch);
                case "PPF": // Instance method with pointer, then float
                    return CreateDetour3_PPF(patch);
                case "PFF": // Instance method with 2 floats
                    return CreateDetour3_PFF(patch);
                case "PFPP":
                    return CreateDetour4_PFPP(patch);
                case "PPFP":
                    return CreateDetour4_PPFP(patch);
                case "PPPF":
                    return CreateDetour4_PPPF(patch);
                case "PFFP":
                    return CreateDetour4_PFFP(patch);
                case "PFPF":
                    return CreateDetour4_PFPF(patch);
                case "PPFF":
                    return CreateDetour4_PPFF(patch);
                case "PFFF":
                    return CreateDetour4_PFFF(patch);
                default:
                    return null; // No matching float delegate, fall back to standard
            }
        }
        
        /// <summary>
        /// Create an original delegate matching the float signature.
        /// </summary>
        private static Delegate CreateOriginalDelegateForSignature(string sig, IntPtr originalPtr)
        {
            switch (sig)
            {
                case "PF":
                    return Marshal.GetDelegateForFunctionPointer<Detour2_PF>(originalPtr);
                case "PFP":
                    return Marshal.GetDelegateForFunctionPointer<Detour3_PFP>(originalPtr);
                case "PPF":
                    return Marshal.GetDelegateForFunctionPointer<Detour3_PPF>(originalPtr);
                case "PFF":
                    return Marshal.GetDelegateForFunctionPointer<Detour3_PFF>(originalPtr);
                case "PFPP":
                    return Marshal.GetDelegateForFunctionPointer<Detour4_PFPP>(originalPtr);
                case "PPFP":
                    return Marshal.GetDelegateForFunctionPointer<Detour4_PPFP>(originalPtr);
                case "PPPF":
                    return Marshal.GetDelegateForFunctionPointer<Detour4_PPPF>(originalPtr);
                case "PFFP":
                    return Marshal.GetDelegateForFunctionPointer<Detour4_PFFP>(originalPtr);
                case "PFPF":
                    return Marshal.GetDelegateForFunctionPointer<Detour4_PFPF>(originalPtr);
                case "PPFF":
                    return Marshal.GetDelegateForFunctionPointer<Detour4_PPFF>(originalPtr);
                case "PFFF":
                    return Marshal.GetDelegateForFunctionPointer<Detour4_PFFF>(originalPtr);
                default:
                    // Fall back to standard delegate based on length
                    int len = sig?.Length ?? 0;
                    switch (len)
                    {
                        case 0: return Marshal.GetDelegateForFunctionPointer<Detour0>(originalPtr);
                        case 1: return Marshal.GetDelegateForFunctionPointer<Detour1>(originalPtr);
                        case 2: return Marshal.GetDelegateForFunctionPointer<Detour2>(originalPtr);
                        case 3: return Marshal.GetDelegateForFunctionPointer<Detour3>(originalPtr);
                        case 4: return Marshal.GetDelegateForFunctionPointer<Detour4>(originalPtr);
                        case 5: return Marshal.GetDelegateForFunctionPointer<Detour5>(originalPtr);
                        default: return Marshal.GetDelegateForFunctionPointer<Detour6>(originalPtr);
                    }
            }
        }

        #region Detour Creators - Generate detours with proper signatures

        private static Detour0 CreateDetour0(PatchInfo patch)
        {
            return (IntPtr methodInfo) =>
            {
                return ExecutePatch(patch, IntPtr.Zero, Array.Empty<IntPtr>(), methodInfo,
                    (modifiedArgs) => ((Detour0)patch.OriginalDelegate)(methodInfo));
            };
        }

        private static Detour1 CreateDetour1(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr methodInfo) =>
            {
                IntPtr instance = patch.IsStatic ? IntPtr.Zero : arg0;
                IntPtr[] args = patch.IsStatic ? new[] { arg0 } : Array.Empty<IntPtr>();
                
                return ExecutePatch(patch, instance, args, methodInfo,
                    (modifiedArgs) => {
                        if (patch.IsStatic && modifiedArgs.Length > 0)
                            return ((Detour1)patch.OriginalDelegate)(modifiedArgs[0], methodInfo);
                        return ((Detour1)patch.OriginalDelegate)(arg0, methodInfo);
                    });
            };
        }

        private static Detour2 CreateDetour2(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, IntPtr methodInfo) =>
            {
                IntPtr instance = patch.IsStatic ? IntPtr.Zero : arg0;
                IntPtr[] args = patch.IsStatic ? new[] { arg0, arg1 } : new[] { arg1 };
                
                return ExecutePatch(patch, instance, args, methodInfo,
                    (modifiedArgs) => {
                        if (patch.IsStatic && modifiedArgs.Length >= 2)
                            return ((Detour2)patch.OriginalDelegate)(modifiedArgs[0], modifiedArgs[1], methodInfo);
                        else if (!patch.IsStatic && modifiedArgs.Length >= 1)
                            return ((Detour2)patch.OriginalDelegate)(arg0, modifiedArgs[0], methodInfo);
                        return ((Detour2)patch.OriginalDelegate)(arg0, arg1, methodInfo);
                    });
            };
        }

        private static Detour3 CreateDetour3(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, IntPtr arg2, IntPtr methodInfo) =>
            {
                IntPtr instance = patch.IsStatic ? IntPtr.Zero : arg0;
                IntPtr[] args = patch.IsStatic ? new[] { arg0, arg1, arg2 } : new[] { arg1, arg2 };
                
                return ExecutePatch(patch, instance, args, methodInfo,
                    (modifiedArgs) => {
                        if (patch.IsStatic && modifiedArgs.Length >= 3)
                            return ((Detour3)patch.OriginalDelegate)(modifiedArgs[0], modifiedArgs[1], modifiedArgs[2], methodInfo);
                        else if (!patch.IsStatic && modifiedArgs.Length >= 2)
                            return ((Detour3)patch.OriginalDelegate)(arg0, modifiedArgs[0], modifiedArgs[1], methodInfo);
                        return ((Detour3)patch.OriginalDelegate)(arg0, arg1, arg2, methodInfo);
                    });
            };
        }

        private static Detour4 CreateDetour4(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = patch.IsStatic ? IntPtr.Zero : arg0;
                IntPtr[] args = patch.IsStatic ? new[] { arg0, arg1, arg2, arg3 } : new[] { arg1, arg2, arg3 };
                
                return ExecutePatch(patch, instance, args, methodInfo,
                    (modifiedArgs) => {
                        if (patch.IsStatic && modifiedArgs.Length >= 4)
                            return ((Detour4)patch.OriginalDelegate)(modifiedArgs[0], modifiedArgs[1], modifiedArgs[2], modifiedArgs[3], methodInfo);
                        else if (!patch.IsStatic && modifiedArgs.Length >= 3)
                            return ((Detour4)patch.OriginalDelegate)(arg0, modifiedArgs[0], modifiedArgs[1], modifiedArgs[2], methodInfo);
                        return ((Detour4)patch.OriginalDelegate)(arg0, arg1, arg2, arg3, methodInfo);
                    });
            };
        }

        private static Detour5 CreateDetour5(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4, IntPtr methodInfo) =>
            {
                IntPtr instance = patch.IsStatic ? IntPtr.Zero : arg0;
                IntPtr[] args = patch.IsStatic ? new[] { arg0, arg1, arg2, arg3, arg4 } : new[] { arg1, arg2, arg3, arg4 };
                
                return ExecutePatch(patch, instance, args, methodInfo,
                    (modifiedArgs) => {
                        if (patch.IsStatic && modifiedArgs.Length >= 5)
                            return ((Detour5)patch.OriginalDelegate)(modifiedArgs[0], modifiedArgs[1], modifiedArgs[2], modifiedArgs[3], modifiedArgs[4], methodInfo);
                        else if (!patch.IsStatic && modifiedArgs.Length >= 4)
                            return ((Detour5)patch.OriginalDelegate)(arg0, modifiedArgs[0], modifiedArgs[1], modifiedArgs[2], modifiedArgs[3], methodInfo);
                        return ((Detour5)patch.OriginalDelegate)(arg0, arg1, arg2, arg3, arg4, methodInfo);
                    });
            };
        }

        private static Detour6 CreateDetour6(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4, IntPtr arg5, IntPtr methodInfo) =>
            {
                IntPtr instance = patch.IsStatic ? IntPtr.Zero : arg0;
                IntPtr[] args = patch.IsStatic 
                    ? new[] { arg0, arg1, arg2, arg3, arg4, arg5 } 
                    : new[] { arg1, arg2, arg3, arg4, arg5 };
                
                return ExecutePatch(patch, instance, args, methodInfo,
                    (modifiedArgs) => {
                        if (patch.IsStatic && modifiedArgs.Length >= 6)
                            return ((Detour6)patch.OriginalDelegate)(modifiedArgs[0], modifiedArgs[1], modifiedArgs[2], modifiedArgs[3], modifiedArgs[4], modifiedArgs[5], methodInfo);
                        else if (!patch.IsStatic && modifiedArgs.Length >= 5)
                            return ((Detour6)patch.OriginalDelegate)(arg0, modifiedArgs[0], modifiedArgs[1], modifiedArgs[2], modifiedArgs[3], modifiedArgs[4], methodInfo);
                        return ((Detour6)patch.OriginalDelegate)(arg0, arg1, arg2, arg3, arg4, arg5, methodInfo);
                    });
            };
        }

        // === Float-aware detour creators ===
        
        private static Detour2_PF CreateDetour2_PF(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    (modArgs) => ((Detour2_PF)patch.OriginalDelegate)(arg0, (float)modArgs[0], methodInfo));
            };
        }
        
        private static Detour3_PFP CreateDetour3_PFP(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, IntPtr arg2, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    (modArgs) => ((Detour3_PFP)patch.OriginalDelegate)(arg0, (float)modArgs[0], (IntPtr)modArgs[1], methodInfo));
            };
        }
        
        private static Detour3_PPF CreateDetour3_PPF(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, float arg2, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    (modArgs) => ((Detour3_PPF)patch.OriginalDelegate)(arg0, (IntPtr)modArgs[0], (float)modArgs[1], methodInfo));
            };
        }
        
        private static Detour3_PFF CreateDetour3_PFF(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, float arg2, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    (modArgs) => ((Detour3_PFF)patch.OriginalDelegate)(arg0, (float)modArgs[0], (float)modArgs[1], methodInfo));
            };
        }
        
        private static Detour4_PFPP CreateDetour4_PFPP(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, IntPtr arg2, IntPtr arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    (modArgs) => ((Detour4_PFPP)patch.OriginalDelegate)(arg0, (float)modArgs[0], (IntPtr)modArgs[1], (IntPtr)modArgs[2], methodInfo));
            };
        }
        
        private static Detour4_PPFP CreateDetour4_PPFP(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, float arg2, IntPtr arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    (modArgs) => ((Detour4_PPFP)patch.OriginalDelegate)(arg0, (IntPtr)modArgs[0], (float)modArgs[1], (IntPtr)modArgs[2], methodInfo));
            };
        }
        
        private static Detour4_PPPF CreateDetour4_PPPF(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, IntPtr arg2, float arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    (modArgs) => ((Detour4_PPPF)patch.OriginalDelegate)(arg0, (IntPtr)modArgs[0], (IntPtr)modArgs[1], (float)modArgs[2], methodInfo));
            };
        }
        
        private static Detour4_PFFP CreateDetour4_PFFP(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, float arg2, IntPtr arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    (modArgs) => ((Detour4_PFFP)patch.OriginalDelegate)(arg0, (float)modArgs[0], (float)modArgs[1], (IntPtr)modArgs[2], methodInfo));
            };
        }
        
        private static Detour4_PFPF CreateDetour4_PFPF(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, IntPtr arg2, float arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    (modArgs) => ((Detour4_PFPF)patch.OriginalDelegate)(arg0, (float)modArgs[0], (IntPtr)modArgs[1], (float)modArgs[2], methodInfo));
            };
        }
        
        private static Detour4_PPFF CreateDetour4_PPFF(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, float arg2, float arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    (modArgs) => ((Detour4_PPFF)patch.OriginalDelegate)(arg0, (IntPtr)modArgs[0], (float)modArgs[1], (float)modArgs[2], methodInfo));
            };
        }
        
        private static Detour4_PFFF CreateDetour4_PFFF(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, float arg2, float arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    (modArgs) => ((Detour4_PFFF)patch.OriginalDelegate)(arg0, (float)modArgs[0], (float)modArgs[1], (float)modArgs[2], methodInfo));
            };
        }

        #endregion

        #region Patch Execution

        /// <summary>
        /// Execute the patch logic (prefix, original, postfix, finalizer).
        /// All exception handling is done here - mod code doesn't need try/catch.
        /// Implements HarmonyX-style prefix semantics:
        /// - Prefix returning false skips the original method
        /// - Prefix can set __result via ref parameter when skipping original
        /// </summary>
        private static IntPtr ExecutePatch(PatchInfo patch, IntPtr instance, IntPtr[] args, IntPtr methodInfo, Func<IntPtr[], IntPtr> callOriginal)
        {
            bool runOriginal = true;
            IntPtr result = IntPtr.Zero;
            Exception caughtException = null;

            try
            {
                // Call prefix
                if (patch.PrefixMethod != null)
                {
                    var prefixInvocation = InvokePatchMethodEx(patch.PrefixMethod, instance, args, IntPtr.Zero, null);
                    
                    // Check if prefix wants to skip original
                    if (prefixInvocation.ReturnValue is bool b && !b)
                    {
                        runOriginal = false;
                        
                        // If prefix set __result via ref parameter, use that as the return value
                        if (prefixInvocation.HasModifiedResult)
                        {
                            result = prefixInvocation.ModifiedResult;
                        }
                        // Otherwise result stays as IntPtr.Zero (default)
                    }
                }

                // Call original with potentially modified args
                if (runOriginal && patch.OriginalDelegate != null)
                {
                    result = callOriginal(args);
                }

                // Call postfix (receives the actual result, whether from original or prefix)
                if (patch.PostfixMethod != null)
                {
                    var postfixInvocation = InvokePatchMethodEx(patch.PostfixMethod, instance, args, result, null);
                    
                    // Postfix can also modify __result via ref parameter
                    if (postfixInvocation.HasModifiedResult)
                    {
                        result = postfixInvocation.ModifiedResult;
                    }
                }
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Call finalizer (always runs)
            if (patch.FinalizerMethod != null)
            {
                try
                {
                    object finalizerResult = InvokePatchMethod(patch.FinalizerMethod, instance, args, result, caughtException);
                    if (finalizerResult is Exception newEx)
                        caughtException = newEx;
                    else if (finalizerResult == null)
                        caughtException = null;
                }
                catch (Exception finalizerEx)
                {
                    caughtException = finalizerEx;
                }
            }

            // Log any unhandled exception but don't crash the game
            if (caughtException != null)
            {
                ModLogger.LogInternal("PatchProcessor", $"[ERROR] Exception in patch {patch.PatchClass.Name}: {caughtException.Message}");
            }

            return result;
        }

        /// <summary>
        /// Execute the patch logic with float-aware argument handling.
        /// Args is object[] that may contain floats directly instead of IntPtrs.
        /// Implements HarmonyX-style prefix semantics:
        /// - Prefix returning false skips the original method
        /// - Prefix can set __result via ref parameter when skipping original
        /// </summary>
        private static IntPtr ExecutePatchWithFloats(PatchInfo patch, IntPtr instance, object[] args, IntPtr methodInfo, Func<object[], IntPtr> callOriginal)
        {
            bool runOriginal = true;
            IntPtr result = IntPtr.Zero;
            Exception caughtException = null;

            try
            {
                // Call prefix
                if (patch.PrefixMethod != null)
                {
                    var prefixInvocation = InvokePatchMethodWithFloatsEx(patch.PrefixMethod, instance, args, IntPtr.Zero, null);
                    
                    // Check if prefix wants to skip original
                    if (prefixInvocation.ReturnValue is bool b && !b)
                    {
                        runOriginal = false;
                        
                        // If prefix set __result via ref parameter, use that as the return value
                        if (prefixInvocation.HasModifiedResult)
                        {
                            result = prefixInvocation.ModifiedResult;
                        }
                        // Otherwise result stays as IntPtr.Zero (default)
                    }
                }

                // Call original with potentially modified args
                if (runOriginal && patch.OriginalDelegate != null)
                {
                    result = callOriginal(args);
                }

                // Call postfix (receives the actual result, whether from original or prefix)
                if (patch.PostfixMethod != null)
                {
                    var postfixInvocation = InvokePatchMethodWithFloatsEx(patch.PostfixMethod, instance, args, result, null);
                    
                    // Postfix can also modify __result via ref parameter
                    if (postfixInvocation.HasModifiedResult)
                    {
                        result = postfixInvocation.ModifiedResult;
                    }
                }
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Call finalizer (always runs)
            if (patch.FinalizerMethod != null)
            {
                try
                {
                    object finalizerResult = InvokePatchMethodWithFloats(patch.FinalizerMethod, instance, args, result, caughtException);
                    if (finalizerResult is Exception newEx)
                        caughtException = newEx;
                    else if (finalizerResult == null)
                        caughtException = null;
                }
                catch (Exception finalizerEx)
                {
                    caughtException = finalizerEx;
                }
            }

            // Log any unhandled exception but don't crash the game
            if (caughtException != null)
            {
                ModLogger.LogInternal("PatchProcessor", $"[ERROR] Exception in patch {patch.PatchClass.Name}: {caughtException.Message}");
            }

            return result;
        }

        #endregion

        #region Patch Method Invocation

        /// <summary>
        /// Invoke a patch method, automatically converting parameters to the expected types.
        /// Supports ref parameters - modified values are written back to args array.
        /// Returns PatchInvocationResult which includes modified __result for HarmonyX compatibility.
        /// </summary>
        private static PatchInvocationResult InvokePatchMethodEx(MethodInfo method, IntPtr instance, IntPtr[] args, IntPtr result, Exception exception)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object[] invokeArgs = new object[parameters.Length];
            
            // Track which parameters map to which args indices for ref writeback
            int[] argIndexMapping = new int[parameters.Length];
            for (int i = 0; i < argIndexMapping.Length; i++)
                argIndexMapping[i] = -1;
            
            // Track __result parameter index for writeback
            int resultParamIndex = -1;
            bool resultIsRef = false;

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo param = parameters[i];
                string name = param.Name;
                Type ptype = param.ParameterType;
                
                // Handle ref/out parameters - get the element type
                Type elementType = ptype.IsByRef ? ptype.GetElementType() : ptype;

                try
                {
                    if (name == "__instance")
                    {
                        invokeArgs[i] = ConvertToType(instance, elementType);
                    }
                    else if (name == "__result")
                    {
                        invokeArgs[i] = ConvertToType(result, elementType);
                        resultParamIndex = i;
                        resultIsRef = ptype.IsByRef;
                    }
                    else if (name == "__exception")
                    {
                        invokeArgs[i] = exception;
                    }
                    else if (name.StartsWith("__") && int.TryParse(name.Substring(2), out int argIndex))
                    {
                        if (argIndex >= 0 && argIndex < args.Length)
                        {
                            invokeArgs[i] = ConvertToType(args[argIndex], elementType);
                            argIndexMapping[i] = argIndex; // Track for writeback
                        }
                        else
                        {
                            invokeArgs[i] = GetDefault(elementType);
                        }
                    }
                    else
                    {
                        invokeArgs[i] = GetDefault(elementType);
                    }
                }
                catch
                {
                    invokeArgs[i] = GetDefault(elementType);
                }
            }

            try
            {
                object returnValue = method.Invoke(null, invokeArgs);
                
                // Write back ref parameters to args array
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType.IsByRef && argIndexMapping[i] >= 0)
                    {
                        int argIndex = argIndexMapping[i];
                        object modifiedValue = invokeArgs[i];
                        
                        // Convert modified value back to IntPtr for the args array
                        args[argIndex] = ConvertToIntPtr(modifiedValue);
                    }
                }
                
                // Build result with potential __result modification
                var invocationResult = new PatchInvocationResult
                {
                    ReturnValue = returnValue,
                    HasModifiedResult = resultIsRef && resultParamIndex >= 0,
                    ModifiedResult = IntPtr.Zero
                };
                
                // If __result was a ref parameter, extract the modified value
                if (invocationResult.HasModifiedResult)
                {
                    invocationResult.ModifiedResult = ConvertToIntPtr(invokeArgs[resultParamIndex]);
                }
                
                return invocationResult;
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }
        
        /// <summary>
        /// Invoke a patch method, automatically converting parameters to the expected types.
        /// Supports ref parameters - modified values are written back to args array.
        /// </summary>
        private static object InvokePatchMethod(MethodInfo method, IntPtr instance, IntPtr[] args, IntPtr result, Exception exception)
        {
            return InvokePatchMethodEx(method, instance, args, result, exception).ReturnValue;
        }

        /// <summary>
        /// Invoke a patch method with float-aware argument handling.
        /// Args is object[] that may contain floats directly instead of IntPtrs.
        /// Returns PatchInvocationResult which includes modified __result for HarmonyX compatibility.
        /// </summary>
        private static PatchInvocationResult InvokePatchMethodWithFloatsEx(MethodInfo method, IntPtr instance, object[] args, IntPtr result, Exception exception)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object[] invokeArgs = new object[parameters.Length];
            
            // Track __result parameter index for writeback
            int resultParamIndex = -1;
            bool resultIsRef = false;

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo param = parameters[i];
                string name = param.Name;
                Type ptype = param.ParameterType;
                
                // Handle ref/out parameters - get the element type
                Type elementType = ptype.IsByRef ? ptype.GetElementType() : ptype;

                try
                {
                    if (name == "__instance")
                    {
                        invokeArgs[i] = ConvertToType(instance, elementType);
                    }
                    else if (name == "__result")
                    {
                        invokeArgs[i] = ConvertToType(result, elementType);
                        resultParamIndex = i;
                        resultIsRef = ptype.IsByRef;
                    }
                    else if (name == "__exception")
                    {
                        invokeArgs[i] = exception;
                    }
                    else if (name.StartsWith("__") && int.TryParse(name.Substring(2), out int argIndex))
                    {
                        if (argIndex >= 0 && argIndex < args.Length)
                        {
                            object arg = args[argIndex];
                            // If arg is already the correct type (e.g., float), use it directly
                            if (arg != null && elementType.IsAssignableFrom(arg.GetType()))
                            {
                                invokeArgs[i] = arg;
                            }
                            else if (arg is IntPtr ptr)
                            {
                                invokeArgs[i] = ConvertToType(ptr, elementType);
                            }
                            else if (arg is float f && elementType == typeof(float))
                            {
                                invokeArgs[i] = f;
                            }
                            else if (arg is double d && elementType == typeof(double))
                            {
                                invokeArgs[i] = d;
                            }
                            else
                            {
                                invokeArgs[i] = GetDefault(elementType);
                            }
                        }
                        else
                        {
                            invokeArgs[i] = GetDefault(elementType);
                        }
                    }
                    else
                    {
                        invokeArgs[i] = GetDefault(elementType);
                    }
                }
                catch
                {
                    invokeArgs[i] = GetDefault(elementType);
                }
            }

            try
            {
                object returnValue = method.Invoke(null, invokeArgs);
                
                // Build result with potential __result modification
                var invocationResult = new PatchInvocationResult
                {
                    ReturnValue = returnValue,
                    HasModifiedResult = resultIsRef && resultParamIndex >= 0,
                    ModifiedResult = IntPtr.Zero
                };
                
                // If __result was a ref parameter, extract the modified value
                if (invocationResult.HasModifiedResult)
                {
                    invocationResult.ModifiedResult = ConvertToIntPtr(invokeArgs[resultParamIndex]);
                }
                
                return invocationResult;
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }
        
        /// <summary>
        /// Invoke a patch method with float-aware argument handling.
        /// Args is object[] that may contain floats directly instead of IntPtrs.
        /// </summary>
        private static object InvokePatchMethodWithFloats(MethodInfo method, IntPtr instance, object[] args, IntPtr result, Exception exception)
        {
            return InvokePatchMethodWithFloatsEx(method, instance, args, result, exception).ReturnValue;
        }

        #endregion
    }
}
