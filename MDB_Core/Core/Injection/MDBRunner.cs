// ==============================
// MDBRunner — MonoBehaviour instantiation + main-thread dispatch
// ==============================
// Two-phase design:
//   Phase 1 (bg thread): Register the IL2CPP class via ClassInjector (pure memory work)
//   Phase 2 (try bg, defer to main): Create GameObject, AddComponent, DontDestroyOnLoad
//
// Once active, Unity's player loop calls our Update/FixedUpdate/LateUpdate trampolines
// which dispatch to ModManager.DispatchX() methods.

using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using GameSDK.ModHost;

namespace GameSDK.Injection
{
    /// <summary>
    /// Manages injecting and instantiating the MDBRunner MonoBehaviour subclass
    /// to receive main-thread Update/FixedUpdate/LateUpdate callbacks.
    /// </summary>
    internal static class MDBRunner
    {
        private static readonly ModLogger _logger = new ModLogger("INJECT");

        /// <summary>Log a message only when ClassInjector.VerboseLogging is enabled.</summary>
        private static void LogVerbose(string message)
        {
            if (ClassInjector.VerboseLogging)
                _logger.Info(message);
        }

        private static IntPtr s_injectedClass = IntPtr.Zero;
        private static IntPtr s_gameObject = IntPtr.Zero;
        private static IntPtr s_component = IntPtr.Zero;
        private static volatile bool s_deferredInstall = false;
        private static volatile bool s_installed = false;
        private static volatile bool s_mainThreadActive = false;

        // ==============================
        // VEH crash handler
        // ==============================

