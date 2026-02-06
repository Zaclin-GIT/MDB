# Unity Debug Interceptor

An MDB mod that intercepts Unity's `Debug.Log`, `Debug.LogWarning`, and `Debug.LogError` calls and redirects them to the MDB console with proper formatting and color coding.

---

<a href="https://buymeacoffee.com/winnforge">
  <img src="https://img.shields.io/badge/Buy%20Me%20A%20Coffee-%23FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=black" alt="Buy Me A Coffee">
</a>

## Features

- **Captures Debug.Log** - Displayed in gray
- **Captures Debug.LogWarning** - Displayed in yellow
- **Captures Debug.LogError** - Displayed in red
- Non-blocking - Original Unity logging still executes

## Requirements

- MDB Framework (MDB_Core)
- Target game using IL2CPP Unity runtime

## Installation

1. Build the project
2. Copy the compiled DLL to your game's `MDB/Mods/` folder
3. The mod will automatically load and begin intercepting Unity debug output

## How It Works

The mod uses MDB's patching system to hook into Unity's `Debug` class methods. Each intercepted log message is formatted and displayed in the MDB console with appropriate coloring based on log level.

## Building

```bash
dotnet build -c Release
```

## License

MIT License - See LICENSE file for details.
