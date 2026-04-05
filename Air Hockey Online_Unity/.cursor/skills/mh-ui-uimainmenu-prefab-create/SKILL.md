---
name: mh-ui-uimainmenu-prefab-create
description: "Creates Assets/_MH/Prefab/UI/UIMainMenu.prefab: full-screen blocking background (Image), title (TMP), Start button with TMP label. Requires TMP essentials imported (Window → TextMeshPro → Import TMP Essential Resources) for default font."
---

# MH / UI / UIMainMenu Prefab / Create

## How to Call

```bash
unity-mcp-cli run-tool mh-ui-uimainmenu-prefab-create --input '{}'
```


### Troubleshooting

If `unity-mcp-cli` is not found, either install it globally (`npm install -g unity-mcp-cli`) or use `npx unity-mcp-cli` instead.
Read the /unity-initial-setup skill for detailed installation instructions.

## Input

This tool takes no input parameters.

### Input JSON Schema

```json
{
  "type": "object",
  "additionalProperties": false
}
```

## Output

### Output JSON Schema

```json
{
  "type": "object",
  "properties": {
    "result": {
      "type": "string"
    }
  },
  "required": [
    "result"
  ]
}
```