        private const uint EXCEPTION_ACCESS_VIOLATION = 0xC0000005;
        private const int EXCEPTION_CONTINUE_SEARCH = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct EXCEPTION_RECORD
        {
            public uint ExceptionCode;
            public uint ExceptionFlags;
            public IntPtr ExceptionRecordPtr;
            public IntPtr ExceptionAddress;
            public uint NumberParameters;
            // followed by ExceptionInformation array — we don't need it
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int VectoredExceptionHandlerDelegate(IntPtr exceptionPointers);

        private static VectoredExceptionHandlerDelegate s_vehDelegate;
        private static GCHandle s_vehDelegateHandle;
        private static IntPtr s_vehHandle;
        private static volatile string s_lastCrashInfo;

        // Windows x64 CONTEXT offsets for general-purpose registers
        private const int CTX_RAX = 0x78;
        private const int CTX_RCX = 0x80;
        private const int CTX_RDX = 0x88;
        private const int CTX_RBX = 0x90;
        private const int CTX_RSP = 0x98;
        private const int CTX_RBP = 0xA0;
        private const int CTX_RSI = 0xA8;
        private const int CTX_RDI = 0xB0;
        private const int CTX_R8  = 0xB8;
        private const int CTX_R9  = 0xC0;
        private const int CTX_R10 = 0xC8;
        private const int CTX_R11 = 0xD0;
        private const int CTX_R12 = 0xD8;
        private const int CTX_R13 = 0xE0;
        private const int CTX_R14 = 0xE8;
        private const int CTX_R15 = 0xF0;
        private const int CTX_RIP = 0xF8;

        private static int VehCrashHandler(IntPtr exceptionPointersPtr)
        {
            try
            {
                // EXCEPTION_POINTERS: ExceptionRecord ptr (8 bytes) + ContextRecord ptr (8 bytes)
                IntPtr recordPtr = Marshal.ReadIntPtr(exceptionPointersPtr, 0);
                IntPtr contextPtr = Marshal.ReadIntPtr(exceptionPointersPtr, 8);
                EXCEPTION_RECORD record = Marshal.PtrToStructure<EXCEPTION_RECORD>(recordPtr);

                if (record.ExceptionCode == EXCEPTION_ACCESS_VIOLATION)
                {
                    long gameAssembly = Il2CppExports.GameAssemblyBase.ToInt64();
                    long crashAddr = record.ExceptionAddress.ToInt64();
                    long rva = crashAddr - gameAssembly;

                    // Read all general-purpose registers from CONTEXT
                    long rax = Marshal.ReadInt64(contextPtr, CTX_RAX);
                    long rcx = Marshal.ReadInt64(contextPtr, CTX_RCX);
                    long rdx = Marshal.ReadInt64(contextPtr, CTX_RDX);
                    long rbx = Marshal.ReadInt64(contextPtr, CTX_RBX);
                    long rsp = Marshal.ReadInt64(contextPtr, CTX_RSP);
                    long rbp = Marshal.ReadInt64(contextPtr, CTX_RBP);
                    long rsi = Marshal.ReadInt64(contextPtr, CTX_RSI);
                    long rdi = Marshal.ReadInt64(contextPtr, CTX_RDI);
                    long r8 = Marshal.ReadInt64(contextPtr, CTX_R8);
                    long r9 = Marshal.ReadInt64(contextPtr, CTX_R9);
                    long r14 = Marshal.ReadInt64(contextPtr, CTX_R14);
                    long r15 = Marshal.ReadInt64(contextPtr, CTX_R15);
                    long rip = Marshal.ReadInt64(contextPtr, CTX_RIP);

                    // Read first 8 QWORDs from stack (RSP) for return addresses
                    string stackDump = "";
                    try
                    {
                        for (int i = 0; i < 16; i++)
                        {
                            long stackVal = Marshal.ReadInt64(new IntPtr(rsp + i * 8));
                            long stackRva = stackVal - gameAssembly;
                            bool isCode = (stackRva > 0 && stackRva < 0x10000000); // within GameAssembly range
                            stackDump += $"\n  [RSP+0x{i * 8:X2}] = 0x{stackVal:X}{(isCode ? $" (RVA=0x{stackRva:X})" : "")}";
                        }
                    }
                    catch { stackDump += "\n  (stack read failed)"; }

                    s_lastCrashInfo = $"ACCESS_VIOLATION at 0x{crashAddr:X} (RVA=0x{rva:X})\n" +
                        $"  RAX=0x{rax:X}  RCX=0x{rcx:X}  RDX=0x{rdx:X}\n" +
                        $"  RBX=0x{rbx:X}  RSP=0x{rsp:X}  RBP=0x{rbp:X}\n" +
                        $"  RSI=0x{rsi:X}  RDI=0x{rdi:X}  R8=0x{r8:X}\n" +
                        $"  R9=0x{r9:X}  R14=0x{r14:X}  R15=0x{r15:X}  RIP=0x{rip:X}\n" +
                        $"  Stack:{stackDump}";

                    // Write immediately to file in case process is about to die
                    try
                    {
                        string logPath = System.IO.Path.Combine(
                            System.IO.Path.GetDirectoryName(typeof(MDBRunner).Assembly.Location),
                            "crash_diag.log");
                        System.IO.File.AppendAllText(logPath,
                            $"[{DateTime.Now:HH:mm:ss}] {s_lastCrashInfo}\n");
                    }
                    catch { }
                }
            }
            catch { }
            return EXCEPTION_CONTINUE_SEARCH; // Let the system handle it
        }

        private static void InstallVeh()
        {
            if (s_vehHandle != IntPtr.Zero) return;
            s_vehDelegate = new VectoredExceptionHandlerDelegate(VehCrashHandler);
            s_vehDelegateHandle = GCHandle.Alloc(s_vehDelegate);
            IntPtr fnPtr = Marshal.GetFunctionPointerForDelegate(s_vehDelegate);
            s_vehHandle = Kernel32.AddVectoredExceptionHandler(1, fnPtr); // 1 = call first
            LogVerbose($"[INJECT] VEH installed, handle=0x{s_vehHandle.ToInt64():X}");
        }

        private static void RemoveVeh()
        {
            if (s_vehHandle != IntPtr.Zero)
            {
                Kernel32.RemoveVectoredExceptionHandler(s_vehHandle);
                s_vehHandle = IntPtr.Zero;
                LogVerbose("[INJECT] VEH removed");
            }
        }

        /// <summary>
        /// Install the MDBRunner MonoBehaviour.
        /// Called from ModManager.Initialize() on the background initialization_thread.
        /// </summary>
        /// <returns>True if injection succeeded (instantiation may be deferred).</returns>
        public static bool Install()
        {
            if (s_installed) return true;

            LogVerbose("[INJECT] MDBRunner.Install() starting...");

            try
            {
                // Phase 1: Register the class (pure memory work — thread-safe)
                s_injectedClass = ClassInjector.RegisterMonoBehaviourSubclass("MDBRunner", "MDB.Internal");
                if (s_injectedClass == IntPtr.Zero)
                {
                    _logger.Error("[INJECT] Phase 1 FAILED — class registration returned null");
                    return false;
                }
                LogVerbose($"[INJECT] Phase 1 complete — class @ 0x{s_injectedClass.ToInt64():X}");

                // Wire up callbacks
                ClassInjector.OnUpdateCallback = OnUpdate;
                ClassInjector.OnFixedUpdateCallback = OnFixedUpdate;
                ClassInjector.OnLateUpdateCallback = OnLateUpdate;

                // Phase 2: Try instantiation from background thread
                try
                {
                    Instantiate();
                    s_installed = true;
                    LogVerbose("[INJECT] Phase 2 complete — MDBRunner active on main thread");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Warning($"[INJECT] Phase 2 failed from bg thread (expected): {ex.Message}");
                    LogVerbose("[INJECT] Will retry instantiation on first main-thread tick");
                    s_deferredInstall = true;
                    s_installed = true; // Class is registered, instantiation pending
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[INJECT] MDBRunner.Install() failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Phase 2: Create GameObject, AddComponent, DontDestroyOnLoad.
        /// Uses IL2CPP bridge functions for all Unity API calls.
        /// </summary>
        private static void Instantiate()
        {
            LogVerbose("[INJECT] Phase 2: Instantiating MDBRunner...");

            // Step 1: Create a new GameObject
            IntPtr gameObjectClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "GameObject");
            if (gameObjectClass == IntPtr.Zero)
                throw new InvalidOperationException("Failed to find GameObject class");

            IntPtr ctorMethod = Il2CppBridge.mdb_get_method(gameObjectClass, ".ctor", 1);
            if (ctorMethod == IntPtr.Zero)
                throw new InvalidOperationException("Failed to find GameObject..ctor(string)");

            IntPtr gameObj = Il2CppBridge.mdb_object_new(gameObjectClass);
            if (gameObj == IntPtr.Zero)
                throw new InvalidOperationException("Failed to allocate GameObject");

            IntPtr nameStr = Il2CppBridge.mdb_string_new("__MDB_Runner");
            if (nameStr == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create name string");

            IntPtr exception;
            IntPtr ctorResult = Il2CppBridge.mdb_invoke_method(ctorMethod, gameObj, new IntPtr[] { nameStr }, out exception);
            if (exception != IntPtr.Zero)
                throw new InvalidOperationException($"GameObject..ctor threw exception @ 0x{exception.ToInt64():X}");

            s_gameObject = gameObj;
            LogVerbose($"[INJECT] GameObject created @ 0x{gameObj.ToInt64():X}");

            // Step 2: AddComponent with our injected type
            IntPtr injectedType = Il2CppBridge.mdb_class_get_type(s_injectedClass);
            if (injectedType == IntPtr.Zero)
                throw new InvalidOperationException("Failed to get Il2CppType from injected class");

            LogVerbose($"[INJECT] mdb_class_get_type OK → 0x{injectedType.ToInt64():X}");

            IntPtr typeObj = Il2CppBridge.mdb_type_get_object(injectedType);
            if (typeObj == IntPtr.Zero)
                throw new InvalidOperationException("Failed to get System.Type from Il2CppType");

            LogVerbose($"[INJECT] mdb_type_get_object OK → 0x{typeObj.ToInt64():X}");

            IntPtr addComponentMethod = Il2CppBridge.mdb_get_method(gameObjectClass, "AddComponent", 1);
            if (addComponentMethod == IntPtr.Zero)
                throw new InvalidOperationException("Failed to find AddComponent method");

            // Log AddComponent method details (Unity 2021+ MethodInfo: name at 0x18, klass at 0x20)
            IntPtr acMethodPtr = Marshal.ReadIntPtr(addComponentMethod, 0x00); // methodPointer
            IntPtr acNamePtr = Marshal.ReadIntPtr(addComponentMethod, 0x18);   // name
            string acName = (acNamePtr != IntPtr.Zero) ? Marshal.PtrToStringAnsi(acNamePtr) : "(null)";
            IntPtr acKlass = Marshal.ReadIntPtr(addComponentMethod, 0x20);     // klass
            byte acArgCount = Marshal.ReadByte(addComponentMethod, 0x52);      // argsCount
            LogVerbose($"[INJECT] AddComponent method: name='{acName}' klass=0x{acKlass.ToInt64():X} args={acArgCount} ptr=0x{acMethodPtr.ToInt64():X}");

            // DIAGNOSTIC: Test object creation independently before AddComponent
            try
            {
                IntPtr testObj = Il2CppBridge.mdb_object_new(s_injectedClass);
                LogVerbose($"[INJECT] DIAG: il2cpp_object_new(injectedClass) → {(testObj != IntPtr.Zero ? $"OK 0x{testObj.ToInt64():X}" : "FAILED (null)")}");
                if (testObj != IntPtr.Zero)
                {
                    // Check the klass pointer of the created object (first 8 bytes of Il2CppObject)
                    IntPtr objKlass = Marshal.ReadIntPtr(testObj);
                    LogVerbose($"[INJECT] DIAG: test object klass = 0x{objKlass.ToInt64():X} (expected 0x{s_injectedClass.ToInt64():X})");
                }
            }
            catch (Exception testEx)
            {
                _logger.Error($"[INJECT] DIAG: il2cpp_object_new CRASHED: {testEx.Message}");
            }

            // DIAGNOSTIC: Read a well-known class's bitflags for comparison
            try
            {
                int bf1Off = Il2CppConstants.CLASS_2_OFFSET + 0x6E;
                int bf2Off = Il2CppConstants.CLASS_2_OFFSET + 0x6F;
                byte goBf1 = Marshal.ReadByte(gameObjectClass, bf1Off);
                byte goBf2 = Marshal.ReadByte(gameObjectClass, bf2Off);
                byte injBf1 = Marshal.ReadByte(s_injectedClass, bf1Off);
                byte injBf2 = Marshal.ReadByte(s_injectedClass, bf2Off);
                LogVerbose($"[INJECT] DIAG: GameObject  bf1=0x{goBf1:X2} bf2=0x{goBf2:X2}");
                LogVerbose($"[INJECT] DIAG: Injected    bf1=0x{injBf1:X2} bf2=0x{injBf2:X2}");

                // Verify parent field
                IntPtr injParent = Marshal.ReadIntPtr(s_injectedClass, 0x58);
                string injParentName = "(?)";
                try { injParentName = Il2CppMemory.ReadCString(Marshal.ReadIntPtr(injParent, 0x10)) ?? "(?)"; } catch {}
                LogVerbose($"[INJECT] DIAG: Injected.parent = 0x{injParent.ToInt64():X} ({injParentName})");
            }
            catch (Exception diagEx)
            {
                _logger.Error($"[INJECT] DIAG: bitflags read failed: {diagEx.Message}");
            }

            LogVerbose($"[INJECT] Calling AddComponent...");
            LogVerbose($"[INJECT]   gameObj      @ 0x{gameObj.ToInt64():X}");
            LogVerbose($"[INJECT]   injectedType @ 0x{injectedType.ToInt64():X}");
            LogVerbose($"[INJECT]   typeObj      @ 0x{typeObj.ToInt64():X}");
            LogVerbose($"[INJECT]   addComponent @ 0x{addComponentMethod.ToInt64():X}");
            LogVerbose($"[INJECT]   GameAssembly @ 0x{Il2CppExports.GameAssemblyBase.ToInt64():X}");
            LogVerbose($"[INJECT]   flushing log before AddComponent invoke...");

            // Force log flush before the call that might crash
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(MDBRunner).Assembly.Location), "Mods.log"),
                "");

            // Install VEH to capture crash address + registers
            InstallVeh();

            // Open GetTypeInfo hook gate to log all calls during AddComponent
            InjectorHelpers.s_getTypeInfoGate = true;

            IntPtr component = TryAddComponent(addComponentMethod, gameObj, typeObj, out exception);

            // Close gate + Remove VEH
            InjectorHelpers.s_getTypeInfoGate = false;
            RemoveVeh();

            if (s_lastCrashInfo != null)
            {
                _logger.Error($"[INJECT] VEH captured: {s_lastCrashInfo}");
            }

            if (exception != IntPtr.Zero)
                throw new InvalidOperationException($"AddComponent threw exception @ 0x{exception.ToInt64():X}");
            if (component == IntPtr.Zero)
                throw new InvalidOperationException("AddComponent returned null");

            s_component = component;
            LogVerbose($"[INJECT] AddComponent result: 0x{component.ToInt64():X}");

            // Step 3: DontDestroyOnLoad
            IntPtr objectClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "Object");
            if (objectClass != IntPtr.Zero)
            {
                IntPtr dontDestroyMethod = Il2CppBridge.mdb_get_method(objectClass, "DontDestroyOnLoad", 1);
                if (dontDestroyMethod != IntPtr.Zero)
                {
                    Il2CppBridge.mdb_invoke_method(dontDestroyMethod, IntPtr.Zero, new IntPtr[] { gameObj }, out exception);
                    if (exception != IntPtr.Zero)
                    {
                        _logger.Warning($"[INJECT] DontDestroyOnLoad threw exception @ 0x{exception.ToInt64():X}");
                    }
                    else
                    {
                        LogVerbose("[INJECT] DontDestroyOnLoad applied");
                    }
                }
                else
                {
                    _logger.Warning("[INJECT] Failed to find DontDestroyOnLoad method");
                }
            }
            else
            {
                _logger.Warning("[INJECT] Failed to find UnityEngine.Object class");
            }

            s_mainThreadActive = true;
            LogVerbose("[INJECT] Main thread dispatch active");
        }

