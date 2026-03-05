# EditorToolbar

An extensible toolbar framework for the Unity Editor that provides attribute-based auto-discovery of toolbar sections across any assembly.

## Overview

EditorToolbar lets you add custom controls to the Unity Editor toolbar without modifying any central registry. Sections, workspace providers, and framework detectors are discovered automatically at domain reload using Unity's `TypeCache`, so any assembly that references the package can contribute toolbar content.

## Features

- **Attribute-based auto-discovery** — mark a class with `[ToolbarSection]` and it appears in the toolbar automatically; no manual registration required
- **Cross-assembly discovery** — sections can live in any Editor assembly; the toolbar finds them all at startup
- **Framework detection** — sections are grouped by framework (detected from asset paths, namespaces, or custom detectors); the mode selector shows `framework module - Section Name`
- **Workspace providers** — inject left-side workspace UI (profile indicators, scene selectors, runtime status) via `[ToolbarWorkspaceProvider]` without touching the core system
- **Multiple injection backends** — supports the Paps `UnityToolbarExtenderUIToolkit` plugin and a legacy custom injector as fallback; forward-compatible with the Unity 6.3+ MainToolbar API (disabled by default, enable with `ENABLE_MAIN_TOOLBAR_INJECTOR`)
- **Conditional visibility** — sections implement `ShouldShow()` to appear or hide based on context
- **Graceful error isolation** — a failing section shows an inline error label; other sections continue working
- **Batch-mode safe** — toolbar initialization is skipped automatically when running in CI/headless mode
- **Preference persistence** — selected section is saved per-user in `EditorPrefs`

## Built-in Sections

| Section | Purpose |
|---------|---------|
| Compile | Manual and auto compilation mode controls |
| Debugger | Console controls (clear, pause on error) |
| Input | Input monitoring (mouse, keyboard, gamepad) |
| Layout | Editor layout management |
| None | Empty section (hides the section content area) |
| Performance | Profiling tools |
| Persistence | Save and load utilities |
| PlayMode | Play mode controls |
| Runtime Tools | Runtime debugging utilities |
| Time | Time scale and frame rate controls |

## Installation

**Unity Package Manager (recommended)**

Add the package via git URL in the Package Manager window or directly in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.frostebite.editortoolbar": "https://github.com/frostebite/EditorToolbar.git"
  }
}
```

**Git submodule**

```sh
git submodule add https://github.com/frostebite/EditorToolbar.git Assets/_Engine/Submodules/EditorToolbar
```

## Requirements

- Unity 2021.3 or later
- [Paps.UnityToolbarExtenderUIToolkit](https://github.com/PaulNonatomic/UnityToolbarExtender) — required for toolbar injection

## Quick Start

Create a class in any Editor assembly, apply `[ToolbarSection]`, and implement `IEditorToolbar`:

```csharp
using UnityEditor;
using UnityEngine;

[ToolbarSection("My Tools")]
public class MyToolbarSection : IEditorToolbar
{
    public bool ShouldShow() => true;

    public void OnGUI()
    {
        if (GUILayout.Button("Do Something", EditorStyles.toolbarButton))
        {
            Debug.Log("Button clicked");
        }
    }
}
```

The section appears immediately after the next domain reload. No registration step is required.

Your assembly definition must be Editor-only (`"includePlatforms": ["Editor"]`). No reference to the EditorToolbar assembly is required for discovery — `TypeCache` scans all loaded Editor assemblies.

## Extensibility

### Workspace Providers

Inject UI into the left side of the toolbar (profile selector, scene picker, runtime status) by applying `[ToolbarWorkspaceProvider]` to a static class. The `Priority` property controls ordering when multiple providers are present (higher value = drawn first).

```csharp
[ToolbarWorkspaceProvider(Priority = 10)]
public static class MyWorkspaceProvider
{
    // Required. Draws workspace controls on the left side of the toolbar.
    public static void DrawWorkspaceUI()
    {
        GUILayout.Label("Profile: Dev", EditorStyles.toolbarButton);
    }

    // Optional. Drawn during play mode.
    public static void DrawRuntimeStatus()
    {
        GUILayout.Label($"FPS: {(1f / Time.deltaTime):F0}", EditorStyles.miniLabel);
    }

    // Optional. Invoked when the left toolbar button is clicked (Unity 6.3+ integration).
    public static void ShowMainToolbarLeftMenu() { }

    // Optional. Draws a scene selector control.
    public static void DrawSceneSelector() { }

    // Optional. Draws a profile status indicator.
    public static void DrawProfileStatus() { }
}
```

### Framework Detectors

The toolbar groups sections by framework using a chain of detectors. Register a custom detector to control how your sections are labelled and grouped. Detectors are tried in descending priority order; the first non-empty result wins.

```csharp
[FrameworkDetector(Priority = 10)]
public static class MyFrameworkDetector
{
    // Return (framework, module) for types in your codebase.
    // Return ("", "") to pass to the next detector.
    public static (string Framework, string Module) DetectFramework(Type type)
    {
        if (type.Namespace?.StartsWith("MyStudio.MyGame") == true)
            return ("mygame", "core");

        return ("", "");
    }
}
```

The default detector infers the framework label from the `Submodules/` folder segment in the asset path, falling back to namespace segments and then assembly name.

## Known Limitations

- **Unity 6 UnsupportedElements**: Custom toolbar elements may be moved to an "UnsupportedElements" group. Right-click the toolbar and select the element to restore it.
- **MainToolbar API**: The official Unity 6.3+ API integration exists in the codebase but is disabled due to API instability. Enable it with the `ENABLE_MAIN_TOOLBAR_INJECTOR` scripting define.

## License

See LICENSE file.
