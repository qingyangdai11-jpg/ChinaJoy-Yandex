using UnityEngine;

/// <summary>
/// ItemAudioTrigger 脚本
/// 作用：
/// 1. 游戏运行后自动/动态挂载于场景中的道具与障碍物物体上。
/// 2. 依据标签 (Tag) 判定道具类型（如 Coin, Boost, Barrier, Lightning 等）。
/// 3. 当玩家与其发生碰撞 (OnTriggerEnter / OnCollisionEnter) 时，自动播放对应音效。
/// </summary>
public class ItemAudioTrigger : MonoBehaviour
{
    [Header("Sound Property")]
    [Tooltip("自动判定的 SFX 音效类型")]
    public SFXType detectedSFXType = SFXType.Coin;

    private bool hasTriggered = false;

    private void Awake()
    {
        DetectAudioType();
    }

    /// <summary>
    /// 根据 Tag 或名称自动确定对应的 SFX 音效类型
    /// </summary>
    public void DetectAudioType()
    {
        string tagLower = GetTagInChain().ToLower();
        string nameLower = gameObject.name.ToLower();

        // 优先根据 Tag 识别，其次根据名称识别
        if (tagLower.Contains("coin")) detectedSFXType = SFXType.Coin;
        else if (tagLower.Contains("boost")) detectedSFXType = SFXType.BoostItem;
        else if (tagLower.Contains("barrier") || tagLower.Contains("obstacle")) detectedSFXType = SFXType.Obstacle;
        else if (tagLower.Contains("light")) detectedSFXType = SFXType.Lightning;
        else if (nameLower.Contains("coin")) detectedSFXType = SFXType.Coin;
        else if (nameLower.Contains("boost")) detectedSFXType = SFXType.BoostItem;
        else if (nameLower.Contains("barrier") || nameLower.Contains("obstacle")) detectedSFXType = SFXType.Obstacle;
        else if (nameLower.Contains("light")) detectedSFXType = SFXType.Lightning;
    }

    private string GetTagInChain()
    {
        Transform current = transform;
        while (current != null)
        {
            string t = current.tag;
            if (!string.IsNullOrEmpty(t) && t != "Untagged")
            {
                return t;
            }
            current = current.parent;
        }
        return gameObject.tag;
    }

    private void OnTriggerEnter(Collider other) => TryPlaySound(other.gameObject);
    private void OnCollisionEnter(Collision collision) => TryPlaySound(collision.gameObject);

    private void TryPlaySound(GameObject target)
    {
        if (hasTriggered || IsHoverZone(gameObject)) return;

        // 状态过滤：游戏暂停或倒计时/非 Gameplay 阶段时，不响应道具碰撞音效
        if (SimpleUIManager.Instance != null && SimpleUIManager.Instance.IsPaused) return;
        if (GameFlowController.Instance != null && GameFlowController.Instance.CurrentState != GameFlowController.GameState.Gameplay) return;

        if (IsPlayer(target))
        {
            hasTriggered = true;
            PlayPropSFX();
        }
    }

    private bool IsHoverZone(GameObject obj)
    {
        if (obj == null) return false;
        string nameLower = obj.name.ToLower();
        if (nameLower.Contains("hoverenterzone") || nameLower.Contains("hoverexitzone")) return true;
        var obstacleHandler = obj.GetComponent<ObstacleHandler>();
        return obstacleHandler != null && obstacleHandler.obstacleType != ObstacleType.OBSTACLE_STUN_ZONE;
    }

    private void PlayPropSFX()
    {
        if (AudioManager.Instance != null)
        {
            switch (detectedSFXType)
            {
                case SFXType.Coin: AudioTriggerHub.PlayCoin(); break;
                case SFXType.BoostItem: AudioTriggerHub.PlayBoostItem(); break;
                case SFXType.Obstacle: AudioTriggerHub.PlayObstacle(); break;
                case SFXType.Lightning: AudioTriggerHub.PlayLightning(); break;
                default: AudioManager.Instance.PlaySFX(detectedSFXType); break;
            }
            Debug.Log($"[ItemAudioTrigger] Played SFX {detectedSFXType} for prop: {gameObject.name} (Tag: {gameObject.tag})");
        }
    }

    private bool IsPlayer(GameObject obj)
    {
        if (obj == null) return false;
        return obj.CompareTag("Player") || 
               (obj.transform.root != null && obj.transform.root.CompareTag("Player")) || 
               obj.GetComponentInParent<PlayerController>() != null || 
               obj.GetComponentInParent<SimplePlayerController>() != null || 
               obj.name.ToLower().Contains("player") || 
               (obj.transform.parent != null && obj.transform.parent.name.ToLower().Contains("player"));
    }
}
