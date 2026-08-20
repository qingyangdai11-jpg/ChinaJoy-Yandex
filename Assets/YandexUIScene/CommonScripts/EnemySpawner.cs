using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance { get; private set; }

    [Header("Spawner Settings")]
    //public int maxEnemyCount = 3;
    public float spawnRadiusMin = 2f;
    public float spawnRadiusMax = 6f;
    [Tooltip("Minimum distance between spawned items to prevent overlap")]
    public float minDistanceBetweenItems = 3f;

    [Header("Item Prefabs (Optional, loaded from Resources if null)")]
    public GameObject coinPrefab;
    public GameObject lightningPrefab;
    public GameObject boostPrefab;
    public List<GameObject> barrierPrefabs = new List<GameObject>();

    [Header("Spawn Bag Probabilities (Per Bag Spawning)")]
    public int spawnBagSize = 20;
    public int lightningCountInBag = 1;
    public int boostCountInBag = 1;
    public int barrierCountInBag = 2;

    private List<GameObject> activeEnemies = new List<GameObject>();
    private Transform playerTransform;
    private bool isGameplayActive = false;

    private GameObject[] itemPrefabs;
    private List<ItemType> spawnBag = new List<ItemType>();
    private int currentBagIndex = 0;
    private float lastSpawnZ = 0f;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Ensure PopupManager exists in the scene
        if (PopupManager.Instance == null)
        {
            gameObject.AddComponent<PopupManager>();
        }

        // Find player
        UpdatePlayerTransform();

        // Load item prefabs from Resources
        LoadPrefabs();
    }

    public Transform UpdatePlayerTransform()
    {
        if (playerTransform != null && playerTransform.gameObject.activeInHierarchy)
            return playerTransform;

        var playerObj = GameObject.Find("player");
        if (playerObj == null) playerObj = GameObject.Find("Player");
        if (playerObj != null && playerObj.activeInHierarchy)
        {
            playerTransform = playerObj.transform;
            return playerTransform;
        }

        var controller = FindObjectOfType<PlayerController>();
        if (controller != null && controller.gameObject.activeInHierarchy)
        {
            playerTransform = controller.PlayerMesh != null ? controller.PlayerMesh : controller.transform;
            return playerTransform;
        }

        var simpleController = FindObjectOfType<SimplePlayerController>();
        if (simpleController != null && simpleController.gameObject.activeInHierarchy)
        {
            playerTransform = simpleController.transform;
            return playerTransform;
        }

        var identity = FindObjectOfType<PlayerIdentity>();
        if (identity != null && identity.gameObject.activeInHierarchy)
        {
            playerTransform = identity.transform;
            return playerTransform;
        }

        return null;
    }

    private GameObject indicatorPrefab;
    private Material indicatorMaterial;

    private void EnsureIndicatorAssets()
    {
        if (indicatorPrefab == null) indicatorPrefab = Resources.Load<GameObject>("Prefabs/SM_Obstacle_Indicator");
        if (indicatorPrefab == null) indicatorPrefab = Resources.Load<GameObject>("SM_Obstacle_Indicator");
#if UNITY_EDITOR
        if (indicatorPrefab == null) indicatorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Models/SM_Obstacle_Indicator.fbx");
#endif

        if (indicatorMaterial == null) indicatorMaterial = Resources.Load<Material>("Prefabs/MTL_Obstacle_Glow");
        if (indicatorMaterial == null) indicatorMaterial = Resources.Load<Material>("MTL_Obstacle_Glow");
#if UNITY_EDITOR
        if (indicatorMaterial == null) indicatorMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Project/Materials/Shadergraph/MTL_Obstacle_Glow.mat");
#endif
    }

    private void LoadPrefabs()
    {
        // 1. Prioritize loading from Assets/Project/Prefabs
#if UNITY_EDITOR
        if (coinPrefab == null) coinPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/PF_Pickup_Coin.prefab");
        if (lightningPrefab == null) lightningPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/PF_Pickup_Lightning.prefab");
        if (boostPrefab == null) boostPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/PF_Pickup_Boost.prefab");
#endif

        // Resources fallbacks
        if (coinPrefab == null) coinPrefab = Resources.Load<GameObject>("Prefabs/PF_Pickup_Coin");
        if (coinPrefab == null) coinPrefab = Resources.Load<GameObject>("PF_Pickup_Coin");
        if (coinPrefab == null) coinPrefab = Resources.Load<GameObject>("Prefabs/coin");
        if (coinPrefab == null) coinPrefab = Resources.Load<GameObject>("coin");

        if (lightningPrefab == null) lightningPrefab = Resources.Load<GameObject>("Prefabs/PF_Pickup_Lightning");
        if (lightningPrefab == null) lightningPrefab = Resources.Load<GameObject>("PF_Pickup_Lightning");
        if (lightningPrefab == null) lightningPrefab = Resources.Load<GameObject>("Prefabs/lighting");
        if (lightningPrefab == null) lightningPrefab = Resources.Load<GameObject>("lighting");

        if (boostPrefab == null) boostPrefab = Resources.Load<GameObject>("Prefabs/PF_Pickup_Boost");
        if (boostPrefab == null) boostPrefab = Resources.Load<GameObject>("PF_Pickup_Boost");
        if (boostPrefab == null) boostPrefab = Resources.Load<GameObject>("Prefabs/boost");
        if (boostPrefab == null) boostPrefab = Resources.Load<GameObject>("boost");

        if (barrierPrefabs == null || barrierPrefabs.Count == 0)
        {
            barrierPrefabs = new List<GameObject>();
#if UNITY_EDITOR
            string[] projectBarrierPaths = new string[] {
                "Assets/Project/Prefabs/PF_Obstacle_Holocube.prefab",
                "Assets/Project/Prefabs/PF_Obstacle_Tram.prefab",
                "Assets/Project/Prefabs/PF_Obstacle_Fence.prefab",
                "Assets/Project/Prefabs/PF_Obstacle_Car.prefab"
            };
            foreach (var path in projectBarrierPaths)
            {
                GameObject p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (p != null) barrierPrefabs.Add(p);
            }
#endif
            if (barrierPrefabs.Count == 0)
            {
                string[] resBarrierNames = new string[] {
                    "PF_Obstacle_Holocube", "PF_Obstacle_Tram", "PF_Obstacle_Fence", "PF_Obstacle_Car",
                    "barrier1", "barrier2", "barrier3", "barrier4"
                };
                foreach (var name in resBarrierNames)
                {
                    GameObject p = Resources.Load<GameObject>($"Prefabs/{name}");
                    if (p == null) p = Resources.Load<GameObject>(name);
                    if (p != null) barrierPrefabs.Add(p);
                }
            }
        }

        // Keep itemPrefabs populated for backward compatibility
        List<GameObject> loadedList = new List<GameObject>();
        if (coinPrefab != null) loadedList.Add(coinPrefab);
        if (lightningPrefab != null) loadedList.Add(lightningPrefab);
        if (boostPrefab != null) loadedList.Add(boostPrefab);
        foreach (var p in barrierPrefabs)
        {
            if (p != null) loadedList.Add(p);
        }
        itemPrefabs = loadedList.ToArray();

        EnsureIndicatorAssets();
    }

    private void GenerateNewBag()
    {
        spawnBag.Clear();
        currentBagIndex = 0;

        for (int i = 0; i < lightningCountInBag; i++) spawnBag.Add(ItemType.Lighting);
        for (int i = 0; i < boostCountInBag; i++) spawnBag.Add(ItemType.Boost);
        for (int i = 0; i < barrierCountInBag; i++) spawnBag.Add(ItemType.Barrier);

        int coinCount = spawnBagSize - lightningCountInBag - boostCountInBag - barrierCountInBag;
        if (coinCount < 0) coinCount = 0;
        for (int i = 0; i < coinCount; i++) spawnBag.Add(ItemType.Coin);

        // Fisher-Yates Shuffle
        for (int i = 0; i < spawnBag.Count; i++)
        {
            ItemType temp = spawnBag[i];
            int randomIndex = Random.Range(i, spawnBag.Count);
            spawnBag[i] = spawnBag[randomIndex];
            spawnBag[randomIndex] = temp;
        }
    }

    private ItemType GetNextSpawningType()
    {
        if (spawnBag == null || spawnBag.Count == 0 || currentBagIndex >= spawnBag.Count)
        {
            GenerateNewBag();
        }
        ItemType type = spawnBag[currentBagIndex];
        currentBagIndex++;
        return type;
    }

    //[Header("Timer Spawner Settings (Obsolete, now using distance-based spawner)")]
    //public float spawnInterval = 1.5f;
    //private float spawnTimer = 0f;

    private void Update()
    {
        if (GameFlowController.Instance == null) return;

        bool isCurrentlyPlaying = (GameFlowController.Instance.CurrentState == GameFlowController.GameState.Gameplay);

        if (isCurrentlyPlaying)
        {
            if (!isGameplayActive)
            {
                isGameplayActive = true;
                UpdatePlayerTransform();
                ClearAllEnemies();
            }

            // 任何时候都不自动随机生成其他道具/障碍物！只在触发 light4 时才生成 coin
            if (playerTransform != null)
            {
                float playerZ = playerTransform.position.z;

                // Clean up uncollected items left far behind the player (e.g. 10 meters behind player)
                for (int i = activeEnemies.Count - 1; i >= 0; i--)
                {
                    GameObject item = activeEnemies[i];
                    if (item != null)
                    {
                        if (item.transform.position.z < playerZ - 10f)
                        {
                            activeEnemies.RemoveAt(i);
                            item.transform.DOKill();
                            Destroy(item);
                        }
                    }
                    else
                    {
                        activeEnemies.RemoveAt(i);
                    }
                }
            }
        }
        else if (isGameplayActive)
        {
            // Left gameplay! Clean up.
            isGameplayActive = false;
            ClearAllEnemies();
        }
    }

    public void SpawnEnemyNearPlayer()
    {
        // For backwards compatibility: spawn at the current frontier and advance it
        SpawnEnemyAtZ(lastSpawnZ);
        lastSpawnZ += Random.Range(minDistanceBetweenItems, minDistanceBetweenItems + 2f);
    }

    public void SpawnEnemyAtZ(float zPosition)
    {
        LoadPrefabs(); // Ensure prefabs are loaded

        ItemType nextType = GetNextSpawningType();
        GameObject prefab = null;

        switch (nextType)
        {
            case ItemType.Coin:
                prefab = coinPrefab;
                break;
            case ItemType.Lighting:
                prefab = lightningPrefab;
                break;
            case ItemType.Boost:
                prefab = boostPrefab;
                break;
            case ItemType.Barrier:
                if (barrierPrefabs != null && barrierPrefabs.Count > 0)
                {
                    prefab = barrierPrefabs[Random.Range(0, barrierPrefabs.Count)];
                }
                break;
        }

        if (prefab == null)
        {
            Debug.LogError($"[EnemySpawner] Prefab for item type {nextType} is null!");
            return;
        }

        // Randomly select one of the three lanes: Left (-2), Middle (0), or Right (2)
        float laneX = 0f;
        int laneIndex = Random.Range(0, 3);

        // 避免 barrier3 和 barrier4 同时出现在同一个赛道上
        if (prefab != null && (prefab.name == "barrier3" || prefab.name == "barrier4"))
        {
            List<int> availableLanes = new List<int> { 0, 1, 2 };
            foreach (var active in activeEnemies)
            {
                if (active != null)
                {
                    MovingBarrier mb = active.GetComponent<MovingBarrier>();
                    if (mb != null)
                    {
                        float x = active.transform.position.x;
                        int activeLane = 1; // 默认中间
                        if (x < -1f) activeLane = 0; // 左
                        else if (x > 1f) activeLane = 2; // 右
                        availableLanes.Remove(activeLane);
                    }
                }
            }
            if (availableLanes.Count > 0)
            {
                laneIndex = availableLanes[Random.Range(0, availableLanes.Count)];
            }
        }

        if (laneIndex == 0) laneX = -2f;
        else if (laneIndex == 1) laneX = 0f;
        else laneX = 2f;

        Vector3 spawnPos = new Vector3(laneX, 0.5f, zPosition);
        SpawnItem(prefab, spawnPos, nextType, false);
    }

    private Vector3 CalculateSpawnPosition()
    {
        if (playerTransform == null)
        {
            var player = FindObjectOfType<PlayerIdentity>();
            if (player != null) playerTransform = player.transform;
        }

        float playerZ = 0f;
        if (playerTransform != null)
        {
            playerZ = playerTransform.position.z;
        }

        Vector3 spawnPos = Vector3.zero;
        int maxAttempts = 15;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // Randomly select one of the three lanes: Left (-2), Middle (0), or Right (2)
            float laneX = 0f;
            int laneIndex = Random.Range(0, 3);
            if (laneIndex == 0) laneX = -2f;
            else if (laneIndex == 1) laneX = 0f;
            else laneX = 2f;

            // Spawn at a random distance in front of the player
            float spawnZ = playerZ + Random.Range(spawnRadiusMin, spawnRadiusMax);
            spawnPos = new Vector3(laneX, 0.5f, spawnZ);

            // Check if this position is too close to any active items
            bool tooClose = false;
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null)
                {
                    if (Vector3.Distance(spawnPos, enemy.transform.position) < minDistanceBetweenItems)
                    {
                        tooClose = true;
                        break;
                    }
                }
            }

            if (!tooClose)
            {
                break;
            }
        }

        return spawnPos;
    }

    private void SpawnItem(GameObject prefab, Vector3 spawnPos, ItemType type, bool isBonus)
    {
        GameObject itemObj = Instantiate(prefab, spawnPos, Quaternion.identity);
        itemObj.name = prefab.name;

        // Parent under "props" in Hierarchy
        GameObject propsRoot = GameObject.Find("props");
        if (propsRoot == null)
        {
            propsRoot = new GameObject("props");
        }
        itemObj.transform.SetParent(propsRoot.transform);

        // Set full scale immediately to ensure item is visible
        itemObj.transform.localScale = Vector3.one;

        // Add / Configure Rigidbody
        Rigidbody rb = itemObj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = itemObj.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        // Add / Configure CollectibleItem component
        CollectibleItem itemComponent = itemObj.GetComponent<CollectibleItem>();
        if (itemComponent == null)
        {
            itemComponent = itemObj.AddComponent<CollectibleItem>();
        }
        ConfigureItem(itemComponent, itemObj.name);
        itemComponent.isBonusSpawn = isBonus;

        activeEnemies.Add(itemObj);
    }

    // Helper for lightning effect 4 to spawn 15 coins
    public void SpawnSpecificItemNearPlayer(ItemType type, bool isBonus)
    {
        LoadPrefabs();
        GameObject prefab = null;
        switch (type)
        {
            case ItemType.Coin:
                prefab = coinPrefab;
                break;
            case ItemType.Lighting:
                prefab = lightningPrefab;
                break;
            case ItemType.Boost:
                prefab = boostPrefab;
                break;
        }

        if (prefab == null) return;

        Vector3 spawnPos = CalculateSpawnPosition();
        SpawnItem(prefab, spawnPos, type, isBonus);
    }

    public void ClearItemsAtPosition(Vector3 position, float radius)
    {
        float radiusSqr = radius * radius;
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            GameObject item = activeEnemies[i];
            if (item != null)
            {
                Vector3 diff = item.transform.position - position;
                diff.y = 0f; // 忽略 Y 轴高度差
                if (diff.sqrMagnitude <= radiusSqr)
                {
                    activeEnemies.RemoveAt(i);
                    item.transform.DOKill();
                    Destroy(item);
                    Debug.Log($"[EnemySpawner] 清理了位于 {item.transform.position} 的原有道具 {item.name}，为新道具腾出空间。");
                }
            }
        }
    }

    public void ClearLanesForCoinLine(float centerLineX, float zStart, float zEnd, float laneWidth = 1.5f)
    {
        float minZ = Mathf.Min(zStart, zEnd);
        float maxZ = Mathf.Max(zStart, zEnd);

        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            GameObject item = activeEnemies[i];
            if (item != null)
            {
                float itemX = item.transform.position.x;
                float itemZ = item.transform.position.z;

                if (Mathf.Abs(itemX - centerLineX) <= laneWidth && itemZ >= minZ - 1.5f && itemZ <= maxZ + 1.5f)
                {
                    activeEnemies.RemoveAt(i);
                    item.transform.DOKill();
                    Destroy(item);
                }
            }
        }

        CollectibleItem[] collectibles = FindObjectsOfType<CollectibleItem>();
        foreach (var col in collectibles)
        {
            if (col == null) continue;
            Vector3 pos = col.transform.position;
            if (Mathf.Abs(pos.x - centerLineX) <= laneWidth && pos.z >= minZ - 1.5f && pos.z <= maxZ + 1.5f)
            {
                col.transform.DOKill();
                Destroy(col.gameObject);
            }
        }

        GameObject propsRoot = GameObject.Find("props");
        if (propsRoot != null)
        {
            for (int i = propsRoot.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = propsRoot.transform.GetChild(i);
                if (child == null) continue;
                Vector3 pos = child.position;
                if (Mathf.Abs(pos.x - centerLineX) <= laneWidth && pos.z >= minZ - 1.5f && pos.z <= maxZ + 1.5f)
                {
                    child.DOKill();
                    Destroy(child.gameObject);
                }
            }
        }
    }

    public void SpawnItemAtCoordinates(ItemType type, float x, float relativeZ, bool isBonus, bool clearExisting = false)
    {
        LoadPrefabs();
        GameObject prefab = null;
        switch (type)
        {
            case ItemType.Coin:
                prefab = coinPrefab;
                break;
            case ItemType.Lighting:
                prefab = lightningPrefab;
                break;
            case ItemType.Boost:
                prefab = boostPrefab;
                break;
        }

        if (prefab == null) return;

        float playerZ = 0f;
        Transform pTransform = UpdatePlayerTransform();
        if (pTransform != null)
        {
            playerZ = pTransform.position.z;
        }

        Vector3 spawnPos = new Vector3(x, 0.5f, playerZ + relativeZ);

        if (clearExisting)
        {
            ClearItemsAtPosition(spawnPos, 1.2f);
        }

        SpawnItem(prefab, spawnPos, type, isBonus);
    }

    public void SpawnItemAtExactWorldPosition(ItemType type, Vector3 worldPos, bool isBonus)
    {
        LoadPrefabs();
        GameObject prefab = null;
        switch (type)
        {
            case ItemType.Coin:
                prefab = coinPrefab;
                break;
            case ItemType.Lighting:
                prefab = lightningPrefab;
                break;
            case ItemType.Boost:
                prefab = boostPrefab;
                break;
            case ItemType.Barrier:
                if (barrierPrefabs != null && barrierPrefabs.Count > 0)
                {
                    prefab = barrierPrefabs[Random.Range(0, barrierPrefabs.Count)];
                }
                break;
        }

        if (prefab == null) return;

        SpawnItem(prefab, worldPos, type, isBonus);
    }

    public List<GameObject> GetActiveItems()
    {
        return activeEnemies;
    }

    private void ConfigureItem(CollectibleItem item, string prefabName)
    {
        string lowerName = prefabName.ToLower();
        if (lowerName.Contains("coin"))
        {
            item.Initialize(ItemType.Coin, 1, 0f, "coin");
            item.gameObject.tag = "Coin";
        }
        else if (lowerName.Contains("lighting") || lowerName.Contains("lightning"))
        {
            item.Initialize(ItemType.Lighting, 5, 0f, "lighting");
            item.gameObject.tag = "Lighting";
        }
        else if (lowerName.Contains("boost"))
        {
            item.Initialize(ItemType.Boost, 0, 2f, "boost");
            item.gameObject.tag = "Boost";
        }
        else if (lowerName.Contains("barrier"))
        {
            item.Initialize(ItemType.Barrier, -2, 0f, "barrier");
            item.gameObject.tag = "Barrier";
        }
    }

    public void OnItemCollected(GameObject item, ItemType itemType, string popupName)
    {
        if (activeEnemies.Contains(item))
        {
            activeEnemies.Remove(item);
        }

        // With distance-based spawning, replacements are handled automatically in Update
    }

    // Keep compatibility in case of references in other scripts
    public void OnEnemyDestroyed(GameObject enemy)
    {
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
        }

        // With distance-based spawning, replacements are handled automatically in Update
    }

    private void ClearAllEnemies()
    {
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null)
            {
                enemy.transform.DOKill();
                Destroy(enemy);
            }
        }
        activeEnemies.Clear();
    }
}