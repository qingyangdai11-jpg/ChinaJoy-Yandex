# 游戏机制丰富实现计划 (Implementation Plan)

本计划详细阐述了丰富游戏机制的实现细节，包括金币连续碰撞判定、无障碍计时器、基于“卡包机制”的随机道具生成以及碰撞闪电时的随机效果。所有功能机制均进行模块化设计，关键参数会暴露在 Unity 属性面板（Inspector）中，方便后期修改与调试。

## 用户审核项 (User Review Required)

> [!IMPORTANT]
> **模块化与编辑器易用性**：
> 1. 所有可配置的数值（概率、分值、持续时间、范围等）均作为序列化字段暴露。您可以在 Unity Inspector 中直接进行调整。
> 2. 游戏中动态生成的道具现在会自动归类存放在 Hierarchy 窗口中名为 `props` 的 GameObject 下，避免污染根目录。如果 `props` 物体在场景中不存在，程序会自动创建。
> 3. 如果场景中 `PopUp/Canvas` 下缺少某些弹窗（如 `coin35`、`coin50`、`buff30`、`buff60`、`light1`..`light6`），`PopupManager` 拥有**自我修复机制**：它会在运行时自动克隆现有的弹窗（例如 `coin10`），重命名并动态修改其 TextMeshPro 的文本内容，以确保界面正常显示，不会发生空指针崩溃。

## 待确认问题 (Open Questions)

暂无。所有需求都已精准对应到项目的现有架构中。

---

## 拟作出的修改 (Proposed Changes)

### 1. 核心游戏机制管理器 (Core Mechanics Manager)

#### [NEW] [GameMechanicsManager.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/CommonScripts/GameMechanicsManager.cs)
创建一个全新的单例管理器组件，用来解耦和模块化处理具体的游戏玩法规则：
- **金币连击设置**：
  - 10连击额外加分：`coin10Bonus = 2`
  - 35连击额外加分：`coin35Bonus = 10`
  - 50连击及以后每5个加分：`coin50BaseBonus = 2`
- **无障碍生存设置**：
  - 30秒无碰撞障碍加分：`buff30Bonus = 10`
  - 60秒无碰撞障碍加分：`buff60Bonus = 25`
- **闪电碰撞效果配置**（6种随机效果，概率总和为 100%）：
  - 概率1 (20%)：立刻加5分，弹出 `light1`
  - 概率2 (15%)：5秒内得分翻倍，弹出 `light2`
  - 概率3 (15%)：立刻加10分，弹出 `light3`
  - 概率4 (20%)：玩家附近生成15个金币，弹出 `light4`
  - 概率5 (15%)：3秒内得分翻倍，弹出 `light5`
  - 概率6 (15%)：3秒内磁铁吸附周围金币，弹出 `light6`
- **状态追踪变量**：
  - `coinStreak`：金币连击数。碰撞金币增加；碰撞闪电/加速不打断；碰撞任意障碍（Barrier）重置为0。
  - `timeSinceLastBarrier`：自上次碰撞障碍以来的时间。当游戏处于 Gameplay 状态时累加，碰撞障碍重置为0。在达到30秒和60秒时分别触发加分和弹窗。
  - `doubleScoreTimer`：双倍得分剩余时间计时器。若大于0，则在此期间所有获得的分数翻倍。
  - `vacuumTimer` 和 `vacuumRadius`/`vacuumSpeed`：磁铁吸附计时器及吸附属性。激活时将周围的金币平滑拉向玩家。

### 2. 道具属性与生成逻辑 (Item Properties & Spawning)

#### [MODIFY] [CollectibleItem.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/CommonScripts/CollectibleItem.cs)
- 新增布尔标识 `public bool isBonusSpawn = false`，用来标记该道具是否属于闪电效果额外生成的“奖励金币”。
- 重构 `Collect()` 方法，将碰撞事件分发给全新的机制管理器：`GameMechanicsManager.Instance.OnPlayerCollidedWithItem(this)`，而不是让道具脚本自己执行复杂的积分和状态计算。

#### [MODIFY] [EnemySpawner.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/CommonScripts/EnemySpawner.cs)
- 在 Inspector 中暴露生成概率控制：
  - 卡包容量：`spawnBagSize = 20`
  - 闪电数量：`lightningCountInBag = 1`
  - 加速数量：`boostCountInBag = 1`
  - 障碍数量：`barrierCountInBag = 2`
  - （金币数量将根据上述参数自动计算为 16）
- 在 Inspector 中暴露预制体（Prefab）槽位：
  - `coinPrefab`、`lightningPrefab`、`boostPrefab`、`barrierPrefabs` 列表。
  - 支持直接在编辑器里拖拽替换预制体。若为空，则运行时自动通过 `Resources.Load` 动态加载作为兜底，确保向下兼容。
