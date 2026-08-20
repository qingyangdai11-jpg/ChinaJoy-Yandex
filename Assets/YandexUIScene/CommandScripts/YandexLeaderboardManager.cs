using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using DG.Tweening;

public class YandexLeaderboardManager : MonoBehaviour
{
    [System.Serializable]
    public class RankRowUI
    {
        public GameObject rowObject;
        public TextMeshProUGUI rankText;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI scoreText;
        public CanvasGroup canvasGroup;
    }

    [Header("UI Configuration")]
    [Tooltip("Parent transform containing the 10 ranking child rows. If empty, will search locally.")]
    public Transform rankingParent;

    [Tooltip("Manually configure row references. If left empty, will auto-populate based on rankingParent child objects.")]
    public List<RankRowUI> rows = new List<RankRowUI>();

    public static YandexLeaderboardManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [Tooltip("Interval in seconds between leaderboard refreshes.")]
    public float fetchInterval = 2.0f;

    private List<LeaderboardEntry> previousEntries = new List<LeaderboardEntry>();
    private Coroutine fetchRoutine;
    private Coroutine observerRoutine;
    private bool wasConnected = false;
    private bool isWaitingForSceneInit = false;

    [System.Serializable]
    public class LeaderboardEntry
    {
        public string name;
        public string phone;
        public int score;
        public string finished_time;
    }

    [System.Serializable]
    public class LeaderboardData
    {
        public List<LeaderboardEntry> leaderboard;
    }

    [System.Serializable]
    public class LeaderboardResponse
    {
        public int code;
        public string msg;
        public LeaderboardData data;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // A persistent instance already exists. Don't overwrite.
            // The duplicate GameObject will be destroyed by YandexWebSocketClient.Awake.
            Destroy(this);
            return;
        }
        Instance = this;
        if (rows == null || rows.Count == 0)
        {
            InitializeRows();
        }
        else
        {
            HideAndClearImmediately();
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        wasConnected = false;
        if (observerRoutine != null) StopCoroutine(observerRoutine);
        observerRoutine = StartCoroutine(ConnectionObserverRoutine());
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        if (observerRoutine != null)
        {
            StopCoroutine(observerRoutine);
            observerRoutine = null;
        }
        StopFetching();
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        InitializeRows();
        previousEntries.Clear(); // Clear cached entries to force waterfall cascade animation on scene load
        wasConnected = false; // Force immediate check & refresh on next observer check
        StartCoroutine(WaitAndResetObserver());
    }

    private IEnumerator WaitAndResetObserver()
    {
        isWaitingForSceneInit = true;
        yield return null; // Wait for the next frame to allow all OnSceneLoaded events to initialize
        isWaitingForSceneInit = false;
    }

    private IEnumerator ConnectionObserverRoutine()
    {
        while (true)
        {
            if (isWaitingForSceneInit)
            {
                yield return null;
                continue;
            }
            string currentMode = PlayerPrefs.GetString("GameMode", "ONLINE");
            if (GameFlowController.Instance != null && !string.IsNullOrEmpty(GameFlowController.Instance.ActiveGameMode))
            {
                currentMode = GameFlowController.Instance.ActiveGameMode;
            }

            if (currentMode == "OFFLINE")
            {
                if (wasConnected)
                {
                    wasConnected = false;
                    StopFetchingAndHide();
                }
                yield return new WaitForSecondsRealtime(0.5f);
                continue;
            }

            if (currentMode == "ONLINE")
            {
                // Ensure Ranking parent is active in ONLINE mode
                if (rankingParent != null && !rankingParent.gameObject.activeSelf)
                {
                    rankingParent.gameObject.SetActive(true);
                }

                if (!wasConnected)
                {
                    wasConnected = true;
                    if (enableDebugLogs)
                    {
                        Debug.Log("[Leaderboard] ONLINE mode active. Fetching leaderboard immediately!");
                    }

                    // Initialize server-side game session
                    if (YandexGameSessionInitializer.Instance != null)
                    {
                        YandexGameSessionInitializer.Instance.InitializeSession();
                    }

                    // Activate QR code
                    if (YandexQRCodeGenerator.Instance != null)
                    {
                        if (YandexQRCodeGenerator.Instance.targetRawImage != null)
                        {
                            YandexQRCodeGenerator.Instance.targetRawImage.gameObject.SetActive(true);
                        }
                        YandexQRCodeGenerator.Instance.StartQRGenerator();
                    }

                    StartFetching();
                }
            }
            else
            {
                if (wasConnected)
                {
                    wasConnected = false;

                    // Deactivate QR code immediately (no fade)
                    if (YandexQRCodeGenerator.Instance != null && YandexQRCodeGenerator.Instance.targetRawImage != null)
                    {
                        YandexQRCodeGenerator.Instance.targetRawImage.gameObject.SetActive(false);
                    }

                    StopFetchingAndHide();
                }
            }

            yield return new WaitForSecondsRealtime(0.2f);
        }
    }

