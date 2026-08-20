using UnityEngine;
using System.Collections;

/// <summary>
/// AudioTriggerHub - 统一音频事件触发中心
/// 作用：
/// 1. 作为 AudioManager 的事件接入层，集中处理所有游戏/UI 事件的 SFX 触发
/// 2. 提供静态 API（如 PlayCoin / PlayBarrier / PlayJump 等），方便任意脚本一行调用
/// 3. 内置冷却时间（Cooldown）防抖，避免短时间内重复触发（如连续吃多个金币）
/// 4. 通过订阅 GameFlowController 的状态切换事件，自动驱动 BGM
///
/// 使用方法：
///   AudioTriggerHub.PlaySFX(SFXType.Coin);
///   AudioTriggerHub.PlayLaneChange();   // 等价于 PlaySFX(SFXType.LaneChange)
///   AudioTriggerHub.PlayCountdownNumber();
/// </summary>
public class AudioTriggerHub : MonoBehaviour
{
    public static AudioTriggerHub Instance { get; private set; }

    [Header("SFX 防抖设置 (Cooldown)")]
    [Tooltip("金币音效最小触发间隔（秒），防止连续吃金币导致音效堆叠")]
    public float coinSfxCooldown = 0.06f;
    [Tooltip("切换跑道音效最小触发间隔（秒）")]
    public float laneChangeCooldown = 0.05f;
    [Tooltip("按钮点击音效最小触发间隔（秒）")]
    public float buttonClickCooldown = 0.05f;

    // 内部冷却时间戳记录
    private float lastCoinTime = -10f;
    private float lastLaneChangeTime = -10f;
    private float lastButtonClickTime = -10f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 尝试在启动后自动绑定场景中的按钮（兜底方案）
        AutoBindUIButtons();

        // 订阅 GameFlowController 的状态切换事件，自动驱动 BGM
        StartCoroutine(BindGameFlowControllerNextFrame());
    }

    private IEnumerator BindGameFlowControllerNextFrame()
    {
        // 等待一帧，确保 GameFlowController 已初始化
        yield return null;

        if (GameFlowController.Instance != null)
        {
            // 播放初始 BGM（Home / Idle）
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBGM(BGMType.Home);
            }
        }
    }

    // ==========================================
    // 静态便捷 API - 推荐在业务脚本中直接调用
    // ==========================================

    /// <summary>通用 SFX 播放（带静音检查）</summary>
    public static void PlaySFX(SFXType sfx, float volumeScale = 1.0f)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(sfx, volumeScale);
        }
    }

    /// <summary>播放金币音效（带冷却防抖）</summary>
    public static void PlayCoin()
    {
        if (Instance == null) { PlaySFX(SFXType.Coin); return; }
        if (Time.unscaledTime - Instance.lastCoinTime < Instance.coinSfxCooldown) return;
        Instance.lastCoinTime = Time.unscaledTime;
        PlaySFX(SFXType.Coin);
    }

    /// <summary>播放切换跑道音效（带冷却防抖）</summary>
    public static void PlayLaneChange()
    {
        if (Instance == null) { PlaySFX(SFXType.LaneChange); return; }
        if (Time.unscaledTime - Instance.lastLaneChangeTime < Instance.laneChangeCooldown) return;
        Instance.lastLaneChangeTime = Time.unscaledTime;
        PlaySFX(SFXType.LaneChange);
    }

    /// <summary>播放按钮点击音效（带冷却防抖）</summary>
    public static void PlayButtonClick()
    {
        if (Instance == null) { PlaySFX(SFXType.ButtonClick); return; }
        if (Time.unscaledTime - Instance.lastButtonClickTime < Instance.buttonClickCooldown) return;
        Instance.lastButtonClickTime = Time.unscaledTime;
        PlaySFX(SFXType.ButtonClick);
    }

    /// <summary>倒计时数字 (3, 2, 1)</summary>
    public static void PlayCountdownNumber() => PlaySFX(SFXType.CountdownNum);

    /// <summary>GO!</summary>
    public static void PlayGo() => PlaySFX(SFXType.Go);

    /// <summary>FINISH!</summary>
    public static void PlayFinish() => PlaySFX(SFXType.Finish);

    /// <summary>跳跃</summary>
    public static void PlayJump() => PlaySFX(SFXType.Jump);

    /// <summary>加速</summary>
    public static void PlaySpeedUp() => PlaySFX(SFXType.SpeedUp);

    /// <summary>减速 / 撞击停顿</summary>
    public static void PlaySlowdown() => PlaySFX(SFXType.Slowdown);

    /// <summary>闪电道具收集</summary>
    public static void PlayLightning() => PlaySFX(SFXType.Lightning);

    /// <summary>加速道具收集</summary>
    public static void PlayBoostItem() => PlaySFX(SFXType.BoostItem);

    /// <summary>障碍物碰撞</summary>
    public static void PlayObstacle() => PlaySFX(SFXType.Obstacle);

    /// <summary>确认</summary>
    public static void PlayConfirm() => PlaySFX(SFXType.Confirm);

    /// <summary>返回</summary>
    public static void PlayBack() => PlaySFX(SFXType.Back);

    /// <summary>连击奖励（10/35/50）</summary>
    public static void PlayCoinStreak(int milestone)
    {
        switch (milestone)
        {
            case 10: PlaySFX(SFXType.CoinStreak10); break;
            case 35: PlaySFX(SFXType.CoinStreak35); break;
            case 50: PlaySFX(SFXType.CoinStreak50); break;
            default: PlaySFX(SFXType.CoinStreak10); break;
        }
    }

    /// <summary>生存奖励（30s / 60s）</summary>
    public static void PlaySurvivalReward(int seconds)
    {
        if (seconds >= 60) PlaySFX(SFXType.Survival60s);
        else PlaySFX(SFXType.Survival30s);
    }

    /// <summary>闪电效果触发</summary>
    public static void PlayLightningEffect() => PlaySFX(SFXType.LightningEffect);

    /// <summary>双倍积分</summary>
    public static void PlayDoubleScore() => PlaySFX(SFXType.DoubleScore);

    /// <summary>磁铁吸金</summary>
    public static void PlayMagnet() => PlaySFX(SFXType.Magnet);

    // ==========================================
    // BGM 控制便捷方法
    // ==========================================

    public static void PlayBGM(BGMType bgm, float fadeDuration = 0.3f)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(bgm, fadeDuration);
        }
    }

    public static void StopBGM(float fadeDuration = 0.3f)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopBGM(fadeDuration);
        }
    }

    // ==========================================
    // UI Button 自动绑定
    // ==========================================

    /// <summary>扫描场景中所有 Button 并自动绑定点击音效</summary>
    public void AutoBindUIButtons()
    {
        UnityEngine.UI.Button[] buttons = FindObjectsOfType<UnityEngine.UI.Button>(true);
        foreach (var btn in buttons)
        {
            if (btn != null)
            {
                btn.onClick.RemoveListener(PlayButtonClick);
                btn.onClick.AddListener(PlayButtonClick);
            }
        }
    }

    private void OnEnable()
    {
        // 场景加载时重新绑定按钮
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 延迟一帧再绑定，确保所有 UI 已实例化
        StartCoroutine(DelayAndBindButtons());
    }

    private IEnumerator DelayAndBindButtons()
    {
        yield return null;
        AutoBindUIButtons();
    }
}
