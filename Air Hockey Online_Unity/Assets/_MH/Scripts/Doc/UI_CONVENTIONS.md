# UI conventions

## Text: TextMesh Pro only

All **on-screen UI text** in this project uses **TextMesh Pro** (`TMPro.TMP_Text` / `TextMeshProUGUI`), not the legacy **`UnityEngine.UI.Text`** component.

- Prefer **`TMP_Text`** in `[SerializeField]` fields when a script needs a reference to a label (works for both `TextMeshPro` and `TextMeshProUGUI` in the hierarchy).
- In prefabs and scenes, add **UI → Text - TextMeshPro** (not **Legacy → Text**).
- **First-time setup** in a new clone: if labels look wrong or fonts are missing, use **Window → TextMeshPro → Import TMP Essential Resources** once so default font assets exist.

**Prefabs**: Screen prefabs (for example `UIMainMenu`) live under `Assets/_MH/Prefab/UI/`. Build or update them in the Unity Editor, or via your **Unity MCP** integration in Cursor when that server is connected and exposes asset/prefab tools (this repo does not ship an Editor script for that).

Related: [`UI_SYSTEM_ARCHITECTURE.md`](UI_SYSTEM_ARCHITECTURE.md) (UIManager prefab list, `ShowWhenStart`, layers, window stack).