    private void InitializeRows()
    {
        if (rankingParent == null)
        {
            // Try to find a GameObject named "Ranking" in local scope or hierarchy
            rankingParent = transform.Find("Ranking");
            if (rankingParent == null)
            {
                // Fallback: search anywhere in hierarchy if this object is named Ranking or has a child named Ranking
                var rankObj = GameObject.Find("Ranking");
                if (rankObj != null)
                {
                    rankingParent = rankObj.transform;
                }
            }
        }

        if (rankingParent == null)
        {
            Debug.LogError("[Leaderboard] rankingParent is null and could not be auto-located! Please assign it in the Inspector.");
            return;
        }

        rows = new List<RankRowUI>();
        int childCount = rankingParent.childCount;
        int limit = Mathf.Min(childCount, 10);

        for (int i = 0; i < limit; i++)
        {
            Transform child = rankingParent.GetChild(i);
            RankRowUI row = new RankRowUI();
            row.rowObject = child.gameObject;

            // Extract Text (TMP) components in order: rank, name, score
            TextMeshProUGUI[] tmps = child.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (tmps.Length >= 3)
            {
                row.rankText = tmps[0];
                row.nameText = tmps[1];
                row.scoreText = tmps[2];
            }
            else
            {
                row.rankText = child.Find("rank")?.GetComponent<TextMeshProUGUI>();
                row.nameText = child.Find("name")?.GetComponent<TextMeshProUGUI>();
                row.scoreText = child.Find("score")?.GetComponent<TextMeshProUGUI>();
            }

            // Ensure CanvasGroup is attached for fading
            row.canvasGroup = child.GetComponent<CanvasGroup>();
            if (row.canvasGroup == null)
            {
                row.canvasGroup = child.gameObject.AddComponent<CanvasGroup>();
            }

            rows.Add(row);
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[Leaderboard] Auto-initialized {rows.Count} ranking rows.");
        }

        HideAndClearImmediately();
    }

    public void HideAndClearImmediately()
    {
        previousEntries.Clear();
        foreach (var row in rows)
        {
            if (row.canvasGroup != null)
            {
                row.canvasGroup.DOKill();
                row.canvasGroup.alpha = 0f;
            }
            
            // Clear all text components in this row object to be absolutely sure
            if (row.rowObject != null)
            {
                var tmps = row.rowObject.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var tmp in tmps)
                {
                    if (tmp != null)
                    {
                        tmp.text = "";
                    }
                }
            }
            else
            {
                if (row.rankText != null) row.rankText.text = "";
                if (row.nameText != null) row.nameText.text = "";
                if (row.scoreText != null) row.scoreText.text = "";
            }
        }

        // Hide the entire ranking parent
        if (rankingParent != null)
        {
            rankingParent.gameObject.SetActive(false);
        }

