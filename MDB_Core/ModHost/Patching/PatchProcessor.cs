// ==============================
// PatchProcessor - Harmony-like Patch Discovery and Application
// ==============================
// Discovers [Patch], [Prefix], [Postfix] attributes and applies hooks
// Uses the correct IL2CPP calling convention for detours

using System;
using System.Collections.Generic;
using System.Linq;
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
    /// </summary>
    public static class PatchProcessor
    {
        private static readonly Dictionary<Type, List<PatchInfo>> _patchesByClass = new Dictionary<Type, List<PatchInfo>>();
        private static readonly Dictionary<Assembly, List<PatchInfo>> _patchesByAssembly = new Dictionary<Assembly, List<PatchInfo>>();
        private static readonly ModLogger _logger = new ModLogger("PatchProcessor");

        // Keep delegates alive to prevent GC
        private static readonly List<object> _keepAlive = new List<object>();
        
        // Map from hook handle to patch info for detour lookup
        private static readonly Dictionary<long, PatchInfo> _hookToPatch = new Dictionary<long, PatchInfo>();

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
        
        private static bool _hooksHeaderShown = false;

        /// <summary>
        /// Process and apply all patches from an assembly.
        /// </summary>
        public static int ProcessAssembly(Assembly assembly)
        {
            List<PatchInfo> appliedPatches = new List<PatchInfo>();

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            foreach (Type type in types)
            {
                try
                {
                    ProcessPatchClass(type, appliedPatches);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to process patch class {type.Name}: {ex.Message}");
                }
            }

            if (appliedPatches.Count > 0)
            {
                _patchesByAssembly[assembly] = appliedPatches;
            }

            return appliedPatches.Count;
        }

        private static void ProcessPatchClass(Type patchClass, List<PatchInfo> appliedPatches)
        {
            PatchAttribute[] patchAttrs = patchClass.GetCustomAttributes<PatchAttribute>().ToArray();
            if (patchAttrs.Length == 0)
                return;

            PatchMethodAttribute patchMethodAttr = patchClass.GetCustomAttribute<PatchMethodAttribute>();
            PatchRvaAttribute patchRvaAttr = patchClass.GetCustomAttribute<PatchRvaAttribute>();

            string targetNs = null;
            string targetType = null;
            string targetMethod = null;
            string targetAssembly = "Assembly-CSharp";

            foreach (PatchAttribute attr in patchAttrs)
            {
                if (attr.TargetType != null)
                {
                    targetNs = attr.Namespace;
                    targetType = attr.TypeName;
                    targetAssembly = attr.Assembly;
                }
                else if (attr.TypeName != null && string.IsNullOrEmpty(attr.MethodName))
                {
                    targetNs = attr.Namespace;
                    targetType = attr.TypeName;
                    targetAssembly = attr.Assembly;
                }
                else if (!string.IsNullOrEmpty(attr.MethodName))
                {
                    targetMethod = attr.MethodName;
                }
            }

            if (patchMethodAttr != null)
            {
                targetMethod = patchMethodAttr.MethodName;
            }

            if (string.IsNullOrEmpty(targetType))
            {
                _logger.Warning($"Patch class {patchClass.Name} has no target type specified");
                return;
            }

            if (string.IsNullOrEmpty(targetMethod) && patchRvaAttr == null)
            {
                _logger.Warning($"Patch class {patchClass.Name} has no target method specified");
                return;
            }

            MethodInfo prefixMethod = FindPatchMethod(patchClass, typeof(PrefixAttribute));
            MethodInfo postfixMethod = FindPatchMethod(patchClass, typeof(PostfixAttribute));
            MethodInfo finalizerMethod = FindPatchMethod(patchClass, typeof(FinalizerAttribute));

            if (prefixMethod == null && postfixMethod == null && finalizerMethod == null)
            {
                _logger.Warning($"Patch class {patchClass.Name} has no [Prefix], [Postfix], or [Finalizer] methods");
                return;
            }

            int paramCount = patchMethodAttr?.ParameterCount ?? -1;
            
            // Infer parameter count from patch method if not specified
            if (paramCount < 0 && prefixMethod != null)
            {
                paramCount = InferParameterCount(prefixMethod);
            }
            if (paramCount < 0 && postfixMethod != null)
            {
                paramCount = InferParameterCount(postfixMethod);
            }

            PatchInfo patchInfo = new PatchInfo
            {
                PatchClass = patchClass,
                TargetNamespace = targetNs ?? "",
                TargetTypeName = targetType,
                TargetMethodName = targetMethod,
                TargetRva = patchRvaAttr?.Rva,
                ParameterCount = paramCount,
                PrefixMethod = prefixMethod,
                PostfixMethod = postfixMethod,
                FinalizerMethod = finalizerMethod
            };

            if (ApplyPatch(patchInfo, targetAssembly))
            {
                appliedPatches.Add(patchInfo);

                if (!_patchesByClass.ContainsKey(patchClass))
                    _patchesByClass[patchClass] = new List<PatchInfo>();
                _patchesByClass[patchClass].Add(patchInfo);
            }
        }

        /// <summary>
        /// Infer the number of parameters from a patch method's __N parameters.
        /// </summary>
        private static int InferParameterCount(MethodInfo method)
        {
            int maxIndex = -1;
            foreach (var param in method.GetParameters())
            {
                if (param.Name.StartsWith("__") && param.Name != "__instance" && 
                    param.Name != "__result" && param.Name != "__exception")
                {
                    if (int.TryParse(param.Name.Substring(2), out int idx))
                    {
                        if (idx > maxIndex) maxIndex = idx;
                    }
                }
            }
            return maxIndex + 1;  // __0 means 1 param, __1 means 2 params, etc.
        }

        private static MethodInfo FindPatchMethod(Type patchClass, Type attributeType)
        {
            return patchClass.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.GetCustomAttribute(attributeType) != null);
        }

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
                BuildParameterSignature(patchInfo, method);

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

        private static IntPtr FindClass(string assembly, string ns, string typeName)
        {
            IntPtr klass = Il2CppBridge.mdb_find_class(assembly, ns, typeName);
            if (klass != IntPtr.Zero) return klass;

            string[] assemblies = { "Assembly-CSharp", "UnityEngine.CoreModule", "UnityEngine", "mscorlib" };
            foreach (string asm in assemblies)
            {
                klass = Il2CppBridge.mdb_find_class(asm, ns, typeName);
                if (klass != IntPtr.Zero) return klass;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Build the parameter signature string for a method by querying IL2CPP type info.
        /// Signature uses: P = pointer/int, F = float, D = double
        /// For instance methods, first character is always 'P' for 'this' pointer.
        /// </summary>
        private static void BuildParameterSignature(PatchInfo patchInfo, IntPtr method)
        {
            var sig = new System.Text.StringBuilder();
            
            // Check if there's an __instance parameter to determine if it's an instance method
            bool hasInstance = false;
            if (patchInfo.PrefixMethod != null)
                hasInstance = patchInfo.PrefixMethod.GetParameters().Any(p => p.Name == "__instance");
            else if (patchInfo.PostfixMethod != null)
                hasInstance = patchInfo.PostfixMethod.GetParameters().Any(p => p.Name == "__instance");
            
            // Instance methods have 'this' pointer as first arg
            if (hasInstance)
            {
                sig.Append('P');
            }
            
            // Query each parameter's type
            int paramCount = patchInfo.ParameterCount >= 0 ? patchInfo.ParameterCount : 0;
            for (int i = 0; i < paramCount; i++)
            {
                IntPtr paramType = Il2CppBridge.mdb_method_get_param_type(method, i);
                if (paramType == IntPtr.Zero)
                {
                    sig.Append('P'); // Default to pointer if we can't get type
                    continue;
                }
                
                int typeEnum = Il2CppBridge.mdb_type_get_type_enum(paramType);
                switch (typeEnum)
                {
                    case Il2CppBridge.IL2CPP_TYPE_R4: // float
                        sig.Append('F');
                        break;
                    case Il2CppBridge.IL2CPP_TYPE_R8: // double
                        sig.Append('D');
                        break;
                    default:
                        sig.Append('P'); // int, pointer, object, etc.
                        break;
                }
            }
            
            patchInfo.ParameterSignature = sig.ToString();
            
            // Check return type
            IntPtr returnType = Il2CppBridge.mdb_method_get_return_type(method);
            if (returnType != IntPtr.Zero)
            {
                int returnTypeEnum = Il2CppBridge.mdb_type_get_type_enum(returnType);
                patchInfo.ReturnsFloat = (returnTypeEnum == Il2CppBridge.IL2CPP_TYPE_R4 || 
                                          returnTypeEnum == Il2CppBridge.IL2CPP_TYPE_R8);
            }
        }

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
            // Use mdb_create_hook_ptr since methodPtr is already the function pointer
            long hookHandle = Il2CppBridge.mdb_create_hook_ptr(methodPtr, detourPtr, out originalPtr);
            
            if (hookHandle <= 0)
            {
                _logger.Error($"  Failed to create hook: {Il2CppBridge.GetLastError()}");
                handle.Free();
                return false;
            }

            patchInfo.OriginalMethodPtr = originalPtr;
            patchInfo.HookHandle = hookHandle;
            
            // Create a delegate for the original method with matching signature
            // For float-aware delegates, we need to match the signature
            // Note: sig is already declared above in this method
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
                    () => ((Detour0)patch.OriginalDelegate)(methodInfo));
            };
        }

        private static Detour1 CreateDetour1(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr methodInfo) =>
            {
                IntPtr instance = patch.IsStatic ? IntPtr.Zero : arg0;
                IntPtr[] args = patch.IsStatic ? new[] { arg0 } : Array.Empty<IntPtr>();
                
                return ExecutePatch(patch, instance, args, methodInfo,
                    () => ((Detour1)patch.OriginalDelegate)(arg0, methodInfo));
            };
        }

        private static Detour2 CreateDetour2(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, IntPtr methodInfo) =>
            {
                IntPtr instance = patch.IsStatic ? IntPtr.Zero : arg0;
                IntPtr[] args = patch.IsStatic ? new[] { arg0, arg1 } : new[] { arg1 };
                
                return ExecutePatch(patch, instance, args, methodInfo,
                    () => ((Detour2)patch.OriginalDelegate)(arg0, arg1, methodInfo));
            };
        }

        private static Detour3 CreateDetour3(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, IntPtr arg2, IntPtr methodInfo) =>
            {
                IntPtr instance = patch.IsStatic ? IntPtr.Zero : arg0;
                IntPtr[] args = patch.IsStatic ? new[] { arg0, arg1, arg2 } : new[] { arg1, arg2 };
                
                return ExecutePatch(patch, instance, args, methodInfo,
                    () => ((Detour3)patch.OriginalDelegate)(arg0, arg1, arg2, methodInfo));
            };
        }

        private static Detour4 CreateDetour4(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = patch.IsStatic ? IntPtr.Zero : arg0;
                IntPtr[] args = patch.IsStatic ? new[] { arg0, arg1, arg2, arg3 } : new[] { arg1, arg2, arg3 };
                
                return ExecutePatch(patch, instance, args, methodInfo,
                    () => ((Detour4)patch.OriginalDelegate)(arg0, arg1, arg2, arg3, methodInfo));
            };
        }

        private static Detour5 CreateDetour5(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, IntPtr arg2, IntPtr arg3, IntPtr arg4, IntPtr methodInfo) =>
            {
                IntPtr instance = patch.IsStatic ? IntPtr.Zero : arg0;
                IntPtr[] args = patch.IsStatic ? new[] { arg0, arg1, arg2, arg3, arg4 } : new[] { arg1, arg2, arg3, arg4 };
                
                return ExecutePatch(patch, instance, args, methodInfo,
                    () => ((Detour5)patch.OriginalDelegate)(arg0, arg1, arg2, arg3, arg4, methodInfo));
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
                    () => ((Detour6)patch.OriginalDelegate)(arg0, arg1, arg2, arg3, arg4, arg5, methodInfo));
            };
        }

        // === Float-aware detour creators ===
        
        private static Detour2_PF CreateDetour2_PF(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1 }; // Float captured correctly!
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    () => ((Detour2_PF)patch.OriginalDelegate)(arg0, arg1, methodInfo));
            };
        }
        
        private static Detour3_PFP CreateDetour3_PFP(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, IntPtr arg2, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    () => ((Detour3_PFP)patch.OriginalDelegate)(arg0, arg1, arg2, methodInfo));
            };
        }
        
        private static Detour3_PPF CreateDetour3_PPF(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, float arg2, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    () => ((Detour3_PPF)patch.OriginalDelegate)(arg0, arg1, arg2, methodInfo));
            };
        }
        
        private static Detour3_PFF CreateDetour3_PFF(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, float arg2, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    () => ((Detour3_PFF)patch.OriginalDelegate)(arg0, arg1, arg2, methodInfo));
            };
        }
        
        private static Detour4_PFPP CreateDetour4_PFPP(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, IntPtr arg2, IntPtr arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    () => ((Detour4_PFPP)patch.OriginalDelegate)(arg0, arg1, arg2, arg3, methodInfo));
            };
        }
        
        private static Detour4_PPFP CreateDetour4_PPFP(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, float arg2, IntPtr arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    () => ((Detour4_PPFP)patch.OriginalDelegate)(arg0, arg1, arg2, arg3, methodInfo));
            };
        }
        
        private static Detour4_PPPF CreateDetour4_PPPF(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, IntPtr arg2, float arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    () => ((Detour4_PPPF)patch.OriginalDelegate)(arg0, arg1, arg2, arg3, methodInfo));
            };
        }
        
        private static Detour4_PFFP CreateDetour4_PFFP(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, float arg2, IntPtr arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    () => ((Detour4_PFFP)patch.OriginalDelegate)(arg0, arg1, arg2, arg3, methodInfo));
            };
        }
        
        private static Detour4_PFPF CreateDetour4_PFPF(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, IntPtr arg2, float arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    () => ((Detour4_PFPF)patch.OriginalDelegate)(arg0, arg1, arg2, arg3, methodInfo));
            };
        }
        
        private static Detour4_PPFF CreateDetour4_PPFF(PatchInfo patch)
        {
            return (IntPtr arg0, IntPtr arg1, float arg2, float arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    () => ((Detour4_PPFF)patch.OriginalDelegate)(arg0, arg1, arg2, arg3, methodInfo));
            };
        }
        
        private static Detour4_PFFF CreateDetour4_PFFF(PatchInfo patch)
        {
            return (IntPtr arg0, float arg1, float arg2, float arg3, IntPtr methodInfo) =>
            {
                IntPtr instance = arg0;
                object[] args = new object[] { arg1, arg2, arg3 };
                
                return ExecutePatchWithFloats(patch, instance, args, methodInfo,
                    () => ((Detour4_PFFF)patch.OriginalDelegate)(arg0, arg1, arg2, arg3, methodInfo));
            };
        }

        #endregion

        /// <summary>
        /// Execute the patch logic (prefix, original, postfix, finalizer).
        /// All exception handling is done here - mod code doesn't need try/catch.
        /// </summary>
        private static IntPtr ExecutePatch(PatchInfo patch, IntPtr instance, IntPtr[] args, IntPtr methodInfo, Func<IntPtr> callOriginal)
        {
            bool runOriginal = true;
            IntPtr result = IntPtr.Zero;
            Exception caughtException = null;

            try
            {
                // Call prefix
                if (patch.PrefixMethod != null)
                {
                    object prefixResult = InvokePatchMethod(patch.PrefixMethod, instance, args, IntPtr.Zero, null);
                    if (prefixResult is bool b && !b)
                    {
                        runOriginal = false;
                    }
                }

                // Call original
                if (runOriginal && patch.OriginalDelegate != null)
                {
                    result = callOriginal();
                }

                // Call postfix
                if (patch.PostfixMethod != null)
                {
                    InvokePatchMethod(patch.PostfixMethod, instance, args, result, null);
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
        /// </summary>
        private static IntPtr ExecutePatchWithFloats(PatchInfo patch, IntPtr instance, object[] args, IntPtr methodInfo, Func<IntPtr> callOriginal)
        {
            bool runOriginal = true;
            IntPtr result = IntPtr.Zero;
            Exception caughtException = null;

            try
            {
                // Call prefix
                if (patch.PrefixMethod != null)
                {
                    object prefixResult = InvokePatchMethodWithFloats(patch.PrefixMethod, instance, args, IntPtr.Zero, null);
                    if (prefixResult is bool b && !b)
                    {
                        runOriginal = false;
                    }
                }

                // Call original
                if (runOriginal && patch.OriginalDelegate != null)
                {
                    result = callOriginal();
                }

                // Call postfix
                if (patch.PostfixMethod != null)
                {
                    InvokePatchMethodWithFloats(patch.PostfixMethod, instance, args, result, null);
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

        /// <summary>
        /// Invoke a patch method, automatically converting parameters to the expected types.
        /// </summary>
        private static object InvokePatchMethod(MethodInfo method, IntPtr instance, IntPtr[] args, IntPtr result, Exception exception)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object[] invokeArgs = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo param = parameters[i];
                string name = param.Name;
                Type ptype = param.ParameterType;

                try
                {
                    if (name == "__instance")
                    {
                        invokeArgs[i] = ConvertToType(instance, ptype);
                    }
                    else if (name == "__result")
                    {
                        invokeArgs[i] = ConvertToType(result, ptype);
                    }
                    else if (name == "__exception")
                    {
                        invokeArgs[i] = exception;
                    }
                    else if (name.StartsWith("__") && int.TryParse(name.Substring(2), out int argIndex))
                    {
                        if (argIndex >= 0 && argIndex < args.Length)
                        {
                            invokeArgs[i] = ConvertToType(args[argIndex], ptype);
                        }
                        else
                        {
                            invokeArgs[i] = GetDefault(ptype);
                        }
                    }
                    else
                    {
                        invokeArgs[i] = GetDefault(ptype);
                    }
                }
                catch
                {
                    invokeArgs[i] = GetDefault(ptype);
                }
            }

            try
            {
                return method.Invoke(null, invokeArgs);
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
            ParameterInfo[] parameters = method.GetParameters();
            object[] invokeArgs = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo param = parameters[i];
                string name = param.Name;
                Type ptype = param.ParameterType;

                try
                {
                    if (name == "__instance")
                    {
                        invokeArgs[i] = ConvertToType(instance, ptype);
                    }
                    else if (name == "__result")
                    {
                        invokeArgs[i] = ConvertToType(result, ptype);
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
                            if (arg != null && ptype.IsAssignableFrom(arg.GetType()))
                            {
                                invokeArgs[i] = arg;
                            }
                            else if (arg is IntPtr ptr)
                            {
                                invokeArgs[i] = ConvertToType(ptr, ptype);
                            }
                            else if (arg is float f && ptype == typeof(float))
                            {
                                invokeArgs[i] = f;
                            }
                            else if (arg is double d && ptype == typeof(double))
                            {
                                invokeArgs[i] = d;
                            }
                            else
                            {
                                invokeArgs[i] = GetDefault(ptype);
                            }
                        }
                        else
                        {
                            invokeArgs[i] = GetDefault(ptype);
                        }
                    }
                    else
                    {
                        invokeArgs[i] = GetDefault(ptype);
                    }
                }
                catch
                {
                    invokeArgs[i] = GetDefault(ptype);
                }
            }

            try
            {
                return method.Invoke(null, invokeArgs);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        /// <summary>
        /// Convert an IL2CPP pointer to the expected managed type.
        /// </summary>
        private static object ConvertToType(IntPtr ptr, Type targetType)
        {
            if (targetType == typeof(IntPtr))
                return ptr;
            
            if (ptr == IntPtr.Zero)
                return GetDefault(targetType);

            // Primitive types - IL2CPP passes small values directly in the pointer
            if (targetType == typeof(int))
                return ptr.ToInt32();
            if (targetType == typeof(long))
                return ptr.ToInt64();
            if (targetType == typeof(bool))
                return ptr != IntPtr.Zero && ptr.ToInt32() != 0;
            if (targetType == typeof(float))
            {
                // Float is passed as IntPtr containing the bits
                int bits = ptr.ToInt32();
                return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
            }
            if (targetType == typeof(double))
            {
                long bits = ptr.ToInt64();
                return BitConverter.ToDouble(BitConverter.GetBytes(bits), 0);
            }

            // String - convert from IL2CPP string
            if (targetType == typeof(string))
            {
                return Il2CppStringHelper.ObjectToString(ptr);
            }

            // Il2CppObject or derived type - wrap the pointer
            if (typeof(Il2CppObject).IsAssignableFrom(targetType))
            {
                return Activator.CreateInstance(targetType, ptr);
            }

            // Fallback - return as IntPtr
            return ptr;
        }

        private static object GetDefault(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        /// <summary>
        /// Remove all patches from an assembly.
        /// </summary>
        public static void RemovePatchesFromAssembly(Assembly assembly)
        {
            if (!_patchesByAssembly.TryGetValue(assembly, out List<PatchInfo> patches))
                return;

            foreach (PatchInfo patch in patches)
            {
                if (patch.HookHandle > 0)
                {
                    string target = GetPatchTargetName(patch);
                    Il2CppBridge.mdb_remove_hook(patch.HookHandle);
                    _hookToPatch.Remove(patch.HookHandle);
                    _logger.Info($"x {target}", ConsoleColor.Red);
                }
            }

            _patchesByAssembly.Remove(assembly);
        }

        public static IEnumerable<PatchInfo> GetAllPatches()
        {
            return _patchesByClass.Values.SelectMany(p => p);
        }

        public static IEnumerable<PatchInfo> GetPatchesForClass(Type patchClass)
        {
            return _patchesByClass.TryGetValue(patchClass, out List<PatchInfo> patches)
                ? patches
                : Enumerable.Empty<PatchInfo>();
        }
        
        /// <summary>
        /// Enable a hook by its handle.
        /// </summary>
        public static bool EnableHook(long hookHandle)
        {
            if (!_hookToPatch.TryGetValue(hookHandle, out PatchInfo patch))
                return false;
                
            int result = Il2CppBridge.mdb_set_hook_enabled(hookHandle, true);
            if (result == 0)
            {
                string target = GetPatchTargetName(patch);
                _logger.Info($"+ {target}", ConsoleColor.Blue);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Disable a hook by its handle.
        /// </summary>
        public static bool DisableHook(long hookHandle)
        {
            if (!_hookToPatch.TryGetValue(hookHandle, out PatchInfo patch))
                return false;
                
            int result = Il2CppBridge.mdb_set_hook_enabled(hookHandle, false);
            if (result == 0)
            {
                string target = GetPatchTargetName(patch);
                _logger.Info($"- {target}", ConsoleColor.DarkGray);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Enable or disable a hook by its handle.
        /// </summary>
        public static bool SetHookEnabled(long hookHandle, bool enabled)
        {
            return enabled ? EnableHook(hookHandle) : DisableHook(hookHandle);
        }
        
        /// <summary>
        /// Enable or disable a hook by patch class type.
        /// </summary>
        public static bool SetHookEnabled(Type patchClass, bool enabled)
        {
            if (!_patchesByClass.TryGetValue(patchClass, out List<PatchInfo> patches))
                return false;
                
            bool allSuccess = true;
            foreach (var patch in patches)
            {
                if (patch.HookHandle > 0)
                {
                    if (!SetHookEnabled(patch.HookHandle, enabled))
                        allSuccess = false;
                }
            }
            return allSuccess;
        }
        
        private static string GetPatchTargetName(PatchInfo patch)
        {
            string ns = patch.TargetNamespace;
            string type = patch.TargetTypeName;
            string method = patch.TargetMethodName;
            
            if (!string.IsNullOrEmpty(ns))
                return $"{ns}.{type}.{method}";
            return $"{type}.{method}";
        }
    }
}
