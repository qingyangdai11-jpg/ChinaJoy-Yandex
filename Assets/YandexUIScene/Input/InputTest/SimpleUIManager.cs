using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SimpleUIManager : MonoBehaviour
{
    public static SimpleUIManager Instance { get; private set; }

    [Header("Menu UI Canvas")]
    public GameObject menuCanvasObject;

    [Header("Buttons")]
    public Button resumeButton;
    public Button quitButton;
    public Button controlsTutorialButton;

    [Header("Controls Tutorial Panel")]
    public GameObject controlsTutorialPanel;

    [Header("Pause Panel Text")]
    [Tooltip("PausePanel 下的 Text 游戏物体")]
    public GameObject pausePanelText;

    [Header("Default Selected Button (for Gamepad)")]
    public Button defaultSelectedButton;

    private InputAction pauseAction;
    private InputAction confirmAction;
    private bool isPaused = false;
    private bool isShowingTutorial = false;
    private bool isToggling = false;
    private GameObject lastSelectedGameObject;

    public bool IsPaused => isPaused;
    public static int LastResumeFrame { get; private set; } = -1;
    public static float LastResumeTime { get; private set; } = -100f;

    private void Awake()
    {
        // Singleton pattern setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Configure input action for Escape key and Gamepad Start button
        pauseAction = new InputAction("Pause", type: InputActionType.Button);
        pauseAction.AddBinding("<Keyboard>/escape");
        pauseAction.AddBinding("<Gamepad>/start");

        pauseAction.performed += ctx => TogglePause();

        // Configure input action for confirm key (Space, Enter, Gamepad A)
        confirmAction = new InputAction("MenuConfirm", type: InputActionType.Button);
        confirmAction.AddBinding("<Keyboard>/space");
        confirmAction.AddBinding("<Keyboard>/enter");
        confirmAction.AddBinding("<Keyboard>/numpadEnter");
        confirmAction.AddBinding("<Gamepad>/buttonSouth");

        confirmAction.performed += ctx => OnConfirmPressed();

        // Register button callbacks
        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeGame);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        if (controlsTutorialButton != null)
            controlsTutorialButton.onClick.AddListener(ToggleControlsTutorial);

        // Make sure menu is closed and time is running on start
        ResumeGame(); 

        // Hide controls tutorial panel on start
        if (controlsTutorialPanel != null)
            controlsTutorialPanel.SetActive(false);
    }

    private void Start()
    {
        // Auto-find controls tutorial panel if not assigned
        if (controlsTutorialPanel == null && menuCanvasObject != null)
        {
            Transform found = menuCanvasObject.transform.Find("ControlsTutorialPanel");
            if (found != null)
            {
                controlsTutorialPanel = found.gameObject;
            }
        }

        // Auto-find controls tutorial button if not assigned
        if (controlsTutorialButton == null && menuCanvasObject != null)
        {
            Button[] buttons = menuCanvasObject.GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                if (btn.gameObject.name.ToLower().Contains("tutorial") || 
                    btn.gameObject.name.ToLower().Contains("control"))
                {
                    controlsTutorialButton = btn;
                    break;
                }
            }
        }

        // Re-register button click in Start as a safety measure
        if (controlsTutorialButton != null)
        {
            controlsTutorialButton.onClick.AddListener(ToggleControlsTutorial);
        }

        // Ensure tutorial panel is hidden on start
        if (controlsTutorialPanel != null)
        {
            controlsTutorialPanel.SetActive(false);
        }

        // Auto-find pause panel text if not assigned
        if (pausePanelText == null && menuCanvasObject != null)
        {
            Transform pausePanel = menuCanvasObject.transform.Find("PausePanel");
            if (pausePanel != null)
            {
                Transform foundText = pausePanel.Find("Text (TMP)");
                if (foundText != null)
                {
                    pausePanelText = foundText.gameObject;
                }
            }
        }
    }

    private void OnEnable()
    {
        pauseAction.Enable();
        confirmAction?.Enable();
    }

    private void OnDisable()
    {
        pauseAction.Disable();
        confirmAction?.Disable();
    }

    private void Update()
    {
        if (isPaused)
        {
            // Ensure tutorial panel stays hidden when not showing tutorial
            if (!isShowingTutorial && controlsTutorialPanel != null && controlsTutorialPanel.activeSelf)
            {
                controlsTutorialPanel.SetActive(false);
            }

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
                    // If selection was lost (e.g. clicked empty space), restore it
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

    public void TogglePause()
    {
        if (GameFlowController.Instance != null && GameFlowController.Instance.CurrentState != GameFlowController.GameState.Gameplay)
        {
            return;
        }

        // Block pause menu if the online/connection status panel is active
        if (YandexWebSocketClient.Instance != null && YandexWebSocketClient.Instance.gameObject.activeInHierarchy && !YandexWebSocketClient.Instance.IsCanvasClosed)
        {
            return;
        }

        // Block pause menu if the game mode selection page is active
        if (GameModeSelector.Instance != null && GameModeSelector.Instance.IsOpen)
        {
            return;
        }

        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    // Hide pause menu canvas without altering timeScale or isPaused state
    public void HidePauseMenuCanvas()
    {
        if (menuCanvasObject != null)
            menuCanvasObject.SetActive(false);
    }

    // Re-show pause menu canvas if game is still paused
    public void ShowPauseMenuCanvas()
    {
        if (isPaused && menuCanvasObject != null)
        {
            menuCanvasObject.SetActive(true);

            // Re-select the last active button or fallback to default
            if (lastSelectedGameObject != null && lastSelectedGameObject.activeInHierarchy)
            {
                var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                if (eventSystem != null)
                {
                    eventSystem.SetSelectedGameObject(lastSelectedGameObject);
                    var button = lastSelectedGameObject.GetComponent<Button>();
                    if (button != null) button.Select();
                }
            }
            else if (defaultSelectedButton != null)
            {
                defaultSelectedButton.Select();
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(defaultSelectedButton.gameObject);
                }
            }
        }
    }

    // Temporarily disable pause and confirm actions to prevent same-frame input trigger conflicts
    public void DisableInputs()
    {
        pauseAction?.Disable();
        confirmAction?.Disable();
    }

    // Re-enable pause and confirm actions on the next frame to prevent same-frame conflicts
    public void EnableInputs()
    {
        StartCoroutine(EnableInputsNextFrame());
    }

    private System.Collections.IEnumerator EnableInputsNextFrame()
    {
        yield return null; // Wait for next frame
        if (enabled && gameObject.activeInHierarchy)
        {
            pauseAction?.Enable();
            confirmAction?.Enable();
        }
    }

    private void OnConfirmPressed()
    {
        if (isPaused)
        {
            if (isShowingTutorial)
            {
                ToggleControlsTutorial();
            }
            else if (controlsTutorialButton != null)
            {
                var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                if (eventSystem != null && eventSystem.currentSelectedGameObject == controlsTutorialButton.gameObject)
                {
                    ToggleControlsTutorial();
                }
            }
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; // Pause physics and time-based updates
        AudioListener.pause = true; // 游戏暂停：瞬间同步暂停全局背景音乐与所有音效

        if (menuCanvasObject != null)
            menuCanvasObject.SetActive(true);

        // Hide controls tutorial panel when showing main menu
        if (controlsTutorialPanel != null)
            controlsTutorialPanel.SetActive(false);

        isShowingTutorial = false;

        // Ensure PausePanel's text is visible on pause
        if (pausePanelText != null)
            pausePanelText.SetActive(true);

        // Select the default button for gamepad/keyboard UI navigation
        if (defaultSelectedButton != null)
        {
            defaultSelectedButton.Select();
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(defaultSelectedButton.gameObject);
            }
            lastSelectedGameObject = defaultSelectedButton.gameObject;
        }

        Debug.Log("SimpleUIManager: Game Paused");
    }

    public void ResumeGame()
    {
        isPaused = false;
        LastResumeFrame = Time.frameCount;
        LastResumeTime = Time.unscaledTime;
        Time.timeScale = 1f; // Resume physics and time-based updates
        AudioListener.pause = false; // 恢复游戏：瞬间无缝恢复播放背景音乐与音效

        if (menuCanvasObject != null)
            menuCanvasObject.SetActive(false);

        if (controlsTutorialPanel != null)
            controlsTutorialPanel.SetActive(false);

        isShowingTutorial = false;
        lastSelectedGameObject = null;

        // Reset PausePanel's text state to visible
        if (pausePanelText != null)
            pausePanelText.SetActive(true);

        Debug.Log("SimpleUIManager: Game Resumed");
    }

    public void QuitGame()
    {
        ResumeGame();

        Debug.Log("SimpleUIManager: Quit to GameOver sequence...");
        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.QuitToGameOver();
        }
    }

    public void ToggleControlsTutorial()
    {
        if (isToggling) return;
        isToggling = true;

        if (isShowingTutorial)
        {
            HideControlsTutorial();
        }
        else
        {
            ShowControlsTutorial();
        }

        isToggling = false;
    }

    private void ShowControlsTutorial()
    {
        isShowingTutorial = true;

        // Remember the currently selected button before showing tutorial
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
        {
            lastSelectedGameObject = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        }

        GameObject panel = controlsTutorialPanel;

        // If inspector reference is null, search for it
        if (panel == null)
        {
            // First, try to find it under menuCanvasObject
            if (menuCanvasObject != null)
            {
                Transform[] allChildren = menuCanvasObject.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allChildren)
                {
                    if (t.name == "ControlsTutorialPanel")
                    {
                        panel = t.gameObject;
                        break;
                    }
                }
            }

            // If still not found, search the whole scene
            if (panel == null)
            {
                GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    if (obj != null && obj.name == "ControlsTutorialPanel" && obj.scene.IsValid())
                    {
                        panel = obj;
                        break;
                    }
                }
            }

            if (panel != null)
            {
                controlsTutorialPanel = panel;
            }
            else
            {
                Debug.LogWarning("SimpleUIManager: Could not find ControlsTutorialPanel in scene! Make sure a GameObject named 'ControlsTutorialPanel' exists in the scene.");
                return;
            }
        }

        // Activate panel and bring to front
        controlsTutorialPanel.SetActive(true);

        // Move to the end of the parent's children to render on top
        controlsTutorialPanel.transform.SetAsLastSibling();

        // Hide PausePanel's text when showing controls tutorial
        if (pausePanelText != null)
            pausePanelText.SetActive(false);

        Debug.Log($"SimpleUIManager: Controls Tutorial shown. Panel active: {controlsTutorialPanel.activeSelf}, name: {controlsTutorialPanel.name}");
    }

    public void HideControlsTutorial()
    {
        isShowingTutorial = false;
        if (controlsTutorialPanel != null)
            controlsTutorialPanel.SetActive(false);

        // Show PausePanel's text when hiding controls tutorial
        if (pausePanelText != null)
            pausePanelText.SetActive(true);

        if (isPaused)
        {
            // Clear current selection first to remove highlight from previous button
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }

            // Restore to the last selected button before tutorial was opened
            GameObject buttonToSelect = null;
            if (lastSelectedGameObject != null && lastSelectedGameObject.activeInHierarchy)
            {
                buttonToSelect = lastSelectedGameObject;
            }
            else if (controlsTutorialButton != null)
            {
                buttonToSelect = controlsTutorialButton.gameObject;
            }
            else if (defaultSelectedButton != null)
            {
                buttonToSelect = defaultSelectedButton.gameObject;
            }

            if (buttonToSelect != null)
            {
                var button = buttonToSelect.GetComponent<Button>();
                if (button != null) button.Select();
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(buttonToSelect);
                }
                lastSelectedGameObject = buttonToSelect;
            }
        }

        Debug.Log("SimpleUIManager: Controls Tutorial hidden");
    }
}