        // Deactivate QR code immediately (no fade)
        if (YandexQRCodeGenerator.Instance != null && YandexQRCodeGenerator.Instance.targetRawImage != null)
        {
            YandexQRCodeGenerator.Instance.targetRawImage.gameObject.SetActive(false);
        }
    }

    public void StartFetching()
    {
        StopFetching();
        fetchRoutine = StartCoroutine(FetchLeaderboardRoutine());
    }

    public void StopFetching()
    {
        if (fetchRoutine != null)
        {
            StopCoroutine(fetchRoutine);
            fetchRoutine = null;
        }
    }

    public void TriggerFetch()
    {
        string currentMode = GameFlowController.Instance != null ? GameFlowController.Instance.ActiveGameMode : "OFFLINE";
        if (currentMode != "ONLINE")
        {
            StopFetchingAndHide();
            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log("[Leaderboard] TriggerFetch: Starting leaderboard fetching loop.");
        }
        StartFetching();
    }

    public void TriggerWaterfallRefresh()
    {
        previousEntries.Clear();
        if (rows != null)
        {
            foreach (var row in rows)
            {
                if (row.canvasGroup != null)
                {
                    row.canvasGroup.DOKill();
                    row.canvasGroup.alpha = 0f;
                }
            }
        }

        if (rankingParent != null && !rankingParent.gameObject.activeSelf)
        {
            rankingParent.gameObject.SetActive(true);
        }
        StartFetching();
    }

    public void StopFetchingAndHide()
    {
        StopFetching();
        HideAndClearImmediately();
    }

    private IEnumerator FetchLeaderboardRoutine()
    {
        while (true)
        {
            yield return StartCoroutine(GetLeaderboardData());
            yield return new WaitForSecondsRealtime(fetchInterval);
        }
    }

    private void ClearLeaderboard()
    {
        if (previousEntries.Count > 0)
        {
            previousEntries.Clear();
            StartCoroutine(UpdateLeaderboardRowsRoutine(new List<LeaderboardEntry>()));
        }
        else
        {
            // Ensure UI is completely cleared and hidden even if previousEntries was already empty
            foreach (var row in rows)
            {
                if (row.canvasGroup != null && row.canvasGroup.alpha > 0f)
                {
                    row.canvasGroup.DOKill();
                    row.canvasGroup.alpha = 0f;
                    if (row.rankText != null) row.rankText.text = "";
                    if (row.nameText != null) row.nameText.text = "";
                    if (row.scoreText != null) row.scoreText.text = "";
                }
            }
        }
    }

    private IEnumerator GetLeaderboardData()
    {
        using (UnityWebRequest request = UnityWebRequest.Get("https://yandexa.bbtech.cc/api/leaderboard"))
        {
            request.timeout = 2; // Short timeout of 2 seconds
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                if (enableDebugLogs)
                {
                    Debug.Log($"[Leaderboard] Fetched raw leaderboard data: {json}");
                }
                ParseAndUpdateLeaderboard(json);
            }
            else
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[Leaderboard] Get leaderboard failed: {request.error}");
                }
            }
        }
    }

    private void ParseAndUpdateLeaderboard(string json)
    {
        try
        {
            LeaderboardResponse response = JsonUtility.FromJson<LeaderboardResponse>(json);
            if (response != null && response.code == 200 && response.data != null && response.data.leaderboard != null)
            {
                List<LeaderboardEntry> newEntries = response.data.leaderboard;
                if (enableDebugLogs)
                {
                    Debug.Log($"[Leaderboard] Parsed {newEntries.Count} entries successfully.");
                    for (int i = 0; i < newEntries.Count; i++)
                    {
                        var entry = newEntries[i];
                        //Debug.Log($"[Leaderboard Entry #{i + 1}] Name: {entry.name}, Phone: {entry.phone}, Score: {entry.score}, FinishedTime: {entry.finished_time}");
                    }
                }
                StartCoroutine(UpdateLeaderboardRowsRoutine(newEntries));
            }
            else
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[Leaderboard] Response invalid or code != 200. JSON: {json}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Leaderboard] Error parsing response: {ex.Message}");
        }
    }

    private bool IsEntryDifferent(LeaderboardEntry oldEntry, LeaderboardEntry newEntry)
    {
        if (oldEntry == null && newEntry == null) return false;
        if (oldEntry == null || newEntry == null) return true;
        return oldEntry.name != newEntry.name || 
               oldEntry.phone != newEntry.phone || 
               oldEntry.score != newEntry.score;
    }

    private IEnumerator UpdateLeaderboardRowsRoutine(List<LeaderboardEntry> newEntries)
    {
        int maxRows = Mathf.Min(rows.Count, 10);
        List<int> changedIndices = new List<int>();

        for (int i = 0; i < maxRows; i++)
        {
            LeaderboardEntry oldEntry = i < previousEntries.Count ? previousEntries[i] : null;
            LeaderboardEntry newEntry = i < newEntries.Count ? newEntries[i] : null;

            if (IsEntryDifferent(oldEntry, newEntry))
            {
                changedIndices.Add(i);
            }
        }

        // Cache the new entries list
        previousEntries = new List<LeaderboardEntry>(newEntries);

        if (changedIndices.Count == 0) yield break;

        // Cascade/Sequential fade animations for changed rows downwards
        for (int i = 0; i < changedIndices.Count; i++)
        {
            int index = changedIndices[i];
            LeaderboardEntry newEntry = index < newEntries.Count ? newEntries[index] : null;

            StartCoroutine(FadeAndUpdateRow(rows[index], index + 1, newEntry));
            
            // Small cascade delay between row refreshes to create the dynamic flowing/sorting visual effect
            yield return new WaitForSecondsRealtime(0.08f);
        }
    }

    private IEnumerator FadeAndUpdateRow(RankRowUI row, int rank, LeaderboardEntry entry)
    {
        if (row.canvasGroup == null) yield break;

        row.canvasGroup.DOKill();

        // 1. Fade out quickly
        yield return row.canvasGroup.DOFade(0f, 0.15f).SetUpdate(true).WaitForCompletion();

        // 2. Set texts while invisible
        if (entry != null)
        {
            if (row.rankText != null) row.rankText.text = rank.ToString();
            if (row.nameText != null) row.nameText.text = entry.name;
            if (row.scoreText != null) row.scoreText.text = entry.score.ToString();
        }
        else
        {
            if (row.rankText != null) row.rankText.text = "";
            if (row.nameText != null) row.nameText.text = "";
            if (row.scoreText != null) row.scoreText.text = "";
        }

        // 3. Fade in smoothly
        yield return row.canvasGroup.DOFade(1f, 0.25f).SetUpdate(true).WaitForCompletion();
    }
}
