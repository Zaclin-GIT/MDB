// ==============================
// WindowConfig - Persists window positions and sizes
// ==============================

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using GameSDK.ModHost;

namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// Stores position and size for a single ImGui window.
    /// </summary>
    public class WindowState
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public bool Visible { get; set; } = true;

        public Vector2 Position => new Vector2(X, Y);
        public Vector2 Size => new Vector2(Width, Height);

        public bool IsValid => Width > 50 && Height > 50 && X >= 0 && Y >= 0;
    }

    /// <summary>
    /// Manages loading/saving window layout configuration to a simple INI-style file.
    /// Auto-saves when windows change, auto-loads on startup.
    /// </summary>
    public class WindowConfig
    {
        private const string LOG_TAG = "WindowConfig";
        private const string CONFIG_FILENAME = "explorer_layout.cfg";
        private const float SAVE_INTERVAL = 2.0f; // seconds between save checks
        private const float POSITION_THRESHOLD = 2.0f; // minimum change to trigger save

        private readonly string _configPath;
        private readonly Dictionary<string, WindowState> _windows = new Dictionary<string, WindowState>();
        private readonly Dictionary<string, WindowState> _lastSaved = new Dictionary<string, WindowState>();
        private bool _dirty;
        private DateTime _lastSaveTime = DateTime.MinValue;
        private bool _loaded;

        /// <summary>
        /// Whether a saved config was loaded (affects whether to use saved or default positions).
        /// </summary>
        public bool HasSavedLayout => _loaded;

        public WindowConfig(string modsFolder)
        {
            _configPath = Path.Combine(modsFolder, CONFIG_FILENAME);
            Load();
        }

        /// <summary>
        /// Get saved state for a window, or null if not saved.
        /// </summary>
        public WindowState GetWindow(string name)
        {
            return _windows.TryGetValue(name, out var state) && state.IsValid ? state : null;
        }

        /// <summary>
        /// Update the current state of a window. Call each frame from inside Begin/End.
        /// </summary>
        public void UpdateWindow(string name, Vector2 pos, Vector2 size, bool visible)
        {
            if (!_windows.TryGetValue(name, out var state))
            {
                state = new WindowState();
                _windows[name] = state;
            }

            // Check if anything actually changed
            if (Math.Abs(state.X - pos.X) > POSITION_THRESHOLD ||
                Math.Abs(state.Y - pos.Y) > POSITION_THRESHOLD ||
                Math.Abs(state.Width - size.X) > POSITION_THRESHOLD ||
                Math.Abs(state.Height - size.Y) > POSITION_THRESHOLD ||
                state.Visible != visible)
            {
                state.X = pos.X;
                state.Y = pos.Y;
                state.Width = size.X;
                state.Height = size.Y;
                state.Visible = visible;
                _dirty = true;
            }
        }

        /// <summary>
        /// Update only the visibility of a window (for when a window is hidden and not drawn).
        /// </summary>
        public void UpdateVisibility(string name, bool visible)
        {
            if (_windows.TryGetValue(name, out var state) && state.Visible != visible)
            {
                state.Visible = visible;
                _dirty = true;
            }
        }

        /// <summary>
        /// Call periodically (e.g. each frame) to auto-save if dirty.
        /// Uses a cooldown to avoid excessive I/O.
        /// </summary>
        public void AutoSave()
        {
            if (!_dirty) return;
            if ((DateTime.Now - _lastSaveTime).TotalSeconds < SAVE_INTERVAL) return;

            Save();
            _dirty = false;
            _lastSaveTime = DateTime.Now;
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_configPath))
                    return;

                var lines = File.ReadAllLines(_configPath);
                string currentSection = null;

                foreach (var rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                        continue;

                    // Section header: [WindowName]
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2);
                        if (!_windows.ContainsKey(currentSection))
                            _windows[currentSection] = new WindowState();
                        continue;
                    }

                    // Key=Value
                    if (currentSection != null)
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;

                        string key = line.Substring(0, eq).Trim();
                        string val = line.Substring(eq + 1).Trim();
                        var state = _windows[currentSection];

                        switch (key.ToLowerInvariant())
                        {
                            case "x": if (float.TryParse(val, out float x)) state.X = x; break;
                            case "y": if (float.TryParse(val, out float y)) state.Y = y; break;
                            case "width": if (float.TryParse(val, out float w)) state.Width = w; break;
                            case "height": if (float.TryParse(val, out float h)) state.Height = h; break;
                            case "visible": if (bool.TryParse(val, out bool v)) state.Visible = v; break;
                        }
                    }
                }

                _loaded = _windows.Count > 0;
                if (_loaded)
                    ModLogger.LogInternal(LOG_TAG, $"[INFO] Loaded layout for {_windows.Count} window(s) from {CONFIG_FILENAME}");
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[WARN] Failed to load config: {ex.Message}");
            }
        }

        private void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var writer = new StreamWriter(_configPath, false))
                {
                    writer.WriteLine("# MDB Explorer Window Layout");
                    writer.WriteLine($"# Auto-saved {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine();

                    foreach (var kvp in _windows)
                    {
                        writer.WriteLine($"[{kvp.Key}]");
                        writer.WriteLine($"X={kvp.Value.X:F0}");
                        writer.WriteLine($"Y={kvp.Value.Y:F0}");
                        writer.WriteLine($"Width={kvp.Value.Width:F0}");
                        writer.WriteLine($"Height={kvp.Value.Height:F0}");
                        writer.WriteLine($"Visible={kvp.Value.Visible}");
                        writer.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[WARN] Failed to save config: {ex.Message}");
            }
        }
    }
}
