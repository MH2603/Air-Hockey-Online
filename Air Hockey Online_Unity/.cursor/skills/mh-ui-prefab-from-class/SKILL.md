---
name: mh-ui-prefab-from-class
description: >-
  Designs a Unity UI prefab hierarchy from a UI MonoBehaviour class, gets explicit user approval on the structure, then builds the prefab via Unity MCP (script-execute). Use when the user wants a new UI prefab derived from a C# UI class (e.g. UIWindow subclasses), says "prefab from class", "UI prefab from script", or invokes this MH UI pipeline.
---

# MH / UI / Prefab from UI class

Two-phase workflow. **Do not call Unity MCP or generate the prefab until the user clearly accepts the proposed structure** (e.g. "yes", "approved", "go ahead", "create it").

## Conventions (this repo)

- Read [`Assets/_MH/Scripts/Doc/UI_CONVENTIONS.md`](../../../Assets/_MH/Scripts/Doc/UI_CONVENTIONS.md): on-screen text uses **TextMeshPro** (`TextMeshProUGUI` for Canvas UI), not legacy `UnityEngine.UI.Text`.
- Default prefab output folder: **`Assets/_MH/Prefab/UI/`**. Filename matches the class: **`{ClassName}.prefab`** unless the user specifies another path.
- UI roots use **`RectTransform`**. Child controls are usual **`GameObject` + `RectTransform`** plus the components implied by field types.

## Phase 1 — Design (always first)

1. **Locate the class** (user path, `@ClassName`, or search under `Assets/_MH/Scripts/UI/`).
2. **Read the class and relevant bases** (e.g. `UIWindow` → `UIView`): note only what affects the prefab (`[SerializeField]`, `[Header]`, tooltips). Ignore methods unless they imply required child names.
3. **Map each serialized field** to hierarchy nodes and components:

   | Field type (typical) | GameObject components |
   |---------------------|------------------------|
   | `UnityEngine.UI.Image` | `RectTransform`, `Image` |
   | `TMPro.TMP_Text` | `RectTransform`, `TextMeshProUGUI` |
   | `UnityEngine.UI.Button` | `RectTransform`, `Image` (target graphic), `Button`; optional child **`…Label`** with `TextMeshProUGUI` if a caption is needed (not referenced by script) |
   | `UnityEngine.UI.*` (Toggle, Slider, …) | Match Unity’s default UI setup for that control |
   | `GameObject` / `Transform` / `RectTransform` | Empty node or reference holder as appropriate |

4. **Order and grouping**: Draw order = hierarchy order (first child = back if overlapping). Suggest sensible anchors (full-bleed `Image` for backgrounds; centered rows for titles/buttons).
5. **Present for approval** using the template below. Ask the user to confirm or revise hierarchy, paths, labels, and colors.

### Structure proposal template (copy and fill)

```markdown
## UI prefab proposal: `{ClassName}`

**Output asset:** `Assets/_MH/Prefab/UI/{ClassName}.prefab` (confirm or change)

### Hierarchy
```
{RootName}                    ← RectTransform + {Namespace}.{ClassName}
├── {ChildA}                  ← … + components …
└── …
```

### Serialized wiring
| Field | Node | Components |
|-------|------|------------|
| `_fieldName` | `ChildPath` | … |

### Notes
- TMP: remind user to import TMP essentials if fonts are missing.
- Canvas: prefab is usually parented under a scene **Canvas** + **GraphicRaycaster** at runtime.
```

Stop here until the user **explicitly accepts**.

## Phase 2 — Build (only after acceptance)

1. **Prerequisites**: Unity Editor open with this project; Unity MCP connected (HTTP server). Read the **`script-execute`** skill: [script-execute](../script-execute/SKILL.md).
2. **Preferred tool**: MCP **`script-execute`** — one Roslyn snippet that:
   - Creates the hierarchy with `RectTransform` and UI components.
   - Adds the **exact** script component (`{Namespace}.{ClassName}`) on the root.
   - Wires private serialized fields with **`SerializedObject`** / **`FindProperty("fieldName")`** / **`objectReferenceValue`** (use names **exactly** as in the C# source).
   - Ensures parent folder exists; saves with **`PrefabUtility.SaveAsPrefabAsset`**, then **`DestroyImmediate`** the temp instance; **`AssetDatabase.SaveAssets`** + **`Refresh`**.
3. **Usings** in the snippet typically include: `UnityEngine`, `UnityEngine.UI`, `TMPro`, `UnityEditor`, and the UI script’s namespace (e.g. `MH.UI`).
4. **After the call**: If the MCP client reports a schema warning, **still verify** the `.prefab` file exists under `Assets/_MH/Prefab/UI/` and that YAML references the script GUID and serialized `fileID`s for `_…` fields.
5. **Fallback**: If MCP is unavailable, tell the user to run the same logic from a temporary Editor script or build the prefab in the Editor; **do not** invent YAML prefabs by hand unless unavoidable.

### Minimal `script-execute` shape

- `className` / `methodName`: point at a static `Main()` (or as allowed by the tool).
- C# must be a **single compilable class** with a **static** entry method; no top-level statements.
- Use **`[RuntimeInitializeOnLoadMethod]`**-style patterns only if appropriate; usually unnecessary for prefab creation.

## Related skills

- [unity-initial-setup](../unity-initial-setup/SKILL.md) — `unity-mcp-cli` / Node, opening the project.
- [script-execute](../script-execute/SKILL.md) — MCP JSON shape for `csharpCode`.
- [assets-refresh](../assets-refresh/SKILL.md) — if assets do not appear in the Project window.

## Anti-patterns

- Creating or modifying the prefab **before** the user approves the structure.
- Using legacy **`Text`** for on-screen labels when the field is `TMP_Text` / design calls for TMP.
- Guessing **child names** that the script finds by `transform.Find` without checking the code — search the class for string paths first.
