using UnityEngine;
using System.Collections;
using DG.Tweening;
using UnityEngine.Animations; // 引入朝向约束命名空间

/// <summary>
/// PlayerStartStopController 脚本
/// 作用：
/// 1. 控制角色（Player）在游戏倒计时（Panel3）与正式开始及游戏结束时的移入、待机、跑酷启动与停止。
/// 2. 锁定角色旋转，确保角色自始至终保持场景原样旋转，并硬编码新猫咪物体朝向背对摄像头。
/// 3. 在倒计时阶段，将 LookAtConstraint 禁用，防止角色因看向前方目标而发生倾斜；正式奔跑时恢复启用。
/// 4. 倒计时开始时，将 PlayerMesh2 的本地 Z 轴坐标在两秒内从 -3 移动到 0，之后进入待机。
/// 5. 独家动态配置主摄像机：游戏正式开始前（GO 结束前）摄像机保持在初始编辑器机位并禁用跟随；开始后摄像机挂载到 !!player/PlayerMesh2 下跟随。
/// </summary>
public class PlayerStartStopController : MonoBehaviour
{
    [Header("Target References")]
    [Tooltip("角色控制器 (PlayerController)")]
    public PlayerController playerController;
    
    [Tooltip("角色的动画控制器 (Animator)，留空则自动在子物体中寻找")]
    public Animator characterAnimator;

    [Header("Position Settings")]
    [Tooltip("从下方移入到场景默认位置所需的时间（秒）")]
    public float entryDuration = 2f;

    [Header("Animation Settings")]
    [Tooltip("待机动画状态名 (可选，直接 Play 状态名称)")]
    public string idleStateName = "Idle";

    [Header("Camera Settings")]
    [Tooltip("摄像机本地 X 轴坐标")]
    public float cameraLocalX = 0f;

    [Tooltip("摄像机本地 Y 轴坐标")]
    public float cameraLocalY = 1.74f;

    [Tooltip("摄像机初始本地 Z 轴坐标 (0)")]
    public float initialCameraLocalZ = 0f;

    [Tooltip("摄像机倒计时结束/正常运行时的本地 Z 轴坐标 (-4.34)")]
    public float targetCameraLocalZ = -4.34f;

    private Quaternion defaultRotation;
    private bool positionCaptured = false;
    private GameFlowController.GameState lastState = GameFlowController.GameState.Idle;

    private Transform newCatPlayerTransform;

    private Quaternion newCatPlayerDefaultLocalRotation;
    private LookAtConstraint lookAtConstraint;
    private float originalConstraintWeight = 1.0f;
    private bool hasLookAtConstraint = false;

    private bool isGameOverFrozen = false;
    private Vector3 frozenPlayerPos;
    private Vector3 frozenMeshLocalPos;

    private Camera mainCam;

    private void Awake()
    {
        // 自动定位角色控制器
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        if (playerController != null)
        {
            // 自动动态关联挂载 PlayerCollisionHandler，防止用户未手动挂载导致物理检测失败
            AttachHandlerIfNeeded(playerController.gameObject);
            if (playerController.PlayerMesh != null) AttachHandlerIfNeeded(playerController.PlayerMesh.gameObject);

            // 自动遍历向上搜索所有父级节点（包括挂载了 SceneBridge 的根节点等），确保全部关联 CollisionHandler
            Transform parent = playerController.transform;
            while (parent != null)
            {
                AttachHandlerIfNeeded(parent.gameObject);
                parent = parent.parent;
            }

            // 捕获并记录角色的默认初始旋转角度
            defaultRotation = playerController.transform.rotation;
            positionCaptured = true;

            // 物理层防旋转：冻结刚体的全部旋转
            Rigidbody rb = playerController.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }

            // 禁用 PathFollower，防止它强制修改角色位置
            if (playerController.pathFollower != null)
            {
                playerController.pathFollower.enabled = false;
            }

            // 在 !!player 子对象上查找 LookAtConstraint 组件
            newCatPlayerTransform = FindChildRecursive(playerController.transform, "!!player");
            if (newCatPlayerTransform != null)
            {
                AttachHandlerIfNeeded(newCatPlayerTransform.gameObject);
                lookAtConstraint = newCatPlayerTransform.GetComponent<LookAtConstraint>();
                if (lookAtConstraint != null)
                {
                    hasLookAtConstraint = true;
                    originalConstraintWeight = lookAtConstraint.weight;
                    lookAtConstraint.enabled = false;
                }
                
                // 捕获猫物体的原始本地旋转，防止世界旋转转换时 -180 到 180 翻转
                newCatPlayerDefaultLocalRotation = newCatPlayerTransform.localRotation;
            }
            else
            {
                lookAtConstraint = playerController.GetComponent<LookAtConstraint>();
                if (lookAtConstraint != null)
                {
                    hasLookAtConstraint = true;
                    originalConstraintWeight = lookAtConstraint.weight;
                    lookAtConstraint.enabled = false;
                }
            }
        }

