using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using PathCreation;
using PathCreation.Examples;

public class GameMechanicsManager : MonoBehaviour
{
    public static GameMechanicsManager Instance { get; private set; }

    [System.Serializable]
    public class LightningEffect
    {
        [Tooltip("Effect name or identifier")]
        public string name;
        [Tooltip("Probability percentage (e.g. 20 for 20%)")]
        [Range(0f, 100f)] public float probability;
        [Tooltip("Immediate score bonus on triggering this effect")]
        public int scoreBonus = 0;
        [Tooltip("Duration of score doubling (in seconds)")]
        public float doubleScoreDuration = 0f;
        [Tooltip("Number of extra coins to spawn near player")]
        public int spawnCoinsCount = 0;
        [Tooltip("Duration of vacuum coin absorption (in seconds)")]
        public float vacuumDuration = 0f;
        [Tooltip("Popup canvas name to show on trigger")]
        public string popupName = "";

        public LightningEffect(string name, float probability, int scoreBonus, float doubleScoreDuration, int spawnCoinsCount, float vacuumDuration, string popupName)
        {
            this.name = name;
            this.probability = probability;
            this.scoreBonus = scoreBonus;
            this.doubleScoreDuration = doubleScoreDuration;
            this.spawnCoinsCount = spawnCoinsCount;
            this.vacuumDuration = vacuumDuration;
            this.popupName = popupName;
        }
    }

    [Header("Coin Streak Settings")]
    [Tooltip("Streak of 10 coins bonus points")]
    public int coin10Bonus = 2;
    [Tooltip("Streak of 35 coins bonus points")]
    public int coin35Bonus = 10;
    [Tooltip("Streak of 50 coins and beyond (every 5 coins) bonus points")]
    public int coin50BaseBonus = 2;

    [Header("Barrier-Free Survival Settings")]
    [Tooltip("30 seconds without colliding with any barrier bonus points")]
    public int buff30Bonus = 10;
    [Tooltip("60 seconds without colliding with any barrier bonus points")]
    public int buff60Bonus = 25;

    [Header("Lightning Collision Outcomes Settings")]
    public List<LightningEffect> lightningEffects = new List<LightningEffect>();

    [Header("Vacuum Attraction Settings")]
    public float vacuumRadius = 5f;
    public float vacuumSpeed = 8f;

    [Header("Debug Filter Settings")]
    [Tooltip("Log coin collection streak milestones")]
    public bool logCoinStreakMilestones = true;
    [Tooltip("Log barrier-free survival milestones (30s/60s)")]
    public bool logSurvivalMilestones = true;
    [Tooltip("Log lightning random outcome selections")]
    public bool logLightningEffects = true;
    [Tooltip("Log player speed boost and slowdown states")]
    public bool logSpeedChanges = true;
    [Tooltip("Log score additions and deductions (e.g. bonus scores)")]
    public bool logScoreChanges = true;

    // Runtime variables
    private int consecutiveCoins = 0;
    private float timeSinceLastBarrier = 0f;
    private bool is30sBuffTriggered = false;
    private bool is60sBuffTriggered = false;

    private float doubleScoreTimer = 0f;
    private float vacuumTimer = 0f;
    private float light2BuffTimer = 0f;

