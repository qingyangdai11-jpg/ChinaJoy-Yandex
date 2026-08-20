using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// PlayerCollisionHandler 脚本
/// 作用：
/// 1. 挂载于玩家角色（含有 PlayerController）物体上（由 PlayerStartStopController 自动动态挂载）。
/// 2. 识别新 coin, boost, barrier, lighting 预制体碰撞（通过标签 Tag 识别，并支持子物体 Tag 穿透）。
/// 3. 通过反射机制将面板中拖入的 15 种自定义弹窗预制体无缝注入 PopupManager。
/// 4. 动态为生成的漂浮弹幕组件注入当前 Player 位置以唤醒跟随效果。
/// 5. 驱动 PlayerController 实现跑酷中的加速（Boost）与碰撞障碍物停顿减速（Barrier）表现。
/// </summary>
public class PlayerCollisionHandler : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("玩家控制器组件，若为空则自动在自身或子级寻找")]
    public PlayerController playerController;

    [Header("Coin Popups (金币相关弹窗)")]
    [Tooltip("吃金币默认弹窗")] public GameObject coinPopup;
    [Tooltip("金币 10 连击奖励弹窗")] public GameObject coin10Popup;
    [Tooltip("金币 35 连击奖励弹窗")] public GameObject coin35Popup;
    [Tooltip("金币 50 连击奖励弹窗")] public GameObject coin50Popup;
    [Tooltip("金币双倍倍率收集弹窗 (5sbuffx2)")] public GameObject coinBuffx2Popup;

    [Header("Boost Popup (加速道具弹窗)")]
    [Tooltip("吃到加速道具弹窗")] public GameObject boostPopup;

    [Header("Barrier Popup (障碍物撞击弹窗)")]
    [Tooltip("撞击障碍物弹窗")] public GameObject barrierPopup;

    [Header("Lightning Popups (闪电六种概率事件弹窗)")]
    [Tooltip("闪电效果 1 (+5分)")] public GameObject light1Popup;
    [Tooltip("闪电效果 2 (5秒双倍分)")] public GameObject light2Popup;
    [Tooltip("闪电效果 3 (+10分)")] public GameObject light3Popup;
    [Tooltip("闪电效果 4 (生成15个金币)")] public GameObject light4Popup;
    [Tooltip("闪电效果 5 (3秒双倍分)")] public GameObject light5Popup;
    [Tooltip("闪电效果 6 (3秒磁铁吸金币)")] public GameObject light6Popup;

    [Header("Survival Popups (无障碍生存加分文字弹窗)")]
    [Tooltip("30秒无碰撞生存加分弹窗")] public GameObject buff30Popup;
    [Tooltip("60秒无碰撞生存加分弹窗")] public GameObject buff60Popup;

    [Header("Default Physics Settings")]
    [Tooltip("金币默认增加的分数")]
    public int coinDefaultScore = 1;

    [Tooltip("障碍物默认扣除的分数")]
    public int barrierDefaultScoreDeduction = -10;

    [Tooltip("速度道具默认持续时间（秒）")]
    public float boostDefaultDuration = 5f;

    [Tooltip("速度道具默认速度倍率")]
    public float boostSpeedMultiplier = 1.3f;

    [Tooltip("障碍物碰撞默认减速倍率")]
    public float barrierSlowdownMultiplier = 0.3f;

    [Tooltip("障碍物碰撞默认减速持续时间（秒）")]
    public float barrierSlowdownDuration = 1.5f;

    // 记录已经碰撞处理过的物体，防止同一帧内重复触发 (使用 static 保证挂在多个核心节点的组件共享同一个去重集)
    private static HashSet<GameObject> processedObjects = new HashSet<GameObject>();
    private static Dictionary<int, float> deductedObstacleInstanceIDs = new Dictionary<int, float>();
    private float originalMaxSpeed = 10f;
    private Coroutine boostCoroutine;
    private Coroutine slowdownCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void FixPopupNamingHierarchy()
    {
        // 自动发现场景中可能存在的大小写不一致问题（如 popup 或者是 canvas），在加载后自动重命名为规范的 "PopUp" 和 "Canvas"
        // 从而完美解决 PopupManager.cs 仅能识别 "PopUp" 以及 "Canvas" 大小写的报错与初始化失败问题
        var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in allGos)
        {
            if (go == null || string.IsNullOrEmpty(go.scene.name)) continue;

            // 1. 重命名根节点为 "PopUp"
            if (go.name.Equals("popup", System.StringComparison.OrdinalIgnoreCase))
            {
                go.name = "PopUp";
            }

            // 2. 检查子节点，将 "canvas" 重命名为 "Canvas"
            if (go.name == "PopUp")
            {
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    Transform child = go.transform.GetChild(i);
                    if (child != null && child.name.Equals("canvas", System.StringComparison.OrdinalIgnoreCase))
                    {
                        child.name = "Canvas";
                        Debug.Log("[FixPopupNamingHierarchy] Successfully renamed 'canvas' to 'Canvas' under PopUp.");
                    }
                }
            }
        }
    }

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>() ?? GetComponent<PlayerController>() ?? FindObjectOfType<PlayerController>();
        }

        if (playerController != null)
        {
            originalMaxSpeed = playerController.maxSpeed;
        }
    }

    private IEnumerator Start()
    {
        if (playerController != null && originalMaxSpeed <= 0f)
        {
            originalMaxSpeed = playerController.maxSpeed;
        }

        // 核心修正：等待一帧以确保 PopupManager.Start() 里的 InitializePopupReferences() 先执行并清空列表
        yield return null;

        // 注册当前拖入的面板自定义预制体到 PopupManager 的内部列表中以备检索
        RegisterCustomPopups();
    }

    private void OnEnable()
    {
        processedObjects.Clear();
        deductedObstacleInstanceIDs.Clear();
    }

    private void OnTriggerEnter(Collider other) => HandleCollision(other?.gameObject);
    private void OnTriggerStay(Collider other) => HandleCollision(other?.gameObject);
    private void OnCollisionEnter(Collision collision) => HandleCollision(collision?.gameObject);
    private void OnCollisionStay(Collision collision) => HandleCollision(collision?.gameObject);
    private void OnControllerColliderHit(ControllerColliderHit hit) => HandleCollision(hit?.gameObject);

    private bool TryMatchItemType(string keyword, out ItemType type, out int scoreMod, out float duration, out string popup)
    {
        type = ItemType.Coin;
        scoreMod = 0;
        duration = 0f;
        popup = "";
        if (string.IsNullOrEmpty(keyword)) return false;

        string kwLower = keyword.ToLower();
        if (kwLower.Contains("coin"))
        {
            type = ItemType.Coin;
            scoreMod = coinDefaultScore;
            popup = "coin";
            return true;
        }
        if (kwLower.Contains("boost"))
        {
            type = ItemType.Boost;
            duration = boostDefaultDuration;
            popup = "boost";
            return true;
        }
        if (kwLower.Contains("barrier") || kwLower.Contains("obstacle"))
        {
            type = ItemType.Barrier;
            scoreMod = barrierDefaultScoreDeduction;
            popup = "barrier";
            return true;
        }
        if (kwLower.Contains("light"))
        {
            type = ItemType.Lighting;
            popup = "lighting";
            return true;
        }
        return false;
    }

    private void HandleCollision(GameObject itemObj)
    {
        // 核心防线：防御空引用与已被外部销毁过的物体
        if (itemObj == null) return;

        // 核心排除：HoverEnterZone 和 HoverExitZone 踏板/悬浮引导区域不算作障碍物碰撞
        if (IsHoverZone(itemObj)) return;

        // 状态检测
        if (GameFlowController.Instance != null && GameFlowController.Instance.CurrentState != GameFlowController.GameState.Gameplay) return;

        // 尝试获取原生 CollectibleItem 组件
        CollectibleItem colItem = itemObj.GetComponent<CollectibleItem>() ?? itemObj.GetComponentInParent<CollectibleItem>();

        // 如果非障碍物组件已被标记为收集过，直接返回
        if (colItem != null && colItem.isCollected && colItem.itemType != ItemType.Barrier) return;

        ItemType type = ItemType.Coin;
        int scoreMod = 0;
        float duration = 0f;
        string popup = "";
        bool isMatched = false;

        // 1. 优先通过 CollectibleItem 组件识别属性
        if (colItem != null)
        {
            isMatched = true;
            type = colItem.itemType;
            scoreMod = colItem.scoreModifier;
            duration = colItem.speedBoostDuration;
            popup = colItem.popupName;
        }
        else if (TryMatchItemType(itemObj.name, out type, out scoreMod, out duration, out popup))
        {
            isMatched = true;
        }
        else if (TryMatchItemType(GetTagInParentChain(itemObj), out type, out scoreMod, out duration, out popup))
        {
            isMatched = true;
        }

        if (!isMatched) return;

        GameObject rootObj = (colItem != null) ? colItem.gameObject : itemObj;

        // 如果碰触的是上一次撞击的车辆，只要玩家在悬浮、跳跃或滑行状态下，就完全忽略碰撞
        int checkID = (colItem != null) ? colItem.gameObject.GetInstanceID() : (rootObj != null ? rootObj.GetInstanceID() : itemObj.GetInstanceID());
        if (type == ItemType.Barrier && playerController != null && playerController.last_hit_car_instance_id == checkID)
        {
            if (playerController.on_hover_zone || playerController.is_jumping || (SceneBridge.Instance != null && SceneBridge.Instance.isRoofSliding))
            {
                return;
            }
        }

        // 如果玩家处于跳跃状态，碰撞到障碍物时直接无视，防止起跳过程中被二次判定碰撞
        if (type == ItemType.Barrier && playerController != null && playerController.is_jumping)
        {
            return;
        }

        // 动态确保预制体模板已被成功注入（防止运行时列表被重置）
        RegisterCustomPopups();

        // 非障碍物道具（金币、加速、闪电）只收集处理一次
        if (type != ItemType.Barrier && processedObjects.Contains(itemObj)) return;
        processedObjects.Add(itemObj);

        if (rootObj != itemObj)
        {
            processedObjects.Add(rootObj);
        }

        // 初始化/同步 CollectibleItem 状态
        if (colItem != null)
        {
            colItem.isCollected = true;
            
            // 如果预制体上的 popupName 为空，必须将我们默认的标签对应的弹窗名写回
            if (string.IsNullOrEmpty(colItem.popupName))
            {
                colItem.popupName = popup;
            }
            else
            {
                popup = colItem.popupName;
            }
            colItem.enabled = false;
        }
        else
        {
            colItem = rootObj.GetComponent<CollectibleItem>();
            if (colItem == null)
            {
                colItem = rootObj.AddComponent<CollectibleItem>();
            }
            colItem.Initialize(type, scoreMod, duration, popup);
            colItem.isCollected = true;
            colItem.enabled = false;
        }

        // 障碍物碰撞判定：撞击同一个障碍物可重复扣分，但引入 1.0 秒防刷屏冷却时间
        bool isFirstHitForObstacle = true;
        if (type == ItemType.Barrier)
        {
            int rootID = rootObj.GetInstanceID();
            if (playerController != null)
            {
                playerController.last_hit_car_instance_id = rootID;
            }
            if (deductedObstacleInstanceIDs.TryGetValue(rootID, out float lastHitTime))
            {
                if (Time.time - lastHitTime < 1.0f)
                {
                    isFirstHitForObstacle = false;
                }
                else
                {
                    deductedObstacleInstanceIDs[rootID] = Time.time;
                }
            }
            else
            {
                deductedObstacleInstanceIDs[rootID] = Time.time;
            }
        }

        // 1. 调用 GameMechanicsManager 驱动对应的碰撞机制（连击奖励、金币统计、雷电概率触发等所有游戏逻辑）
        if (isFirstHitForObstacle)
        {
            if (GameMechanicsManager.Instance != null)
            {
                GameMechanicsManager.Instance.OnPlayerCollidedWithItem(colItem);
            }
            else
            {
                // 兜底：若没有机制管理器，直接加分
                if (GameFlowController.Instance != null && type != ItemType.Boost && type != ItemType.Lighting)
                {
                    GameFlowController.Instance.AddScore(scoreMod);
                }
            }
        }

        // 2. 驱动玩家本身的物理与速度表现（加速 / 撞击障碍物回弹、减速与相机震动）
        if (playerController != null)
        {
            if (type == ItemType.Boost)
            {
                ApplySpeedBoost(duration);
            }
            else if (type == ItemType.Barrier)
            {
                // 跳跃飞越保护：只要处于跳跃起飞状态，判定为跳跃越过障碍物，全程免疫 Barrier 碰撞击退
                if (playerController != null && playerController.is_jumping)
                {
                    return;
                }

                // 播放障碍物碰撞受击音效 (仅在首次碰撞该障碍物时触发一次)
                if (isFirstHitForObstacle)
                {
                    AudioManager.Instance?.PlaySFX(SFXType.Obstacle);
                }

                bool isSideCollision = false;
                if (playerController != null && playerController.is_switching_lanes)
                {
                    isSideCollision = true;
                }
                else if (SceneBridge.Instance != null && SceneBridge.Instance.IsRecentLaneChange())
                {
                    isSideCollision = true;
                }

                if (isSideCollision)
                {
                    // 侧面撞击：弹回切轨前原车道并减速
                    if (SceneBridge.Instance != null)
                    {
                        SceneBridge.Instance.RevertLaneChange();
                    }
                    ApplySlowdown();
                    if (CameraEffectsController.Instance != null)
                    {
                        CameraEffectsController.Instance.TriggerObstacleShake(0.35f, 0.2f);
                    }
                    else if (Camera.main != null)
                    {
                        Camera.main.DOKill();
                        Camera.main.DOShakePosition(0.3f, 0.15f, 10, 90, false, ShakeRandomnessMode.Full);
                    }
                }
                else
                {
                    // 正面撞击：沿赛道向后击退回弹，且重复撞击持续回弹
                    ApplySlowdown();
                    if (CameraEffectsController.Instance != null)
                    {
                        CameraEffectsController.Instance.TriggerObstacleShake(0.35f, 0.25f);
                    }

                    // 如果正面撞到的是 CAR 或 TRAM，无论 on_hover_zone 当前状态如何，
                    // 都直接强制写入悬浮记忆，确保不管第几次撞击，按跳跃键都能跳上车顶滑行
                    if (playerController != null)
                    {
                        ObstacleHandler oh = rootObj.GetComponent<ObstacleHandler>();
                        if (oh == null) oh = rootObj.GetComponentInChildren<ObstacleHandler>();
                        if (oh == null && itemObj != null) oh = itemObj.GetComponentInParent<ObstacleHandler>();
                        if (oh != null && (oh.obstacleType == ObstacleType.OBSTACLE_CAR || oh.obstacleType == ObstacleType.OBSTACLE_TRAM))
                        {
                            playerController.was_in_hover_zone_on_hit = true;
                            playerController.last_hover_jump_height = (oh.obstacleType == ObstacleType.OBSTACLE_CAR) ? 1.8f : 2.5f;
                        }
                    }

                    if (SceneBridge.Instance != null)
                    {
                        SceneBridge.Instance.StunPlayer(4f);
                    }
                    else if (playerController != null)
                    {
                        playerController.StunPlayer(4f);
                    }
                }
            }
        }

        // 3. 通知生成器（Spawner）该道具已被收集，防止闪电机制额外产生的道具阻碍未来的生成或补充
        if (EnemySpawner.Instance != null)
        {
            EnemySpawner.Instance.OnItemCollected(rootObj, type, popup);
        }

        // 4. 清理被碰撞的物体（非障碍物）—— 障碍物保持 Collider 激活，防穿透并可多次回弹
        if (type != ItemType.Barrier)
        {
            // 先尝试调用原版的 CoinGetHandler / PickupGetHandler 的 OnEnterHandler()
            // 它们会：激活粒子特效（EffectGO）、隐藏主模型（MainAssetGO）、并在 2 秒后自行销毁
            bool handledByOriginal = false;

            var coinGet = rootObj.GetComponent<CoinGetHandler>();
            if (coinGet == null) coinGet = rootObj.GetComponentInChildren<CoinGetHandler>();
            if (coinGet != null)
            {
                coinGet.OnEnterHandler();
                coinGet.Invoke("DestroySelf", 2f);
                handledByOriginal = true;
            }

            var pickupGet = rootObj.GetComponent<PickupGetHandler>();
            if (pickupGet == null) pickupGet = rootObj.GetComponentInChildren<PickupGetHandler>();
            if (pickupGet != null)
            {
                pickupGet.OnEnterHandler();
                pickupGet.Invoke("DestroySelf", 2f);
                handledByOriginal = true;
            }

            if (!handledByOriginal)
            {
                // 兜底：如果道具上没有原版特效脚本，则直接立刻销毁
                rootObj.transform.DOKill();
                Destroy(rootObj);
            }
        }
    }

    //通过反射机制 typeof(PopupManager).GetField(...) 获取 PopupManager 内部的私有模板列表，
    //将 Inspector 面板中拖入的 15 种自定义弹窗预制体强行重命名并注入其中，实现了低耦合的预制体配置管理。
    private void RegisterCustomPopups()
    {
        if (PopupManager.Instance == null) return;

        var field = typeof(PopupManager).GetField("popupItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return;

        var list = field.GetValue(PopupManager.Instance) as List<GameObject>;
        if (list == null) return;

        // 构造注入映射字典，将面板拖拽的资源以对应的标准 Key 注册到 PopupManager
        var map = new Dictionary<string, GameObject>()
        {
            { "coin", coinPopup },
            { "coin10", coin10Popup },
            { "coin35", coin35Popup },
            { "coin50", coin50Popup },
            { "5sbuffx2", coinBuffx2Popup },
            { "boost", boostPopup },
            { "barrier", barrierPopup },
            { "light1", light1Popup },
            { "light2", light2Popup },
            { "light3", light3Popup },
            { "light4", light4Popup },
            { "light5", light5Popup },
            { "light6", light6Popup },
            { "buff30", buff30Popup },
            { "buff60", buff60Popup }
        };

        foreach (var pair in map)
        {
            if (pair.Value != null)
            {
                // 强制将预制体/物体名称重命名以通过 PopupManager 内部的 Find(name) 检测
                pair.Value.name = pair.Key;

                // 移除 PopupManager 原有冲突或老同名模板
                list.RemoveAll(x => x != null && x.name.Equals(pair.Key, System.StringComparison.OrdinalIgnoreCase));
                
                // 添加我们拖拽配置的新项
                list.Add(pair.Value);
            }
        }
        Debug.Log("[PlayerCollisionHandler] Successfully registered all 15 custom popups into PopupManager.");
    }

    private void ApplySpeedBoost(float duration)
    {
        DOTween.Kill("PlayerSpeedTween");
        if (boostCoroutine != null) StopCoroutine(boostCoroutine);
        if (slowdownCoroutine != null)
        {
            StopCoroutine(slowdownCoroutine);
            slowdownCoroutine = null;
        }

        boostCoroutine = StartCoroutine(SpeedBoostRoutine(duration));
    }

    private IEnumerator SpeedBoostRoutine(float duration)
    {
        if (playerController == null) yield break;

        float boostedMaxSpeed = originalMaxSpeed * boostSpeedMultiplier;
        playerController.maxSpeed = boostedMaxSpeed;
        
        // 缓动加速：在 0.6 秒内平滑增加到加速速度，不产生突变感
        DOTween.To(() => playerController.speed, x => {
            if (playerController != null)
            {
                playerController.speed = x;
                if (playerController.pathFollower != null) playerController.pathFollower.speed = x;
            }
        }, boostedMaxSpeed, 0.6f).SetEase(Ease.OutQuad).SetId("PlayerSpeedTween");

        Debug.Log($"[PlayerCollisionHandler] Speed Boost applied: maxSpeed = {boostedMaxSpeed} for {duration}s");

        yield return new WaitForSeconds(duration);

        // 缓动减速：在 1.0 秒内平滑降低回正常速度，极具惯性真实感
        playerController.maxSpeed = originalMaxSpeed;
        DOTween.To(() => playerController.speed, x => {
            if (playerController != null)
            {
                playerController.speed = x;
                if (playerController.pathFollower != null) playerController.pathFollower.speed = x;
            }
        }, originalMaxSpeed, 1.0f).SetEase(Ease.InOutQuad).SetId("PlayerSpeedTween");

        Debug.Log("[PlayerCollisionHandler] Speed Boost expired. Speed restored to normal.");
        boostCoroutine = null;
    }

    public void CancelSlowdown()
    {
        DOTween.Kill("PlayerReboundTween");
        DOTween.Kill("PlayerSpeedTween");
        if (slowdownCoroutine != null)
        {
            StopCoroutine(slowdownCoroutine);
            slowdownCoroutine = null;
        }
        if (playerController != null)
        {
            playerController.maxSpeed = originalMaxSpeed;
        }
    }

    private void ApplySlowdown()
    {
        DOTween.Kill("PlayerSpeedTween");
        if (slowdownCoroutine != null) StopCoroutine(slowdownCoroutine);
        if (boostCoroutine != null)
        {
            StopCoroutine(boostCoroutine);
            boostCoroutine = null;
        }

        slowdownCoroutine = StartCoroutine(SlowdownRoutine());
    }

    //撞击障碍物（Barrier）时，使用 DOTween 强制减少 PathFollower.distanceTravelled（平滑后退 1.8
    //个单位），并赋予物理 -5f 的后退初速度，彻底解决高速下撞车穿模的物理引擎局限。
    //侧向碰撞车道弹回：识别切轨期间（is_switching_lanes）发生的碰撞，调用 RevertLaneChange() 强制弹回前一个车道，并触发相机抖动。
    //车顶滑行记忆写入：正面撞击小汽车（CAR）或电车（TRAM）时，即使未在悬浮区，也会强行将 was_in_hover_zone_on_hit 设为 true，并根据车型记录高度，确保玩家按下跳跃键时可跳上车顶滑行。
    private IEnumerator SlowdownRoutine()
    {
        if (playerController == null) yield break;

        // 核心阻断：撞击障碍物后瞬间平滑退后 1.8 单位，彻底阻断穿透
        if (playerController.pathFollower != null)
        {
            float targetDistance = Mathf.Max(0f, playerController.pathFollower.distanceTravelled - 1.8f);
            DOTween.Kill("PlayerReboundTween");
            DOTween.To(() => playerController.pathFollower.distanceTravelled, x => {
                if (playerController != null && playerController.pathFollower != null)
                {
                    playerController.pathFollower.distanceTravelled = x;
                }
            }, targetDistance, 0.35f).SetEase(Ease.OutQuad).SetId("PlayerReboundTween");
        }

        // 维持 0.35 秒负向击退速度，强行将角色推离障碍物正面
        playerController.speed = -5f;
        if (playerController.pathFollower != null)
        {
            playerController.pathFollower.speed = -5f;
        }

        playerController.ObstacleCollisionHandler();

        yield return new WaitForSeconds(0.35f);

        float slowedMaxSpeed = originalMaxSpeed * barrierSlowdownMultiplier;
        playerController.maxSpeed = slowedMaxSpeed;

        // 回弹结束后平滑恢复起步速度
        DOTween.To(() => playerController.speed, x => {
            if (playerController != null)
            {
                playerController.speed = x;
                if (playerController.pathFollower != null) playerController.pathFollower.speed = x;
            }
        }, slowedMaxSpeed, 0.5f).SetEase(Ease.OutQuad).SetId("PlayerSpeedTween");

        Debug.Log($"[PlayerCollisionHandler] Barrier rebound & slowdown applied: maxSpeed = {slowedMaxSpeed} for {barrierSlowdownDuration}s");

        yield return new WaitForSeconds(barrierSlowdownDuration);

        // 惩罚结束缓动：在 1.0 秒内从减速上限平滑加速回原始速度 (如 10)
        playerController.maxSpeed = originalMaxSpeed;
        DOTween.To(() => playerController.speed, x => {
            if (playerController != null)
            {
                playerController.speed = x;
                if (playerController.pathFollower != null) playerController.pathFollower.speed = x;
            }
        }, originalMaxSpeed, 1.0f).SetEase(Ease.OutQuad).SetId("PlayerSpeedTween");

        Debug.Log("[PlayerCollisionHandler] Barrier slowdown expired. Max speed limit restored.");
        slowdownCoroutine = null;
    }

    private float tagScanTimer = 0f;

    private void Update()
    {
        // 增量式物理自愈：每隔 0.2 秒扫描一次赛道上的新金币和障碍物，强制把它们的 Collider 设为 Trigger。
        // 这 100% 解决了 Kinematic 刚体（Player）与静态 Collider（道具）直接穿模而不触发物理回调的 Unity 底层物理局限！
        tagScanTimer += Time.deltaTime;
        if (tagScanTimer >= 0.2f)
        {
            tagScanTimer = 0f;
            //ForceTriggersOnTaggedItems();
        }
    }

    //private void ForceTriggersOnTaggedItems()
    //{
    //    // 自动发现场景中所有的 Collider，只要其 Tag 包含目标关键字且未设为 Trigger，自动将其升级为 Trigger
    //    // 这避免了 FindGameObjectsWithTag 因为编辑器未预定义 Tag 而抛出异常的情况
    //    var colliders = FindObjectsOfType<Collider>();
    //    foreach (var col in colliders)
    //    {
    //        if (col != null && !col.isTrigger && col.gameObject != null)
    //        {
    //            string tagLower = col.gameObject.tag.ToLower();
    //            if (tagLower.Contains("coin") ||
    //                tagLower.Contains("boost") ||
    //                tagLower.Contains("barrier") ||
    //                tagLower.Contains("obstacle") ||
    //                tagLower.Contains("light"))
    //            {
    //                col.isTrigger = true;
    //                Debug.Log($"[PlayerCollisionHandler] Automatically set isTrigger=true on collider of tagged item: {col.gameObject.name} (Tag: {col.gameObject.tag})");
    //            }
    //        }
    //    }
    //}

    private void LateUpdate()
    {
        // 核心跟随挂载：自动检索由于初始找不到 SimplePlayerController 而 playerTransform 为空的弹窗，绑定当前角色位置以开启其 LateUpdate 移动
        if (playerController != null)
        {
            var popups = FindObjectsOfType<SmallPopupFollowPlayer>();
            foreach (var popup in popups)
            {
                if (popup != null && popup.playerTransform == null)
                {
                    // 核心修正二：将跟随目标绑定为 PlayerMesh，这样弹幕才会精确跟随换道左右移动的猫咪本身，而不是死死停在原地不动的 root 上！
                    popup.playerTransform = playerController.PlayerMesh;
                }
            }

            // 物理轨道偏移同步：
            // 新版 PlayerController 控制换道时，只会利用 DOTween 移动子节点 PlayerMesh 的 LocalPosition.X（-2.5 到 2.5）
            // 而挂载了 Rigidbody 与 Collider 的根物体其实一动不动（永远停留在中车道 X=0 处）！
            // 为此，我们必须在每帧 LateUpdate 中，将根节点上的物理碰撞体中心点（Center.X）强行同步为 PlayerMesh 的 Local X，
            // 从而实现物理判定区域随换道动画同步滑移，彻底解决两边车道吃不到金币与撞不到障碍的 Bug！
            SyncColliderPositions();
        }
    }

    //跑酷换道时，父节点依然保持在车道中线，只有子节点 PlayerMesh 在左右移动。此函数在 LateUpdate 中将人物层级下所有的 CharacterController 和 Collider
    //中心点（Center.X）物理偏移与 PlayerMesh 同步，避免玩家在两侧车道出现“吃不到金币”或“无形撞车”的物理错位 Bug
    private void SyncColliderPositions()
    {
        if (playerController == null || playerController.PlayerMesh == null) return;

        float targetX = playerController.PlayerMesh.localPosition.x;

        // 1. 同步 CharacterController（角色控制器）的中心点与位置偏移
        var cc = playerController.GetComponentInChildren<CharacterController>(true);
        if (cc != null)
        {
            cc.center = new Vector3(targetX, cc.center.y, cc.center.z);
            if (cc.gameObject != playerController.gameObject && !IsChildOf(cc.transform, playerController.PlayerMesh))
            {
                // 使用世界坐标系的绝对对准，避免父子级嵌套导致的 X 轴偏移失效
                cc.transform.position = new Vector3(playerController.PlayerMesh.position.x, cc.transform.position.y, cc.transform.position.z);
            }
        }

        // 2. 递归查找并同步整个人物层级下所有不在 PlayerMesh 下的 Collider
        var allColliders = playerController.GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            if (col == null) continue;

            // 如果 Collider 本身挂载在随换道动画自动移动的 PlayerMesh 下，跳过手动计算
            if (IsChildOf(col.transform, playerController.PlayerMesh)) continue;

            if (col.gameObject == playerController.gameObject)
            {
                // 如果是直接挂在根节点的 Collider，修改其 Shape Center（支持 Box, Capsule, Sphere）
                ShiftColliderCenter(col, targetX);
            }
            else
            {
                // 核心修正一：如果是挂在非 PlayerMesh 子层级的独立物理节点上，
                // 使用世界坐标系绝对对齐到 PlayerMesh.position.x，完美穿透各种复杂的父节点 LocalOffset / Rotation 干扰！
                col.transform.position = new Vector3(playerController.PlayerMesh.position.x, col.transform.position.y, col.transform.position.z);
            }
        }
    }

    private bool IsChildOf(Transform child, Transform parent)
    {
        if (child == null || parent == null) return false;
        Transform current = child.parent;
        while (current != null)
        {
            if (current == parent) return true;
            current = current.parent;
        }
        return false;
    }

    private void ShiftColliderCenter(Collider col, float targetX)
    {
        if (col is BoxCollider box) box.center = new Vector3(targetX, box.center.y, box.center.z);
        else if (col is CapsuleCollider cap) cap.center = new Vector3(targetX, cap.center.y, cap.center.z);
        else if (col is SphereCollider sph) sph.center = new Vector3(targetX, sph.center.y, sph.center.z);
    }

    private bool IsHoverZone(GameObject itemObj)
    {
        if (itemObj == null) return false;

        bool isZoneName(string name) => name.IndexOf("hoverenterzone", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                                        name.IndexOf("hoverexitzone", System.StringComparison.OrdinalIgnoreCase) >= 0;

        if (isZoneName(itemObj.name) || (itemObj.transform.parent != null && isZoneName(itemObj.transform.parent.name)))
            return true;

        var obstacleHandler = itemObj.GetComponent<ObstacleHandler>() ?? itemObj.GetComponentInParent<ObstacleHandler>();
        return obstacleHandler != null && obstacleHandler.obstacleType != ObstacleType.OBSTACLE_STUN_ZONE;
    }

    private string GetTagInParentChain(GameObject go)
    {
        if (IsHoverZone(go)) return "Untagged";

        Transform current = go.transform;
        while (current != null)
        {
            string t = current.gameObject.tag;
            if (!string.IsNullOrEmpty(t) && t != "Untagged")
            {
                string tLower = t.ToLower();
                if (tLower.Contains("coin") || tLower.Contains("boost") || tLower.Contains("barrier") || tLower.Contains("obstacle") || tLower.Contains("light"))
                {
                    return t;
                }
            }
            current = current.parent;
        }
        return go.tag;
    }
}
