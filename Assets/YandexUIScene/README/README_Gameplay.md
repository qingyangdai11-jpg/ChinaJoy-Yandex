# 🎮 游戏玩法机制说明 (README_Gameplay)

本文档介绍游戏的核心玩法、战斗系统、敌人机制、得分规则等游戏性相关内容。

---

## 1. 游戏概述

这是一款**第三人称俯视角动作小游戏**。玩家控制一名战士角色，在 60 秒内尽可能多地消灭红色敌人方块，累积最高得分。

* **游戏类型**：动作 / 射击 / 街机
* **视角**：第三人称俯视跟随
* **单局时长**：60 秒
* **核心目标**：在有限时间内消灭尽可能多的敌人，获得高分

---

## 2. 玩家角色 (Player)

### 2.1 角色构成
* **Player_Warrior**：角色根物体，挂载 `SimplePlayerController` 与 `Rigidbody`。
* **Character_Warrior**：角色模型父物体，挂载 `Animator` 组件。
* **Warrior_Body** / **Warrior_Weapon**：模型子物体。

### 2.2 移动系统
* **控制方式**：WASD / 方向键 / 手柄左摇杆，八方向自由移动。
* **移动速度**：默认 5 m/s（`moveSpeed` 参数可调）。
* **物理驱动**：使用 `Rigidbody.MovePosition` 在 `FixedUpdate` 中驱动，确保物理一致性。
* **朝向旋转**：移动时角色自动平滑转向移动方向，插值速度由 `turnSpeed`（默认 15）控制。
* **重力**：开启 Rigidbody 重力，冻结 X/Z 轴旋转防止倾倒。

### 2.3 攻击与消灭机制
* **消灭敌人方式（碰撞接触）**：本游戏实际击杀敌人并非由武器挥砍范围检测判定，而是采用**物理碰撞接触击杀**。当玩家角色（带有 `PlayerIdentity` 脚本）与红色敌人方块（`EnemyCube`）发生接触碰撞（通过 `OnCollisionEnter` 或 `OnTriggerEnter`）时，敌人就会被立即消灭并加 10 分。
* **攻击按键作用**：触发键盘空格/鼠标左键/手柄按键时，仅触发 `Animator` 的 `Attack` 触发器播放角色攻击动作动画，起视觉打击反馈作用，无实际的攻击物理伤害判定。

### 2.4 状态阻断
* 非 Gameplay 状态或暂停时，`moveInput` 被强制归零，角色保持待机。
* 攻击输入同样受状态阻断，非游戏中状态按下无效。

### 2.5 结束退场与还原特效
* **触发时机**：当进入 `GameOver` 状态（包括正常完成或中途退出）时。
* **效果**：角色通过 `playerCharacter.transform.DOScale(Vector3.zero, 0.8f)` 以及 `material.DOFade(0f, 0.8f)`，在 0.8 秒内平滑缩小到 0 并淡出消失。
* **恢复机制**：每次重新进入 `Gameplay` 状态时，程序会自动重置角色的 Scale 为 `Vector3.one`，并恢复材质完全不透明（Alpha = 1.0f），确保下一局开始时角色显示完好。

---

## 3. 敌人系统 (Enemies)

### 3.1 敌人实体 (EnemyCube)
* **外观**：红色立方体，缩放 0.8。
* **组件**：`EnemyCube` 脚本 + `Kinematic Rigidbody` + `BoxCollider`。
* **行为**：
  * 静止不动，等待玩家靠近接触。
  * 碰撞检测：监听 `OnCollisionEnter` 和 `OnTriggerEnter` 检测带有 `PlayerIdentity` 组件的主角对象。
  * 被接触后调用 `OnAttacked()` 触发被消灭逻辑，播放缩小消失动画（DOTween，0.2 秒，Ease.InBack），动画播放完毕后自动销毁 GameObject。
  * 销毁时通知生成器补充新敌人。
  * 销毁时向 `GameFlowController` 上报 +10 分。

### 3.2 敌人生成器 (EnemySpawner)
* **生命周期**：在 `GameFlowController.Start()` 中动态创建并挂载。
* **最大同屏数**：默认 3 个（`maxEnemyCount`）。
* **生成时机**：
  * 进入 Gameplay 状态时一次性生成全部敌人。
  * 每个敌人被消灭后立即补充一个，保持数量恒定。
* **生成位置**：
  * 以玩家为中心，半径 2~6 单位的圆环内随机位置（XZ 平面）。
  * Y 轴固定在 0.5。
