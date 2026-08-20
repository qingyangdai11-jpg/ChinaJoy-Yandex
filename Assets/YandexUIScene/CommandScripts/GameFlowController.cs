using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;

public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance { get; private set; }

    public enum GameState
    {
        Idle,            // 待机
        LanguageSelect,  // 语言选择
        RuleRead,        // 规则阅读
        CountdownStart,  // 倒计时开始
        Gameplay,        // 游戏中
        GameOver,        // 游戏结束
        GameOverPage     // 游戏结束页面
    }

    public enum Language
    {
        CN,
        EN
    }

    [Header("Current State Info")]
    [SerializeField] private GameState currentState = GameState.Idle;
    public GameState CurrentState => currentState;

    public static event System.Action<Language> OnLanguageChanged;

    [SerializeField] private Language selectedLanguage = Language.CN;
    public Language SelectedLanguage
    {
        get => selectedLanguage;
        set
        {
            selectedLanguage = value;
            OnLanguageChanged?.Invoke(selectedLanguage);
        }
    }

    public string ActiveGameMode { get; set; } = "ONLINE";

    [Header("References")]
    public PageSwitcher pageSwitcher;
    public SceneReLoad sceneReLoader;

    [Header("Flow Control Settings")]
    [Tooltip("是否开启使用 确认键开始游戏")]
    public bool enableConfirmToStart = true;

    [Header("Game Score Settings")]
    [Tooltip("Global parameter for current game score")]
    public int gameScore = 0;
    [Tooltip("Whether to enable debug mode score adjustments (PgUp/PgDn)")]
    public bool enableDebugMode = false;

    [Header("Language Selection UI")]
    public Button buttonCN;
    public Button buttonEN;

    [Header("Countdown Start UI")]
    public TextMeshProUGUI countdownTipTMP;
    public TextMeshProUGUI countdownNumberTMP;
    public Image countdownBlueBgImage;
    public float startCountdownDuration = 3f;

    [Header("Gameplay UI")]
    public TextMeshProUGUI gameTimerTMP;
    public TextMeshProUGUI gameScoreTMP;
    public TextMeshProUGUI scoreNumTMP;
    public GameObject finishImagePopup;
    public CanvasGroup finishCanvasGroup;
    public float gameplayDuration = 60f;

    [Header("Game Over Effects")]
    public GameObject playerCharacter;
    public Image overlayFilter;
    [Tooltip("玩家在游戏结束后渐隐退场动画的时间")]
    public float characterFadeDuration = 0.2f;
    [Tooltip("角色Mesh渐隐消失的时间")]
    public float meshFadeOutDuration = 0.8f;
    [Tooltip("拖入 playermesh3/Mesh_Cat 的 Renderer")]
    public Renderer meshCatRenderer;
    [Tooltip("拖入 playermesh3/bip.root/SM_Hoverboard 的 Renderer")]
    public Renderer hoverboardRenderer;

    [Header("GameOver Page UI")]
    public TextMeshProUGUI gameOverCountdownTMP;
    public float gameOverCountdownDuration = 10f;
    public TextMeshProUGUI scoreTMP;

    public int CurrentScore { get; private set; }

    [Header("Background Settings")]
    [Tooltip("黑色背景背景图物体")]
    public GameObject imageBlackBg;

    [Header("Start Page Settings")]
    [Tooltip("开始页面-离线图片")]
    public GameObject startPageImageOffline;
    [Tooltip("开始页面-在线图片")]
    public GameObject startPageImageOnline;
    [Tooltip("开始页面-二维码")]
    public GameObject startPageQrCore;

    [Header("Panel 5 Settlement UI Settings")]
    [Tooltip("结算页面-离线区域")]
    public GameObject panel5ImageOffline;
    [Tooltip("结算页面-排行榜")]
    public GameObject panel5Ranking1;
    [Tooltip("结算页面-离线分数文本")]
    public TextMeshProUGUI panel5OfflineScoreTMP;
    [Tooltip("结算页面-在线分数文本")]
    public TextMeshProUGUI panel5OnlineScoreTMP;


    //private Vector2 lastMousePos;
    //private static extern bool SetCursorPos(int X, int Y);

    private struct SavedMaterialInfo
    {
        public Renderer renderer;
        public Material[] originalMaterials;
    }
    private List<SavedMaterialInfo> savedMaterials = new List<SavedMaterialInfo>();

    private InputAction confirmAction;
    private InputAction cancelAction;
    private float remainingGameTime;
    private GameObject lastSelectedGameObject;
    private float stateEntryTime;

    private Coroutine startCountdownCoroutine;
    private Coroutine gameOverSequenceCoroutine;
    private Coroutine gameOverPageCoroutine;
    private Transform offlineCountdownTMP;

    private void StopStateCoroutines()
    {
        if (startCountdownCoroutine != null)
        {
            StopCoroutine(startCountdownCoroutine);
            startCountdownCoroutine = null;
        }
        if (gameOverSequenceCoroutine != null)
        {
            StopCoroutine(gameOverSequenceCoroutine);
            gameOverSequenceCoroutine = null;
        }
        if (gameOverPageCoroutine != null)
        {
            StopCoroutine(gameOverPageCoroutine);
            gameOverPageCoroutine = null;
        }
    }

    public static bool IsFirstBoot = true;

    private void Awake()
    {
        if (IsFirstBoot)
        {
            IsFirstBoot = false;
            PlayerPrefs.SetString("GameMode", "ONLINE");
            PlayerPrefs.Save();
        }

        // Set enableConfirmToStart based on current GameMode (ONLINE = false, OFFLINE = true)
        string currentMode = PlayerPrefs.GetString("GameMode", "ONLINE");
        ActiveGameMode = currentMode;
        enableConfirmToStart = (currentMode != "ONLINE");



        // Load persistent debug mode from PlayerPrefs
        if (PlayerPrefs.HasKey("EnableDebugMode"))
        {
            enableDebugMode = PlayerPrefs.GetInt("EnableDebugMode") == 1;
        }

        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Configure Confirm Input Action (Space, Enter, and Gamepad A/Cross)
        confirmAction = new InputAction("Confirm", type: InputActionType.Button);
        confirmAction.AddBinding("<Keyboard>/space");
        confirmAction.AddBinding("<Keyboard>/enter");
        confirmAction.AddBinding("<Keyboard>/numpadEnter");
        confirmAction.AddBinding("<Gamepad>/buttonSouth");// 手柄A键

        // Configure Cancel Input Action (Escape, Backspace, and Gamepad B/Circle)
        cancelAction = new InputAction("Cancel", type: InputActionType.Button);
        cancelAction.AddBinding("<Keyboard>/escape");
        cancelAction.AddBinding("<Keyboard>/backspace");
        cancelAction.AddBinding("<Gamepad>/buttonEast");// 手柄B键

        // Set up language buttons onClick listeners
        if (buttonCN != null)
        {
            buttonCN.onClick.AddListener(() => {
                if (currentState == GameState.LanguageSelect)
                {
                    if (Time.unscaledTime - stateEntryTime < GetStateCooldown(currentState)) return;
                    SelectedLanguage = Language.CN;
                    SetState(GameState.RuleRead);
                }
            });
        }
        if (buttonEN != null)
        {
            buttonEN.onClick.AddListener(() => {
                if (currentState == GameState.LanguageSelect)
                {
                    if (Time.unscaledTime - stateEntryTime < GetStateCooldown(currentState)) return;
                    SelectedLanguage = Language.EN;
                    SetState(GameState.RuleRead);
                }
            });
        }

        // Configure offline/online elements early in Awake to avoid any 1-frame flickering/flashing on reload
        ConfigureStartPageElements();

        Debug.Log(currentMode);

    }

    private void OnEnable()// 开启输入监听
    {
        confirmAction.Enable();
        cancelAction.Enable();
    }

    private void OnDisable()//关闭输入监听
    {
        confirmAction.Disable();
        cancelAction.Disable();
    }

    private float GetStateCooldown(GameState state)
    {
        if (state == GameState.LanguageSelect || state == GameState.RuleRead)
        {
            return 1.0f;
        }
        return 0.2f;
    }

    private void Start()
    {
        Cursor.visible = false;
        // 重要：不要用 Locked！设为None
        Vector2 targetPosition = new Vector2(0, Screen.height);

        if (Mouse.current != null)
        {
            Mouse.current.WarpCursorPosition(targetPosition);
        }


        // Initialize timer UI to start values
        UpdateGameplayTimerUI(gameplayDuration);//初始化游戏时间并定义游戏时间的格式

        if (finishImagePopup != null)
            finishImagePopup.SetActive(false);

        // Turn on background by default
        if (imageBlackBg != null)
        {
            imageBlackBg.SetActive(true);
            var bgImage = imageBlackBg.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = new Color(bgImage.color.r, bgImage.color.g, bgImage.color.b, 1f);
            }
        }
        // Initialize overlay filter
        if (overlayFilter != null)
        {
            //overlayFilter.color = new Color(0.3f, 0.2f, 0.6f, 0f);
            overlayFilter.gameObject.SetActive(false);
        }
        if (FindObjectOfType<GameMechanicsManager>() == null)
        {
            gameObject.AddComponent<GameMechanicsManager>();
        }
        if (FindObjectOfType<ScoreBridge>() == null)
        {
            gameObject.AddComponent<ScoreBridge>();
            Debug.Log("[GameFlowController] Automatically added missing ScoreBridge component to scene.");
        }

        // Initialize player setup from Inspector assignment
        if (playerCharacter != null)
        {
            playerCharacter.SetActive(true);
            playerCharacter.transform.localScale = Vector3.one;
            SaveOriginalMaterials();//材质备份，为游戏结束渐隐角色做准备
        }

        // Transition to Idle state
        SetState(GameState.Idle);
    }

    private void Update()
    {
        // Real-time score UI update
        if (gameScore < 0) gameScore = 0;
        if (gameScoreTMP != null)
        {
            gameScoreTMP.text = gameScore.ToString();
        }
        if (scoreNumTMP != null)
        {
            scoreNumTMP.text = gameScore.ToString();
        }

        // Block all updates and state transitions if scene reload is in progress
        //如果场景重载正在运行中，则组织所有更新和状态转换
        if (sceneReLoader != null && sceneReLoader.isReloading) return;

      

        if (Time.unscaledTime - stateEntryTime < GetStateCooldown(currentState)) return;//状态切换冷却期（防连击穿透）

        switch (currentState)
        {
            case GameState.Idle:
                if ((enableConfirmToStart || ActiveGameMode == "OFFLINE") && confirmAction.triggered)
                {
                    // Block UI transition to next step if GameModeSelector is open
                    if (GameModeSelector.Instance != null && GameModeSelector.Instance.IsOpen)
                    {
                        break;
                    }
                    SetState(GameState.LanguageSelect);
                }
                break;

            case GameState.LanguageSelect:
                HandleLanguageInput();
                // Backup manual check in case EventSystem selection is bypassed
                if (confirmAction.triggered)
                {
                    var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                    if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
                    {
                        var btn = eventSystem.currentSelectedGameObject.GetComponent<Button>();
                        if (btn != null)
                        {
                            btn.onClick.Invoke();
                        }
                    }
                }
                break;

            case GameState.RuleRead:
                if (confirmAction.triggered)
                {
                    if (Time.unscaledTime - stateEntryTime < GetStateCooldown(currentState)) break;
                    SetState(GameState.CountdownStart);
                }
                break;

            case GameState.Gameplay://游戏倒计时计算，实时更新倒计时 UI，并在时间用尽时自动触发“游戏结束（GameOver）
                remainingGameTime -= Time.deltaTime;
                if (remainingGameTime <= 0)
                {
                    remainingGameTime = 0;
                    UpdateGameplayTimerUI(0);
                    gameOverSequenceCoroutine = StartCoroutine(GameOverSequenceRoutine());//该协程会处理播放慢动作、禁用玩家操控、让角色渐隐、弹出结算页等复杂的“游戏结束”过渡动画
                }
                else
                {
                    UpdateGameplayTimerUI(remainingGameTime);//时间没有用尽，更新倒计时UI
                }
                break;
        }
    }

    public void SetState(GameState newState)
    {
        // Anti-penetration check when transitioning from RuleRead to CountdownStart
        if (currentState == GameState.RuleRead && newState == GameState.CountdownStart)
        {
            if (Time.unscaledTime - stateEntryTime < GetStateCooldown(currentState)) return;
        }
        // 如果是从“阅读规则（RuleRead）”切换到“开始倒计时（CountdownStart）”，会检查停留时间是否达到了设定的冷却值。如果停留时间过短，直接拦截，不予切换

        GameState oldState = currentState;//旧状态记录
        currentState = newState;//记录新状态
        stateEntryTime = Time.unscaledTime;//重置计时

        switch (currentState)
        {
            case GameState.Idle:
                StopStateCoroutines();//清理当前运行的所有状态协程。
                if (oldState != GameState.Idle)//场景重置：如果不是刚开机，而是从其他状态返回 Idle，会自动调用 TriggerReload() 重新加载场景，彻底清空上一局的数据
                {
                    TriggerReload();
                    break;
                }
                ConfigureStartPageElements();//配置离线/在线背景、生成 Yandex 二维码
                if (pageSwitcher != null)
                {
                    pageSwitcher.SwitchToPage(0);//切换 UI 到 Page 0
                }
                if (AudioManager.Instance != null) AudioManager.Instance.PlayBGM(BGMType.Home);//播放大厅背景音乐
                if (imageBlackBg != null)//开启不透明黑底挡住后方 3D 场景
                {
                    imageBlackBg.SetActive(true);
                    var bgImage = imageBlackBg.GetComponent<Image>();
                    if (bgImage != null)
                    {
                        bgImage.DOKill();
                        bgImage.color = new Color(bgImage.color.r, bgImage.color.g, bgImage.color.b, 1f);
                    }
                }
                if (YandexQRCodeGenerator.Instance != null)//生成 Yandex 二维码
                {
                    YandexQRCodeGenerator.Instance.GenerateQRCode();
                }
                break;

            case GameState.LanguageSelect:
                StopStateCoroutines();
                if (pageSwitcher != null) pageSwitcher.SwitchToPage(1);
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBGM(BGMType.Rules);
                    AudioManager.Instance.AutoRegisterUIButtons();
                }
                if (imageBlackBg != null)
                {
                    imageBlackBg.SetActive(true);
                    var bgImage = imageBlackBg.GetComponent<Image>();
                    if (bgImage != null)
                    {
                        bgImage.DOKill();
                        bgImage.color = new Color(bgImage.color.r, bgImage.color.g, bgImage.color.b, 1f);
                    }
                }
                // Default select CN button
                SelectUIObject(buttonCN != null ? buttonCN.gameObject : null);
                break;

            case GameState.RuleRead:
                StopStateCoroutines();
                if (pageSwitcher != null) pageSwitcher.SwitchToPage(2);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayBGM(BGMType.Rules);
                if (imageBlackBg != null)
                {
                    imageBlackBg.SetActive(true);
                    var bgImage = imageBlackBg.GetComponent<Image>();
                    if (bgImage != null)
                    {
                        bgImage.DOKill();
                        bgImage.color = new Color(bgImage.color.r, bgImage.color.g, bgImage.color.b, 1f);
                    }
                }
                break;

            case GameState.CountdownStart:
                StopStateCoroutines();
                if (pageSwitcher != null) pageSwitcher.SwitchToPage(3);
                if (AudioManager.Instance != null) AudioManager.Instance.StopBGM(0.2f);
                if (imageBlackBg != null)
                {
                    var bgImage = imageBlackBg.GetComponent<Image>();
                    if (bgImage != null)
                    {
                        bgImage.DOKill();
                        bgImage.DOFade(0f, 0.3f).SetUpdate(true).OnComplete(() => {
                            imageBlackBg.SetActive(false);
                        });
                    }
                    else
                    {
                        imageBlackBg.SetActive(false);
                    }
                }
                startCountdownCoroutine = StartCoroutine(StartCountdownRoutine());
                break;

            case GameState.Gameplay:
                StopStateCoroutines();
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.StopCountdownSFX();
                    AudioManager.Instance.PlayBGM(BGMType.Gameplay);
                }
                gameScore = 0; // Reset score on gameplay start
                CurrentScore = 0;

                // Reset GameMechanicsManager state
                if (GameMechanicsManager.Instance != null)
                {
                    GameMechanicsManager.Instance.ResetState();
                }
                if (pageSwitcher != null) pageSwitcher.SwitchToPage(4);
                remainingGameTime = gameplayDuration;
                if (scoreNumTMP != null) scoreNumTMP.text = "0";
                if (finishImagePopup != null) finishImagePopup.SetActive(false);
                
                // Restore player character opacity and scale
                if (playerCharacter != null)
                {
                    playerCharacter.SetActive(true); // Ensure it is active
                    playerCharacter.transform.DOKill();
                    playerCharacter.transform.localScale = Vector3.one;

                    // Reset player lane position
                    var playerController = playerCharacter.GetComponent<SimplePlayerController>();
                    if (playerController != null)
                    {
                        playerController.ResetPosition();
                    }

                    // Restore original shared materials using sharedMaterials to avoid material instantiation flash
                    RestoreOriginalMaterials();

                    var canvasRenderers = playerCharacter.GetComponentsInChildren<CanvasRenderer>();
                    foreach (var canvasRenderer in canvasRenderers)
                    {
                        canvasRenderer.SetAlpha(1f);
                    }
                }

                // During gameplay, Mesh_Cat and SM_Hoverboard must use an opaque surface.
                RestoreMeshAlpha();
                break;

            case GameState.GameOver:
                if (gameScore < 0) gameScore = 0;
                if (CurrentScore < 0) CurrentScore = 0;
                // We do NOT stop coroutines here so the active GameOverSequenceRoutine can run its course.
                // Switch Surface Type to Transparent as soon as the game ends, before the
                // game-over visual sequence begins. The coroutine then fades its alpha out.
                StartCoroutine(FadeMeshCatAndHoverboard(meshFadeOutDuration));
                break;

            case GameState.GameOverPage:
                // Clean up any remaining countdown or page coroutines, keeping gameOverSequenceCoroutine clear
                if (startCountdownCoroutine != null) { StopCoroutine(startCountdownCoroutine); startCountdownCoroutine = null; }
                if (gameOverPageCoroutine != null) { StopCoroutine(gameOverPageCoroutine); gameOverPageCoroutine = null; }

                if (pageSwitcher != null) pageSwitcher.SwitchToPage(5);
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBGM(BGMType.GameOver);
                    AudioManager.Instance.PlaySFX(SFXType.Finish);
                }

                // Configure Panel_5 elements based on offline/online mode
                offlineCountdownTMP = null;
                bool isOffline = (ActiveGameMode == "OFFLINE");

                if (panel5ImageOffline != null)
                {
                    panel5ImageOffline.SetActive(isOffline);
                }

                if (panel5Ranking1 != null)
                {
                    panel5Ranking1.SetActive(!isOffline);
                }

                if (panel5OfflineScoreTMP != null)
                {
                    panel5OfflineScoreTMP.text = gameScore.ToString();
                }

                if (panel5OnlineScoreTMP != null)
                {
                    panel5OnlineScoreTMP.text = gameScore.ToString();
                }

                if (scoreTMP != null)
                {
                    scoreTMP.text = gameScore.ToString();
                }

                gameOverPageCoroutine = StartCoroutine(GameOverPageRoutine());

                // Report score to WSS server if online
                if (YandexWebSocketClient.Instance != null)
                {
                    YandexWebSocketClient.Instance.OnGameOverPageReached();
                }
                break;
        }
    }

    //    监听玩家的键盘或手柄输入，以实现“中文（buttonCN）”和“英文（buttonEN）”语言选择按钮的左右焦点切换。
    private void HandleLanguageInput()
    {
        float horizontalInput = 0;
        if (Gamepad.current != null)//采集手柄输入 (Gamepad)，当左摇杆向左或向右推的幅度超过 0.5f 时，才将摇杆的水平轴向值赋给 horizontalInput
        {
            var dpad = Gamepad.current.dpad.ReadValue();
            var stick = Gamepad.current.leftStick.ReadValue();
            if (dpad.x != 0) horizontalInput = dpad.x;
            else if (Mathf.Abs(stick.x) > 0.5f) horizontalInput = stick.x;
        }
        if (Keyboard.current != null)//采集键盘输入 (Keyboard)，如果玩家在本帧按下了左方向键或 A键，强制将输入值设为 -1f（向左/右移）
        {
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
                horizontalInput = -1f;
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
                horizontalInput = 1f;
        }

        if (horizontalInput < -0.1f)//执行按钮焦点切换
        {
            SelectUIObject(buttonCN != null ? buttonCN.gameObject : null);
        }
        else if (horizontalInput > 0.1f)
        {
            SelectUIObject(buttonEN != null ? buttonEN.gameObject : null);
        }
    }

    private void SelectUIObject(GameObject obj)
    {
        if (obj == null) return;
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(obj);
            var button = obj.GetComponent<Button>();
            if (button != null) button.Select();
        }
    }

    private CanvasGroup GetPanel3CanvasGroup()
    {
        if (countdownNumberTMP != null && countdownNumberTMP.transform.parent != null)
        {
            return countdownNumberTMP.transform.parent.GetComponent<CanvasGroup>();
        }
        return null;
    }

    private IEnumerator StartCountdownRoutine()
    {
        CanvasGroup panel3CanvasGroup = GetPanel3CanvasGroup();

        if (countdownBlueBgImage != null)
        {
            countdownBlueBgImage.DOKill();
            Color c = countdownBlueBgImage.color;
            c.a = 1f;
            countdownBlueBgImage.color = c;
        }
        if (panel3CanvasGroup != null)
        {
            panel3CanvasGroup.DOKill();
            panel3CanvasGroup.alpha = 1f;
        }

        if (countdownTipTMP != null) countdownTipTMP.gameObject.SetActive(true);
        if (countdownNumberTMP != null) countdownNumberTMP.gameObject.SetActive(true);

        for (int i = (int)startCountdownDuration; i >= 1; i--)
        {
            // 仅在倒计时第一次（数字 3）时播放音效，后续 2 和 1 取消音效调用，让音效自然完整播放
            if (i == (int)startCountdownDuration)
            {
                AudioManager.Instance?.PlaySFX(SFXType.CountdownNum);
            }

            if (countdownNumberTMP != null)
            {
                countdownNumberTMP.text = i.ToString();
                
                // DOTween animation: Reset scale & alpha, then animate
                countdownNumberTMP.transform.DOKill();
                countdownNumberTMP.DOKill();

                countdownNumberTMP.transform.localScale = Vector3.one * 2.0f;
                countdownNumberTMP.color = new Color(countdownNumberTMP.color.r, countdownNumberTMP.color.g, countdownNumberTMP.color.b, 1f);

                countdownNumberTMP.transform.DOScale(1f, 0.8f).SetEase(Ease.OutBack);
                countdownNumberTMP.DOFade(0f, 0.8f).SetEase(Ease.InQuad).SetDelay(0.2f);
            }
            yield return new WaitForSeconds(1f);
        }

        if (countdownNumberTMP != null)
        {
            AudioManager.Instance?.PlaySFX(SFXType.Go);

            countdownNumberTMP.text = "GO!";
            
            countdownNumberTMP.transform.DOKill();
            countdownNumberTMP.DOKill();

            countdownNumberTMP.transform.localScale = Vector3.one * 2.0f;
            countdownNumberTMP.color = new Color(countdownNumberTMP.color.r, countdownNumberTMP.color.g, countdownNumberTMP.color.b, 1f);

            countdownNumberTMP.transform.DOScale(1f, 0.8f).SetEase(Ease.OutBack);
            countdownNumberTMP.DOFade(0f, 0.8f).SetEase(Ease.InQuad).SetDelay(0.2f);
        }
        yield return new WaitForSeconds(1f);

        if (countdownTipTMP != null) countdownTipTMP.gameObject.SetActive(false);
        if (countdownNumberTMP != null) countdownNumberTMP.gameObject.SetActive(false);

        SetState(GameState.Gameplay);
    }

    private void UpdateGameplayTimerUI(float time)
    {
        if (gameTimerTMP != null)
        {
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            gameTimerTMP.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    private IEnumerator GameOverSequenceRoutine()
    {
        SetState(GameState.GameOver);

        // 立即隐藏销毁场景中所有活动的弹幕
        if (PopupManager.Instance != null)
        {
            PopupManager.Instance.HideAllPopupsImmediately();
        }

        // Show and fade in overlay filter (蓝紫色半透明滤镜)
        if (overlayFilter != null)
        {
            overlayFilter.gameObject.SetActive(true);
            overlayFilter.DOKill();
            overlayFilter.color = new Color(0.3f, 0.2f, 0.6f, 0f);
            overlayFilter.DOColor(new Color(0.3f, 0.2f, 0.6f, 0.5f), characterFadeDuration).SetEase(Ease.OutQuad);
        }

        // Player character freeze-frame (定格当前游戏画面，完全停止移动且不修改位置)
        if (playerCharacter != null)
        {
            playerCharacter.transform.DOKill();
            var pc = playerCharacter.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.speed = 0f;
                //pc.enabled = false;
                if (pc.pathFollower != null)
                {
                    pc.pathFollower.speed = 0f;
                    pc.pathFollower.enabled = false;
                }
                if (pc.PlayerMesh != null)
                {
                    pc.PlayerMesh.DOKill();
                }
            }
        }

        // 保持主相机固定在当前画面位置
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.DOKill();
        }

        // FINISH text fade in (FINISH慢慢浮现)
        if (finishImagePopup != null)
        {
            finishImagePopup.SetActive(true);
            finishImagePopup.transform.DOKill();
            finishImagePopup.transform.localScale = Vector3.zero;
            
            if (finishCanvasGroup != null)
            {
                finishCanvasGroup.DOKill();
                finishCanvasGroup.alpha = 0f;
                finishCanvasGroup.DOFade(1f, 0.8f).SetEase(Ease.OutQuad).SetUpdate(true);
            }
            
            finishImagePopup.transform.DOScale(1f, 0.8f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(2f);

        if (finishImagePopup != null)
            finishImagePopup.SetActive(false);

        if (overlayFilter != null)
            overlayFilter.gameObject.SetActive(false);

        SetState(GameState.GameOverPage);
    }

    private IEnumerator GameOverPageRoutine()
    {
        float timer = gameOverCountdownDuration;
        while (timer > 0)
        {
            if (gameOverCountdownTMP != null)
            {
                gameOverCountdownTMP.text = ((int)timer).ToString();
            }

            if (offlineCountdownTMP != null)
            {
                var tmpComponent = offlineCountdownTMP.GetComponent<TextMeshProUGUI>();
                if (tmpComponent != null)
                {
                    tmpComponent.text = ((int)timer).ToString();
                }
            }

            // Check if cancel/B key is pressed to return instantly
            if (cancelAction.triggered)//按下按键b返回到主页，修改为confirmAction.triggeed即可改为按键a返回主页
            {
                TriggerReload();
                yield break;
            }

            timer -= Time.deltaTime;
            yield return null;
        }

        TriggerReload();
    }

    public void QuitToGameOver()
    {
        StopStateCoroutines();
        gameOverSequenceCoroutine = StartCoroutine(QuitToGameOverRoutine());
    }

    private IEnumerator QuitToGameOverRoutine()
    {
        SetState(GameState.GameOver);

        // Show and fade in overlay filter (蓝紫色半透明滤镜)
        if (overlayFilter != null)
        {
            overlayFilter.gameObject.SetActive(true);
            overlayFilter.DOKill();
            overlayFilter.color = new Color(0.3f, 0.2f, 0.6f, 0f);
            overlayFilter.DOColor(new Color(0.3f, 0.2f, 0.6f, 0.5f), characterFadeDuration).SetEase(Ease.OutQuad);
        }

        // Player character freeze-frame (保持游戏结束瞬间画面，停止移动)
        if (playerCharacter != null)
        {
            playerCharacter.transform.DOKill();
        }

        // FINISH text fade in (FINISH慢慢浮现)
        if (finishImagePopup != null)
        {
            if (PopupManager.Instance != null)
            {
                PopupManager.Instance.HideAllMainPopupsQuickly();
            }
            finishImagePopup.SetActive(true);
            finishImagePopup.transform.DOKill();
            finishImagePopup.transform.localScale = Vector3.zero;
            
            if (finishCanvasGroup != null)
            {
                finishCanvasGroup.DOKill();
                finishCanvasGroup.alpha = 0f;
                finishCanvasGroup.DOFade(1f, 0.8f).SetEase(Ease.OutQuad);
            }
            
            finishImagePopup.transform.DOScale(1f, 0.8f).SetEase(Ease.OutBack);
        }

        yield return new WaitForSeconds(2f);

        if (finishImagePopup != null)
            finishImagePopup.SetActive(false);

        if (overlayFilter != null)
            overlayFilter.gameObject.SetActive(false);

        SetState(GameState.GameOverPage);
    }

    private void TriggerReload()
    {
        // Keep OFFLINE mode if already in OFFLINE mode. Only reset to ONLINE if not in OFFLINE mode.
        if (PlayerPrefs.GetString("GameMode", "ONLINE") != "OFFLINE")
        {
            PlayerPrefs.SetString("GameMode", "ONLINE");
            PlayerPrefs.Save();
        }

        if (sceneReLoader != null)
        {
            sceneReLoader.Reload();
        }
        else
        {
            var loader = FindObjectOfType<SceneReLoad>();
            if (loader != null)
            {
                loader.Reload();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            }
        }
    }

    public void AddScore(int amount, bool isBonus = false)
    {
        int prevScore = gameScore;
        bool doubleActive = false;
        if (GameMechanicsManager.Instance != null)
        {
            doubleActive = GameMechanicsManager.Instance.IsDoubleScoreActive();
            amount = GameMechanicsManager.Instance.ModifyScoreAmount(amount);
        }
        CurrentScore += amount;
        if (CurrentScore < 0) CurrentScore = 0;

        gameScore += amount;
        if (gameScore < 0) gameScore = 0;

        // Print logging with high visibility only if it is a bonus/special score and logging is enabled in Mechanics Manager
        if (isBonus && GameMechanicsManager.Instance != null && GameMechanicsManager.Instance.logScoreChanges)
        {
            string sign = (amount >= 0) ? "+" : "";
            string multiplierStr = (doubleActive && amount > 0) ? " (Double Buff x2 Active!)" : "";
            Debug.Log($"[Score Update] Score change: {sign}{amount}{multiplierStr}. New Total Score: {gameScore} (Previous: {prevScore})");
        }

        if (scoreNumTMP != null)
            scoreNumTMP.text = CurrentScore.ToString();

        // Display every positive score gain with a cloned coin popup at the cat's head.
        if (amount > 0 && PopupManager.Instance != null)
        {
            PopupManager.Instance.ShowScoreCoinPopup(amount);
        }

        // 每次非里程碑的额外加分（如击碎方块的+10或+20，排除普通金币的+1或双倍+2）都通过猫咪头顶的弹幕显示
        if (amount > 0 && !isBonus && amount != 1 && amount != 2)
        {
            if (PopupManager.Instance != null)
            {
                PopupManager.Instance.ShowPopup("+" + amount);
            }
        }
    }

    /// <summary>
    /// 使用 Canvas 下的 image-black bg 进行经典黑幕渐隐渐显过渡
    /// 流程：黑幕淡入(0->1) 覆盖画面 ➔ 幕后切换回调 ➔ 黑幕淡出(1->0) 露出新画面
    /// </summary>
    public void PerformBlackFadeTransition(System.Action onBlackout, float duration = 0.4f)
    {
     

        if (imageBlackBg == null)
        {
            onBlackout?.Invoke();
            return;
        }

        imageBlackBg.SetActive(true);
        imageBlackBg.transform.SetAsLastSibling();

        var bgImage = imageBlackBg.GetComponent<Image>();
        var cg = imageBlackBg.GetComponent<CanvasGroup>();
        if (cg == null) cg = imageBlackBg.gameObject.AddComponent<CanvasGroup>();

        cg.DOKill();
        if (bgImage != null)
        {
            bgImage.DOKill();
            bgImage.color = new Color(0f, 0f, 0f, 1f);
        }

        cg.alpha = 0f;
        float halfDuration = duration * 0.5f;

        // Sequence: 黑幕渐显(0->1) ➔ 幕后切换回调 ➔ 黑幕渐隐(1->0)
        Sequence fadeSeq = DOTween.Sequence().SetLink(imageBlackBg).SetUpdate(true);
        fadeSeq.Append(cg.DOFade(1f, halfDuration).SetEase(Ease.OutQuad).SetUpdate(true));

        fadeSeq.AppendCallback(() => {
            onBlackout?.Invoke();
        });

        fadeSeq.Append(cg.DOFade(0f, halfDuration).SetEase(Ease.InQuad).SetUpdate(true));
        fadeSeq.OnComplete(() => {
            imageBlackBg.SetActive(false);
        });
    }

    public void ConfigureStartPageElements()
    {
        bool isOffline = (ActiveGameMode == "OFFLINE");
        enableConfirmToStart = isOffline;

        // 离线画面 (Image-offline) 稳定设置
        if (startPageImageOffline != null)
        {         
            startPageImageOffline.SetActive(isOffline);
        }

        // 在线画面 (Image-online) 稳定设置
        if (startPageImageOnline != null)
        {            
            startPageImageOnline.SetActive(!isOffline);
        }

        // 二维码 (RawImage-qrcore) 稳定设置
        if (startPageQrCore != null)
        {
            var cg = startPageQrCore.GetComponent<CanvasGroup>();
            if (cg != null) { cg.DOKill(); cg.alpha = 1f; }
            startPageQrCore.SetActive(!isOffline);
        }
    }


    private void SaveOriginalMaterials()//材质的备份
    {
        if (playerCharacter == null) return;
        savedMaterials.Clear();
        var renderers = playerCharacter.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            savedMaterials.Add(new SavedMaterialInfo
            {
                renderer = r,
                originalMaterials = r.sharedMaterials
            });
        }
    }

    private void RestoreOriginalMaterials()//材质的还原
    {
        foreach (var info in savedMaterials)
        {
            if (info.renderer != null && info.originalMaterials != null)
            {
                info.renderer.sharedMaterials = info.originalMaterials;
            }
        }
    }

    /// <summary>
    /// 将 Inspector 中指定的 Mesh_Cat 和 SM_Hoverboard 的材质 BaseMap alpha
    /// 从 1 渐变为 0，实现渐隐消失效果。
    /// </summary>
    private IEnumerator FadeMeshCatAndHoverboard(float duration)//协程：角色渐隐消失
    {
        List<Renderer> targetRenderers = new List<Renderer>();
        if (meshCatRenderer != null) targetRenderers.Add(meshCatRenderer);
        if (hoverboardRenderer != null) targetRenderers.Add(hoverboardRenderer);

        if (targetRenderers.Count == 0) yield break;

        // 缓存所有实例材质，避免在动画循环中反复调用 renderer.materials 创建新实例
        List<Material[]> cachedMaterials = new List<Material[]>();

        // 为每个目标 Renderer 创建实例材质并切换到透明模式
        foreach (var renderer in targetRenderers)
        {
            Material[] mats = renderer.materials; // 创建实例材质（仅此一次）
            foreach (var mat in mats)
            {
                // HDRP Lit 透明模式设置
                mat.SetFloat("_SurfaceType", 1); // 1 = Transparent
                mat.SetFloat("_BlendMode", 0);   // 0 = Alpha
                mat.SetFloat("_AlphaCutoffEnable", 0);                
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_AlphaSrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_AlphaDstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0);
                mat.SetFloat("_ZTestDepthEqualForOpaque", 4); // 4 = LessEqual
                mat.SetFloat("_EnableBlendModePreserveSpecularLighting", 1);
                // HDRP transparent depth passes: keep the fading character in both depth passes.
                mat.SetFloat("_TransparentDepthPrepassEnable", 1);
                mat.SetFloat("_TransparentDepthPostpassEnable", 1);
                mat.SetShaderPassEnabled("TransparentDepthPrepass", true);
                mat.SetShaderPassEnabled("TransparentDepthPostpass", true);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_BLENDMODE_ALPHA");
                mat.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");

                // 确保初始 BaseColor alpha 为 1
                if (mat.HasProperty("_BaseColor"))
                {
                    Color baseColor = mat.GetColor("_BaseColor");
                    baseColor.a = 1f;
                    mat.SetColor("_BaseColor", baseColor);
                }
            }
            renderer.materials = mats; // 赋回实例材质
            cachedMaterials.Add(mats); // 缓存引用
        }

        // 渐变 alpha 从 1 到 0（使用缓存的材质引用，不再调用 renderer.materials）
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);

            foreach (var mats in cachedMaterials)
            {
                foreach (var mat in mats)
                {
                    if (mat != null && mat.HasProperty("_BaseColor"))
                    {
                        Color c = mat.GetColor("_BaseColor");
                        c.a = alpha;
                        mat.SetColor("_BaseColor", c);
                    }
                }
            }
            yield return null;
        }

        // 最终确保 alpha 为 0
        foreach (var mats in cachedMaterials)
        {
            foreach (var mat in mats)
            {
                if (mat != null && mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor");
                    c.a = 0f;
                    mat.SetColor("_BaseColor", c);
                }
            }
        }
    }

    /// <summary>
    /// 恢复 Mesh_Cat 和 SM_Hoverboard 材质为 Opaque 且 alpha 完全不透明。
    /// </summary>
    private void RestoreMeshAlpha()
    {
        RestoreRendererToOpaque(meshCatRenderer);
        RestoreRendererToOpaque(hoverboardRenderer);
    }

    private void RestoreRendererToOpaque(Renderer r)
    {
        if (r == null) return;

        foreach (var mat in r.sharedMaterials)
        {
            if (mat == null) continue;

            // 切换 SurfaceType 为 Opaque
            mat.SetFloat("_SurfaceType", 0); // 0 = Opaque
            mat.SetFloat("_BlendMode", 0);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetFloat("_AlphaSrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_AlphaDstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetFloat("_ZWrite", 1);
            mat.SetFloat("_ZTestDepthEqualForOpaque", 3); // 3 = Equal (HDRP Opaque default)
            mat.SetFloat("_TransparentDepthPrepassEnable", 0);
            mat.SetFloat("_TransparentDepthPostpassEnable", 0);
            mat.SetShaderPassEnabled("TransparentDepthPrepass", false);
            mat.SetShaderPassEnabled("TransparentDepthPostpass", false);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_BLENDMODE_ALPHA");
            mat.DisableKeyword("_ENABLE_FOG_ON_TRANSPARENT");

            // 恢复 BaseColor alpha 为 1
            if (mat.HasProperty("_BaseColor"))
            {
                Color c = mat.GetColor("_BaseColor");              
                    c.a = 1f;
                    mat.SetColor("_BaseColor", c);                
            }
        }
    }
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        // Save enableDebugMode from Inspector to PlayerPrefs so it persists across runtime scene reloads
        PlayerPrefs.SetInt("EnableDebugMode", enableDebugMode ? 1 : 0);
        PlayerPrefs.Save();
    }
#endif
}
