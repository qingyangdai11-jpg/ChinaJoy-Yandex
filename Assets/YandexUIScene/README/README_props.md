变更说明 - 游戏机制修改
这里总结了修改游戏机制的实现细节。
所做变更
1. 预制体设置与标签注册
将 7 个预制体（coin、lighting、boost、barrier1、barrier2、barrier3、barrier4）拷贝至 Assets/Resources/Prefabs 中，从而可以通过 Resources.Load<GameObject>() 在运行时动态且干净地加载它们。
在 ProjectSettings/TagManager.asset 中注册了自定义标签 "Coin"、"Lighting"、"Boost" 和 "Barrier"。
2. 道具收集组件 (Collectible Item Component)
创建了 CollectibleItem.cs：
在运行时动态附加到每个生成的道具上。
自动处理与玩家物体的物理碰撞及触发器事件。
调用原有的 GameFlowController.Instance.AddScore(scoreModifier) 修改游戏分数。
收集加速道具（Boost）时，触发玩家的临时速度加成。
收集完成后通知 EnemySpawner，播放 DOTween 缩放消失动画，并销毁自身。
3. 支持队列与加速过渡的动态弹窗管理器 (Popup Manager)
创建了 PopupManager.cs：
启动时自动搜索并绑定场景中的 PopUp 画布（Canvas）根节点。
实现了一个弹窗请求队列（Queue<string>）以处理连续出现的弹窗：
正常播放速度（最后一个弹窗）：缩放出现 0.3秒 (Ease.OutBack)，展示停留 1.2秒，缩放消失 0.2秒 (Ease.InBack)。
加速播放速度（除最后一个外的此前弹窗）：缩放出现 0.1秒，展示停留 0.15秒，缩放消失 0.08秒。
动态中断：如果一个弹窗正在以正常速度展示，中途突然有新弹窗被加进队列，则当前弹窗会立刻缩短展示时间并以加速动画关闭，以便让下一个弹窗立刻展现。
4. 生成器与计数器逻辑 (Spawner & Counter Logic)
修改了 EnemySpawner.cs：
将原来的红色方块敌人替换为 7 种不同的道具预制体。
不限数量定时生成：移除了生成上限限制，在游戏进行中将以固定时间间隔（默认 1.5 秒，可在 EnemySpawner 组件面板的 spawnInterval 中配置）持续在主角周围随机生成道具，实现无限制的道具生成累积。
在实例化道具时自动挂载 CollectibleItem 脚本，并配置其属性（设置 Tag、分数增减、加速时间以及对应的弹窗名称）。
若场景中不存在 PopupManager，则会在启动时自动创建，保证系统开箱即用。
跟踪连续收集的金币数量：
金币 (Coin)：得分 +1。增加连续收集计数。当计数达到 5 时，触发 coin10 弹窗并重置计数。
闪电 (Lighting)：得分 +5，重置连续金币计数，并立刻触发 lighting 弹窗。
加速 (Boost)：触发角色移动速度加成 2 秒，重置连续金币计数，并立刻触发 boost 弹窗。
障碍物 (Barrier 1-4)：得分 -2，重置连续金币计数，并立刻触发 barrier 弹窗。
5. 玩家速度加成 (Player Speed Boost)
修改了 SimplePlayerController.cs：
添加了速度增益计时器和速度乘率属性。
暴露了 ApplySpeedBoost(float duration, float multiplier) 公共方法，允许外部临时增加移动速度。
在 Update() 中自动扣减速度加成时长，并在 FixedUpdate() 物理移动时将速度乘以对应的倍率。
s