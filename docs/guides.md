---
layout: default
title: Guides
---

# Guides

In-depth technical guides covering MDB Framework's internal architecture and design decisions. These documents explain **how** the framework works under the hood — from the Windows DLL loading exploit to the fabrication of IL2CPP metadata structures in memory.

---

## Available Guides

| Guide | Description |
|-------|-------------|
| [Architecture Overview]({{ '/guides/architecture' | relative_url }}) | Full injection chain, initialization sequence, component architecture, and how everything fits together |
| [Proxy DLL Injection]({{ '/guides/proxy-injection' | relative_url }}) | How the version.dll proxy works — DLL search order, API forwarding, loader lock safety, double-load problem, P/Invoke resolution |
| [Managed Class Injection]({{ '/guides/class-injection' | relative_url }}) | MonoBehaviour fabrication in IL2CPP memory — memory layouts, hook system, negative token strategy, crash history |

---

## Who Are These For?

These guides are aimed at **framework contributors** and **advanced mod developers** who want to understand MDB's internals. You don't need to read these to write mods — the [Getting Started]({{ '/getting-started' | relative_url }}) guide and [API Reference]({{ '/api' | relative_url }}) are sufficient for mod development.

However, if you want to:
- Contribute to MDB Framework itself
- Debug deep framework issues
- Understand why certain design decisions were made
- Port MDB to new Unity versions or IL2CPP metadata formats
- Build similar systems for other runtimes

...then these guides are for you.

---

[← Back to Home]({{ '/' | relative_url }}) | [API Reference →]({{ '/api' | relative_url }})
