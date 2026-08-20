using UnityEngine;
using TMPro;
using System.Text.RegularExpressions;

/// <summary>
/// ScoreBridge 脚本
/// 作用：
/// 1. 通道一：将源文本中的数字参数提取并同步到目标文本（如 text_score -> panel_4 结算文本），同时实时同步给 GameFlowController.Instance.gameScore，以便排行榜上传正确的得分。
/// 2. 通道二：将 panel_4 结算文本的分数提取并同步到 panel_5 的分数文本（如在线的 Text (TMP)-score 或离线 score 文本）。
/// 完全通过 Inspector 面板拖拽绑定组件，无任何运行时模糊路径查找，性能最优且绝对稳定。
/// </summary>
public class ScoreBridge : MonoBehaviour
{
    [Header("Connection 1 (Default: text_score -> panel_4 text-time out)")]
    [Tooltip("通道一源文本（TextMeshPro）")]
    public TMP_Text sourceTMPComponent;
    [Tooltip("通道一目标文本（TextMeshPro）")]
    public TMP_Text destTMPComponent;

    [Header("Connection 2 (New: panel_4 text-time out -> panel_5 score fields)")]
    [Tooltip("通道二源文本（TextMeshPro）")]
    public TMP_Text sourceTMPComponent2;
    [Tooltip("通道二目标文本（TextMeshPro）")]
    public TMP_Text destTMPComponent2;

    [Header("Connection 2 Multiple Targets (Optional)")]
    [Tooltip("如果通道二有多个目标文本（如在线排名和离线得分两个框均需同步），可将它们直接拖入此处")]
    public TMP_Text[] destTMPComponents2;

    private void Update()
    {
        // 1. 同步通道一，并将干净的数字分数同步写入全局单例 gameScore
        string val1 = SyncText(sourceTMPComponent, destTMPComponent);
        if (val1 != null && GameFlowController.Instance != null)
        {
            if (int.TryParse(val1, out int score))
            {
                GameFlowController.Instance.gameScore = Mathf.Max(0, score);
            }
        }

        // 2. 同步通道二，将分数广播给排行榜面板下的各个分数显示框
        if (sourceTMPComponent2 != null && (destTMPComponent2 != null || (destTMPComponents2 != null && destTMPComponents2.Length > 0)))
        {
            string val2 = SyncText(sourceTMPComponent2, destTMPComponent2);
            if (val2 != null && destTMPComponents2 != null)
            {
                foreach (var dest in destTMPComponents2)
                {
                    if (dest != null && dest.text != val2) dest.text = val2;
                }
            }
        }
    }

    /// <summary>
    /// 核心提取与同步方法：提取源文本中的数字并写入目标文本
    /// </summary>
    private string SyncText(TMP_Text src, TMP_Text dest)
    {
        if (src == null) return null;

        string raw = src.text;
        // 使用正则过滤掉所有非数字字符
        string clean = Regex.Replace(raw, @"[^\d]", "");
        
        // 负数安全过滤
        if (raw.Contains("-") || string.IsNullOrEmpty(clean)) clean = "0";

        // 更新目标文本框（仅在内容不一致时修改以优化性能）
        if (dest != null && dest.text != clean)
        {
            dest.text = clean;
        }
        return clean;
    }
}
