using UnityEngine;
using UnityEngine.InputSystem;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// SceneBridge 脚本
/// 作用：仿照 SimplePlayerController 接收键盘（仅 W/A/S/D）和手柄输入，
/// 并通过反射（Reflection）调用 PlayerController 内的私有方法 LaneChangeCheck 来控制角色移动。
/// 原有代码 [PlayerController.cs] 不需要进行任何修改。
/// 同时动态补齐玩家 Tag 和 Name，唤醒原版粒子特效与跳跃物理效果。
/// </summary>
public class SceneBridge : MonoBehaviour
{
    public static SceneBridge Instance { get; private set; }

    [Header("Target Controller")]
    public PlayerController playerController;

    private InputAction moveAction;
    private InputAction jumpAction;
    private bool usePlayerInputComponent = false;
    private Vector2 moveInput;
    private bool isHoldingLeft = false;
    private bool isHoldingRight = false;
    private bool isAutoLanding = false;
    private float hoverZoneTimer = 0f;

    [Header("Roof Sliding Status")]
    public bool isRoofSliding = false;

    [Header("Lane Change Rebound Status")]
    public LanePosition previousLanePosition = LanePosition.CENTER;
    public float lastLaneChangeTime = -10f;

    private PlayerInput playerInput;
    
    // 反射缓存以提高性能
    private MethodInfo laneChangeCheckMethod;
    private PlayerController cachedPlayerController;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        // 如果未在 Inspector 赋值，则在场景中自动寻找 PlayerController
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }
        EnsureMethodCached();

        // 尝试获取现有的 PlayerInput 组件以避免冲突
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            moveAction = playerInput.actions.FindAction("Movement");
            jumpAction = playerInput.actions.FindAction("Jump");
            if (moveAction != null)
            {
                usePlayerInputComponent = true;
                Debug.Log("[SceneBridge] Successfully linked to PlayerInput component actions.");
            }
        }

        if (!usePlayerInputComponent)
        {
            Debug.Log("[SceneBridge] No PlayerInput component found or actions missing. Using manual C# bindings.");

            // 动态配置移动 Input Action (Vector2 类型)
            moveAction = new InputAction("Move", type: InputActionType.Value, expectedControlType: "Vector2");

            // 手柄左摇杆，D-pad 与通用手柄支持
            moveAction.AddBinding("<Gamepad>/leftStick");
            moveAction.AddBinding("<Gamepad>/dpad");
            moveAction.AddBinding("<Joystick>/stick");

            // 键盘仅绑定 W/A/S/D。键盘方向键由 PlayerController.Update() 自行原生处理，避免双重输入触发。
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        }
    }

    private void Start()
    {
        FixPlayerTagAndName();
    }

    private void FixPlayerTagAndName()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        if (playerController != null)
        {
            // 动态强制将玩家根节点的名字设为 "Player"
            playerController.gameObject.name = "Player";

            // 动态将渲染网格 PlayerMesh（即持有 Rigidbody + CapsuleCollider 的物体）的名字设为 "Player"
            // 这是关键！因为原版 CoinGetHandler 检查的是 other.name != "Player"，
            // 而 other 就是碰撞时检测到的 Collider 所在的 GameObject（即 PlayerMesh）
            if (playerController.PlayerMesh != null)
            {
                playerController.PlayerMesh.gameObject.name = "Player";
            }

            // 安全地设置 Tag 为 "Player"（必须先在 TagManager 中注册此 Tag）
            // 原版 PickupGetHandler 检查 other.tag == "Player"
            try
            {
                playerController.gameObject.tag = "Player";
                if (playerController.PlayerMesh != null)
                {
                    playerController.PlayerMesh.gameObject.tag = "Player";
                }

                // 递归设置所有子物体的 Tag
                var allTransforms = playerController.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    if (t != null)
                    {
                        try { t.gameObject.tag = "Player"; } catch { /* 忽略单个子节点的 Tag 设置异常 */ }
                    }
                }

                Debug.Log("[SceneBridge] Successfully set 'Player' Tag and Name on player hierarchy.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SceneBridge] Could not set 'Player' tag (not registered in TagManager?): {e.Message}");
                Debug.LogWarning("[SceneBridge] Effects that check other.tag == 'Player' will not trigger. Player Name is still set to 'Player' for other.name checks.");
            }
        }
    }

    private void OnEnable()
    {
        if (!usePlayerInputComponent && moveAction != null)
        {
            moveAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (!usePlayerInputComponent && moveAction != null)
        {
            moveAction.Disable();
        }
    }

    private void EnsureMethodCached()
    {
        if (playerController != cachedPlayerController)
        {
            cachedPlayerController = playerController;
            if (cachedPlayerController != null)
            {
                // 获取私有方法 LaneChangeCheck
                laneChangeCheckMethod = cachedPlayerController.GetType().GetMethod("LaneChangeCheck", 
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            }
            else
            {
                laneChangeCheckMethod = null;
            }
        }
    }

    private void InvokeLaneChange(string direction)
    {
        if (playerController == null) return;
        // Horizontal lane changes and jumping are mutually exclusive.
        if (!IsPlayerOnGround()) return;
        EnsureMethodCached();

        previousLanePosition = playerController.lanePosition;
        lastLaneChangeTime = Time.time;

        if (laneChangeCheckMethod != null)
        {
            laneChangeCheckMethod.Invoke(playerController, new object[] { direction });
            AudioManager.Instance?.PlaySFX(SFXType.LaneChange);
        }
        else
        {
            Debug.LogWarning("[SceneBridge] Could not find LaneChangeCheck method on PlayerController.");
        }
    }

    public bool IsRecentLaneChange()
    {
        if (playerController != null && playerController.is_switching_lanes) return true;
        if (Time.time - lastLaneChangeTime < 0.45f) return true;
        return false;
    }

    public void RevertLaneChange()
    {
        if (playerController == null || playerController.PlayerMesh == null) return;

        playerController.lanePosition = previousLanePosition;
        playerController.is_switching_lanes = false;

        float targetX = 0f;
        if (previousLanePosition == LanePosition.LEFT)
        {
            targetX = -playerController.laneOffset;
        }
        else if (previousLanePosition == LanePosition.RIGHT)
        {
            targetX = playerController.laneOffset;
        }
        else
        {
            targetX = 0f;
        }

        playerController.PlayerMesh.DOKill();
        playerController.PlayerMesh.DOLocalMoveX(targetX, 0.25f).SetEase(Ease.OutQuad).OnComplete(() => {
            if (playerController != null)
            {
                playerController.is_switching_lanes = false;
            }
        });

        // 核心修复：复位 Animator 中的 Direction 参数，使角色动作在 0.25s 内平滑还原为正向直行奔跑姿势
        if (playerController.animator != null)
        {
            int directionHash = Animator.StringToHash("Direction");
            DOTween.Kill("PlayerDirectionTween");
            DOTween.To(() => playerController.animator.GetFloat(directionHash), 
                       x => playerController.animator.SetFloat(directionHash, x), 
                       0f, 0.25f).SetEase(Ease.OutSine).SetId("PlayerDirectionTween");
        }

        Debug.Log($"[SceneBridge] Side collision detected! Reverted lane to {previousLanePosition} (Target X: {targetX}) and reset running animation pose.");
    }

    // ==========================================
    // 桥接并转发所有的 PlayerController 效果与方法
    // ==========================================

    private int lastJumpFrame = -1;

    /// <summary>
    /// 严密检测玩家是否处于地面（非跳跃状态、非车顶滑行状态且 Mesh 本地 Y 轴已着陆）
    /// </summary>
    public bool IsPlayerOnGround()
    {
        if (playerController == null) return false;

        // 1. 车顶滑行模式：在车顶滑行期间屏蔽所有跳跃与切轨
        if (isRoofSliding) return false;

        // 2. 降落接地动画中：在完成落地前屏蔽所有跳跃与切轨
        if (isAutoLanding) return false;

        // 3. 标志位校验：若处于跳跃状态，判定为不在地面
        if (playerController.is_jumping) return false;

        // 4. 物理高度校验：非悬浮区且 PlayerMesh 本地 Y 坐标 > 0.05f，说明仍在空中未完全着陆，严禁二次跳跃与切轨
        if (!playerController.on_hover_zone && playerController.PlayerMesh != null && playerController.PlayerMesh.localPosition.y > 0.05f)
        {
            return false;
        }

        return true;
    }

    public void Jump()
    {
        if (playerController != null)
        {
            // Holding the left/right stick takes priority over jumping.
            if (Mathf.Abs(moveInput.x) > 0.5f)
            {
                return;
            }

            // 刚从菜单恢复游戏时屏蔽本次跳跃按键，防止与菜单选择键冲突
            if (Time.frameCount <= SimpleUIManager.LastResumeFrame + 2 || Time.unscaledTime - SimpleUIManager.LastResumeTime < 0.25f)
            {
                return;
            }

            // 车顶滑行期间无视所有跳跃按键
            if (isRoofSliding) return;

            // 防重复判定：同一帧内只允许触发一次跳跃
            if (Time.frameCount == lastJumpFrame)
            {
                return;
            }

            // 判定：只有玩家处于地面或 HoverEnterZone / HoverExitZone 时才接收按键判定
            if (!IsPlayerOnGround())
            {
                return;
            }

            lastJumpFrame = Time.frameCount;
            bool wasOnHover = playerController.on_hover_zone;
            playerController.Jump();

            if (wasOnHover)
            {
                // 如果是在 HoverEnterZone / HoverExitZone 按下跳跃跳上车顶，进入车顶滑行模式
                isRoofSliding = true;
            }

            AudioManager.Instance?.PlaySFX(SFXType.Jump);
            Debug.Log("[SceneBridge] Jump called on PlayerController.");
        }
    }

    public void JumpDown()
    {
        isRoofSliding = false;
        if (playerController != null)
        {
            playerController.on_hover_zone = false;
            playerController.JumpDown();
            Debug.Log("[SceneBridge] JumpDown called on PlayerController. Restored to ground height.");
        }
    }

    public void StunPlayer(float deflectAmount)
    {
        isRoofSliding = false;
        isAutoLanding = false;
        hoverZoneTimer = 0f;
        if (playerController != null)
        {
            playerController.StunPlayer(deflectAmount);

            // 撞击障碍物后引发主相机微震动
            if (CameraEffectsController.Instance != null)
            {
                CameraEffectsController.Instance.TriggerObstacleShake(0.35f, 0.25f);
            }
            else if (Camera.main != null)
            {
                Camera.main.DOKill();
                Camera.main.DOShakePosition(0.5f, 0.25f, 10, 90, false, ShakeRandomnessMode.Full);
                Camera.main.DOShakeRotation(0.5f, 0.25f, 10, 90, false, ShakeRandomnessMode.Full);
            }

            Debug.Log($"[SceneBridge] StunPlayer called on PlayerController (Deflect: {deflectAmount}). Camera shaken.");
        }
    }

    public void OnCollideWithObstacle(float deflectAmount)
    {
        if (playerController != null)
        {
            playerController.OnCollideWithObstacle(deflectAmount);
            Debug.Log($"[SceneBridge] OnCollideWithObstacle called on PlayerController (Deflect: {deflectAmount}).");
        }
    }

    public void ObstacleCollisionHandler()
    {
        if (playerController != null)
        {
            playerController.ObstacleCollisionHandler();
            Debug.Log("[SceneBridge] ObstacleCollisionHandler called on PlayerController.");
        }
    }

    public void BoostPickupHandler()
    {
        if (playerController != null)
        {
           //playerController.BoostPickupHandler();
            Debug.Log("[SceneBridge] BoostPickupHandler called on PlayerController.");
        }
    }

    public void LightningPickupHandler()
    {
        if (playerController != null)
        {
            playerController.LightningPickupHandler();
            Debug.Log("[SceneBridge] LightningPickupHandler called on PlayerController.");
        }
    }

    public void LaneChangeCheck(string pos)
    {
        InvokeLaneChange(pos);
    }

    private void Update()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
            if (playerController == null) return;
        }
        EnsureMethodCached();

        float horizontalInput = 0f;
        float verticalInput = 0f;

        // 状态过滤：暂停状态或非 Gameplay 状态时不读取输入
        bool isPaused = false;
        if (SimpleUIManager.Instance != null && SimpleUIManager.Instance.IsPaused)
        {
            isPaused = true;
        }

        bool isGameplay = true;
        if (GameFlowController.Instance != null && GameFlowController.Instance.CurrentState != GameFlowController.GameState.Gameplay)
        {
            isGameplay = false;
        }

        if (isPaused || !isGameplay)
        {
            moveInput = Vector2.zero;
        }
        else
        {
            if (moveAction != null)
            {
                moveInput = moveAction.ReadValue<Vector2>();
                horizontalInput = moveInput.x;
                verticalInput = moveInput.y;
            }
        }

        // 离散轨道切换检测：只有完全处于地面 (IsPlayerOnGround()) 且非跳跃/空中/下落中才允许切换轨道
        if (IsPlayerOnGround())
        {
            if (horizontalInput < -0.5f)
            {
                if (!isHoldingLeft)
                {
                    InvokeLaneChange("LEFT");
                    isHoldingLeft = true;
                }
            }
            else
            {
                isHoldingLeft = false;
            }

            if (horizontalInput > 0.5f)
            {
                if (!isHoldingRight)
                {
                    InvokeLaneChange("RIGHT");
                    isHoldingRight = true;
                }
            }
            else
            {
                isHoldingRight = false;
            }
        }
        else
        {
            isHoldingLeft = false;
            isHoldingRight = false;
        }

        // --- 容错与自动恢复着陆保障 (在非车顶滑行且离开悬浮区时强制落地) ---
        if (playerController != null && playerController.PlayerMesh != null && isGameplay && !isPaused)
        {
            float currentLocalY = playerController.PlayerMesh.localPosition.y;

            // 1. 地面状态重置：落回地面 (Y <= 0.08f) 且处于非跳跃状态时解锁车顶滑行标记 isRoofSliding
            if (currentLocalY <= 0.08f && !playerController.is_jumping)
            {
                isRoofSliding = false;
            }

            // 2. 只有在 on_hover_zone 激活、且 Y 高度真正稳定在车顶高度 (> 0.5f) 且非跳跃上升期时，才开启 isRoofSliding
            if (playerController.on_hover_zone && currentLocalY > 0.5f && !playerController.is_jumping)
            {
                isRoofSliding = true;
            }

            // 3. 只有在非跳跃、非 on_hover_zone 且非车顶滑行状态下，才执行强行回落地面
            if (!playerController.is_jumping && !playerController.on_hover_zone && !isRoofSliding && currentLocalY > 0.05f)
            {
                if (!isAutoLanding)
                {
                    isAutoLanding = true;
                    playerController.on_hover_zone = false;
                    playerController.PlayerMesh.DOKill();
                    playerController.PlayerMesh.DOLocalMoveY(0f, 0.35f).SetEase(Ease.OutBounce).OnComplete(() => {
                        isAutoLanding = false;
                        isRoofSliding = false;
                        if (playerController != null && playerController.PlayerMesh != null)
                        {
                            Vector3 pos = playerController.PlayerMesh.localPosition;
                            pos.y = 0f;
                            playerController.PlayerMesh.localPosition = pos;
                        }
                    });
                }
            }

            // 4. 悬浮区超时降落兜底 (未开启车顶滑行且在 hover zone 停留超时)
            if (playerController.on_hover_zone && !isRoofSliding)
            {
                hoverZoneTimer += Time.deltaTime;
                if (hoverZoneTimer > 1.8f)
                {
                    hoverZoneTimer = 0f;
                    JumpDown();
                }
            }
            else
            {
                hoverZoneTimer = 0f;
            }
        }

        // --- 跳跃输入检测 ---
        // 单次按键判定：车顶滑行中 (isRoofSliding) 或刚恢复游戏 (isRecentlyResumed) 时无视所有跳跃按键
        bool isRecentlyResumed = (Time.frameCount <= SimpleUIManager.LastResumeFrame + 2) || (Time.unscaledTime - SimpleUIManager.LastResumeTime < 0.25f);
        bool jumpTriggered = false;
        if (!isPaused && !isRecentlyResumed && isGameplay && !isRoofSliding)
        {
            // 1. 若配置了 PlayerInput 专属 Jump Action，优先判断
            if (jumpAction != null && jumpAction.triggered)
            {
                jumpTriggered = true;
            }
            // 2. 通过键盘直接按键 (Space / W / UpArrow)
            else if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame))
            {
                jumpTriggered = true;
            }
            // 3. 通过手柄直接按键 (仅接收 A 键 / buttonSouth 按下，禁用摇杆向上和 Dpad 向上)
            else if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                jumpTriggered = true;
            }
        }

        if (jumpTriggered)
        {
            // 确保同一帧内不重复触发，且只有完全处于地面上 (IsPlayerOnGround()) 时才接收这次按键判定
            if (Time.frameCount != lastJumpFrame && IsPlayerOnGround() && Mathf.Abs(horizontalInput) <= 0.5f)
            {
                Jump();
            }
        }
    }
}
