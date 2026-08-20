# ⚙️ 游戏代码流程与架构说明 (README_Workflow)

本文档面向开发者，用于阐述游戏的整体代码控制流、核心脚本职责、状态转换细节以及针对常见 Unity 交互问题的优化方案。

---

## 1. 核心架构设计

整个系统的架构可分为四个层级：
1. **控制层 (Controller)**：单例模式的全局进度状态机 `GameFlowController`，这是游戏主循环与逻辑的核心。
2. **表现层 (UI & Animation)**：包含 `PageSwitcher`（页面管理器）、`SimpleUIManager`（暂停/恢复菜单）和 `SceneReLoad`（场景重载与黑屏渐变）。
3. **实体控制层 (Entity & Input)**：`SimplePlayerController`（玩家移动与攻击）、`EnemyCube`（敌人实体）、`EnemySpawner`（敌人生成器）。
4. **摄像机层 (Camera)**：`CameraFollow`（第三人称平滑跟随摄像机）。

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
    ├── CommandScripts/        # 状态机与核心流程控制器（如 GameFlowController.cs）
    ├── CommonScripts/         # 通用工具与 UI 逻辑（如 PageSwitcher, SceneReLoad 等）
    ├── Fonts/                 # 场景 UI 特有字体
    ├── Images/                # 场景 UI 图片及素材
    ├── Inputs/                # 输入系统配置（新 Input System Actions 映射文件）
    ├── README/                # 模块说明文档（导出时随模块打包带走）
    │   ├── README_Workflow.md # 本文档（开发流程与架构说明）
    │   └── README_Buttons.md  # 按键交互说明
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
| `Gameplay` | **4** | 核心游戏状态，激活游戏倒计时（60秒）。玩家可移动、攻击敌人、累积得分。 |
| `GameOver` | **4** (保持) | 触发游戏结束，展示 "Finish" 弹窗（持续2秒）。 |
| `GameOverPage` | **5** | 结算与结束页面，显示最终得分，运行自动返回首页倒计时（10秒）。 |

---

## 5. 得分与战斗系统 (Score & Combat System)

* **得分机制**：
  * 进入 Gameplay 状态时得分清零（`CurrentScore = 0`）。
  * 每消灭一个敌人方块 +10 分（通过 `GameFlowController.AddScore(int)` 方法）。
  * 最终得分在 `GameOverPage` 状态下显示在 `Text (TMP)-score` 文本组件中。
* **战斗检测**：
  * 玩家攻击使用 `Physics.OverlapSphere` 球形检测，半径 2，中心位于角色前方 1.2 单位处。
  * 检测到带有 `EnemyCube` 组件的对象时调用其 `OnAttacked()` 方法。
* **攻击可视化**：
  * `SimplePlayerController.OnDrawGizmosSelected()` 在 Scene 视图中绘制红色攻击范围球体，便于调试。

---

## 6. 核心脚本职责与实现原理

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
* **全局时间恢复**：
  - 在载入新场景前自动将 `Time.timeScale` 恢复成 `1f`，防止载入的新场景处于冻结状态。

### 📁 YandexUIScene/CommonScripts/CountdownTimer_OneNumber.cs (单数字倒计时器)
* **倒计时显示与动画**：
  * 控制 3, 2, 1, GO! 等文本的更新。
  * 配合 DOTween 对倒计时数字实施"放大缩水 + 渐隐"等动效，提升 UI 动态质感。

### 📁 YandexUIScene/CommonScripts/EnemyCube.cs (敌人实体)
* **敌人行为**：
  * 红色立方体敌人，被攻击后触发缩小消失动画（DOTween DOScale）。
  * 被消灭时调用 `GameFlowController.AddScore(10)` 增加 10 分。
  * 销毁时通知 `EnemySpawner` 以便生成新敌人。
  * 带有 Kinematic Rigidbody，不参与物理碰撞但可被射线检测。

### 📁 YandexUIScene/CommonScripts/EnemySpawner.cs (敌人生成器)
* **动态创建**：在 `GameFlowController.Start()` 中通过 `gameObject.AddComponent<EnemySpawner>()` 动态挂载。
* **生成机制**：
  * 进入 Gameplay 状态时，一次性生成 `maxEnemyCount`（默认 3）个敌人。
  * 每个敌人被消灭后自动补充一个，保持场上敌人数量恒定。
  * 生成位置：玩家周围 2~6 单位半径的随机位置（XZ 平面）。
  * 生成时带有从 0 缩放到实际大小的 DOTween 入场动画。
* **状态同步**：离开 Gameplay 状态时自动清除所有敌人，重置生成列表。

### 📁 YandexUIScene/CommonScripts/CameraFollow.cs (摄像机跟随)
* **第三人称跟随**：使用 `Vector3.Lerp` 在 `LateUpdate` 中平滑跟随目标位置。
* **可配置参数**：
  * `target`：跟随目标（通常为 Player_Warrior）。
  * `followSpeed`：跟随平滑度，默认 5（值越大跟随越紧）。
  * `offset`：相对目标的偏移向量，默认 (0, 5, -8)。
* **LateUpdate 执行时机**：确保在角色移动完成后再更新摄像机位置，避免抖动。