    [Header("Player Reference")]
    [Tooltip("在 Inspector 中拖入角色物体的 Transform。若未手动指定，系统会自动检索场景中的角色。")]
    public Transform playerTransform;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeDefaultLightningEffects();
    }

    private void Start()
    {
        ResetState();
    }

    private void InitializeDefaultLightningEffects()
    {
        if (lightningEffects == null || lightningEffects.Count == 0)
        {
            lightningEffects = new List<LightningEffect>()
            {
                new LightningEffect("Effect 1: +5 pts", 20f, 5, 0f, 0, 0f, "light1"),
                new LightningEffect("Effect 2: 5s Double Score", 15f, 0, 5f, 0, 0f, "light2"),
                new LightningEffect("Effect 3: +10 pts", 15f, 10, 0f, 0, 0f, "light3"),
                new LightningEffect("Effect 4: Spawn 10 Coins", 20f, 0, 0f, 10, 0f, "light4"),
                new LightningEffect("Effect 5: 3s Double Score", 15f, 0, 3f, 0, 0f, "light5"),
                new LightningEffect("Effect 6: 3s Coin Vacuum", 15f, 0, 0f, 0, 3f, "light6")
            };
        }
    }

    public void ResetState()
    {
        consecutiveCoins = 0;
        timeSinceLastBarrier = 0f;
        is30sBuffTriggered = false;
        is60sBuffTriggered = false;
        doubleScoreTimer = 0f;
        vacuumTimer = 0f;
        light2BuffTimer = 0f;
        Debug.Log("[GameMechanicsManager] State reset for new gameplay session.");
    }

    private void Update()
    {
        if (GameFlowController.Instance == null || GameFlowController.Instance.CurrentState != GameFlowController.GameState.Gameplay)
        {
            return;
        }

        // 1. Accumulate barrier-free survival timer
        timeSinceLastBarrier += Time.deltaTime;
        CheckBarrierFreeMilestones();

        // 2. Decrement double score buff timer
        if (doubleScoreTimer > 0f)
        {
            doubleScoreTimer -= Time.deltaTime;
        }

        // Decrement light2 buff timer
        if (light2BuffTimer > 0f)
        {
            light2BuffTimer -= Time.deltaTime;
        }

        // 3. Decrement vacuum buff timer & execute pull
        if (vacuumTimer > 0f)
        {
            vacuumTimer -= Time.deltaTime;
            ExecuteVacuumCoins();
        }
    }

    private void CheckMilestone(ref bool triggered, float threshold, int bonus, string popup, SFXType sfx, string logMsg)
    {
        if (!triggered && timeSinceLastBarrier >= threshold)
        {
            triggered = true;
            AddBonusScore(bonus, popup);
            AudioManager.Instance?.PlaySFX(sfx);
            if (logSurvivalMilestones)
            {
                Debug.Log(logMsg);
            }
        }
    }

    //实时累加未撞障碍物的时间。当时间达到 30s（+10 分）和 60s（+25 分）时，派发加分奖励与对应 Buff 飘字。
    private void CheckBarrierFreeMilestones()
    {
        CheckMilestone(ref is30sBuffTriggered, 30f, buff30Bonus, "buff30", SFXType.Survival30s, "[GameMechanicsManager] Survival buff30 triggered!");
        CheckMilestone(ref is60sBuffTriggered, 60f, buff60Bonus, "buff60", SFXType.Survival60s, "[GameMechanicsManager] Survival buff60 triggered!");
    }

    private void AddBonusScore(int bonus, string popupName)
    {
        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.AddScore(bonus, true); // true for isBonus!
        }
        if (PopupManager.Instance != null)
        {
            PopupManager.Instance.ShowPopup(popupName);
        }
    }

    public void OnPlayerCollidedWithItem(CollectibleItem item)
    {
        if (item == null) return;

        switch (item.itemType)
        {
            case ItemType.Coin:
                HandleCoinCollision(item);
                break;

            case ItemType.Lighting:
                HandleLightningCollision();
                break;

            case ItemType.Boost:
                // Boost does not interrupt consecutive coins
                // Standard boost speed handling is done in CollectibleItem using player.ApplySpeedBoost
                if (PopupManager.Instance != null && !string.IsNullOrEmpty(item.popupName))
                {
                    PopupManager.Instance.ShowPopup(item.popupName);
                }
                break;

            case ItemType.Barrier:
                HandleBarrierCollision(item);
                break;
        }
    }

    //金币连击系统，记录连续收集金币的次数，在达到 10、35、50 等里程碑时分发额外奖励分数（Bonus Score），
    //播放专属音效，并调取 PopupManager 飘字。一旦撞击障碍物（Barrier），连击次数重置为 0。
    private void HandleCoinCollision(CollectibleItem item)
    {
        consecutiveCoins++;
        
        if (logCoinStreakMilestones)
        {
            Debug.Log($"[Coin Collection] Collected coin. Current consecutive coin count: {consecutiveCoins}");
        }

        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.AddScore(item.scoreModifier, false);
        }

        if (PopupManager.Instance != null && !string.IsNullOrEmpty(item.popupName))
        {
            string pName = item.popupName;
            if (pName.Equals("coin", System.StringComparison.OrdinalIgnoreCase) && light2BuffTimer > 0f)
            {
                pName = "5sbuffx2";
            }
            PopupManager.Instance.ShowPopup(pName);
        }

        int bonus = 0;
        string popup = "";
        SFXType? sfx = null;

        if (consecutiveCoins == 10) { bonus = coin10Bonus; popup = "coin10"; sfx = SFXType.CoinStreak10; }
        else if (consecutiveCoins == 35) { bonus = coin35Bonus; popup = "coin35"; sfx = SFXType.CoinStreak35; }
        else if (consecutiveCoins == 50) { bonus = coin50BaseBonus; popup = "coin50"; sfx = SFXType.CoinStreak50; }
        else if (consecutiveCoins > 50 && (consecutiveCoins - 50) % 5 == 0) { bonus = coin50BaseBonus; popup = "coin50"; }

        if (bonus > 0)
        {
            if (logCoinStreakMilestones)
            {
                Debug.Log($"[Milestone Reached] {consecutiveCoins} consecutive coins! Triggering bonus (+{bonus} score) and showing popup {popup}.");
            }
            AddBonusScore(bonus, popup);
            if (sfx.HasValue) AudioManager.Instance?.PlaySFX(sfx.Value);
        }
    }

    private void HandleBarrierCollision(CollectibleItem item)
    {
        consecutiveCoins = 0; // Interrupt coin streak
        timeSinceLastBarrier = 0f; // Reset barrier-free survival timer
        is30sBuffTriggered = false;
        is60sBuffTriggered = false;

        Debug.Log("[GameMechanicsManager] Barrier collided! Resetted coin streak and barrier-free timer.");

        // Deduct score
        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.AddScore(item.scoreModifier);
        }

        // Apply slowdown to player
        var player = FindObjectOfType<SimplePlayerController>();
        if (player != null)
        {
            player.ApplySlowDown(player.barrierSlowdownDuration, player.barrierSlowdownMultiplier);
        }

        if (PopupManager.Instance != null && !string.IsNullOrEmpty(item.popupName))
        {
            PopupManager.Instance.ShowPopup(item.popupName);
        }
    }

    private void HandleLightningCollision()
    {
        // Lightning does not interrupt consecutive coins
        TriggerLightningEffect();
    }

    //吃下雷电道具后，根据概率配置，在以下 6 种事件中随机选取 1 种触发：
