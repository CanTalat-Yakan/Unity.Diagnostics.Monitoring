# Unity Essentials

This module is part of the Unity Essentials ecosystem and follows the same lightweight, editor-first approach.
Unity Essentials is a lightweight, modular set of editor utilities and helpers that streamline Unity development. It focuses on clean, dependency-free tools that work well together.

All utilities are under the `UnityEssentials` namespace.

```csharp
using UnityEssentials;
```

## Installation

Install the Unity Essentials entry package via Unity's Package Manager, then install modules from the Tools menu.

- Add the entry package (via Git URL)
    - Window → Package Manager
    - "+" → "Add package from git URL…"
    - Paste: `https://github.com/CanTalat-Yakan/UnityEssentials.git`

- Install or update Unity Essentials packages
    - Tools → Install & Update UnityEssentials
    - Install all or select individual modules; run again anytime to update

---

# Runtime Monitoring

> Quick overview: Annotate fields, properties, events, and methods to see live values in-game. Register instance targets (or inherit helper base classes), open the Monitoring window from the Tools menu, and format or filter the UI with attributes.

Runtime Monitoring shows live values from your C# types while the game is running. Mark members with monitoring attributes, register instances at runtime, and use the built-in UI and filter window to view and organize what matters.

![screenshot](Documentation/Screenshot.png)

## Features
- Monitor many member kinds
  - Fields, Properties, Events, and Methods (including out values)
  - Static and instance members are supported
- Simple setup and UI
  - Tools → Runtime Monitoring → Settings to create/configure a settings asset
  - Tools → Runtime Monitoring → Filter Window to filter and organize what’s shown
- Attributes for display and behavior
  - Core marker: `[Monitor]` (or specific: `[MonitorField]`, `[MonitorProperty]`, `[MonitorEvent]`, `[MonitorMethod]`)
  - Formatting and layout: `[MLabel]`, `[MFormat]`, `[MTextColor]`, `[MBackgroundColor]`, `[MGroupName]`, `[MGroupOrder]`, `[MGroupColor]`, `[MPosition]`, `[MOrder]`, `[MRichText]`, `[MFontSize]`, `[MTextAlign]`, `[MElementIndent]`, `[MTag]`
  - Visibility and updates: `[MShowIf]`, `[MVisible]`, `[MUpdateEvent]`
  - Custom value renderers: `[MValueProcessor]` and global processors via `[GlobalValueProcessor]`
- Helper base types for auto-registration
  - `MonitoredBehaviour`, `MonitoredSingleton<T>`, `MonitoredScriptableObject`, and `MonitoredObject`
- Sample monitors
  - FPS, Console, and System monitors are included as modules you can enable

## Requirements
- Unity 6000.0+
- No external dependencies
- Recommended: create a Monitoring Settings asset from Tools → Runtime Monitoring → Settings

## Usage

1) Create settings and (optionally) import samples
- Menu: Tools → Runtime Monitoring → Settings
  - Click "Create Monitoring Settings" if none exists, then adjust options
- In Package Manager, import samples (e.g., IMGUI UI presets and the Example scene) if you want ready-made UI assets

2) Annotate members you want to see
```csharp
using Baracuda.Monitoring;
using UnityEngine;

public class Player : MonitoredBehaviour // auto register/unregister
{
    [Monitor] public int Health;

    [MonitorProperty]
    public int Score { get; private set; }

    [MonitorEvent]
    public static event System.Action OnRespawn;

    [MonitorMethod]
    public int GetAmmo() => 42;
}
```

3) Or register instances manually
```csharp
public class Enemy : MonoBehaviour
{
    [Monitor] private float _threat;

    void Awake()   => Monitor.StartMonitoring(this);
    void OnDestroy() => Monitor.StopMonitoring(this);
}
```

4) Customize display with attributes
```csharp
[Monitor]
[MLabel("HP")]
[MFormat("F0")]                 // numeric format
[MGroupName("Player")]          // group in UI
[MTextColor(0.9f, 0.2f, 0.2f)]   // RGB in 0..1
public float Health;

// Conditional visibility and on-demand refresh
[Monitor]
[MShowIf(/* your condition name or built-in */)]
[MUpdateEvent(nameof(OnChanged))]
public Vector3 LastSpawn;
public static event System.Action OnChanged;
```

5) Custom processors for values
- Per-member: `[MValueProcessor(nameof(MyProcessor))]`
- Global: mark a static method with `[GlobalValueProcessor]` to format a type everywhere

Skeleton for a global processor:
```csharp
using Baracuda.Monitoring;
using Baracuda.Monitoring.Types; // IFormatData lives in the runtime

public static class MonitorFormatters
{
    [GlobalValueProcessor]
    public static string FormatHealth(IFormatData data, int value)
    {
        return value <= 0 ? "Dead" : $"{value} HP";
    }
}
```

6) View and filter at runtime
- Open Tools → Runtime Monitoring → Filter Window to include/exclude groups and tags on the fly
- Use tags (`[MTag("Gameplay")]`, etc.) and groups to organize UI sections

## How It Works
- Initialization
  - A settings asset controls enable/disable and UI options (Tools → Runtime Monitoring → Settings)
  - On load, `Baracuda.Monitoring.Monitor` builds the core systems (Registry, UI, Events, Validators, Processor Factory) when monitoring is enabled
  - Profiling runs before first scene to cache targets and attributes for low overhead
- Registration
  - Instance targets must be registered to be monitored
  - Use helper base classes to register automatically or call `Monitor.StartMonitoring/StopMonitoring`
- Rendering
  - Each monitored member becomes a unit with label, value text, grouping, ordering, and styling derived from attributes or defaults
  - Optional processors convert raw values into display strings
- Editor integration
  - Settings and Filter Window are available under Tools → Runtime Monitoring

## Notes and Limitations
- Main thread: monitor registration and UI updates should occur on the main thread
- Formats and processors should be fast and allocation-light; heavy work will reflect in UI cost
- Instance lifetime: remember to stop monitoring destroyed objects if not using helper base classes
- IL2CPP/Mono supported; avoid reflection-heavy logic in your processors if you target tight platforms
- Samples are optional; you can build your own UI on top of the provided interfaces if desired

## Files in This Package
- Editor
  - `Editor/MonitoringSettingsWindow.cs`, `Editor/MonitoringFilterWindow.cs`, menu entries in `Editor/MenuItemLayout.cs`
  - Build helpers and inspector utilities
- Runtime API and helpers
  - `Runtime/Scripts/Monitor.cs` (entry point, settings hook, registry/UI/events)
  - Base types: `Runtime/Scripts/Types/MonitoredBehaviour.cs`, `MonitoredSingleton.cs`, `MonitoredScriptableObject.cs`, `MonitoredObject.cs`
  - UI: `Runtime/Scripts/Types/MonitoringUI*.cs`
  - Modules: `Runtime/Scripts/Modules/FPSMonitor.cs`, `ConsoleMonitor.cs`, `SystemMonitor.cs`
  - Attributes: `Runtime/Scripts/Attributes/*` (monitoring, formatting, grouping, visibility, processors)

## Tags
unity, runtime, monitoring, debug, ui, attributes, profiling, diagnostics
