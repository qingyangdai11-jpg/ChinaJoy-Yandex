using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class YandexGameOverLeaderboard : MonoBehaviour
{
    [System.Serializable]
    public class RankRowUI
    {
        public GameObject rowObject;
        public TextMeshProUGUI rankText;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI scoreText;
        public CanvasGroup canvasGroup;

        [HideInInspector] public Color defaultRankColor = Color.white;
        [HideInInspector] public Color defaultNameColor = Color.white;
        [HideInInspector] public Color defaultScoreColor = Color.white;
    }

    [Header("UI Configuration")]
    [Tooltip("Parent transform containing the 10 ranking child rows. If empty, will search locally.")]
    public Transform rankingParent;

    [Tooltip("Manually configure row references. If left empty, will auto-populate based on rankingParent child objects.")]
    public List<RankRowUI> rows = new List<RankRowUI>();

    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    public static YandexGameOverLeaderboard Instance { get; private set; }

    [System.Serializable]
    public class LeaderboardEntry
    {
        public string name;
        public string phone;
        public int score;
        public string finished_time;
    }

    [System.Serializable]
    public class CurrentPlayerEntry
    {
        public string name;
        public string phone;
        public int score;
        public int rank;
        public string finishedTime;
    }

    [System.Serializable]
    public class GameResultData
    {
        public List<LeaderboardEntry> leaderboard;
        public CurrentPlayerEntry currentPlayer;
    }

    [System.Serializable]
    public class GameResultMessage
    {
        public string type;
        public GameResultData data;
    }

    private void Awake()
    {
        Instance = this;

        if (rows == null || rows.Count == 0)
        {
            InitializeRows();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void InitializeRows()
    {
        if (rankingParent == null)
        {
            // 1. Try to find a GameObject named "Ranking (1)" or "Ranking" in local scope first
            rankingParent = transform.Find("Ranking (1)");
            if (rankingParent == null)
            {
                rankingParent = transform.Find("Ranking");
            }
            // 2. Try finding anywhere under this GameObject
            if (rankingParent == null)
            {
                rankingParent = transform.FindDeepChild("Ranking (1)");
            }
            if (rankingParent == null)
            {
                rankingParent = transform.FindDeepChild("Ranking");
            }
            // 3. Fallback: search anywhere in active scene hierarchy
            if (rankingParent == null)
            {
                var rankObj = GameObject.Find("Ranking (1)");
                if (rankObj == null)
                {
                    rankObj = GameObject.Find("Ranking");
                }
                if (rankObj != null)
                {
                    rankingParent = rankObj.transform;
                }
            }
        }

        if (rankingParent == null)
        {
            Debug.LogError("[GameOverLeaderboard] rankingParent is null and could not be auto-located!");
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

            // Extract Text (TMP) components in order
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

            // Save default colors
            if (row.rankText != null) row.defaultRankColor = row.rankText.color;
            if (row.nameText != null) row.defaultNameColor = row.nameText.color;
            if (row.scoreText != null) row.defaultScoreColor = row.scoreText.color;

            // Ensure CanvasGroup is attached
            row.canvasGroup = child.GetComponent<CanvasGroup>();
            if (row.canvasGroup == null)
            {
                row.canvasGroup = child.gameObject.AddComponent<CanvasGroup>();
            }

            rows.Add(row);
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[GameOverLeaderboard] Auto-initialized {rows.Count} rows.");
        }
    }

    /// <summary>
    /// Call this when the game_result websocket message is received.
    /// </summary>
    public void DisplayGameResult(string json)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[GameOverLeaderboard] Displaying game result...");
        }

        try
        {
            GameResultMessage response = JsonUtility.FromJson<GameResultMessage>(json);
            if (response != null && response.data != null)
            {
                List<LeaderboardEntry> leaderboard = response.data.leaderboard ?? new List<LeaderboardEntry>();
                CurrentPlayerEntry currentPlayer = response.data.currentPlayer;

                // Stop any active UI update routines
                StopAllCoroutines();
                StartCoroutine(UpdateLeaderboardRowsRoutine(leaderboard, currentPlayer));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameOverLeaderboard] Error parsing game result json: {ex.Message}");
        }
    }

    private IEnumerator UpdateLeaderboardRowsRoutine(List<LeaderboardEntry> leaderboard, CurrentPlayerEntry currentPlayer)
    {
        int maxRows = Mathf.Min(rows.Count, 10);

        // Pre-clear all rows to prevent lingering data
        foreach (var row in rows)
        {
            if (row.canvasGroup != null)
            {
                row.canvasGroup.alpha = 0f;
            }
        }

        // Sequential cascade fade-in downwards
        for (int i = 0; i < maxRows; i++)
        {
            LeaderboardEntry entry = i < leaderboard.Count ? leaderboard[i] : null;
            StartCoroutine(FadeAndUpdateRow(rows[i], i + 1, entry, currentPlayer));
            
            yield return new WaitForSecondsRealtime(0.08f);
        }
    }

    private IEnumerator FadeAndUpdateRow(RankRowUI row, int rank, LeaderboardEntry entry, CurrentPlayerEntry currentPlayer)
    {
        if (row.canvasGroup == null) yield break;

        row.canvasGroup.DOKill();
        row.canvasGroup.alpha = 0f;

        // Determine if this entry belongs to the current player
        bool isCurrentPlayer = false;
        if (currentPlayer != null && entry != null)
        {
            isCurrentPlayer = (rank == currentPlayer.rank) || 
                              (entry.name == currentPlayer.name && 
                               entry.phone == currentPlayer.phone && 
                               entry.score == currentPlayer.score);
        }

        // Apply colors
        Color highlightColor = new Color32(0xE1, 0xFA, 0x00, 0xFF); // Yellow: E1FA00
        
        if (row.rankText != null)
        {
            row.rankText.text = entry != null ? rank.ToString() : "";
            row.rankText.color = isCurrentPlayer ? highlightColor : row.defaultRankColor;
        }

        if (row.nameText != null)
        {
            row.nameText.text = entry != null ? entry.name : "";
            row.nameText.color = isCurrentPlayer ? highlightColor : row.defaultNameColor;
        }

        if (row.scoreText != null)
        {
            row.scoreText.text = entry != null ? entry.score.ToString() : "";
            row.scoreText.color = isCurrentPlayer ? highlightColor : row.defaultScoreColor;
        }

        // Fade in smoothly
        yield return row.canvasGroup.DOFade(1f, 0.3f).SetUpdate(true).WaitForCompletion();
    }
}

// Extension method helper to find deep children if needed
public static class TransformExtensions
{
    public static Transform FindDeepChild(this Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = child.FindDeepChild(name);
            if (result != null) return result;
        }
        return null;
    }
}
