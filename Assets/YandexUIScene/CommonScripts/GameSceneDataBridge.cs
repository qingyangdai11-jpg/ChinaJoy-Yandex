using UnityEngine;

/// <summary>
/// GamePlay 场景与 OutdoorsScene 2 场景之间的独立参数传递桥梁脚本
/// 职责：仅用于两场景完全解耦的数据参数传递（如得分、倒计时状态、退出标志等）
/// </summary>
public class GameSceneDataBridge : MonoBehaviour
{
    public static GameSceneDataBridge Instance { get; private set; }

    [Header("传递参数 (Data Parameters)")]
    public int finalScore = 0;
    public float timeRemaining = 0f;
    public bool isGameFinishedNormal = false;
    public bool isPlayerManualExit = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 重置本局游戏传递参数
    /// </summary>
    public void ResetSessionData()
    {
        finalScore = 0;
        timeRemaining = 0f;
        isGameFinishedNormal = false;
        isPlayerManualExit = false;
    }

    /// <summary>
    /// 将从 OutdoorsScene 2 收集到的得分与数据传回给 GamePlay 的 GameManager 和 UI
    /// </summary>
    public void PassDataToGamePlayUI()
    {
        GameManager gm = FindObjectOfType<GameManager>();
        if (gm != null)
        {
            gm.Score = finalScore;
            gm.UpdateScoreUI();
            Debug.Log($"[GameSceneDataBridge] 参数传递成功！最终得分 {finalScore} 已传回 GamePlay UI。");
        }
    }
}