* **入场动画**：从 scale 0 放大到 0.8，DOTween Ease.OutBack，时长 0.4 秒。
* **清理机制**：离开 Gameplay 状态时销毁所有存活敌人并清空列表。

---

## 4. 得分系统 (Scoring)

| 项目 | 数值 | 说明 |
| :--- | :--- | :--- |
| 初始分数 | 0 | 进入 Gameplay 状态时清零 |
| 每消灭敌人 | +10 | 由 `EnemyCube.OnAttacked()` 上报 |
| 最终展示 | GameOverPage | 显示在 `Text (TMP)-score` 组件 |

* **得分方法**：`GameFlowController.AddScore(int amount)`
* **得分属性**：`GameFlowController.CurrentScore`（只读）

---

## 5. 摄像机系统 (Camera)

### 5.1 跟随模式
* **脚本**：`CameraFollow.cs`（挂在 Main Camera 上）。
* **跟随目标**：`Player_Warrior` 的 Transform。
* **偏移量**：默认 (0, 5, -8)，即角色后上方俯视角度。
* **平滑方式**：`Vector3.Lerp` 线性插值，`followSpeed` 默认 5。

### 5.2 技术细节
* **执行时机**：`LateUpdate` 中更新，确保角色移动完成后再移动摄像机，避免帧同步抖动。
* **空引用保护**：`target == null` 时直接返回，不报错。
* **可调参数**：
  * `followSpeed`：跟随紧密度，值越大摄像机越"跟手"，越小越平滑但有延迟感。
  * `offset`：相对位置，可调整为不同视角（如越肩、正俯视等）。

---

## 6. 游戏流程时间线

### 6.1 正常完成流程
```
Idle → LanguageSelect → RuleRead → CountdownStart (3,2,1,GO!)
                                                   ↓
                                              Gameplay (60秒)
                                                   ↓
                                          GameOver (Finish! 弹窗 + 角色渐隐缩小 2秒)
                                                   ↓
                                          GameOverPage (结算 + 10秒倒计时 / 按 B 键跳过)
                                                   ↓
                                              重载场景 (强制重置模式为 ONLINE 并联网) → Idle
```

### 6.2 暂停中途退出流程
```
Gameplay (按 ESC/Start 呼出暂停菜单 -> 选择 QUIT 按钮)
                       ↓
         GameOver (Finish! 弹窗 + 角色渐隐缩小 2秒)
                       ↓
         重载场景 (强制重置模式为 ONLINE 并联网) → Idle
```

### 6.3 离线状态自动重置与重联机制
无论是通过正常完成游戏后结算倒计时结束/手动返回，还是在中途退出，最终在重载场景前，`GameFlowController` 都会强制执行 `PlayerPrefs.SetString("GameMode", "ONLINE")`，重置网络模式为“在线”状态。重新加载场景进入首页后，首页将重新向 WebSocket 建立连接，以确保每次返回首页都能重新联网。

---

## 7. 可调参数速查

### 7.1 GameFlowController 参数
| 参数 | 默认值 | 说明 |
| :--- | :--- | :--- |
| `enableConfirmToStart` | true | 是否允许确认键从待机开始 |
| `startCountdownDuration` | 3f | 开局倒计时秒数 |
| `gameplayDuration` | 60f | 游戏时长（秒） |
| `gameOverCountdownDuration` | 10f | 结束页自动返回秒数 |

### 7.2 SimplePlayerController 参数
| 参数 | 默认值 | 说明 |
| :--- | :--- | :--- |
| `moveSpeed` | 5f | 移动速度（m/s） |
| `turnSpeed` | 15f | 转向插值速度 |
| `speedParameterName` | "Movement" | Animator 移动速度参数名 |
| `attackTriggerName` | "Attack" | Animator 攻击触发参数名 |

### 7.3 EnemySpawner 参数
| 参数 | 默认值 | 说明 |
| :--- | :--- | :--- |
| `maxEnemyCount` | 3 | 同屏最大敌人数 |
| `spawnRadiusMin` | 2f | 生成最小半径 |
| `spawnRadiusMax` | 6f | 生成最大半径 |

### 7.4 CameraFollow 参数
| 参数 | 默认值 | 说明 |
| :--- | :--- | :--- |
| `followSpeed` | 5f | 跟随平滑度 |
| `offset` | (0, 5, -8) | 相对目标偏移量 |
