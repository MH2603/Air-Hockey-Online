# Basic UI system (architecture)

This document describes the **intended** structure for a small, predictable UI layer: a single entry point (`UIManager`), a common base for screens (`UIView`), and two visual layers—**HUD** and **Window**—with a **stack** for modal-style navigation and **Escape** to go back.

**Code location**: `Assets/_MH/Scripts/UI`  
**Singleton base**: `MonoSingleton<T>` in `Assets/_MH/Scripts/Common/MonoSingleton.cs` — `UIManager` derives from `MonoSingleton<UIManager>` (a `MonoBehaviour`) for a single global access point (`UIManager.Instance`).

**Text**: All UI text uses **TextMesh Pro** (`TMP_Text`), not legacy UI Text — see [`UI_CONVENTIONS.md`](UI_CONVENTIONS.md).

---

## Goals

- **One API** to show or hide any view type: `Show<TView>()`, `Hide<TView>()`.
- **Clear layering**: persistent overlay (HUD) vs. stacked, dismissible UI (Window).
- **Simple navigation**: opening a window pushes onto a stack; **Escape** pops the top window (or closes it), revealing the previous one.
- **Decoupled views**: each `UIView` knows how to show/hide itself; the manager coordinates layers, stack, and input.

---

## Core types

### `UIManager : MonoSingleton<UIManager>`

Central registry and coordinator.

**Responsibilities**

- Resolve or instantiate views by type `TView : UIView` (see “Lifecycle” below).
- Route `Show<TView>` / `Hide<TView>` to the correct layer (HUD vs Window).
- Maintain the **window stack**: each `Show` of a window pushes; `Hide` or back removes the matching or top entry.
- Handle **Escape** (e.g. in `Update`) and call **pop** on the window stack when appropriate (and optionally ignore when no window is open).

**Public API (conceptual)**

| Method | Role |
|--------|------|
| `Show<TView>()` | Show view `TView`. If it is a **Window**, push onto stack and bring to front. If **HUD**, show without affecting the window stack. |
| `Hide<TView>()` | Hide view `TView`. If it is the top window, pop from stack; otherwise hide without breaking stack order (define policy: usually hide by instance, not only “top”). |

**Optional helpers** (implementation detail): `PopWindow()`, `ClearWindows()`, `Get<TView>()` for cached instances.

---

### `UIView` (base class)

Base for all UI screens/panels (MonoBehaviour or plain class with a reference to a root `GameObject` / `CanvasGroup`, depending on project convention).

**Responsibilities**

- `Show()` — enable root, play show animation if any, refresh bindings.
- `Hide()` — disable root, cleanup subscriptions if needed.

Concrete views (e.g. `MainMenuWindow`, `SettingsWindow`, `ScoreHud`) inherit `UIView` and implement presentation only; **game rules** stay outside or behind thin presenters.

---

## Layers

### HUD

- **Purpose**: always-on or gameplay UI (score, timers, prompts) that should **not** participate in the window back stack.
- **Behavior**: `Show`/`Hide` toggles visibility only; **no push/pop** on Escape.

### Window

- **Purpose**: menus, dialogs, settings—anything that should behave like “screens” or modals.
- **Behavior**: each `Show` **pushes** that window onto a **stack** (same type may appear more than once if you allow it; often you enforce **single instance per type** and replace or ignore duplicates—pick one policy and document it in code).

When the top window is hidden (explicit `Hide` or Escape), **pop** it and show the previous window if any.

---

## Window stack and Escape

```mermaid
flowchart TB
  subgraph stack [Window stack bottom to top]
    W1[Window A]
    W2[Window B]
    W3[Window C]
  end
  Esc[Escape key]
  Esc -->|pop| W3
  W3 -->|after pop| W2
```

- **Escape**: if the window stack is non-empty, **pop the top window** (call its `Hide()` and remove from stack). If the stack is empty, optionally propagate to gameplay or do nothing.
- **Order**: stack order matches **open order**; only the **top** window should typically receive focus/block input unless you add explicit “modal” rules.

HUD elements are **not** on this stack.

---

## Folder layout

| Area | Path |
|------|------|
| UI scripts | `Assets/_MH/Scripts/UI` |
| This doc | `Assets/_MH/Scripts/Doc/UI_SYSTEM_ARCHITECTURE.md` |

Suggested subfolders under `UI` (optional): `Hud/`, `Windows/`, `Core/` (`UIManager`, `UIView`).

---

## Lifecycle and registration (implementation notes)

- **Prefab vs scene**: common patterns are (1) register prefabs in `UIManager` (dictionary `Type → prefab`), or (2) find pre-placed roots under a `Canvas`. The manager instantiates once per type (pooling optional) and reuses instances.
- **Show&lt;T&gt;**: get or create `T`, call `UIView.Show()`, then if `T` is a Window, push to stack.
- **Hide&lt;T&gt;**: call `UIView.Hide()`, remove from stack if it is a Window (by reference or top match).
- **Threading**: UI must run on Unity’s main thread; `MonoSingleton` resolves or creates the manager on first `Instance` access.

---

## Summary

| Concept | Description |
|---------|-------------|
| `UIManager` | `MonoSingleton` hub: `Show<TView>`, `Hide<TView>`, layer routing, window stack, Escape handling. |
| `UIView` | Base with `Show()` / `Hide()` for each panel. |
| HUD | Non-stacked overlay. |
| Window | Stacked; Escape pops top window. |

This keeps navigation rules in one place (`UIManager`) while each `UIView` stays small and testable.
