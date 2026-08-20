# 跑酷奔跑与三赛道机制 - 变更与使用说明文档 (Walkthrough)

本文件汇总了实现**自动跑酷奔跑**和**三赛道控制生成**的全部代码改动，并指导您如何在 Unity 中使用和调整这些功能。

---

## 1. 主要完成的改动

针对您的最新需求，我们将原本的“八方向自由移动动作游戏”重构为了**“严格三赛道的 3D 跑酷游戏”**：

### 🏃‍♂️ 玩家移动控制重构
- **[SimplePlayerController.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/Input/InputTest/SimplePlayerController.cs)**：
  - **自动奔跑**：移除了手动的 Y 轴/前后方向键位移动。只要游戏处于 Gameplay 状态，角色就会自动以 `moveSpeed`（支持闪电/道具加速）沿着 Z 轴正方向不断向前奔跑。
  - **赛道限制（X = -2, 0, 2）**：将赛道划分为左中右三个坐标（X = -2, X = 0, X = 2）。玩家通过向左（A键、左方向键、手柄左偏）或向右（D键、右方向键、手柄右偏）来进行**一次性切道**操作，杜绝随意移动。
  - **切道微侧翻**：切道过程中，X 轴坐标通过 Lerp 平滑滑动到目标坐标。切道期间角色会略微向目标方向偏转倾斜，滑动完毕后自动回正，手感极为灵动流畅。
  - **按键跳跃（Space / 手柄A键）**：按下手柄 A 键或者键盘空格键可以使角色跳跃。程序包含高精度的地面接触判定（结合刚体垂直速度与向下的物理射线），防止空中二连跳，且能够触发角色的跳跃动画。
  - **开局重置**：新增 `ResetPosition()` 方法，清空物理速度并将角色完美重置在跑道中央起点（X=0, Z=0）。

### 🚧 道具严格跑道生成
- **[EnemySpawner.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/CommonScripts/EnemySpawner.cs)**：
  - **位置计算重构**：修改 `CalculateSpawnPosition()`，新生成的道具不再是环绕玩家随机生成，而是**严格在左中右三条跑道（X = -2, 0, 2）上**、且**在玩家前方**的合理视距内（Z = 玩家坐标 + 随机半径）生成。
  - **精确生成辅助**：新增 `SpawnItemAtCoordinates` 方法，允许在特定的跑道 X 轴和相对 Z 轴偏移处实例化道具。

### ⚡ 闪电事件轨迹优化
- **[GameMechanicsManager.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/CommonScripts/GameMechanicsManager.cs)**：
  - 当吃到闪电并触发“生成 15 个奖励金币”时，金币将不再层叠堆在一处。
  - 15 个金币将沿着跑道向前（间距 2 单位，共延伸 30 米），以左右交错（S型）的平滑轨迹生成。玩家可以通过左右切道有节奏地收集一整条轨迹上的金币。

### 🔄 流程状态同步
- **[GameFlowController.cs](file:///d:/unity/Yandex_UI_FLOW_260710/Assets/YandexUIScene/CommandScripts/GameFlowController.cs)**：
  - 在每一次切换至 `Gameplay` 状态时，自动触发玩家的 `ResetPosition()`，确保新的一局完美从赛道起点重新开跑。

---

## 2. 核心数值与属性面板说明

### ⚙️ 跑酷参数调节 (SimplePlayerController)
选中挂载有 `SimplePlayerController` 脚本的角色物体，您可以在面板中自由修改以下参数：
- `moveSpeed`：玩家向前跑的速度（默认值：5f）。
- `laneWidth`：两个赛道之间的跨度（默认值：2f，可对应 X = -2, 0, 2 的间距）。
- `laneChangeSpeed`：滑行切换跑道的平滑过渡速度（默认值：15f，值越高，切道越迅速干脆；值越低，切道越平缓迟钝）。
- `turnSpeed`：切道时身体偏转与回正的旋转速度（默认值：15f）。
- `jumpForce`：跳跃时向上施加的物理冲力大小（默认值：6f，数值越大，跳得越高）。
- `barrierSlowdownDuration`：碰撞到障碍物时的减速持续时间（默认值：0.5s）。
- `barrierSlowdownMultiplier`：碰撞到障碍物时的速度倍率（默认值：0.5f，即速度减半）。

### ⚙️ 道具分布参数调节 (EnemySpawner)
在 `EnemySpawner` 的 Inspector 中：
- `spawnRadiusMin`：道具在玩家前方刷新的最小距离（建议设为：10）。
- `spawnRadiusMax`：道具在玩家前方刷新的最大距离（建议设为：25）。
*(注：如果觉得刷新出的道具距离自己奔跑的视野太近，可以适当调大这两个值。)*

### ⚙️ 调试日志控制 (GameMechanicsManager)
在 Hierarchy 里的 `GameMechanicsManager` 物体面板中，您可以通过勾选以下选项灵活过滤不想查看的日志类型：
- `logCoinStreakMilestones`：是否输出金币连击里程碑触发提示。
- `logSurvivalMilestones`：是否输出 30s/60s 无障碍生存奖励提示。
- `logLightningEffects`：是否输出闪电判定结果与随机效果激活提示。
- `logSpeedChanges`：是否输出速度改变（加速、碰撞减速）的状态变化提示。
- `logScoreChanges`：是否输出分数变动日志（**仅在触发特殊加分时打印**。普通的吃1个金币或碰墙扣2分等高频事件不会打印，以防刷屏）。