- 实现 Fisher-Yates 随机洗牌算法。每当生成卡包中的 20 个道具用尽后，会重新打乱生成一包新的，以此保证每 20 个生成的道具里百分百包含：1个闪电、1个加速、2个随机障碍和16个金币。
- 动态生成的道具在实例化时，其 Parent 将统一设为场景中的 `"props"` 节点，保持 Hierarchy 干净整洁。
- 在 `OnItemCollected` 中，如果被收集的道具 `isBonusSpawn` 为真，则不触发补充生成新道具，避免场景中道具数量无限膨胀。
- 提供公共方法 `SpawnSpecificItemNearPlayer(ItemType type, bool isBonus)`，供闪电效果4调用以在玩家周围瞬间生成 15 个奖励金币。

### 3. 界面弹窗与积分拦截 (Popups & Scoring)

#### [MODIFY] [PopupManager.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/CommonScripts/PopupManager.cs)
- 在 `PlayPopupAnimation` 中加入缺省逻辑。如果请求的弹窗名称在现有列表中找不到，程序会克隆 Canvas 下的第一个弹窗模板，重命名为对应名称，并通过 `GetComponentInChildren<TextMeshProUGUI>()` 动态修改显示的文本内容，加入到可用列表。
- 支持的动态文本包括：“COIN 10\n+2 分！”、“COIN 35\n+10 分！”、“BUFF 30s\n+10 分！”、“闪电触发\n得分翻倍！”等，排版精美。

#### [MODIFY] [GameFlowController.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/CommandScripts/GameFlowController.cs)
- 在 `Start()` 中动态安全挂载组件：
  - `gameObject.AddComponent<GameMechanicsManager>()`（新规则管理器）。
  - 安全检测并挂载 `EnemySpawner`。
- 在 `AddScore(int amount)` 中，在累加分数前通过机制管理器进行过滤：
  ```csharp
  if (GameMechanicsManager.Instance != null)
  {
      amount = GameMechanicsManager.Instance.ModifyScoreAmount(amount);
  }
  ```
  以此拦截所有加分操作并配合闪电的双倍得分 Buff 乘 2。

---

## 验证计划 (Verification Plan)

### 编译验证
- 在 Unity 编辑器或 VS Code 中测试脚本是否有编译错误，确保所有新增类型和引用都能正确编译通过。

### 手动功能测试步骤
1. **生成频率测试**：
   - 检查 `EnemySpawner` 在面板中是否暴露出卡包配置参数和预制体槽位。
   - 运行游戏，观察 Hierarchy 产生的道具数量和类型，确认前 20 个生成的道具里是否刚好为：1个闪电、1个加速、2个障碍和16个金币。
   - 确认道具都存放在 `props` 节点下。
2. **金币连击机制测试**：
   - 吃金币，当吃到第 10 个时，分数额外 +2，弹出 `coin10` 弹窗。
   - 吃到加速/闪电，确认金币连击计数没有重置。
   - 撞到障碍物，确认金币连击计数重置。
   - 吃到第 35 个，确认额外 +10 分，弹出 `coin35`。
   - 吃到第 50 个，确认额外 +2 分，弹出 `coin50`。在此之后每吃 5 个（第 55, 60 个），都额外 +2 并触发 `coin50` 弹窗。
3. **无障碍计时器测试**：
   - 避开障碍生存 30 秒，确认分数 +10 且弹出 `buff30`。
   - 避开障碍生存 60 秒，确认分数 +25 且弹出 `buff60`。
   - 撞障碍物，确认计时重置。
4. **闪电随机效果测试**：
   - 碰撞闪电，根据随机概率触发 6 种效果之一：
     - 如果是 15个金币 (light4)，确认这 15 个金币吃掉后不会生成替换道具。
     - 如果是磁铁吸附 (light6)，确认 3 秒内周围的金币被吸向玩家。
     - 如果是得分翻倍 (light2 / light5)，确认在此期间吃金币或打怪获得的分数乘以了 2。
5. **弹窗自我修复测试**：
   - 即使场景中本来没有手动制作 `buff30`、`light6` 等对应的 UI 物体，在触发时查看游戏界面，确认是否自动克隆并正确显示了对应的文字弹窗，且未产生报错。



# 丰富游戏机制 - 变更与使用说明文档 (Walkthrough)

本文件汇总了为了丰富游戏机制而进行的代码修改，并解释了各个模块之间的协作关系。所有的新逻辑均已完成编写与挂载。

---

## 1. 主要完成的改动

我们对游戏的核心逻辑进行了模块化拆分和修改，新增了一个核心机制管理器，共计新建 1 个文件，修改 4 个文件：

### 🛠️ 新建文件
- **[GameMechanicsManager.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/CommonScripts/GameMechanicsManager.cs)**：核心机制管理器（单例），独立负责金币连击（Streak）、无障碍生存计时（Timer）、闪电击中随机效果、双倍得分计时、金币吸附拉扯等全部具体玩法的状态更新与配置。

### 📝 修改文件
- **[CollectibleItem.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/CommonScripts/CollectibleItem.cs)**：
  - 新增 `isBonusSpawn` 属性，标记是否是由闪电机制产生的额外金币（防止吃掉后无限生出替换金币导致同屏道具泛滥）。
  - 重构 `Collect` 方法，将分值增加、连击计算、弹窗触发等事件完全委托给 `GameMechanicsManager` 统一调度。
