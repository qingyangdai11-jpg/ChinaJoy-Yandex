

### 1. `GameFlowController.cs` (游戏流程与进度控制)
该脚本控制游戏的状态机（主界面、语言选择、倒计时、游戏中、结算页）及基本的游戏时间
与分数面板：
*   **Flow Control Settings (流程控制):**
    *   `Enable Confirm To Start` (布尔值): 控制在主界面时是否允许通过物理确认键
      * （如空格、Enter 或手柄 A 键）直接开始游戏。
*   **Game Score Settings (积分设置):**
    *   `Enable Debug Mode` (布尔值): 是否开启调试模式。开启后，在游戏运行时可通过
      * 键盘 `Page Up` 和 `Page Down` 直接对当前得分进行 `+10` 或 `-10` 的增减测试。
*   **Language Selection UI (语言选择):**
    *   `Button CN` / `Button EN` (按钮组件): 绑定中文和英文的 UI 选择按钮。
*   **Countdown Start UI (开局倒计时 UI):**
    *   `Countdown Tip TMP` (文本): 倒计时开始前的提示文本（如 "GET READY" / "准备
      * 好了吗？"）。
    *   `Countdown Number TMP` (文本): 倒计时数字（3、2、1、GO!）的显示文本。
    *   `Start Countdown Duration` (浮点数): 游戏开局倒计时的时间（默认 3 秒）。
*   **Gameplay UI (游戏进行中 UI):**
    *   `Game Timer TMP` / `Game Score TMP` / `Score Num TMP` (文本): 游戏剩余时间
      * 及当前得分的文本显示。
    *   `Finish Image Popup` (物体) / `Finish Canvas Group` (组件): 时间截止时显示
      * 的 "FINISH" 结算提示弹窗。
    *   `Gameplay Duration` (浮点数): 单局游戏的限制时间（默认 60 秒）。
*   **Game Over Effects (游戏结束表现):**
    *   `Player Character` (物体): 主角物体的引用，用于结算时淡出或做缩放动画。
    *   `Overlay Filter` (图片): 游戏结束时渐变覆盖全屏的彩色滤镜。
*   **GameOver Page UI (游戏结束结算页 UI):**
    *   `GameOver Countdown TMP` (文本): 结算页面倒计时回到首页的文本。
    *   `GameOver Countdown Duration` (浮点数): 结算页面停留的时间（默认 10 秒）。
    *   `Score TMP` (文本): 结算页最终得分显示。
*   **Background Settings (背景设置):**
    *   `Image Black Bg` (物体): 黑色背景图，用于转场或底色。

---

### 2. `EnemySpawner.cs` (道具/方块生成器)
控制道具（金币、闪电、加速、障碍物）的随机生成频率、位置范围和刷新时间：
*   **Spawner Settings (生成基本参数):**
    *   `Spawn Radius Min` / `Spawn Radius Max` (浮点数): 道具相对于玩家前方生成的
      * 最近和最远 Z 轴距离范围。
    *   `Min Distance Between Items` (浮点数): 道具之间防重叠的最小安全距离。
*   **Item Prefabs (预制体引用):**
    *   `Coin Prefab` / `Lightning Prefab` / `Boost Prefab` (物体预制体): 对应金币、
      * 闪电、加速的预制体（如未拖入，运行时会自动从 `Resources/Prefabs` 加载）。
    *   `Barrier Prefabs` (预制体列表): 4 种随机障碍物预制体的列表。
*   **Spawn Bag Probabilities (生成概率袋设置):**
    本脚本使用“抽袋子（Bag）”机制确保概率严格可控，每满一袋子（`Spawn Bag Size`）
  * 道具内，各种道具的严格数量：
    *   `Spawn Bag Size` (整数): 每包生成的道具总基数（默认 20 个）。
    *   `Lightning Count In Bag` (整数): 每包中包含的闪电道具个数（默认 1 个）。
    *   `Boost Count In Bag` (整数): 每包中包含的加速道具个数（默认 1 个）。
    *   `Barrier Count In Bag` (整数): 每包中包含的障碍物道具个数（默认 2 个）。
    *   *(其余剩余名额如 20-1-1-2 = 16 个会自动填充为普通金币 `Coin`，所有道具在生
      * 成前会进行随机洗牌)*。


---

