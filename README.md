# Unity Web UI

在 Unity 游戏中嵌入**本地 HTML/CSS 页面**，由浏览器引擎负责渲染与排版，Unity 负责显示与游戏逻辑。  
通过 **Action Mapper** 将 HTML 按钮点击绑定到场景中的 **UnityEvent**，实现「页式 UI」的快速开发与迭代。

> **定位：** 展示 + 点击为主的界面（主菜单、商店、设置、活动页、弹窗）。  
> **不替代：** 战斗 HUD、头顶血条、跟单位走的 UI、每帧高频刷新的数字（请继续使用 uGUI / UI Toolkit）。

---

## 目录

- [核心特性](#核心特性)
- [工作原理](#工作原理)
- [适用场景与不适用场景](#适用场景与不不适用场景)
- [系统要求](#系统要求)
- [安装方式](#安装方式)
- [首次配置：Windows GPU 插件](#首次配置windows-gpu-插件)
- [5 分钟快速上手](#5-分钟快速上手)
- [场景搭建详解](#场景搭建详解)
- [Action Mapper 完整工作流](#action-mapper-完整工作流)
- [HTML 与 Action ID 规则](#html-与-action-id-规则)
- [Bridge 消息协议](#bridge-消息协议)
- [运行时 API](#运行时-api)
- [ESC / 频繁开关菜单](#esc--频繁开关菜单)
- [Editor 菜单说明](#editor-菜单说明)
- [性能与内存](#性能与内存)
- [与 uGUI 如何分工](#与-ugui-如何分工)
- [AI 生成 HTML 工作流](#ai-生成-html-工作流)
- [示例包 Samples](#示例包-samples)
- [包目录结构](#包目录结构)
- [常见问题排查](#常见问题排查)
- [维护者与发版清单](#维护者与发版清单)
- [版本与许可证](#版本与许可证)

---

## 核心特性

| 特性 | 说明 |
|------|------|
| **HTML/CSS 即 UI** | 用 Web 技术做布局、动画、字体；视觉效果与浏览器一致 |
| **Windows GPU 路径** | WebView2 + D3D11 共享纹理，性能优于纯 CPU 抓屏 |
| **Editor 实时预览** | Action Mapper / WebView Preview 中直接预览 HTML |
| **按钮自动扫描** | 自动识别 `<button>` 与 `data-unity-action` |
| **UnityEvent 绑定** | 绑定保存在场景 `WebViewActionDispatcher` 上，可拖 Hierarchy 对象 |
| **页式开关** | `SetPageVisible` / `WebViewPageToggle`：ESC 菜单无需 Destroy WebView |
| **UPM 包** | `com.unitywebui.core`，Git / 本地 / 嵌入式安装 |
| **可选 gree 回退** | GPU 不可用时可用 CPU 位图回退（**不装也能编译**；Setup 里一键安装） |

---

## 工作原理

```
┌─────────────────────────────────────────────────────────────┐
│  你的 HTML/CSS/JS（StreamingAssets/WebUI/...）               │
└──────────────────────────┬──────────────────────────────────┘
                           │ file:// 加载
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  WebView2（离屏浏览器，Chromium 内核）                        │
│  · 渲染页面、处理 CSS 动画、:hover、:active                   │
│  · unity-bridge.js 监听 click → postMessage(JSON)            │
└──────────────────────────┬──────────────────────────────────┘
                           │ WGC / GPU 抓帧 或 CPU 回退
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  Unity 纹理 → RawImage（Canvas 上显示）                      │
│  WebViewPointerRelay 转发点击坐标 → WebView                  │
└──────────────────────────┬──────────────────────────────────┘
                           │ type: "action", id: "..."
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  WebViewBridge → WebViewActionDispatcher → UnityEvent        │
│  你在 Inspector 里绑定的方法被调用                              │
└─────────────────────────────────────────────────────────────┘
```

**数据流向简述：**

1. **HTML → Unity（已实现）：** 用户点击按钮 → bridge 发送 `action` 消息 → `UnityEvent.Invoke()`  
2. **Unity → HTML（需自行扩展）：** 调用 `ExecuteJavaScript` / 后续可封装数据通道；本模块不内置 MVVM  
3. **显示路径：** 每帧或按间隔 capture WebView 画面 → 贴到 `RawImage`（比浏览器直接显示多约 1～2 帧延迟）

---

## 适用场景与不适用场景

### 适合用 Unity Web UI

- 主菜单、标题画面  
- 商店、背包（以展示和点击为主）  
- 设置、帮助、公告、活动 H5 风格页面  
- 需要 **快速迭代视觉**、或 **AI 生成静态 HTML** 的界面  
- **ESC 暂停菜单**（配合 `SetPageVisible`，同一 HTML 反复开关）  
- 以 **点击触发 Unity 逻辑** 为主的交互（开始游戏、购买、关闭窗口等）

### 不建议用 Unity Web UI（请用 uGUI）

- 战斗 HUD：血条、技能 CD、资源数字 **每帧刷新**  
- **世界空间 UI**：头顶血条、伤害飘字、选中框  
- 需要 **点击穿透到 3D 场景**（点地图选单位）的全屏层  
- 需要 **与 Input System 深度整合** 的快捷键焦点（部分能力未完整封装）  
- 非 Windows 平台作为 **主力** 方案（当前 GPU 路径为 Windows 专用）

---

## 系统要求

| 项目 | 要求 |
|------|------|
| **Unity** | 2022.3 LTS 及以上（包内声明 `2022.3`） |
| **操作系统** | Windows 10/11（GPU 主路径） |
| **图形 API** | **D3D11**（Project Settings → Player → Graphics） |
| **WebView2 Runtime** | 系统需安装 [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)（Win11 通常已带） |
| **可选** | [net.gree.unity-webview](https://github.com/gree/unity-webview)（Setup 菜单一键安装，仅 GPU 失败时需要） |

---

## 安装方式

包名：**`com.unitywebui.core`**

**导入后第一步（推荐）：** 菜单 **Window → Unity Web UI → Setup Project**  
检查 D3D11、GPU DLL，并可一键安装可选 gree 回退。

> **无需再手动装 gree 才能编译。** gree 仅为 GPU 不可用时的可选 CPU 回退。

### 方式 A：Git URL

编辑项目 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.unitywebui.core": "https://github.com/PeterParkers007/Tech-Cosmos.Framework.UnityWebUI.git"
  }
}
```

保存后 Unity 会自动拉取。若 Windows 报 `PackageCache` 的 `EPERM`，见 [常见问题](#缺少-gree-依赖)。

### 方式 B：本地文件夹（最省心）

1. 将仓库复制到 `Packages/com.unitywebui.core`（保留根目录 `package.json`）  
2. `manifest.json` 添加：

```json
"com.unitywebui.core": "file:com.unitywebui.core"
```

### 方式 C：放在 Assets 下（嵌入式包）

将包文件夹放在 `Assets/` 下并保留 `package.json`（例如 `Assets/UnityWebUI`）。**可直接编译，不依赖 manifest 拉 gree。**

---

## 首次配置：Windows GPU 插件

GPU 后端依赖原生插件 `UnityWebUI.WebView2Gpu.dll`。若仓库已包含预编译 DLL，可跳过构建；否则按下列步骤操作。

### 步骤

1. 在 **Windows** 上打开 Unity 项目  
2. 确认 **Graphics API** 为 **Direct3D11**（不要仅 OpenGL/Vulkan）  
3. 菜单：**Window → Unity Web UI → Build Windows GPU Plugin**  
   - 需要本机安装 Visual Studio / MSBuild 与 Windows SDK  
4. 菜单：**Window → Unity Web UI → Apply GPU Plugin Update**  
5. **完全退出并重启 Unity Editor**（仅 Script Reload 无法重载 native DLL）

### 验证

- 菜单：**Window → Unity Web UI → Diagnose GPU Backend**  
- Action Mapper 状态栏应出现 `gpu/` 或 `gpuTex` 相关字样，而非长期 `gree/cpu-bitmap`

### GPU 不可用时的行为

自动回退 **gree unity-webview**（CPU 位图上传），功能可用但：

- 预览与运行时帧率更低  
- 高分辨率页面更吃 CPU  

---

## 5 分钟快速上手

1. **准备 HTML**  
   在 `Assets/StreamingAssets/WebUI/menu/index.html` 放置页面（可参考 `Samples~/BasicWebUI`）。

2. **创建 UI 容器**  
   `GameObject → UI → Canvas`，其下创建 **RawImage**（铺满或按需尺寸）。

3. **添加 WebViewHost**  
   - 新建空物体，添加组件 **Web View Host**  
   - **Display** 拖入上一步的 RawImage  
   - **Html Path Override** 填 `WebUI/menu/index.html`（相对 StreamingAssets）  
   - 或创建 **Web UI View Binding Profile** 并在 Profile 里指定 HTML 路径  

4. **打开 Action Mapper**  
   **Window → Unity Web UI → Action Mapper**  
   - 指定 **Host**  
   - 选择 HTML 文件  
   - 在列表中选中按钮 → 在 **On Invoked** 拖入场景对象与方法  

5. **保存场景**（绑定在 `WebViewActionDispatcher` 上，必须保存）  

6. **Play** → 点击 HTML 按钮 → Console / 游戏逻辑应响应  

---

## 场景搭建详解

### 必需组件关系

```
GameObject (WebViewHost)
├── WebViewHost              ← 加载 HTML、驱动 WebView、提供 Bridge
├── WebViewActionDispatcher  ← 运行时 action → UnityEvent（自动添加）
└── （可选）WebViewPageToggle ← ESC 开关

Canvas
└── RawImage                 ← Display，显示 WebView 纹理
    └── WebViewPointerRelay  ← 自动添加，转发指针事件
```

### WebViewHost 主要字段

| 字段 | 说明 |
|------|------|
| **Width / Height** | WebView 内部分辨率；建议与 RawImage 显示尺寸接近，避免浪费 |
| **Bitmap Refresh Cycle** | 抓帧间隔（1 = 每调度帧抓一次）。静态页可设为 2～4 省性能 |
| **Render Scale** | GPU 内部渲染比例 0.25～1，越低越快、越糊 |
| **Transparent Background** | 透明背景（HUD 式浮层时可开） |
| **Binding Profile** | 可选 ScriptableObject，存 HTML 路径与元数据 |
| **Display** | **必填（Play 模式）**：用于显示与接收点击的 RawImage |
| **Load Profile Html On Start** | 启动时自动加载 Profile 中的 HTML |
| **Html Path Override** | 直接指定 HTML 路径（优先于 Profile） |
| **Visible On Start** | 取消勾选则开局隐藏且不抓帧（ESC 菜单常用） |

### WebViewActionDispatcher

- **UnityEvent 必须绑在本组件上**，不要只绑在 Profile ScriptableObject 上（SO 无法引用场景 Hierarchy 对象）。  
- Action Mapper 在指定 Host 后会编辑此组件上的 `_bindings` 列表。  
- 绑定后务必 **Ctrl+S 保存场景**。

### RawImage 注意点

- 模块会自动设置 `raycastTarget = true` 以接收点击。  
- 全屏 RawImage 会拦截其矩形内所有射线；若需点透到游戏，不要用全屏 WebView 盖战斗画面（见「与 uGUI 分工」）。

---

## Action Mapper 完整工作流

**打开：** Window → Unity Web UI → Action Mapper  

### 界面区域

1. **Host / Profile**  
   - **Host：** 场景中的 `WebViewHost`  
   - **Profile：** 可选，用于记录 HTML 路径与 binding 元数据  

2. **预览区**  
   - 实时显示 HTML（Editor 内 WebView）  
   - 支持点击预览（Editor 内不会触发 Play 模式 UnityEvent，仅验证页面）  

3. **Buttons / Actions 列表**  
   - 自动扫描页面中所有 `<button>`  
   - 显示解析出的 **Action ID** 与绑定状态  

4. **绑定面板**  
   - 选中某 action → 编辑 **On Invoked**（UnityEvent）  
   - 目标方法须为 **`public void MethodName()`**（无参数）  

### 推荐流程

1. 在 StreamingAssets 写好 HTML  
2. Action Mapper 选择 HTML 文件 → 确认预览正常  
3. 指定 Host → 列表同步到 **WebViewActionDispatcher**  
4. 逐个绑定 On Invoked  
5. **保存场景** + 保存 Profile（若使用）  
6. Play 模式验证  

### 从 Inspector 进入

选中带 `WebViewHost` 的对象 → Inspector 底部 **Open Action Mapper**。

---

## HTML 与 Action ID 规则

Bridge 脚本：`StreamingAssets/WebUI/unity-bridge.js`（包内另有 `Resources` 副本，构建后仍可用）。

### 触发 Action 的方式

**1. 显式属性（推荐，ID 稳定）**

```html
<button data-unity-action="open_shop">打开商店</button>
<div data-unity-action="close_panel">关闭</div>
```

**2. 普通 `<button>`（自动解析 ID）**

优先级：

1. `data-unity-action`  
2. HTML `id` 属性  
3. 按钮文本 slug（英文/数字/中文，过长截断，重复则加 `-2` 后缀）  
4.  fallback：`button-0`、`button-1`…（按 DOM 中 button 顺序）

**示例：**

```html
<button>开始游戏</button>          <!-- id 可能为：开始游戏 -->
<button id="settings">设置</button> <!-- id 为：settings -->
```

**Action Mapper 里显示的 ID 必须与运行时一致。** 改 HTML 文字后若 slug 变了，需重新绑定。

### 链接导航

普通 `<a href="other.html">` 会发送 `navigate` 消息（非 action）。`WebViewHost.Bridge.NavigateRequested` 可订阅。  
`href="#anchor"` 不会导航。

### 页面内资源

- CSS/JS/图片与 HTML **同目录或相对路径**  
- 使用 `StreamingAssets` 本地路径，通过 `file://` 加载  
- 不建议依赖外网 CDN（离线、打包路径问题）

---

## Bridge 消息协议

JSON 经 WebView2 `postMessage` 传到 `WebViewBridge.HandleRawMessage`。

| type | 字段 | 说明 |
|------|------|------|
| `action` | `id` | 按钮/action 触发，转发到 `ActionClicked` |
| `navigate` | `href` | 页面内链接导航请求 |

**订阅示例：**

```csharp
webViewHost.Bridge.ActionClicked += actionId => Debug.Log(actionId);
webViewHost.Bridge.NavigateRequested += href => Debug.Log(href);
```

---

## 运行时 API

### WebViewHost

```csharp
// 显示/隐藏页面（隐藏时停止抓帧，WebView 不销毁）
host.SetPageVisible(true);
host.SetPageVisible(false);
bool visible = host.IsPageVisible;

// 加载 HTML（绝对路径或 StreamingAssets 相对路径）
host.LoadLocalHtml(path);

// 指定 Display RawImage
host.SetDisplay(rawImage);

// 访问 Bridge、后端状态、纹理
host.Bridge;
host.Backend;
host.ViewTexture;
host.Status;
```

### WebViewPageToggle

挂在与 `WebViewHost` 同一物体或指定 Host：

- 默认 **ESC** 切换显示  
- `Show()` / `Hide()` / `Toggle()`  
- 可在 Inspector 修改按键  

### 代码绑定 Action（可选）

通常用 Action Mapper 即可。若运行时动态绑定，操作 `WebViewActionDispatcher` 的 `Bindings` 列表（或通过 `FindOrCreate(actionId)`）。

---

## ESC / 频繁开关菜单

**需求：** 战斗中反复按 ESC 开关同一 HTML 菜单，且不想每次重建 WebView。

**做法：**

1. `WebViewHost`：**Visible On Start = false**  
2. 添加 **Web View Page Toggle**（或代码 `SetPageVisible`）  
3. **不要** 每次关闭时 `Destroy` Host  

**行为：**

| 状态 | 抓帧 | WebView | 再打开速度 |
|------|------|---------|------------|
| `SetPageVisible(false)` | 停止 | 保留在内存 | 快 |
| `Destroy(host)` | 无 | 释放 | 慢（需重载 HTML） |

**内存：** 关闭后仍占用 WebView2 进程内存（约 100～300MB 量级），但 **不再占用每帧 CPU/GPU 抓帧**。

**不适合 Destroy 再建的场景：** ESC 暂停、背包、地图等 **同一页面高频 toggle**。  
**适合 Destroy 的场景：** 从战斗回主菜单、换完全不同的 HTML 页。

---

## Editor 菜单说明

| 菜单项 | 作用 |
|--------|------|
| **Window → Unity Web UI → Action Mapper** | 预览 HTML、扫描按钮、绑定 UnityEvent |
| **Window → Unity Web UI → WebView Preview** | 单独预览 HTML 文件 |
| **Assets → Open in WebView Preview** | 在 Project 中右键 HTML 预览 |
| **Window → Unity Web UI → Build Windows GPU Plugin** | 编译 native DLL |
| **Window → Unity Web UI → Apply GPU Plugin Update** | 复制 DLL 到 Plugins |
| **Window → Unity Web UI → Diagnose GPU Backend** | 诊断 GPU / 回退状态 |

---

## 性能与内存

### 开着 WebView 页面时

- **内存：** WebView2 进程 + Unity 纹理（1080p BGRA 约 8MB/张，外加 GPU 资源）  
- **CPU/GPU：** 每 `Bitmap Refresh Cycle` 抓帧同步；动画/CSS 过渡越多，capture 越频繁  
- **延迟：** 比直接浏览器多约 1～2 帧 + 点击视觉_hold（约 60ms 用于 `:active` 效果）

### 关闭显示（SetPageVisible false）

- **抓帧：停止**  
- **内存：WebView2 仍在**（进程级占用保留）  

### 优化建议

| 手段 | 效果 |
|------|------|
| 菜单关闭用 `SetPageVisible` 而非 Destroy | 快速开关 + 省帧时间 |
| 进战斗 **Destroy / 卸场景** WebViewHost | 释放内存 |
| `Bitmap Refresh Cycle = 2～4`（静态页） | 降低 capture 频率 |
| Host 分辨率匹配 RawImage 实际大小 | 减少无效像素 |
| `Render Scale = 0.75`（商店/菜单） | 省 GPU，略降清晰度 |
| 避免页内全屏 video / 复杂 CSS 无限动画 | 降低 WebView2 与 capture 负载 |

---

## 与 uGUI 如何分工

| 界面类型 | 推荐技术 |
|----------|----------|
| 主菜单、商店、设置、活动页 | **Unity Web UI（HTML）** |
| 战斗 HUD、血条、CD、飘字 | **uGUI / UI Toolkit** |
| ESC 暂停（大 HTML 页） | Web UI + `SetPageVisible` |
| ESC 暂停（仅继续/设置 3 按钮） | uGUI 更轻 |

**协作方式：** HTML 按钮 `UnityEvent` → 打开 uGUI 面板 / 切换游戏状态 / `LoadScene`，无需 HTML 与 uGUI 双向绑定。

---

## AI 生成 HTML 工作流

1. 用 AI 生成 **完整静态页**（单文件或 HTML + CSS + 图片）  
2. 放入 `StreamingAssets/WebUI/...`  
3. Action Mapper 预览 → 微调样式  
4. 绑定按钮到 Unity 方法（开始游戏、打开场景等）  
5. Play 验证  

**优势：** AI 擅长 HTML/CSS，比生成 Unity Prefab 可靠；改文案/布局只改文件，无需重搭 RectTransform。

**注意：** 为每个需要绑定的按钮加 `data-unity-action="唯一id"`，避免 slug 随文案变化。

---

## 示例包 Samples

**Package Manager → Unity Web UI → Samples → Basic Web UI Page → Import**

包含示例 `index.html` 与 README。导入后将 HTML 复制到 `Assets/StreamingAssets/WebUI/` 并按说明搭场景。

---

## 包目录结构

```
UnityWebUI/
├── package.json                 # UPM 包清单
├── README.md
├── CHANGELOG.md
├── Runtime/                     # 运行时代码
│   ├── WebView/                 # Host、Bridge、GPU 后端、Pointer 等
│   ├── Resources/UnityWebUI/    # bridge 脚本（构建备用）
│   └── UnityWebUI.Runtime.asmdef
├── Editor/                      # Action Mapper、Preview、GPU 构建
├── Plugins/Windows/x86_64/      # UnityWebUI.WebView2Gpu.dll
├── StreamingAssets/WebUI/       # unity-bridge.js、示例页
├── Native/Windows/              # C++ 源码与 build 脚本
└── Samples~/BasicWebUI/         # 可选导入示例
```

---

## 常见问题排查

### Play 模式点击无反应

1. **Display** 是否指定 RawImage？  
2. 场景是否保存？绑定是否在 **WebViewActionDispatcher** 上？  
3. Action ID 是否与 HTML 一致（看 Action Mapper 列表）？  
4. UnityEvent 方法是否为 **public void Xxx()**？  
5. GPU 插件是否已 Apply 并 **重启 Unity**？

### 预览正常、Play 不行

- 检查 Host 的 **Html Path Override / Profile** 与 Play 加载路径一致  
- 确认 `StreamingAssets` 路径在构建后存在  

### 画面黑屏 / 无纹理

- **Diagnose GPU Backend** 查看是否 CPU 回退且尚未出首帧  
- 等 1～2 秒或确认 WebView2 Runtime 已安装  
- D3D11 是否启用  

### 颜色/显示与浏览器略有差异

- GPU 纹理垂直翻转已由 `WebViewDisplayUtility` 处理  
- 确认 `Render Scale` 与分辨率  

### 点击感觉比浏览器钝

- 架构上存在 capture 延迟；可接受则用于页式 UI  
- 战斗 HUD 请用 uGUI  

### 关菜单后仍占内存

- 正常：WebView2 进程保留；用 `SetPageVisible` 时 **不抓帧**  
- 要释放内存请 **Destroy Host** 或卸载场景  

### 缺少 gree / Git 安装 EPERM

- **编译报错 Gree 找不到：** v1.0+ 已不要求 gree；更新到最新包即可  
- **GPU 不可用且未装 gree：** 菜单 **Window → Unity Web UI → Install Optional Gree Fallback**  
- **Git URL 报 `PackageCache` EPERM：** 关掉 Unity/杀毒/资源管理器对 `Library` 的占用，删 `Library/PackageCache/.tmp-*` 后重开；或改用 **方式 B 本地 `file:` 安装**

---

## 维护者与发版清单

发布 `com.unitywebui.core` 前建议确认：

- [ ] `Plugins/Windows/x86_64/UnityWebUI.WebView2Gpu.dll` 已构建并提交（或 Release 附件）  
- [ ] `package.json` 的 version、repository URL 已更新  
- [ ] 新工程通过 Git URL 导入可编译  
- [ ] Action Mapper 预览 + Play 点击绑定回归通过  
- [ ] `Samples~/BasicWebUI` 可导入  
- [ ] `CHANGELOG.md` 已记录版本变更  

---

## 版本与许可证

- 当前包版本见 `package.json`（`1.0.0`）  
- 变更记录见 [CHANGELOG.md](./CHANGELOG.md)  
- 许可证见仓库根目录 LICENSE 文件  

---

## 一句话总结

**Unity Web UI = 用本地 HTML 做页式游戏 UI，Action Mapper 绑按钮，Play 里贴到 RawImage；频繁开关用 `SetPageVisible`，战斗 HUD 继续用 uGUI。**
