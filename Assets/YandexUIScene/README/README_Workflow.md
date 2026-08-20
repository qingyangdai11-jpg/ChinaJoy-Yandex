# ⚙️ 游戏代码流程与架构说明 (README_Workflow)

本文档面向开发者，用于阐述游戏的整体代码控制流、核心脚本职责、状态转换细节以及针对常见 Unity 交互问题的优化方案。

---

## 1. 核心架构设计

整个系统的架构可分为三个层级：
1. **控制层 (Controller)**：单例模式的全局进度状态机 `GameFlowController`，这是游戏主循环与逻辑的核心。
2. **表现层 (UI & Animation)**：包含 `PageSwitcher`（页面管理器）、`SimpleUIManager`（暂停/恢复菜单）和 `SceneReLoad`（场景重载与黑屏渐变）。
3. **实体控制层 (Entity & Input)**：`SimplePlayerController`（玩家移动与攻击）。

---

## 2. 项目目录结构 (Project Directory Structure)

项目遵循 **“依赖与业务分离”** 和 **“基于功能的模块化”** 设计原则，整体结构划分如下：

```text
Assets/
├── Plugins/                   # 第三方库与 SDK
│   └── Demigiant/             # DOTween 插件，用于处理 UI 动效、淡入淡出及动画过渡
├── Resources/                 # 需要动态加载的资源
├── Samples/                   # 示例资源包
├── Settings/                  # 渲染管线与项目全局配置文件
├── StreamingAssets/           # 外部资源或游戏配置文件目录
├── TextMesh Pro/              # TMPro 依赖资源（字体资产与材质）
└── YandexUIScene/             # 自定义业务模块（Yandex UI Flow 核心模块，可整体导出）
    ├── CommandScripts/        # 状态机与网络/排行榜模块 (如 GameFlowController.cs, YandexWebSocketClient.cs, YandexLeaderboardManager.cs, YandexGameOverLeaderboard.cs, YandexQRCodeGenerator.cs, YandexGameSessionInitializer.cs)
    ├── CommonScripts/         # 通用工具与 UI 逻辑（如 PageSwitcher, SceneReLoad 等）
    ├── Fonts/                 # 场景 UI 特有字体
    ├── Images/                # 场景 UI 图片及素材
    ├── Inputs/                # 输入系统配置（新 Input System Actions 映射文件）
    ├── README/                # 模块说明文档（导出时随模块打包带走）
    │   ├── README_Workflow.md # 本文档（开发流程与架构说明）
    │   ├── README_Buttons.md  # 按键交互说明
    │   └── update_*.md        # 模式切换、连接逻辑与会话初始化等各项更新记录文档
    ├── Scenes/                # 场景文件（GameUI.unity 及其备份与测试场景）
    └── Workflow/              # 工作流说明备忘录（WorkflowSummary.txt 等）
```

---

## 3. 依赖与插件配置 (Dependencies & Plugins)

项目集成了以下核心插件和 Package 以支撑 UI 与游戏交互逻辑：
1. **DOTween (Demigiant)**
   * **作用**：控制 UI 页面的 CanvasGroup 渐显/渐隐，并在 `CountdownStart` 状态下控制数字的放大与渐隐缩放特效。
   * **使用位置**：`PageSwitcher.cs`（页面淡入淡出）、`CountdownTimer_OneNumber.cs`（计时器数字动效）。
2. **Unity New Input System**
   * **作用**：提供跨平台键盘、鼠标及手柄的输入绑定。将物理输入（如 Space、Enter、Gamepad South）抽象为 Action，隔离了输入硬件与业务逻辑。
   * **使用位置**：`GameFlowController.cs`（全局确认/取消）、`SimplePlayerController.cs`（玩家移动与攻击）、`SimpleUIManager.cs`（菜单导航）。
3. **TextMesh Pro (TMP)**
   * **作用**：用于高质量的文本呈现与排版，支持在倒计时、语言选择和结算页面使用动态富文本与字体动画。
   * **使用位置**：场景中所有的文本显示组件。

---

## 4. 状态机与页面索引映射 (GameState & Page Index)

`GameFlowController` 内部维护了游戏生命周期状态枚举 `GameState`。每个状态会触发 `PageSwitcher` 切换对应的 Canvas 页面：