- **[EnemySpawner.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/CommonScripts/EnemySpawner.cs)**：
  - 移除了硬编码的金币连击和弹窗逻辑。
  - 在 Inspector 中暴露了概率卡包大小 `spawnBagSize` (默认20)、闪电数 `lightningCountInBag` (默认1)、加速数 `boostCountInBag` (默认1) 和障碍数 `barrierCountInBag` (默认2) 参数，以及各道具预制体拖拽槽位。
  - 引入 Fisher-Yates 随机打乱卡包生成机制，确保每生成一包 20 个物体中刚好有：1个闪电、1个加速、2个随机障碍和16个金币。
  - 将所有实例化生成的道具在 Hierarchy 窗口中统一存放在 `"props"` 节点下，自动进行父子层级整理。
  - 新增公共方法 `SpawnSpecificItemNearPlayer` 供闪电随机事件 4 调用。
- **[PopupManager.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/CommonScripts/PopupManager.cs)**：
  - 新增 `CreatePopupDynamically` 自我修复与克隆方法。若场景中没有制作 `buff30`、`light1`..`light6` 等弹窗 GameObject，它会自动克隆 Canvas 下的现有弹窗模板，动态重命名并填充正确的中文标题文字。
- **[GameFlowController.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/CommandScripts/GameFlowController.cs)**：
  - 在 `Start` 中动态挂载全新的 `GameMechanicsManager` 规则组件，并增强 `EnemySpawner` 挂载安全性。
  - 在 `GameState.Gameplay` 开始时自动重置 `GameMechanicsManager` 的统计状态。
  - 拦截并增强 `AddScore`，配合闪电的双倍得分 Buff 对加分额度进行相乘乘数计算。

---

## 2. 核心模块与配置说明

### 💎 A. 金币连续碰撞机制（连击）
- **触发规则**：吃到 Coin 时连击数加 1。碰撞 Lightning 和 Boost 时**不中断**连击，碰撞 Barrier 时**立刻中断**重置为0。
- **奖励规则**：
  - 连续 10 个：额外加 2 分，弹出 `coin10`。
  - 连续 35 个：额外加 10 分，弹出 `coin35`。
  - 连续 50 个及以后：吃到 50 个触发，此后每吃 5 个（55, 60, 65, 70...）都额外加 2 分，并弹出 `coin50`。

### ⏱️ B. 无障碍生存计时机制
- **触发规则**：游戏开始后，只要没有碰撞任何 Barrier 障碍物，无障碍计时器便会累加。若发生 Barrier 碰撞，计时器瞬间归 0 重新计算。
- **奖励规则**：
  - 累计生存 30 秒：额外 +10 分，弹出 `buff30`。
  - 累计生存 60 秒：额外 +25 分，弹出 `buff60`。

### ⚡ C. 闪电碰撞概率事件 (100% 覆盖)
在 `GameMechanicsManager` 面板中，闪电的 6 种碰撞事件概率和分值已序列化：
1. **20% 概率**：立刻 +5 分，弹出 `light1`。
2. **15% 概率**：5秒内吃金币/打怪**积分 x2 倍**，弹出 `light2`。
3. **15% 概率**：立刻 +10 分，弹出 `light3`。
4. **20% 概率**：在玩家附近随机位置**生成 15 个金币**，弹出 `light4`。
5. **15% 概率**：3秒内吃金币/打怪**积分 x2 倍**，弹出 `light5`。
6. **15% 概率**：3秒内以玩家为中心，小范围（默认半径5单位）内的所有金币被**磁力吸附**拉向玩家，弹出 `light6`。

---

## 3. 编辑器使用指引

1. **修改道具生成频率**：
   - 选中场景中的主控制器 GameObject。
   - 运行后，`EnemySpawner` 会作为组件动态挂载其上。您也可以提前在 Editor 中将 `EnemySpawner` 组件手动挂载到场景中。
   - 在面板中找到 `Spawn Bag Probabilities`：
     - `spawnBagSize` (卡包大小)：20
     - `lightningCountInBag` (闪电数)：1
     - `boostCountInBag` (加速数)：1
     - `barrierCountInBag` (障碍数)：2
     - （剩下的 16 个会自动判断为金币类型，可根据需求随时修改）
2. **拖拽替换道具预制体**：
   - 如果需要替换道具，可直接在挂载的 `EnemySpawner` 组件面板中的 `Item Prefabs` 槽位拖拽您新制作的 Prefabs。
   - 留空时会自动 fallback 从 `Assets/Resources/Prefabs` 读取默认配置。
3. **调整闪电概率和参数**：
   - 运行后 `GameMechanicsManager` 会动态附加。您同样可以提前手动附加。
   - 面板的 `Lightning Collision Outcomes Settings` 列表暴露了所有6种效果。你可以随意更改每一种的 `Probability`（概率），或更改如 `scoreBonus`（额外加分）、`doubleScoreDuration`（双倍时间）、`spawnCoinsCount`（额外金币数）、`vacuumDuration`（吸附时间）等参数，极易修改和扩展。
