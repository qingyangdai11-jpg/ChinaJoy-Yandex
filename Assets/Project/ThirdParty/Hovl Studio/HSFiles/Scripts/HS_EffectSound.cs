using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HS_EffectSound 第三方粒子音效辅助组件
/// 注意：已移除原有的 InvokeRepeating 自动重复播放机制，防止倒计时及游戏运行过程中产生无声源盲目重复干扰噪音。
/// 所有游戏音效统一由 AudioManager 与 AudioTriggerHub 管理。
/// </summary>
public class HS_EffectSound : MonoBehaviour
{
    public bool Repeating = false;
    public float RepeatTime = 2.0f;
    public float StartTime = 0.0f;
    public bool RandomVolume;
    public float minVolume = .4f;
    public float maxVolume = 1f;

    private void Start()
    {
        // 禁用独立重复音效播放，防止干扰背景音乐与游戏音效
        CancelInvoke();
    }

    void RepeatSound()
    {
        // 留空以兼容原有逻辑，不再主动产生杂音
    }
}
