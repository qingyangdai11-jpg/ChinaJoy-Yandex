# 游戏机制调整记录

我们对项目中的摄像机跟随逻辑、障碍道具行走与碰撞机制、以及闪电道具的特定效果进行了修改与扩展。

---

## 1. 摄像机速度跟随角色速度调整

我们修改了摄像机跟随脚本和角色控制器脚本，使摄像机移动的平滑度/跟随速度随着角色的加速与减速而实时调整。

### 修改内容

* **角色控制器：**
  在 [SimplePlayerController.cs](file:///d:/unity/Yandex_UI_FLOW_260716/Assets/YandexUIScene/Input/InputTest/SimplePlayerController.cs) 中新增了只读属性 `CurrentSpeedMultiplier`，根据当前的加速状态（`speedBoostTimer`）和减速状态（`slowDownTimer`）动态计算当前的移动速度倍率。

* **摄像机跟随：**
  在 [CameraFollow.cs](file:///d:/unity/Yandex_UI_FLOW_260716/Assets/YandexUIScene/CommonScripts/CameraFollow.cs) 中增加了对目标对象上的 `SimplePlayerController` 组件的获取与缓存。在 `LateUpdate` 更新位置时，将摄像机的 `followSpeed` 乘以角色当前的 `CurrentSpeedMultiplier`。
  * **加速时（如 1.5x 倍速）**：摄像机跟随速度也提升 1.5 倍，从而保持跟随间距不会拉大。
  * **减速时（如 0.5x 倍速）**：摄像机跟随速度降低 0.5 倍，使镜头过渡更柔和。

---

## 2. barrier3 和 barrier4 障碍道具自动行走与碰撞消除

为了增加游戏难度和互动性，我们让 `barrier3` 和 `barrier4` 道具具备了自动往人物行进反方向行走的能力，且与其它道具碰撞时能令其它道具消失。

### 修改内容

* **移动参数（Inspector 窗口暴露）：**
  在 [MovingBarrier.cs](file:///d:/unity/Yandex_UI_FLOW_260716/Assets/YandexUIScene/CommonScripts/MovingBarrier.cs) 脚本中暴露了配置参数以供 Inspector 自定义：`movementDirection`、`movementSpeed`、`useDynamicPlayerDirection`、`isMoving`。

* **碰撞销毁其他道具：**
  在 [MovingBarrier.cs](file:///d:/unity/Yandex_UI_FLOW_260716/Assets/YandexUIScene/CommonScripts/MovingBarrier.cs) 中添加了碰撞和触发器检测：当运动的 `barrier3` 或 `barrier4` 碰到任何其他道具（含有 `CollectibleItem` 且不是自己）时，其他道具将被瞬间销毁并从生成器列表中移除。

* **避开同一赛道共存：**
  修改了 [EnemySpawner.cs](file:///d:/unity/Yandex_UI_FLOW_260716/Assets/YandexUIScene/CommonScripts/EnemySpawner.cs) 的随机生成逻辑。如果生成的预制体为 `barrier3` 或 `barrier4`，则会提前检查场景内所有活动状态的运动障碍（`MovingBarrier`）所处的赛道，自动规避已有赛道进行生成，确保它们在短时间内不会同时出现在同一个赛道中。

* **自动预制体挂载（编辑器辅助类）：**
  [AttachMovingBarrier.cs](file:///d:/unity/Yandex_UI_FLOW_260716/Assets/YandexUIScene/CommonScripts/Editor/AttachMovingBarrier.cs) 会自动在编译或加载时为 `barrier3.prefab` 和 `barrier4.prefab` 自动挂载 `MovingBarrier` 脚本。

---

## 3. 闪电第四种效果调整 (Effect 4: Spawn Coins)

修改了闪电道具（Lighting）的第 4 种随机效果触发逻辑。

### 修改内容

* **赛道中心线密集刷金币：**
  当玩家吃到闪电并随机触发第四种效果（`light4`）时，修改了原有的弯曲金币路径行为。现在它会在玩家前方**赛道的中心线上 (X = 0)** 凭空刷出一排密集的金币（金币个数可直接由 Effect 配置 of `spawnCoinsCount` 修改，其中心线间隔由新增变量 `effect4CoinSpacing` 控制）。

* **清空金币路径上的障碍与道具：**
  修改了 [EnemySpawner.cs](file:///d:/unity/Yandex_UI_FLOW_260716/Assets/YandexUIScene/CommonScripts/EnemySpawner.cs)。在生成中心线金币时，如果金币坐标处原有其他障碍物或道具，它们将自动消失被清空，确保新生成的金币连线纯净。

* **自动开启吸附金币效果：**
  当触发第四种效果时，无论该效果在配置中是否设置了吸附时长，玩家都将**自动激活金币吸附效果（磁铁效果）**，持续时间由新增变量 `effect4VacuumDuration` 控制，令玩家在向前跑时能顺畅吸附这一长排金币。

* **新增 Inspector 参数：**
  在 [GameMechanicsManager.cs](file:///d:/unity/Yandex_UI_FLOW_260716/Assets/YandexUIScene/CommonScripts/GameMechanicsManager.cs) 的 **"Lightning Effect 4 Settings"** 中添加了以下暴露参数：
  * `effect4CoinSpacing`: 闪电效果 4 生成的中心金币的间隔距离（默认 `1.0f`）。
  * `effect4VacuumDuration`: 闪电效果 4 触发时的自动磁铁吸附持续时间（默认 `5.0f`）。

---

## 4. 全局 DOTween 报错彻底清理与修复

* **问题成因分析：**
  除了上一轮在闪电刷新和障碍碰撞处添加的 `DOKill()` 外，以下场景也会导致相同报错：
  1. **道具被玩家正常吃掉：** 当玩家移速极快时，会迅速吃掉刚刚生成（尚在 `0.4` 秒生成缩放动画内）的道具。此时 [CollectibleItem.cs](file:///d:/unity/Yandex_UI_FLOW_260716/Assets/YandexUIScene/CommonScripts/CollectibleItem.cs) 直接执行 `Destroy(gameObject)`，导致 DOTween 报错。
  2. **因掉在玩家身后被系统自然清理：** 偶尔掉在玩家身后的道具在未过动画期时被 [EnemySpawner.cs](file:///d:/unity/Yandex_UI_FLOW_260716/Assets/YandexUIScene/CommonScripts/EnemySpawner.cs) 的清理循环销毁，也会发生此报错。

* **解决方案：**
  * 修改了 [CollectibleItem.cs](file:///d:/unity/Yandex_UI_FLOW_260716/Assets/YandexUIScene/CommonScripts/CollectibleItem.cs)，在玩家收集道具调用 `Destroy` 之前，强制执行 `transform.DOKill()`。
  * 修改了 [EnemySpawner.cs](file:///d:/unity/Yandex_UI_FLOW_260716/Assets/YandexUIScene/CommonScripts/EnemySpawner.cs)，在清理掉落在 player 身后的落后道具时，执行 `Destroy` 之前，强制执行 `item.transform.DOKill()`。
  * 这样，所有道具在因为任何原因（被吃掉、被系统清理、被刷金币清除、或者被障碍物碰撞消除）被销毁前，都会安全地停止并清除已有的 DOTween 动画，完美保证控制台无报错。
