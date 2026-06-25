# Basic Web UI Sample

## Import

1. **Package Manager → Unity Web UI → Samples → Import** this sample.
2. Copy `WebUI/index.html` to your project:
   `Assets/StreamingAssets/WebUI/index.html`

## Scene setup

1. **Canvas → RawImage** (full screen or panel size).
2. Empty GameObject + **WebView Host**:
   - **Display** = RawImage
   - **Html Path Override** = `WebUI/index.html` (under StreamingAssets)
   - **Visible On Start** = on
3. **WebView Action Dispatcher** is added automatically.
4. **Window → Unity Web UI → Action Mapper** — bind `sample_hello` to a public method.

## ESC menu (optional)

- **Visible On Start** = off
- Add **Web View Page Toggle** on the same object
