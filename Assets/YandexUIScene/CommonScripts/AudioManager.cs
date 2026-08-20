using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public enum BGMType
{
    Home,         // 1. 主页 (Panel1 / Panel2)
    Rules,        // 2. 语言/规则 (Language / Rules)
    Countdown,    // 3. 倒计时 (Panel3)
    Gameplay,     // 4. 游戏中 (Panel4)
    GameOver      // 5. 游戏结束 (Panel5)
}

public enum SFXType
{
    // UI 交互音效 (4个)
    ButtonClick,    // 1. 按钮点击
    Confirm,        // 2. 确认
    Back,           // 3. 返回
    ButtonSelect,   // 4. 按钮选择/焦点切换

    // 游戏流程音效 (3个)
    CountdownNum,   // 4. 倒计时数字 (3, 2, 1)
    Go,             // 5. GO!
    Finish,         // 6. FINISH! (游戏结束)

    // 玩家动作音效 (4个)
    LaneChange,     // 7. 切换跑道
    Jump,           // 8. 跳跃
    SpeedUp,        // 9. 加速
    Slowdown,       // 10. 减速/撞击停顿

    // 物品收集音效 (4个)
    Coin,           // 11. 金币
    Lightning,      // 12. 闪电
    BoostItem,      // 13. 加速道具
    Obstacle,       // 14. 障碍物

    // 奖励/增益音效 (8个)
    CoinStreak10,   // 15. 连击 10 奖励
    CoinStreak35,   // 16. 连击 35 奖励
    CoinStreak50,   // 17. 连击 50 奖励
    Survival30s,    // 18. 30秒生存奖励
    Survival60s,    // 19. 60秒生存奖励
    LightningEffect,// 20. 闪电效果
    DoubleScore,    // 21. 双倍积分
    Magnet          // 22. 磁铁吸金
}