| 游戏状态 (`GameState`) | 页面索引 (`Page ID`) | 说明 |
| :--- | :--- | :--- |
| `Idle` | **0** | 待机首页，按确认键进入下一步。 |
| `LanguageSelect` | **1** | 语言选择页面，记录所选语言。 |
| `RuleRead` | **2** | 规则阅读页面。 |
| `CountdownStart` | **3** | 开始游戏前的 3, 2, 1 倒计时。 |
| `Gameplay` | **4** | 核心游戏状态，激活游戏倒计时（60秒）。 |
| `GameOver` | **4** (保持) | 触发游戏结束，展示 "Finish" 弹窗（持续2秒）。 |
| `GameOverPage` | **5** | 结算与结束页面，运行自动返回首页倒计时（10秒）。 |

---

## 5. 核心脚本职责与实现原理

### 📁 YandexUIScene/CommandScripts/GameFlowController.cs (总进度控制器)
* **状态机流转**：通过 `SetState(GameState newState)` 驱动状态过渡，并利用协程处理 `StartCountdownRoutine`（开始倒计时）以及 `GameOverPageRoutine`（结束页倒计时）。
* **独立输入 Action 绑定**：
  - 动态配置了 `Confirm` Action（Space / Enter / Gamepad South）与 `Cancel` Action（Esc / Backspace / Gamepad East）。
* **防按键穿透（Double-Click Bleed-through）保护**：
  - 记录状态切换时的时间戳 `stateEntryTime = Time.unscaledTime`。
  - 在 `Update` 的按键检测和语言按钮的 `onClick` 事件中增加 `Time.unscaledTime - stateEntryTime < 0.2f` 的限制，过滤前 0.2 秒内的输入，以防止上一页面按下的确认键穿透影响新页面。
* **协程安全性**：
  - 维护了各个协程的引用（`Coroutine`），避免使用破坏性的全局 `StopAllCoroutines`，保证 `GameOverSequenceRoutine` 能够平滑度过 2 秒的过渡期再跳入结束页面。

### 📁 YandexUIScene/CommonScripts/PageSwitcher.cs (页面切换控制器)
* **DOTween 渐变**：对 `CanvasGroup` 的 `alpha` 进行 0 到 1 的渐显控制。
* **手柄高亮同步优化**：
  - **淡入开始时即设置 `interactable = true`**：使按钮在渐变刚开始时就是可交互的，从而让 `EventSystem.SetSelectedGameObject` 能够同步高亮默认按钮。
  - **淡入结束后才设置 `blocksRaycasts = true`**：防止鼠标悬停的射线检测在渐变中产生干扰，确保在渐变过程中**只有且必定是**手柄默认选中的按钮呈高亮状态。

### 📁 YandexUIScene/Input/InputTest/SimplePlayerController.cs (玩家控制器)
* **状态阻断机制**：
  - 只有当 `GameFlowController.Instance.CurrentState == GameState.Gameplay` 且游戏**未处于暂停状态**时，键盘/手柄的物理移动和攻击指令才会被派发。
  - 其他状态下，`moveInput` 会被强制设定为 `Vector2.zero`，确保玩家保持待机静止。

### 📁 YandexUIScene/Input/InputTest/SimpleUIManager.cs (暂停界面管理器)
* **手柄焦点丢失自动定位**：
  - 监听 `EventSystem.current.currentSelectedGameObject`。一旦其由于玩家鼠标点击空白处而变为 `null` 时，系统会自动从 `lastSelectedGameObject` 中恢复聚焦，或者退回 `defaultSelectedButton`，保证手柄控制永远有效。
  - 只允许在 `Gameplay` 状态下使用暂停键呼出菜单。

### 📁 YandexUIScene/CommonScripts/SceneReLoad.cs (场景加载与渐变)
* **非缩放时间更新**：
  - 渐显与渐隐动画增加了 `.SetUpdate(true)` 声明，使得在 `Time.timeScale = 0` (暂停状态下) 也能够平稳渲染黑屏渐变。
* **基于模式的动态场景加载**：
  - 加载场景前自动将 `Time.timeScale` 恢复成 `1f`。读取 PlayerPrefs 中的 "GameMode"，动态重载对应的 `GameONLINE` 或 `GameOFFLINE` 场景，而非写死 Scene 索引。

### 📁 YandexUIScene/CommonScripts/CountdownTimer_OneNumber.cs (单数字倒计时器)
* **倒计时显示与动画**：
  - 控制 3, 2, 1, GO! 等文本的更新。
  - 配合 DOTween 对倒计时数字实施“放大缩水 + 渐隐”等动效，提升 UI 动态质感。

### 📁 YandexUIScene/CommonScripts/GameModeSelector.cs (游戏模式选择器)
* **连按五次唤出菜单**：
  - 在**游戏任何阶段/状态下**，连续按下 **5 次**“菜单键”（Escape / Gamepad Start）即可呼出模式/场景切换菜单。再次按菜单键或“返回键”（Backspace / Gamepad B）即可关闭。
