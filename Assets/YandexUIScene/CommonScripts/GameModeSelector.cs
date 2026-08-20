using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameModeSelector : MonoBehaviour
{
    public static GameModeSelector Instance { get; private set; }

    [Header("Selector UI Canvas")]
    public GameObject selectorCanvasObject;

    [Header("Buttons")]
    public Button onlineButton;
    public Button offlineButton;

    [Header("Default Selected Button (for Gamepad)")]
    public Button defaultSelectedButton;

    private InputAction menuAction;
    private InputAction closeAction;
    private bool isOpen = false;
    private GameObject lastSelectedGameObject;
    private float previousTimeScale = 1f;

    // Five press detection
    private float lastMenuPressTime = 0f;
    private int menuPressCount = 0;
    private const float menuPressTimeout = 1.0f;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (GameFlowController.IsFirstBoot)
        {
            GameFlowController.IsFirstBoot = false;
            PlayerPrefs.SetString("GameMode", "ONLINE");
            PlayerPrefs.Save();
        }

        // Singleton pattern setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Fetch current game mode from PlayerPrefs, defaulting to ONLINE
        string currentMode = PlayerPrefs.GetString("GameMode", "ONLINE");
        string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        Debug.Log($"[GameModeSelector] Awake called. Active Scene: '{activeSceneName}', Saved Mode: '{currentMode}'");

        // Configure input action for Escape key and Gamepad Start button (Menu key)
        menuAction = new InputAction("MenuTrigger", type: InputActionType.Button);
        menuAction.AddBinding("<Keyboard>/escape");
        menuAction.AddBinding("<Gamepad>/start");
        menuAction.performed += ctx => OnMenuPressed();

        // Configure close action for Backspace and Gamepad B button (Cancel key)
        closeAction = new InputAction("CloseTrigger", type: InputActionType.Button);
        closeAction.AddBinding("<Keyboard>/backspace");
        closeAction.AddBinding("<Gamepad>/buttonEast");
        closeAction.performed += ctx => OnClosePressed();

        // Register button callbacks
        if (onlineButton != null)
            onlineButton.onClick.AddListener(SelectOnline);

        if (offlineButton != null)
            offlineButton.onClick.AddListener(SelectOffline);

        // Make sure the selector canvas is hidden on start
        if (selectorCanvasObject != null)
            selectorCanvasObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (menuAction != null)
        {
            menuAction.Enable();
            Debug.Log("[GameModeSelector] menuAction enabled.");
        }
        if (closeAction != null)
        {
            closeAction.Enable();
            Debug.Log("[GameModeSelector] closeAction enabled.");
        }
    }

    private void OnDisable()
    {
        if (menuAction != null) menuAction.Disable();
        if (closeAction != null) closeAction.Disable();
        Debug.Log("[GameModeSelector] Actions disabled.");
    }

    private void Update()
    {
        if (isOpen)
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null)
            {
                if (eventSystem.currentSelectedGameObject != null)
                {
                    // Remember the last active selection
                    lastSelectedGameObject = eventSystem.currentSelectedGameObject;
                }
                else
                {
                    // Restore focus if lost (e.g., clicked empty screen area)
                    if (lastSelectedGameObject != null && lastSelectedGameObject.activeInHierarchy)
                    {
                        eventSystem.SetSelectedGameObject(lastSelectedGameObject);
                    }
                    else if (defaultSelectedButton != null)
                    {
                        defaultSelectedButton.Select();
                        eventSystem.SetSelectedGameObject(defaultSelectedButton.gameObject);
                    }
                }
            }
        }
    }

    private void OnMenuPressed()
    {
        bool hasController = GameFlowController.Instance != null;
        string stateStr = hasController ? GameFlowController.Instance.CurrentState.ToString() : "NULL";
        Debug.Log($"[GameModeSelector] Menu key pressed! IsOpen: {isOpen}, GameFlowController: {(hasController ? "Found" : "NULL")}, CurrentState: {stateStr}");

        if (isOpen)
        {
            CloseSelector();
        }
        else
        {
            // Detect 5 presses within the timeout window
            float timeSinceLast = Time.unscaledTime - lastMenuPressTime;
            if (timeSinceLast < menuPressTimeout)
            {
                menuPressCount++;
            }
            else
            {
                menuPressCount = 1;
            }
            lastMenuPressTime = Time.unscaledTime;

            Debug.Log($"[GameModeSelector] Press registered. Count: {menuPressCount}/5, Time since last: {timeSinceLast:F3}s");

            if (menuPressCount >= 5)
            {
                OpenSelector();
                menuPressCount = 0; // Reset count
            }
        }
    }

    private void OnClosePressed()
    {
        if (isOpen)
        {
            CloseSelector();
        }
    }

    public void OpenSelector()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isOpen = true;
        if (selectorCanvasObject != null)
            selectorCanvasObject.SetActive(true);

        // Hide pause menu canvas and temporarily disable its physical input to avoid overlap and conflict
        if (SimpleUIManager.Instance != null && SimpleUIManager.Instance.IsPaused)
        {
            SimpleUIManager.Instance.HidePauseMenuCanvas();
            SimpleUIManager.Instance.DisableInputs();
        }

        // Fetch current game mode from PlayerPrefs, defaulting to ONLINE
        string currentMode = PlayerPrefs.GetString("GameMode", "ONLINE");
        Button buttonToSelect = (currentMode == "OFFLINE" && offlineButton != null) ? offlineButton : onlineButton;

        if (buttonToSelect == null)
            buttonToSelect = defaultSelectedButton;

        // Select the appropriate button for keyboard/gamepad navigation
        if (buttonToSelect != null)
        {
            buttonToSelect.Select();
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(buttonToSelect.gameObject);
            }
            lastSelectedGameObject = buttonToSelect.gameObject;
        }

        Debug.Log("GameModeSelector: Opened selection page");
    }

    public void CloseSelector()
    {
        Time.timeScale = previousTimeScale;
        isOpen = false;
        if (selectorCanvasObject != null)
            selectorCanvasObject.SetActive(false);

        lastSelectedGameObject = null;
        Debug.Log("GameModeSelector: Closed selection page");

        // Re-enable input and show pause menu canvas if the game state is still paused
        if (SimpleUIManager.Instance != null && SimpleUIManager.Instance.IsPaused)
        {
            SimpleUIManager.Instance.ShowPauseMenuCanvas();
            SimpleUIManager.Instance.EnableInputs();
        }
    }

    public void SelectOnline()
    {
        YandexWebSocketClient.Instance.gameObject.SetActive(true);

        string previousMode = PlayerPrefs.GetString("GameMode", "ONLINE");
        bool isFromOffline = (previousMode == "OFFLINE");

        PlayerPrefs.SetString("GameMode", "ONLINE");
        PlayerPrefs.Save();
        Debug.Log($"GameModeSelector: Mode set to ONLINE (isFromOffline: {isFromOffline}). Triggering blackout transition...");


        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.PerformBlackFadeTransition(() => {
                GameFlowController.Instance.ActiveGameMode = "ONLINE";
                GameFlowController.Instance.ConfigureStartPageElements();

                if (PageSwitcher.Instance != null && PageSwitcher.Instance.pages != null && PageSwitcher.Instance.pages.Length > 5 && PageSwitcher.Instance.pages[5] != null)
                {
                    PageSwitcher.Instance.UpdatePanel5ModeVisibility(PageSwitcher.Instance.pages[5]);
                }

                // 从离线切在线：建立连接并显示链接成功/失败提示
                // 从在线切在线：不需要重新连接，保持静默连接
                if (YandexWebSocketClient.Instance != null)
                {
                    YandexWebSocketClient.Instance.ConnectWithUIStatus(isFromOffline);
                }

                if (YandexQRCodeGenerator.Instance != null)
                {
                    YandexQRCodeGenerator.Instance.StartQRGenerator();
                }

                // 每次进入在线模式，对排行榜强制进行瀑布式刷新
                if (YandexLeaderboardManager.Instance != null)
                {
                    YandexLeaderboardManager.Instance.TriggerWaterfallRefresh();
                }

                GameFlowController.Instance.SetState(GameFlowController.GameState.Idle);
            }, 0.4f);
        }

        CloseSelector();

    }

    public void SelectOffline()
    {
        YandexWebSocketClient.Instance.gameObject.SetActive(false);

        //Time.timeScale = 1f;
        PlayerPrefs.SetString("GameMode", "OFFLINE");
        PlayerPrefs.Save();
        Debug.Log("GameModeSelector: Mode set to OFFLINE. Updating in-place with smooth blackout transition...");

        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.PerformBlackFadeTransition(() => {
                GameFlowController.Instance.ActiveGameMode = "OFFLINE";
                GameFlowController.Instance.ConfigureStartPageElements();

                if (PageSwitcher.Instance != null && PageSwitcher.Instance.pages != null && PageSwitcher.Instance.pages.Length > 5 && PageSwitcher.Instance.pages[5] != null)
                {
                    PageSwitcher.Instance.UpdatePanel5ModeVisibility(PageSwitcher.Instance.pages[5]);
                }

                if (YandexQRCodeGenerator.Instance != null)
                {
                    YandexQRCodeGenerator.Instance.StopQRGenerator();
                }

                if (YandexLeaderboardManager.Instance != null)
                {
                    YandexLeaderboardManager.Instance.StopFetchingAndHide();
                }

                GameFlowController.Instance.SetState(GameFlowController.GameState.Idle);
            }, 0.4f);
        }

        CloseSelector();
        Time.timeScale = 1f;

        //Destroy(YandexWebSocketClient.Instance.gameObject);
    }

    private void TriggerSceneLoad()
    {
        var loader = FindObjectOfType<SceneReLoad>();
        if (loader != null)
        {
            loader.Reload();
        }
        else
        {
            string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            UnityEngine.SceneManagement.SceneManager.LoadScene(activeSceneName);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