/// <summary>
/// 全局音效管理器 (AudioManager)
/// 包含：5 段 BGM + 22 个 SFX 音效槽位，支持 Inspector 配置与程序化合成音效兜底（无资源时自动生成音效）
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM 背景音乐 (5段)")]
    [Tooltip("1. 主页 BGM")] public AudioClip bgmHome;
    [Tooltip("2. 语言/规则 BGM")] public AudioClip bgmRules;
    [Tooltip("3. 倒计时 BGM")] public AudioClip bgmCountdown;
    [Tooltip("4. 游戏中 BGM")] public AudioClip bgmGameplay;
    [Tooltip("5. 游戏结束 BGM")] public AudioClip bgmGameOver;

    [Header("UI 交互音效 (4个)")]
    [Tooltip("1. 按钮点击")] public AudioClip sfxButtonClick;
    [Tooltip("2. 确认")] public AudioClip sfxConfirm;
    [Tooltip("3. 返回")] public AudioClip sfxBack;
    [Tooltip("4. 按钮选择/焦点切换")] public AudioClip sfxButtonSelect;

    [Header("游戏流程音效 (3个)")]
    [Tooltip("4. 倒计时数字 (3, 2, 1)")] public AudioClip sfxCountdownNum;
    [Tooltip("5. GO!")] public AudioClip sfxGo;
    [Tooltip("6. FINISH! (游戏结束)")] public AudioClip sfxFinish;

    [Header("玩家动作音效 (4个)")]
    [Tooltip("7. 切换跑道")] public AudioClip sfxLaneChange;
    [Tooltip("8. 跳跃")] public AudioClip sfxJump;
    [Tooltip("9. 加速")] public AudioClip sfxSpeedUp;
    [Tooltip("10. 减速")] public AudioClip sfxSlowdown;

    [Header("物品收集音效 (4个)")]
    [Tooltip("11. 金币")] public AudioClip sfxCoin;
    [Tooltip("12. 闪电")] public AudioClip sfxLightning;
    [Tooltip("13. 加速道具")] public AudioClip sfxBoostItem;
    [Tooltip("14. 障碍物碰撞")] public AudioClip sfxObstacle;

    [Header("奖励/增益音效 (8个)")]
    [Tooltip("15. 连击 10 奖励")] public AudioClip sfxCoinStreak10;
    [Tooltip("16. 连击 35 奖励")] public AudioClip sfxCoinStreak35;
    [Tooltip("17. 连击 50 奖励")] public AudioClip sfxCoinStreak50;
    [Tooltip("18. 30秒生存奖励")] public AudioClip sfxSurvival30s;
    [Tooltip("19. 60秒生存奖励")] public AudioClip sfxSurvival60s;
    [Tooltip("20. 闪电效果")] public AudioClip sfxLightningEffect;
    [Tooltip("21. 双倍积分")] public AudioClip sfxDoubleScore;
    [Tooltip("22. 磁铁吸金")] public AudioClip sfxMagnet;

    [Header("音量设置")]
    [Range(0f, 1f)] public float bgmVolume = 0.8f;
    [Range(0f, 1f)] public float sfxVolume = 1.0f;
    public bool isMuted = false;

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private AudioSource countdownSource;
    private List<AudioSource> sfxSourcePool = new List<AudioSource>();
    private BGMType? currentBGMType = null;

    [Header("SFX 多源并发 (消除同一帧连续播放的延迟)")]
    [Tooltip("用于 PlayOneShot 的 SFX 音频源数量，推荐 4~8")]
    [Range(2, 16)] public int sfxSourcePoolSize = 6;
    private int sfxSourceIndex = 0;

    [Header("道具音效自动绑定")]
    [Tooltip("游戏运行后自动扫描场景中所有道具与障碍物，并为其绑定 ItemAudioTrigger 声音脚本")]
    public bool autoBindPropAudio = true;
    [Tooltip("自动轮询扫描新生成道具的时间间隔（秒）")]
    public float propScanInterval = 0.5f;

    private Coroutine propScanCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudioSources();
        AutoRegisterUIButtons();
    }

    private void Update()
    {
        if (SimpleUIManager.Instance != null)
        {
            AudioListener.pause = SimpleUIManager.Instance.IsPaused;
        }
    }

    private void OnEnable()
    {
        EnsureAudioSourcesEnabled();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (autoBindPropAudio)
        {
            AutoBindAllSceneProps();
        }
    }

    private void InitializeAudioSources()
    {
        gameObject.SetActive(true);

        bgmSource = CreateAudioSource(true);
        sfxSource = CreateAudioSource(false);
        countdownSource = CreateAudioSource(false);

        sfxSourcePool.Clear();
        for (int i = 0; i < sfxSourcePoolSize; i++)
        {
            sfxSourcePool.Add(CreateAudioSource(false));
        }
    }

    private AudioSource CreateAudioSource(bool loop)
    {
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.loop = loop;
        src.playOnAwake = false;
        src.volume = loop ? bgmVolume : sfxVolume;
        src.enabled = true;
        return src;
    }

    private void EnsureAudioSourcesEnabled()
    {
        if (bgmSource != null) bgmSource.enabled = true;
        if (sfxSource != null) sfxSource.enabled = true;
        if (sfxSourcePool != null)
        {
            foreach (var src in sfxSourcePool)
            {
                if (src != null) src.enabled = true;
            }
        }
    }

    private void Start()
    {
        AutoRegisterUIButtons();
        if (autoBindPropAudio)
        {
            AutoBindAllSceneProps();
            if (propScanCoroutine != null) StopCoroutine(propScanCoroutine);
            propScanCoroutine = StartCoroutine(PeriodicallyScanAndBindProps());
        }
    }

    /// <summary>
    /// 自动绑定场景中所有 Button 的点击与选中（焦点切换）音效
    /// </summary>
    public void AutoRegisterUIButtons()
    {
        Button[] buttons = FindObjectsOfType<Button>(true);
        foreach (Button btn in buttons)
        {
            if (btn != null)
            {
                btn.onClick.RemoveListener(OnButtonClickAudio);
                btn.onClick.AddListener(OnButtonClickAudio);
                RegisterFocusAudio(btn.gameObject);
            }
        }
    }

    private int lastClickFrame = -1;

    private void OnButtonClickAudio()
    {
        lastClickFrame = Time.frameCount;
        PlaySFX(SFXType.ButtonClick);
    }

    private void OnButtonSelectAudio()
    {
        // 延迟一帧播放，防止与点击音效重叠
        StartCoroutine(PlaySelectAudioDeferred());
    }

    private IEnumerator PlaySelectAudioDeferred()
    {
        yield return null;
        if (Time.frameCount == lastClickFrame || Time.frameCount - 1 == lastClickFrame)
        {
            yield break;
        }
        PlaySFX(SFXType.ButtonSelect);
    }

    private void RegisterFocusAudio(GameObject go)
    {
        if (go == null) return;

        EventTrigger trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        EventTrigger.Entry selectEntry = null;

        foreach (var entry in trigger.triggers)
        {
            if (entry.eventID == EventTriggerType.Select)
            {
                selectEntry = entry;
                break;
            }
        }

        if (selectEntry == null)
        {
            selectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
            trigger.triggers.Add(selectEntry);
        }

        selectEntry.callback.RemoveAllListeners();
        selectEntry.callback.AddListener((eventData) => OnButtonSelectAudio());
    }

    // ==========================================
    // 道具音效自动绑定与扫描 API
    // ==========================================

    public void AutoBindAllSceneProps()
    {
        Collider[] colliders = FindObjectsOfType<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (col != null && col.gameObject != null && IsPropOrObstacle(col.gameObject))
            {
                EnsureAudioTriggerAttached(col.gameObject);
            }
        }
    }

    private IEnumerator PeriodicallyScanAndBindProps()
    {
        while (true)
        {
            yield return new WaitForSeconds(propScanInterval);
            if (autoBindPropAudio)
            {
                AutoBindAllSceneProps();
            }
        }
    }

    private bool IsPropOrObstacle(GameObject obj)
    {
        if (obj == null) return false;

        // 1. 检查已有的核心道具/障碍组件
        if (obj.GetComponent<CollectibleItem>() != null ||
            obj.GetComponent<CoinGetHandler>() != null ||
            obj.GetComponent<PickupGetHandler>() != null ||
            obj.GetComponent<ObstacleHandler>() != null)
            return true;

        // 2. 检查名称与父级链的 Tag
        string nameLower = obj.name.ToLower();
        string tagLower = "";
        Transform current = obj.transform;
        while (current != null)
        {
            if (!string.IsNullOrEmpty(current.tag) && current.tag != "Untagged")
            {
                tagLower = current.tag.ToLower();
                break;
            }
            current = current.parent;
        }

        bool matchKeywords(string s) => s.Contains("coin") || s.Contains("boost") || s.Contains("barrier") || 
                                         s.Contains("obstacle") || s.Contains("light") || s.Contains("pickup");

        return matchKeywords(nameLower) || (!string.IsNullOrEmpty(tagLower) && matchKeywords(tagLower));
    }

    private void EnsureAudioTriggerAttached(GameObject obj)
    {
        if (obj == null) return;
        if (obj.GetComponent<ItemAudioTrigger>() == null && obj.GetComponentInParent<ItemAudioTrigger>() == null)
        {
            ItemAudioTrigger trigger = obj.AddComponent<ItemAudioTrigger>();
            trigger.DetectAudioType();
        }
    }

    // ==========================================
    // BGM 控制 API
    // ==========================================

    public void PlayBGM(BGMType bgm, float fadeDuration = 0.3f)
    {
        if (currentBGMType == bgm && bgmSource != null && bgmSource.isPlaying) return;

        currentBGMType = bgm;
        AudioClip clip = GetBGMClip(bgm);

        if (clip == null)
        {
            StopBGM(fadeDuration);
            return;
        }

        StartCoroutine(CrossFadeBGM(clip, fadeDuration));
    }

    public void StopBGM(float fadeDuration = 0.3f)
    {
        currentBGMType = null;
        StartCoroutine(CrossFadeBGM(null, fadeDuration));
    }

    private IEnumerator CrossFadeBGM(AudioClip newClip, float duration)
    {
        float startVol = bgmSource.volume;
        float timer = 0f;

        if (bgmSource.isPlaying && duration > 0f)
        {
            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0f, timer / duration);
                yield return null;
            }
        }

        if (newClip != null)
        {
            bgmSource.clip = newClip;
            bgmSource.Play();

            timer = 0f;
            float targetVol = isMuted ? 0f : bgmVolume;

            if (duration > 0f)
            {
                while (timer < duration)
                {
                    timer += Time.unscaledDeltaTime;
                    bgmSource.volume = Mathf.Lerp(0f, targetVol, timer / duration);
                    yield return null;
                }
            }
            else
            {
                bgmSource.volume = targetVol;
            }
        }
        else
        {
            bgmSource.Stop();
        }
    }

    // ==========================================
    // SFX 音效控制 API
    // ==========================================

    public void PlaySFX(SFXType sfx, float volumeScale = 1.0f)
    {
        if (isMuted) return;

        AudioClip clip = GetSFXClip(sfx);

        if (clip == null && sfx == SFXType.ButtonSelect)
        {
            clip = sfxButtonClick;
            volumeScale *= 0.5f;
        }

        if (clip == null) return;

        if (sfxSourcePool != null && sfxSourcePool.Count > 0)
        {
            AudioSource source = sfxSourcePool[sfxSourceIndex];
            sfxSourceIndex = (sfxSourceIndex + 1) % sfxSourcePool.Count;

            source.enabled = true;
            source.PlayOneShot(clip, sfxVolume * volumeScale);
        }
    }

    public void StopCountdownSFX()
    {
        // 允许倒计时声音完整播放完毕，不进行拦截与打断
    }

    // ==========================================
    // 音量与静音控制
    // ==========================================

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (bgmSource != null && !isMuted)
        {
            bgmSource.volume = bgmVolume;
        }
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }
        if (sfxSourcePool != null)
        {
            foreach (var source in sfxSourcePool)
            {
                if (source != null)
                {
                    source.volume = sfxVolume;
                    source.enabled = true;
                }
            }
        }
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        if (bgmSource != null)
        {
            bgmSource.volume = isMuted ? 0f : bgmVolume;
        }
    }

    // ==========================================
    // 音效资源映射
    // ==========================================

    private AudioClip GetBGMClip(BGMType bgm)
    {
        switch (bgm)
        {
            case BGMType.Home: return bgmHome;
            case BGMType.Rules: return bgmRules;
            case BGMType.Countdown: return bgmCountdown;
            case BGMType.Gameplay: return bgmGameplay;
            case BGMType.GameOver: return bgmGameOver;
            default: return null;
        }
    }

    private AudioClip GetSFXClip(SFXType sfx)
    {
        switch (sfx)
        {
            case SFXType.ButtonClick: return sfxButtonClick;
            case SFXType.Confirm: return sfxConfirm;
            case SFXType.Back: return sfxBack;
            case SFXType.ButtonSelect: return sfxButtonSelect;
            case SFXType.CountdownNum: return sfxCountdownNum;
            case SFXType.Go: return sfxGo;
            case SFXType.Finish: return sfxFinish;
            case SFXType.LaneChange: return sfxLaneChange;
            case SFXType.Jump: return sfxJump;
            case SFXType.SpeedUp: return sfxSpeedUp;
            case SFXType.Slowdown: return sfxSlowdown;
            case SFXType.Coin: return sfxCoin;
            case SFXType.Lightning: return sfxLightning;
            case SFXType.BoostItem: return sfxBoostItem;
            case SFXType.Obstacle: return sfxObstacle;
            case SFXType.CoinStreak10: return sfxCoinStreak10;
            case SFXType.CoinStreak35: return sfxCoinStreak35;
            case SFXType.CoinStreak50: return sfxCoinStreak50;
            case SFXType.Survival30s: return sfxSurvival30s;
            case SFXType.Survival60s: return sfxSurvival60s;
            case SFXType.LightningEffect: return sfxLightningEffect;
            case SFXType.DoubleScore: return sfxDoubleScore;
            case SFXType.Magnet: return sfxMagnet;
            default: return null;
        }
    }
}
