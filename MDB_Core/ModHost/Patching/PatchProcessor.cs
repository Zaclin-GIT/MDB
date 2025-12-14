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

        #endregion

        /// <summary>
        /// Process and apply all patches from an assembly.
        /// </summary>
        public static int ProcessAssembly(Assembly assembly)
        {
            _logger.Info($"Processing patches from: {assembly.GetName().Name}");

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
                _logger.Info($"Applied {appliedPatches.Count} patch(es) from {assembly.GetName().Name}");
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
            _logger.Info($"Applying patch to: {target}");

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

            _logger.Info($"  Successfully patched: {target}");
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

        private static bool InstallHook(PatchInfo patchInfo, IntPtr methodPtr, int totalArgs, string description)
        {
            Delegate detour;
            IntPtr detourPtr;
            
            // Create the appropriate detour based on argument count
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

            _hookToPatch[hookHandle] = patchInfo;
            return true;
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
                    Il2CppBridge.mdb_remove_hook(patch.HookHandle);
                    _hookToPatch.Remove(patch.HookHandle);
                }
            }

            _patchesByAssembly.Remove(assembly);
            _logger.Info($"Removed all patches from: {assembly.GetName().Name}");
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
    }
}