        // 自动定位动画组件
        if (characterAnimator == null && playerController != null)
        {
            characterAnimator = playerController.GetComponentInChildren<Animator>();
        }

        // 预先缓存主相机
        GetMainCamera();
    }

    private void Start()
    {
        // 初始状态下，确保角色在初始位置且相机保持为猫 PlayerMesh3 的子物体
        if (positionCaptured && playerController != null)
        {
            ResetPlayerToStart();
        }
    }

    private void Update()
    {
        // 锁死根节点旋转：防止任何其他脚本或动画修改角色的旋转，保持初始角度原样不变
        if (playerController != null && positionCaptured && !isGameOverFrozen)
        {
            playerController.transform.rotation = defaultRotation;
        }

        if (GameFlowController.Instance == null) return;

        // 监测 GameFlowController 的全局游戏状态变化
        GameFlowController.GameState currentState = GameFlowController.Instance.CurrentState;
        if (currentState != lastState)
        {
            OnGameStateChanged(lastState, currentState);
            lastState = currentState;
        }
    }

    private void LateUpdate()
    {
        // 游戏结束/FINISH 定格状态下，在每帧 LateUpdate 强行锁定角色的绝对坐标，完全取消对旋转的重置与控制
        if (isGameOverFrozen && playerController != null)
        {
            playerController.transform.position = frozenPlayerPos;

            if (playerController.PlayerMesh != null)
            {
                playerController.PlayerMesh.localPosition = frozenMeshLocalPos;
            }
        }
    }

    private void OnGameStateChanged(GameFlowController.GameState oldState, GameFlowController.GameState newState)
    {
        Debug.Log($"[PlayerStartStopController] State changed from {oldState} to {newState}");

        switch (newState)
        {
            case GameFlowController.GameState.CountdownStart:
                // 1. 切换到 Panel3 开始 3, 2, 1 倒计时
                OnCountdownStart();
                break;

            case GameFlowController.GameState.Gameplay:
                // 2. 倒计时播放完，游戏正式开始，角色向前跑
                OnGameplayStart();
                break;

            case GameFlowController.GameState.GameOver:
            case GameFlowController.GameState.GameOverPage:
                // 3. 当倒计时结束、FINISH 弹出或进入结算页时，定格画面，不重置摄像机与角色位置
                OnGameOver();
                break;

            default:
                // 4. 其他非运行中状态（如返回菜单等），将角色还原到偏移动画起点，相机还原
                if (newState == GameFlowController.GameState.Idle || 
                    newState == GameFlowController.GameState.LanguageSelect || 
                    newState == GameFlowController.GameState.RuleRead)
                {
                    ResetPlayerToStart();
                }
                break;
        }
    }

    private void SetConstraintWeight(float weight)
    {
        if (hasLookAtConstraint && lookAtConstraint != null)
        {
            lookAtConstraint.enabled = weight > 0;
        }
    }

    private Camera GetMainCamera()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null)
            {
                mainCam = FindObjectOfType<Camera>();
            }
        }
        return mainCam;
    }

    private Transform GetActivePlayerMesh()
    {
        if (playerController == null) return null;
        if (playerController.PlayerMesh != null) return playerController.PlayerMesh;

        Transform root = newCatPlayerTransform != null ? newCatPlayerTransform : playerController.transform;
        Transform mesh3 = FindChildRecursive(root, "PlayerMesh3");
        if (mesh3 != null && mesh3.gameObject.activeInHierarchy) return mesh3;

        Transform mesh2 = FindChildRecursive(root, "PlayerMesh2");
        if (mesh2 != null && mesh2.gameObject.activeInHierarchy) return mesh2;

        return mesh3 != null ? mesh3 : (mesh2 != null ? mesh2 : root);
    }

    /// <summary>
    /// 始终保持主相机作为猫 PlayerMesh3 的子物体，并初始化本地坐标为 (0, 1.74, 0)
    /// </summary>
    public void EnsureCameraAttachedToPlayerMesh(bool resetToInitialPosition = true)
    {
        Camera cam = GetMainCamera();
        if (cam == null) return;

        Transform targetParent = GetActivePlayerMesh();
        if (targetParent == null) return;

        // 禁用 CameraFollow 脚本防止干扰父子级跟随
        var camFollow = cam.GetComponent<CameraFollow>();
        if (camFollow != null)
        {
            camFollow.enabled = false;
        }

        // 始终保持 main camera 作为猫 PlayerMesh3 的子物体
        if (cam.transform.parent != targetParent)
        {
            cam.transform.SetParent(targetParent, false);
        }

        if (resetToInitialPosition)
        {
            cam.transform.DOKill();
            cam.transform.localPosition = new Vector3(cameraLocalX, cameraLocalY, initialCameraLocalZ);
        }
    }

    private void OnCountdownStart()
    {
        if (playerController == null) return;

        // 0. 强行停止所有旋转 Tween，并精确定位角色与子物体的本地旋转
        playerController.transform.DOKill();
        playerController.transform.rotation = defaultRotation;
        if (newCatPlayerTransform != null)
        {
            newCatPlayerTransform.DOKill();
            newCatPlayerTransform.localRotation = newCatPlayerDefaultLocalRotation;
        }
        if (playerController.PlayerMesh != null)
        {
            playerController.PlayerMesh.localRotation = Quaternion.identity;
        }

        // 1. 约束与速度重置
        SetConstraintWeight(0f);
        playerController.speed = 0f;
        if (playerController.pathFollower != null)
        {
            playerController.pathFollower.speed = 0f;
        }

        // 2. 播放待机动画
        PlayIdleAnimation();

        // 3. 同步猫与相机的动画时间线：猫做 -4.34 到 0 的移动，相机做 0 到 -4.34 的 DOLocalMoveZ，实现相对场景静止的效果
        Transform playerMesh = GetActivePlayerMesh();

        if (playerMesh != null)
        {
            playerMesh.DOKill();

            Vector3 startLocalPos = playerMesh.localPosition;
            startLocalPos.y = 0f;   // 重置 Y，防止上局跳跃未完成导致角色悬停
            startLocalPos.z = targetCameraLocalZ; // -4.34f
            playerMesh.localPosition = startLocalPos;
            playerMesh.localRotation = Quaternion.identity;

            // 猫往前走 (Z 从 -4.34 到 0)
            playerMesh.DOLocalMoveZ(0f, entryDuration).SetEase(Ease.OutQuad);

            // 同时摄像机作为猫的子物体，同步时间线做 DOLocalMoveZ 从 0 到 -4.34
            Camera cam = GetMainCamera();
            if (cam != null)
            {
                EnsureCameraAttachedToPlayerMesh(false);
                cam.transform.DOKill();
                cam.transform.localPosition = new Vector3(cameraLocalX, cameraLocalY, 0f);

                cam.transform.DOLocalMoveZ(targetCameraLocalZ, entryDuration)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => {
                        Debug.Log($"[PlayerStartStopController] Camera DOLocalMoveZ complete. Local pos: {cam.transform.localPosition}");
                    });
            }
        }

        playerController.DisablePlayerControl();
    }

    private void OnGameplayStart()
    {
        if (playerController == null) return;

        // 1. 恢复 LookAtConstraint，开始看向前方路径跟随目标
        SetConstraintWeight(originalConstraintWeight);

        // 2. 保持主摄像机挂载在活跃 PlayerMesh 下跟随 (保持倒计时拉远后的位置)
        EnsureCameraAttachedToPlayerMesh(false);

        // 3. 启动角色控制器与恢复动画播放
        isGameOverFrozen = false;
        if (characterAnimator != null)
        {
            characterAnimator.speed = 1f;
        }
        playerController.EnablePlayerControl();

        // 4. 启用 PathFollower，让角色沿路径移动
        if (playerController.pathFollower != null)
        {
            playerController.pathFollower.enabled = true;
        }

        if (playerController.gameManager != null)
        {
            playerController.gameManager.GameStart();
        }
        else
        {
            playerController.speed = playerController.startingSpeed;
        }

        // 5. 播放奔跑动画
        PlayRunAnimation();
    }

    private void OnGameOver()
    {
        if (playerController == null) return;

        EnsureCameraAttachedToPlayerMesh(false);

        if (!isGameOverFrozen)
        {
            playerController.transform.DOKill();
            frozenPlayerPos = playerController.transform.position;

            if (playerController.PlayerMesh != null)
            {
                playerController.PlayerMesh.DOKill();
                frozenMeshLocalPos = playerController.PlayerMesh.localPosition;
            }
            if (newCatPlayerTransform != null)
            {
                newCatPlayerTransform.DOKill();
            }

            isGameOverFrozen = true;
        }

        playerController.DisablePlayerControl();
        playerController.speed = 0f;
        if (playerController.pathFollower != null)
        {
            playerController.pathFollower.speed = 0f;
            playerController.pathFollower.enabled = false;
        }

        // 冻结 Animator 动画播放，定格在最后一帧
        if (characterAnimator == null && playerController != null)
        {
            characterAnimator = playerController.GetComponentInChildren<Animator>();
        }
        if (characterAnimator != null)
        {
            characterAnimator.speed = 0f;
        }

        Debug.Log("[PlayerStartStopController] Game Over triggered. Position frozen without modifying rotation.");
    }

    private void ResetPlayerToStart()
    {
        if (playerController == null) return;

        isGameOverFrozen = false;
        if (characterAnimator != null)
        {
            characterAnimator.speed = 1f;
        }

        // 约束权重清零
        SetConstraintWeight(0f);

        playerController.DisablePlayerControl();
        playerController.speed = 0f;
        if (playerController.pathFollower != null)
        {
            playerController.pathFollower.speed = 0f;
            playerController.pathFollower.enabled = false;
        }

        // 角色保持当前位置，不重置，锁定朝向
        if (newCatPlayerTransform != null)
        {
            newCatPlayerTransform.DOKill();
            newCatPlayerTransform.localRotation = newCatPlayerDefaultLocalRotation;
        }
        else
        {
            playerController.transform.DOKill();
        }

        // 预设 Idle/Start 状态下的初始位置与相机位置，防止在点击 321 倒计时时发生 1 帧的坐标跃迁与闪烁
        Transform playerMesh = GetActivePlayerMesh();
        if (playerMesh != null)
        {
            playerMesh.DOKill();
            Vector3 startLocalPos = playerMesh.localPosition;
            startLocalPos.y = 0f;
            startLocalPos.z = targetCameraLocalZ; // -4.34f
            playerMesh.localPosition = startLocalPos;
            playerMesh.localRotation = Quaternion.identity;
        }

        Camera cam = GetMainCamera();
        if (cam != null)
        {
            EnsureCameraAttachedToPlayerMesh(false);
            cam.transform.DOKill();
            cam.transform.localPosition = new Vector3(cameraLocalX, cameraLocalY, initialCameraLocalZ);
        }

        PlayIdleAnimation();
    }

    private void PlayIdleAnimation()
    {
        if (characterAnimator != null && !string.IsNullOrEmpty(idleStateName))
        {
            characterAnimator.Play(idleStateName);
        }
    }

    private void PlayRunAnimation()
    {
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
        {
            return parent;
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private void AttachHandlerIfNeeded(GameObject go)
    {
        if (go != null)
        {
            var handler = go.GetComponent<PlayerCollisionHandler>();
            if (handler == null)
            {
                go.AddComponent<PlayerCollisionHandler>();
                Debug.Log($"[PlayerStartStopController] Automatically attached PlayerCollisionHandler to {go.name}");
            }
        }
    }
}
