// ==============================
// ModManager - Mod Discovery and Loading
// ==============================
// Discovers, loads, and manages all mods from the Mods folder

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using GameSDK.ModHost.Patching;

namespace GameSDK.ModHost
{
    /// <summary>
    /// Manages the discovery, loading, and lifecycle of all mods.
    /// </summary>
    public static class ModManager
    {
        private static readonly List<ModBase> _loadedMods = new List<ModBase>();
        private static readonly ModLogger _logger = new ModLogger("ModManager");
        private static bool _initialized = false;
        private static Thread _updateThread;
        private static volatile bool _running = false;
        
        // Paths
        private static string _mdbDirectory;
        private static string _modsDirectory;
        private static string _logsDirectory;
        private static string _managedDirectory;

        /// <summary>
        /// Get all currently loaded mods.
        /// </summary>
        public static IReadOnlyList<ModBase> LoadedMods => _loadedMods.AsReadOnly();

        /// <summary>
        /// Entry point called by the native bridge DLL.
        /// This is called via ExecuteInDefaultAppDomain.
        /// </summary>
        /// <param name="args">Arguments (unused)</param>
        /// <returns>0 on success, non-zero on failure</returns>
        public static int Initialize(string args)
        {
            if (_initialized)
            {
                return 0;
            }

            try
            {
                // Determine paths based on where our DLL is located
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                _managedDirectory = Path.GetDirectoryName(assemblyPath);
                _mdbDirectory = Path.GetDirectoryName(_managedDirectory);
                _modsDirectory = Path.Combine(_mdbDirectory, "Mods");
                _logsDirectory = Path.Combine(_mdbDirectory, "Logs");

                // Initialize logging
                ModLogger.Initialize(_logsDirectory);
                
                // Set up assembly resolver to help mods find GameSDK.ModHost
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

                // Initialize IL2CPP runtime
                if (!Il2CppRuntime.Initialize())
                {
                    _logger.Error("Failed to initialize IL2CPP runtime!");
                    return 1;
                }

                // Note: IMGUI OnGUI hook removed - using console output and Canvas UI instead

                // Create mods directory if it doesn't exist
                if (!Directory.Exists(_modsDirectory))
                {
                    Directory.CreateDirectory(_modsDirectory);
                }

                // Discover and load mods
                DiscoverAndLoadMods();

                // Start update loop
                StartUpdateLoop();

                _initialized = true;
                
                ModLogger.Section("Ready", ConsoleColor.Magenta);
                _logger.Info($"{_loadedMods.Count} mod(s) active");
                
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error("ModManager initialization failed", ex);
                return 1;
            }
        }