//    加分事件：直接获得额外加分（+5 或 +10 分）。
//双倍得分状态：在 3 秒或 5 秒内，将玩家收集金币和道具获得的正常积分乘以 2（通过

//ModifyScoreAmount
// 拦截实现）。
//前方生成金币(Effect 4)：在玩家前方约 4 米起，沿着路径 Spline（通过 PathFollower 获取）依次生成 10 个金币。生成前会调用 EnemySpawner 清理这段车道上的旧障碍物，防止金币与障碍物穿模重叠。
//磁铁真空吸附(Effect 6)：开启 3 秒的磁铁状态。
    private void TriggerLightningEffect()
    {
        float totalProb = 0f;
        foreach (var effect in lightningEffects)
        {
            totalProb += effect.probability;
        }

        if (totalProb <= 0f)
        {
            Debug.LogError("[GameMechanicsManager] Total probability of lightning effects is 0!");
            return;
        }

        float randomVal = Random.Range(0f, totalProb);
        float currentSum = 0f;

        foreach (var effect in lightningEffects)
        {
            currentSum += effect.probability;
            if (randomVal <= currentSum)
            {
                ApplyEffect(effect);
                break;
            }
        }
    }

    private EnemySpawner GetEnemySpawner()
    {
        if (EnemySpawner.Instance != null) return EnemySpawner.Instance;
        var spawner = FindObjectOfType<EnemySpawner>();
        if (spawner == null)
        {
            var go = new GameObject("EnemySpawner");
            spawner = go.AddComponent<EnemySpawner>();
        }
        return spawner;
    }

    public void ForceTriggerLight4()
    {
        LightningEffect testEffect = new LightningEffect("Effect 4: Spawn 10 Coins", 20f, 0, 0f, 10, 3f, "light4");
        ApplyEffect(testEffect);
    }

    private void ApplyEffect(LightningEffect effect)
    {
        if (logLightningEffects)
        {
            Debug.Log($"[Lightning Collision] Hit lightning item! Triggered random outcome: {effect.name} (Popup: {effect.popupName})");
        }

        AudioManager.Instance?.PlaySFX(SFXType.LightningEffect);

        // 1. Show Popup
        if (PopupManager.Instance != null && !string.IsNullOrEmpty(effect.popupName))
        {
            PopupManager.Instance.ShowPopup(effect.popupName);
        }

        // 2. Apply Score Bonus (immediate)
        if (effect.scoreBonus != 0)
        {
            if (logLightningEffects)
            {
                Debug.Log($"[Lightning Outcome] Applied immediate score bonus: {(effect.scoreBonus >= 0 ? "+" : "")}{effect.scoreBonus} pts.");
            }
            if (GameFlowController.Instance != null)
            {
                GameFlowController.Instance.AddScore(effect.scoreBonus, true);
            }
        }

        // 3. Apply Double Score Buff
        if (effect.doubleScoreDuration > 0f)
        {
            doubleScoreTimer = Mathf.Max(doubleScoreTimer, effect.doubleScoreDuration);
            AudioManager.Instance?.PlaySFX(SFXType.DoubleScore);
            if (logLightningEffects)
            {
                Debug.Log($"[Buff Activated] Double Score active! Any score added will be multiplied by 2 for the next {effect.doubleScoreDuration} seconds.");
            }
        }

        if (!string.IsNullOrEmpty(effect.popupName) && effect.popupName.Equals("light2", System.StringComparison.OrdinalIgnoreCase))
        {
            light2BuffTimer = 5f;
        }

        // 4. Spawn Coins near player
        bool isEffect4 = (!string.IsNullOrEmpty(effect.popupName) && effect.popupName.ToLower().Contains("light4")) ||
                         (!string.IsNullOrEmpty(effect.name) && (effect.name.ToLower().Contains("effect 4") || effect.name.ToLower().Contains("light4")));

        if (isEffect4 || effect.spawnCoinsCount > 0)
        {
            if (isEffect4)
            {
                int coinsToSpawn = 10;
                Debug.Log($"[GameMechanicsManager] Light4 Triggered! Spawning {coinsToSpawn} coins along path/track under 'props' in front of character.");

                var spawner = GetEnemySpawner();
                if (spawner != null)
                {
                    float spacing = 1.4f; // 10 个金币依次间隔 1.4 米，确保全量 10 个金币一次性清晰呈现
                    float startDist = 4f; // 从角色正前方 4 米出开始向远端延伸生成

                    // 优先检查场景中是否存在 PathFollower 路径组件
                    PathFollower pathFollower = null;
                    var playerController = FindObjectOfType<PlayerController>();
                    if (playerController != null && playerController.pathFollower != null && playerController.pathFollower.pathCreator != null)
                    {
                        pathFollower = playerController.pathFollower;
                    }
                    if (pathFollower == null)
                    {
                        pathFollower = FindObjectOfType<PathFollower>();
                    }

                    if (pathFollower != null && pathFollower.pathCreator != null && pathFollower.pathCreator.path != null)
                    {
                        // 1. 清理该段中间赛道上原有的旧障碍物，为 10 个金币腾出空间
                        float baseDist = pathFollower.distanceTravelled;
                        float zStart = baseDist + startDist;
                        float zEnd = baseDist + startDist + (coinsToSpawn - 1) * spacing;
                        spawner.ClearLanesForCoinLine(0f, zStart - 0.5f, zEnd + 0.5f, 1.5f);

                        // 2. 沿 PathCreator 路径 Spline 曲线在角色正前方中间赛道一次性生成 10 个金币（使用 Loop 指令防止终点截断重叠）
                        for (int i = 0; i < coinsToSpawn; i++)
                        {
                            float targetDist = baseDist + startDist + i * spacing;
                            Vector3 pathPos = pathFollower.pathCreator.path.GetPointAtDistance(targetDist, EndOfPathInstruction.Loop);
                            Vector3 coinWorldPos = pathPos + Vector3.up * 0.5f;
                            spawner.SpawnItemAtExactWorldPosition(ItemType.Coin, coinWorldPos, true);
                        }
                    }
                    else
                    {
                        // 直线场景兜底：在角色正前方 Z 轴方向一次性生成 10 个金币
                        Transform pTransform = GetPlayerTransform();
                        Vector3 playerPos = (pTransform != null) ? pTransform.position : Vector3.zero;

                        float zStart = playerPos.z + startDist;
                        float zEnd = playerPos.z + startDist + (coinsToSpawn - 1) * spacing;
                        spawner.ClearLanesForCoinLine(0f, zStart - 0.5f, zEnd + 0.5f, 1.5f);

                        for (int i = 0; i < coinsToSpawn; i++)
                        {
                            Vector3 coinWorldPos = new Vector3(0f, 0.5f, playerPos.z + startDist + i * spacing);
                            spawner.SpawnItemAtExactWorldPosition(ItemType.Coin, coinWorldPos, true);
                        }
                    }
                }
            }
            else
            {
                if (logLightningEffects)
                {
                    Debug.Log($"[Buff Activated] Spawning bonus coins in front of player.");
                }
                var spawner = GetEnemySpawner();
                if (spawner != null)
                {
                    Transform pTransform = GetPlayerTransform();
                    Vector3 playerPos = (pTransform != null) ? pTransform.position : Vector3.zero;

                    int currentLane = Random.Range(0, 3);
                    for (int i = 0; i < effect.spawnCoinsCount; i++)
                    {
                        if (Random.value < 0.4f)
                        {
                            currentLane = (currentLane + Random.Range(-1, 2) + 3) % 3;
                        }
                        float laneX = (currentLane - 1) * 2f;
                        Vector3 coinWorldPos = new Vector3(laneX, 0.5f, playerPos.z + 4f + i * 2f);
                        spawner.SpawnItemAtExactWorldPosition(ItemType.Coin, coinWorldPos, true);
                    }
                }
            }
        }

        // 5. Apply Vacuum Buff
        if (effect.vacuumDuration > 0f)
        {
            vacuumTimer = 3f;
            AudioManager.Instance?.PlaySFX(SFXType.Magnet);
            if (logLightningEffects)
            {
                Debug.Log($"[Buff Activated] Instant Coin Absorption active for 3 seconds!");
            }
        }
    }

    public Transform GetPlayerTransform()
    {
        if (playerTransform != null && playerTransform.gameObject.activeInHierarchy)
            return playerTransform;

        GameObject playerObj = GameObject.Find("player") ?? GameObject.Find("Player");
        if (playerObj != null && playerObj.activeInHierarchy)
            return playerTransform = playerObj.transform;

        var controller = FindObjectOfType<PlayerController>();
        if (controller != null && controller.gameObject.activeInHierarchy)
            return playerTransform = controller.PlayerMesh != null ? controller.PlayerMesh : controller.transform;

        var simpleController = FindObjectOfType<SimplePlayerController>();
        if (simpleController != null && simpleController.gameObject.activeInHierarchy)
            return playerTransform = simpleController.transform;

        var identity = FindObjectOfType<PlayerIdentity>();
        if (identity != null && identity.gameObject.activeInHierarchy)
            return playerTransform = identity.transform;

        return null;
    }

    //磁铁状态激活期间，每帧以玩家头部上方 0.8f 为中心，在 7 米半径内扫描未被收集的金币，
    //并使用 Vector3.MoveTowards 将其高速吸向玩家。当金币与玩家距离小于 1.4f 时，强制触发收集逻辑。
    private void ExecuteVacuumCoins()
    {
        Transform pTransform = GetPlayerTransform();
        if (pTransform == null) return;

        Vector3 targetPos = pTransform.position + Vector3.up * 0.8f;
        // 磁铁吸附范围设定在合理的近距离 (7 米)，吸附邻近车道金币
        float effectiveRadius = Mathf.Max(vacuumRadius, 7f);
        float radiusSqr = effectiveRadius * effectiveRadius;

        CollectibleItem[] collectibles = FindObjectsOfType<CollectibleItem>();
        foreach (var colItem in collectibles)
        {
            if (colItem == null || colItem.isCollected || colItem.itemType != ItemType.Coin) continue;

            Vector3 itemPos = colItem.transform.position;
            float distSqr = (itemPos - pTransform.position).sqrMagnitude;

            if (distSqr <= radiusSqr)
            {
                colItem.transform.position = Vector3.MoveTowards(colItem.transform.position, targetPos, 24f * Time.deltaTime);

                if (Vector3.Distance(colItem.transform.position, targetPos) < 1.4f)
                {
                    colItem.CheckAndCollect(pTransform.gameObject);
                }
            }
        }
    }

    public int ModifyScoreAmount(int originalAmount)
    {
        // Double positive points if the doubleScoreTimer is active
        if (doubleScoreTimer > 0f && originalAmount > 0)
        {
            return originalAmount * 2;
        }
        return originalAmount;
    }

    public bool IsDoubleScoreActive()
    {
        return doubleScoreTimer > 0f;
    }
}