        /// <summary>
        /// Attempt AddComponent with crash protection.
        /// Uses HandleProcessCorruptedStateExceptions to catch access violations.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private static IntPtr TryAddComponent(IntPtr method, IntPtr gameObj, IntPtr typeObj, out IntPtr exception)
        {
            exception = IntPtr.Zero;
            try
            {
                IntPtr result = Il2CppBridge.mdb_invoke_method(method, gameObj, new IntPtr[] { typeObj }, out exception);
                LogVerbose($"[INJECT] AddComponent returned: 0x{result.ToInt64():X}");
                return result;
            }
            catch (AccessViolationException avEx)
            {
                _logger.Error($"[INJECT] AddComponent ACCESS VIOLATION caught: {avEx.Message}");
                _logger.Error($"[INJECT] VEH crash info: {s_lastCrashInfo ?? "(no VEH data)"}");
                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                _logger.Error($"[INJECT] AddComponent exception: {ex.GetType().Name}: {ex.Message}");
                _logger.Error($"[INJECT] VEH crash info: {s_lastCrashInfo ?? "(no VEH data)"}");
                return IntPtr.Zero;
            }
        }

        // ==============================
        // Callbacks from trampolines
        // ==============================

        private static void OnUpdate()
        {
            // Check if deferred installation is pending
            if (s_deferredInstall)
            {
                s_deferredInstall = false;
                LogVerbose("[INJECT] Deferred install — retrying Phase 2 from main thread...");
                try
                {
                    Instantiate();
                    LogVerbose("[INJECT] Deferred Phase 2 complete!");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[INJECT] Deferred Phase 2 FAILED: {ex}");
                }
            }

            try
            {
                ModManager.DispatchUpdate();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[INJECT] DispatchUpdate exception: {ex.Message}");
            }
        }

        private static void OnFixedUpdate()
        {
            try
            {
                ModManager.DispatchFixedUpdate();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[INJECT] DispatchFixedUpdate exception: {ex.Message}");
            }
        }

        private static void OnLateUpdate()
        {
            try
            {
                ModManager.DispatchLateUpdate();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[INJECT] DispatchLateUpdate exception: {ex.Message}");
            }
        }
    }
}