        /// <summary>
        /// Discover and load all mods from the Mods directory.
        /// </summary>
        private static void DiscoverAndLoadMods()
        {
            string[] dllFiles = Directory.GetFiles(_modsDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            
            // First pass: Load assemblies and discover mods
            ModLogger.Section("Mods", ConsoleColor.Magenta);
            var loadedAssemblies = new List<(Assembly assembly, string path)>();
            foreach (string dllPath in dllFiles)
            {
                try
                {
                    byte[] assemblyBytes = File.ReadAllBytes(dllPath);
                    Assembly assembly = Assembly.Load(assemblyBytes);
                    loadedAssemblies.Add((assembly, dllPath));
                    
                    // Log discovered mods
                    foreach (var modType in GetModTypesFromAssembly(assembly))
                    {
                        var attr = modType.GetCustomAttribute<ModAttribute>();
                        string modName = attr?.Name ?? modType.Name;
                        string version = attr?.Version ?? "1.0.0";
                        string author = attr?.Author ?? "Unknown";
                        _logger.Info($"  {modName} v{version} by {author}", ConsoleColor.Blue);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to load: {Path.GetFileName(dllPath)}");
                }
            }
            
            // Second pass: Process patches (hooks)
            foreach (var (assembly, _) in loadedAssemblies)
            {
                PatchProcessor.ProcessAssembly(assembly);
            }
            
            // Third pass: Initialize mod types
            ModLogger.Section("Initializing", ConsoleColor.Magenta);
            foreach (var (assembly, dllPath) in loadedAssemblies)
            {
                try
                {
                    LoadModTypesFromAssembly(assembly, dllPath);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to initialize: {Path.GetFileName(dllPath)}");
                }
            }
        }
        
        private static IEnumerable<Type> GetModTypesFromAssembly(Assembly assembly)
        {
            Type[] allTypes;
            try
            {
                allTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                allTypes = ex.Types.Where(t => t != null).ToArray();
            }
            return allTypes.Where(t => t != null && typeof(ModBase).IsAssignableFrom(t) && !t.IsAbstract);
        }
        
        private static void LoadModTypesFromAssembly(Assembly assembly, string dllPath)
        {
            string fileName = Path.GetFileName(dllPath);

            // Find all types that inherit from ModBase
            Type[] allTypes;
            try
            {
                allTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Use the types that did load successfully
                allTypes = ex.Types.Where(t => t != null).ToArray();
            }

            Type[] modTypes = allTypes
                .Where(t => t != null && typeof(ModBase).IsAssignableFrom(t) && !t.IsAbstract)
                .ToArray();

            if (modTypes.Length == 0)
            {
                return;  // Not a mod DLL, skip silently
            }

            foreach (Type modType in modTypes)
            {
                try
                {
                    LoadModType(modType, dllPath);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to load mod type {modType.Name}", ex);
                }
            }
        }

        /// <summary>
        /// Instantiate and initialize a mod type.
        /// </summary>
        private static void LoadModType(Type modType, string dllPath)
        {
            // Get mod info from attribute
            ModInfo info = GetModInfo(modType, dllPath);

            // Create instance
            ModBase mod = (ModBase)Activator.CreateInstance(modType);
            mod.Info = info;
            mod.Logger = new ModLogger(info.Name);

            // Call OnLoad
            try
            {
                mod.OnLoad();
                _logger.Info($"  ✓ {info.Name}", ConsoleColor.Blue);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load {info.Name}: {ex.Message}");
                return;
            }

            _loadedMods.Add(mod);
        }

        /// <summary>
        /// Extract ModInfo from a mod type using the ModAttribute or defaults.
        /// </summary>
        private static ModInfo GetModInfo(Type modType, string dllPath)
        {
            ModInfo info = new ModInfo();
            info.FilePath = dllPath;

            // Try to get ModAttribute
            ModAttribute attr = modType.GetCustomAttribute<ModAttribute>();
            if (attr != null)
            {
                info.Id = attr.Id;
                info.Name = attr.Name;
                info.Version = attr.Version;
                info.Author = attr.Author;
                info.Description = attr.Description;
            }
            else
            {
                // Use defaults based on type/file name
                info.Id = modType.FullName;
                info.Name = modType.Name;
                info.Version = "1.0.0";
            }

            return info;
        }

        // Note: IMGUI OnGUI hook code removed - game uses Canvas UI, not IMGUI
        // The OnGUI method in mods will not be called, use OnUpdate instead

        /// <summary>
        /// Start the update loop thread.
        /// </summary>
        private static void StartUpdateLoop()
        {
            _running = true;
            _updateThread = new Thread(UpdateLoop)
            {
                Name = "MDB_ModUpdateThread",
                IsBackground = true
            };
            _updateThread.Start();
        }

        /// <summary>
        /// The main update loop that dispatches callbacks to mods.
        /// </summary>
        private static void UpdateLoop()
        {
            // Attach this thread to IL2CPP
            IntPtr domain = Il2CppBridge.mdb_domain_get();
            if (domain != IntPtr.Zero)
            {
                Il2CppBridge.mdb_thread_attach(domain);
            }

            int frameCount = 0;
            DateTime lastFixedUpdate = DateTime.Now;
            TimeSpan fixedDeltaTime = TimeSpan.FromSeconds(1.0 / 50.0); // 50 Hz (Unity default)

            while (_running)
            {
                try
                {
                    // OnUpdate - every frame
                    foreach (ModBase mod in _loadedMods)
                    {
                        try
                        {
                            mod.OnUpdate();
                        }
                        catch (Exception ex)
                        {
                            mod.Logger?.Error($"OnUpdate exception: {ex.Message}");
                        }
                    }

                    // OnFixedUpdate - at fixed intervals
                    DateTime now = DateTime.Now;
                    if (now - lastFixedUpdate >= fixedDeltaTime)
                    {
                        lastFixedUpdate = now;
                        foreach (ModBase mod in _loadedMods)
                        {
                            try
                            {
                                mod.OnFixedUpdate();
                            }
                            catch (Exception ex)
                            {
                                mod.Logger?.Error($"OnFixedUpdate exception: {ex.Message}");
                            }
                        }
                    }

                    // OnLateUpdate - after Update
                    foreach (ModBase mod in _loadedMods)
                    {
                        try
                        {
                            mod.OnLateUpdate();
                        }
                        catch (Exception ex)
                        {
                            mod.Logger?.Error($"OnLateUpdate exception: {ex.Message}");
                        }
                    }

                    frameCount++;

                    // Small sleep to prevent maxing out CPU
                    // Target roughly 60 FPS for our update loop
                    Thread.Sleep(16);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Update loop exception: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Called during Unity's GUI rendering phase.
        /// This must be called from Unity's main thread during the OnGUI callback.
        /// Entry point for native bridge to dispatch GUI rendering to all mods.
        /// </summary>
        /// <param name="args">Unused parameter for COM interop compatibility</param>
        /// <returns>0 on success</returns>
        public static int DispatchOnGUI(string args)
        {
            foreach (ModBase mod in _loadedMods)
            {
                try
                {
                    mod.OnGUI();
                }
                catch (Exception ex)
                {
                    mod.Logger?.Error($"OnGUI exception: {ex.Message}");
                }
            }
            return 0;
        }

        /// <summary>
        /// Direct OnGUI dispatch method for use from managed code.
        /// Call this from a MonoBehaviour's OnGUI method.
        /// </summary>
        public static void OnGUI()
        {
            foreach (ModBase mod in _loadedMods)
            {
                try
                {
                    mod.OnGUI();
                }
                catch (Exception ex)
                {
                    mod.Logger?.Error($"OnGUI exception: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Stop the mod manager and all mods.
        /// Called when the game is closing.
        /// </summary>
        public static void Shutdown()
        {
_logger.Info("Shutting down");
            
            _running = false;
            _updateThread?.Join(1000);

            _loadedMods.Clear();
            _initialized = false;

            ModLogger.Shutdown();
        }

        /// <summary>
        /// Assembly resolver to help mods find GameSDK.ModHost and other dependencies.
        /// </summary>
        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Parse the assembly name
            string assemblyName = new AssemblyName(args.Name).Name;
            
            // If it's our own assembly, return it
            if (assemblyName == "GameSDK.ModHost" || assemblyName == "GameSDK.Core")
            {
                return Assembly.GetExecutingAssembly();
            }
            
            // Try to load from Managed directory
            string assemblyPath = Path.Combine(_managedDirectory, assemblyName + ".dll");
            if (File.Exists(assemblyPath))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(assemblyPath);
                    return Assembly.Load(bytes);
                }
                catch
                {
                    // Fall through
                }
            }
            
            // Try to load from Mods directory
            assemblyPath = Path.Combine(_modsDirectory, assemblyName + ".dll");
            if (File.Exists(assemblyPath))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(assemblyPath);
                    return Assembly.Load(bytes);
                }
                catch
                {
                    // Fall through
                }
            }
            
            return null;
        }
    }
}