### 3. `GameMechanicsManager.cs` (游戏核心进阶机制管理器)
该脚本是新游戏机制的“大脑”，用于处理金币连击、无伤生存奖励、闪电随机效果及磁铁吸
附：
*   **Coin Streak Settings (连续吃金币里程碑):**
    *   `Coin 10 Bonus` (整数): 连续吃满 10 个金币的额外奖励分值（默认 +2 分，并弹
      * 出 coin10 弹窗）。
    *   `Coin 35 Bonus` (整数): 连续吃满 35 个金币的额外奖励分值（默认 +10 分，并弹
      * 出 coin35 弹窗）。
    *   `Coin 50 Base Bonus` (整数): 连续吃满 50 个金币（及之后每多 5 个连击）的奖励
      * 分值（默认 +2 分）。
*   **Barrier-Free Survival Settings (无障碍生存里程碑):**
    *   `Buff 30 Bonus` (整数): 连续 30 秒未碰撞任何障碍物的奖励分值（默认 +10 分，
      * 并弹出 buff30 弹窗）。
    *   `Buff 60 Bonus` (整数): 连续 60 秒未碰撞任何障碍物的奖励分值（默认 +25 分，
      * 并弹出 buff60 弹窗）。
*   **Lightning Collision Outcomes Settings (闪电碰撞随机效果列表):**
    *   `Lightning Effects` (列表): 可以自由在面板中添加/删除或修改闪电碰撞出的不同
      * 技能效果，默认有 6 种技能，每种技能均可调节以下参数：
        *   `Name` (字符串): 效果描述。
        *   `Probability` (0~100 浮点数): 触发该效果的概率比例。
        *   `Score Bonus` (整数): 触发时获得的即时分数。
        *   `Double Score Duration` (浮点数): 分数翻倍 Buff 的持续时间（秒）。
        *   `Spawn Coins Count` (整数): 在玩家面前额外生成奖励金币的个数。
        *   `Vacuum Duration` (浮点数): 磁铁吸附金币效果的持续时间（秒）。
        *   `Popup Name` (字符串): 对应弹出的 UI 界面名称（如 light1 至 light6）。
*   **Vacuum Attraction Settings (磁铁吸附参数):**
    *   `Vacuum Radius` (浮点数): 磁铁吸附金币的感知半径（默认 5 米）。
    *   `Vacuum Speed` (浮点数): 金币被吸附移向玩家的飞行速度（默认每秒 8 米）。
*   **Debug Filter Settings (控制台日志开关):**
    *   `Log Coin Streak Milestones` / `Log Survival Milestones` / `Log Lightning 
Effects` / `Log Speed Changes` / `Log Score Changes` (布尔值): 可分别开启或关闭对应
      * 机制在 Unity 控制台的日志输出，方便精细调试。

---

### 4. `SimplePlayerController.cs` (玩家角色控制器)
控制主角的物理移动、轨道（Lanes）切换、跳跃及受击减速：
*   **Movement Settings (基础移动参数):**
    *   `Move Speed` (浮点数): 玩家前进/左右移动的基础速度。
    *   `Turn Speed` (浮点数): 玩家角色转向的灵敏度。
*   **Lane Runner Settings (跑道参数):**
    *   `Lane Width` (浮点数): 左、中、右三条跑道之间的横向跨度（默认 2 米）。
    *   `Lane Change Speed` (浮点数): 切换跑道时的平滑滑动速度。
*   **Jump Settings (跳跃):**
    *   `Jump Force` (浮点数): 跳跃的垂直向上施加力度。
*   **Slowdown Settings (受击减速参数):**
    *   `Barrier Slowdown Duration` (浮点数): 撞到障碍物后的减速持续时间（默认 0.5 
     秒）。
    *   `Barrier Slowdown Multiplier` (浮点数): 减速期间的移动速度乘数（默认 0.5 倍
速）。
*   **Animation Parameter Names (动画参数映射):**
    *   `Speed ParameterName` (字符串): Animator 动画控制器中的速度 Float 变量名
    （默认 "Movement"）。


---

### 5. `CameraFollow.cs` (摄像机跟随)
控制镜头对主角的跟踪平滑度与观察距离：
*   **跟随目标:**
    *   `Target` (Transform): 摄像机要跟随的目标（如果为空，运行时相机会自动去寻找
      * 并锁定制定的 active 状态的 player）。