* **呼出游戏自动暂停**：
  - 呼出模式切换菜单时，游戏会**自动暂停**（`Time.timeScale` 设为 `0f`），在关闭选单或模式切换重载时，恢复原有的 `Time.timeScale`，防止干扰其他正常暂停。
* **PlayerPrefs 模式存储与场景重载**：
  - 模式分为 `ONLINE` 与 `OFFLINE`（默认 `ONLINE`）。在模式选择菜单中确认后，保存至 PlayerPrefs 并自动执行场景淡出与重载。
* **手柄高亮与焦点定位**：
  - 仿照 `SimpleUIManager` 实现手柄/键盘焦点重定位机制。开启菜单时，依据当前 PlayerPrefs 记录自动聚焦对应的按钮。

### 📁 YandexUIScene/CommandScripts/YandexWebSocketClient.cs (长连接网络客户端)
* **即时感知心跳机制**：
  - 每 2 秒发送一次 `{"type":"ping"}` 并在 5.5 秒内若未收到任何服务器响应则强制断开以开启重连，大幅缩短公网丢包感知时间。
* **零延迟秒级重连**：
  - 在断网第一瞬间立刻发起重连，避免了之前的 3 秒无意义等待。
* **防 UI 闪烁与淡出安全**：
  - 重连轮询期间保证 Canvas 面板不再重置闪烁，断线瞬间通过停止淡出协程并终止 DOTween 动画，确保网络错误面板处于绝对可见状态。

### 📁 YandexUIScene/CommandScripts/YandexQRCodeGenerator.cs (二维码生成器)
* **平滑淡入（1.5s）**：
  - 根据网络连接状态，在连线成功时，控制二维码 RawImage 的 `CanvasGroup` 在 **1.5 秒** 内平滑渐显（DOFade In），提升交互连贯性。
* **离线自动停用**：
  - 游戏处于 `OFFLINE` 模式时，自动停用协程并隐藏 RawImage，避免不必要的 CPU 消耗。

### 📁 YandexUIScene/CommandScripts/YandexLeaderboardManager.cs (实时轮询排行榜)
* **连接建立期静默**：
  - 游戏初次启动与连接服务器（WSSCanvas 打开）期间，排行榜完全保持清空与隐藏状态，不发起任何 HTTP 网络请求。
* **断线隐藏与限流**：
  - 检测到网络断开重连时，排行榜自动清空数据并渐隐关闭，且停止向外网发送 API 请求以节省性能与带宽。
* **精准触发拉取（场景重启/网络恢复）并初始化会话**：
  - 只有在首次连接或断网重连成功、且 `WSSCanvas` 完全关闭的瞬间，以及场景重载/重启后，才由 WebSocket 客户端主动触发 `TriggerFetch()` 开始高频轮询，并在状态确认为 Connected 的瞬间触发 `YandexGameSessionInitializer.Instance.InitializeSession()` 进行服务器端游戏会话重置。
* **瀑布级联排序动画**：
  - 仅对排名发生变动的行从上到下按 0.08 秒间隔瀑布式淡出、刷新、淡入，呈现丝滑动态排版。
* **可配刷新频率与 Debug 数据输出**：
  - 增加公共参数 `fetchInterval`（默认 `2.0` 秒）以供在 Inspector 中灵活调整刷新间隔。在 `enableDebugLogs` 开启时，会详细输出拉取到的 raw JSON 串以及解析后的每一项排行榜条目。

### 📁 YandexUIScene/CommandScripts/YandexGameSessionInitializer.cs (服务器端会话初始化器)
* **游戏会话重置接口对接**：
  - 当排行榜观测器确认网络连通 the 瞬间，向服务器发送 `POST https://yandexa.bbtech.cc/api/machine/reset` 请求，以 JSON 格式发送当前机器的 `machineId`，以初始化本局游戏会话状态。
  - 支持从 WebSocket 客户端提取标识或读取本地 `config.json` 配置文件。

### 📁 YandexUIScene/CommandScripts/YandexGameOverLeaderboard.cs (结算高亮打榜面板)
* **单次派发与级联特效**：
  - 接收到 WebSocket 下发的结算事件后执行一次瀑布渐显动画。
* **个人打榜黄色高亮**：
  - 当数据行与本局玩家的成绩（Rank 或名字分数）相符时，名次、名字、得分文本自动着色为高亮黄色（E1FA00），彰显个人打榜荣誉。