*   **跟随参数:**
    *   `Follow Speed` (浮点数): 镜头跟随的响应速度（平滑插值速度）。
    *   `Offset` (Vector3): 摄像机相对于角色位置的固定 3D 偏移量（控制摄像机的俯视
      * 角和视距）。

---

### 6. `PopupManager.cs` (中央弹窗管理器)
*   **Popup References (弹窗引用):**
    *   `Popup Root` (物体): 存放弹窗的 `PopUp` 画布父节点。
    *   `Popup Items` (列表): 缓存的弹窗预制体模板或现有子弹窗节点。

---

### 7. `ImageAutoSize.cs` (UI 图片比例自适应)
*   **Auto Size Settings (大小适配设置):**
    *   `Enable Auto Size` (布尔值): 是否在切换图片资源时自动改变 RectTransform 的
      * 宽高。
    *   `Keep Aspect Ratio` (布尔值): 是否锁定图片的原始高宽比进行等比缩放。
    *   `Max Width` / `Max Height` (浮点数): 缩放宽高的最大像素上限限制（设为 0 表
      * 示不限制）。
    *   `Scale Factor` (浮点数): 对最终大小的额外微调缩放系数。

---

### 8. 本地化脚本组件 (Bilingual Support Component)
*   **`LocalizedText.cs` (文本翻译):**
    *   `Database` (LanguageDatabase): 关联的翻译数据库 ScriptableObject。
    *   `Translation Key` (字符串): 关联数据库中对应的文本翻译 Key（例如 `ready` /
    `score`）。
*   **`LocalizedFont.cs` (字体切换):**
    *   `Cn Font` / `En Font` (字体资源): 分别对应中文和英文时显示的 TMP 字体。
*   **`LocalizedImage.cs` (图片切换):**
    *   `Cn Sprite` / `En Sprite` (图片资源): 分别对应中文和英文状态下的 UI 贴图。

---

### 9. `CountdownTimer_OneNumber.cs` (数字倒计时组件)
*   **倒计时参数:**
    *   `Start Count` (整数): 倒计时开始的秒数。
    *   `Interval` (浮点数): 每次跳秒的间隔秒数（默认 1s）。
    *   `Auto Start` (布尔值): 是否在物体激活时自动开始倒计时。
    *   `Text` (Text组件): 显示倒计时的 Text 文本框。
*   **事件回调:**
    *   `On Countdown Finished` (事件): 倒计时归零时触发的 Unity 回调。
    *   `On Countdown Update` (事件): 每次倒计时数字更新时触发的回调，并传出剩余数
      * 值。

---
GameFlowController 组件可配参数：

Character Fade Duration：玩家角色在游戏结束/退出时的渐隐淡出时长（默认 0.8s）。
PopupManager 组件可配参数：
小弹窗配置 (Small Popup Settings)：
World Offset：飘字相对玩家在 3D 世界空间中的偏移位置（默认 Y=2 即玩家头部上方）。
Follow Speed：跟随的平滑插值速度（默认 15f，设为 0 代表瞬间无延迟跟随）。
Enter Duration：进入渐显放大时长（默认 0.3s）。
Stay Duration：停留显示时长（默认 0.8s）。
Exit Duration：正常消失的渐隐时长（默认 0.4s）。
Quick Exit Duration：被打断时快速渐隐的时长（默认 0.15s）。
Enter Ease：进入时的缩放缓动曲线类型（支持 Back, Quad 等曲线）。
大弹窗配置 (Main Popup Settings)：
Normal Stay Duration：最后一个/单次弹窗正常停留时间（默认 1.2s）。
Enter Scale Duration：大弹窗进入缩放时长（默认 0.3s）。
Exit Fade Duration：大弹窗原位渐隐时长（默认 0.2s）。
Previous Exit Up Offset：前置弹窗向上渐隐平移的 Y 轴像素偏移量（默认 150）。
Previous Exit Duration：前置弹窗向上渐隐的动画时长（默认 0.3s）。
Start Scale：大弹窗进入的起始缩放比例（默认 0.5f）。
Main Enter Ease / Main Previous Exit Ease：大弹窗对应的缓动曲线。

所有代码与资源结构均未进行任何修改，以上参数均可在 Unity 编辑器中点击对应 GameObject
的 Inspector 面板进行调整与配表。