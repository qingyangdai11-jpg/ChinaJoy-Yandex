using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Best.WebSockets;

public class YandexWebSocketClient : MonoBehaviour
{
    public static YandexWebSocketClient Instance { get; private set; }

    [Header("Connection Settings")]
    [Tooltip("Leave empty to auto-generate based on machine unique ID")]
    [SerializeField] private string machineId = "";
    [SerializeField] private bool enableDebugLogs = true;

    [Header("UI References")]
    public Canvas wssCanvas;
    public TextMeshProUGUI titleTMP;
    public TextMeshProUGUI contentTMP;
    public TextMeshProUGUI reconnectingTMP;

    [System.Serializable]
    public class GameSessionData
    {
        public string sessionId = "";
        public string name = "";
        public string phone = "";
    }

    [Header("Current Session Info")]
    public GameSessionData currentSessionData;

    public string MachineId => machineId;
    public bool IsCanvasClosed => currentUIState == UIState.Hidden;
    public bool IsWebSocketOpen => webSocket != null && webSocket.IsOpen;

    private WebSocket webSocket;
    private Coroutine heartbeatCoroutine;
    private Coroutine autoReconnectCoroutine;
    private Coroutine reconnectingDotsCoroutine;
    private Coroutine transitionRoutine;

    private enum UIState
    {
        Hidden,
        FirstConnecting,
        ErrorReconnecting,
        ConnectedSuccess
    }

    private UIState currentUIState = UIState.Hidden;
    private string lastErrorMessage = "";
    private static bool isFirstTimeWSSConnect = true;
    private float lastReceivedMessageTime = 0f;

    // 修复：防止主动关闭时触发错误重连
    private bool isIntentionalClose = false;
    // 修复：防止多个重连协程重叠启动
    private bool isReconnecting = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadConfig();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            if (wssCanvas != null && !wssCanvas.transform.IsChildOf(transform))
            {
                Instance.UpdateUIReferences(wssCanvas, titleTMP, contentTMP, reconnectingTMP);
            }
            else
            {
                if (enableDebugLogs)
                {
                    Debug.Log("[WSS] New Canvas is null or a child of the duplicate manager (will be destroyed). Retaining persistent Canvas references.");
                }
            }
            Destroy(gameObject);
            return;
        }
    }

    string currentMode;
    private void Start()
    {
        if (Instance != this) return;

        currentMode = PlayerPrefs.GetString("GameMode", "ONLINE");
        Debug.Log($"[WSS] Start called. Current GameMode: '{currentMode}', WebSocket is null: {webSocket == null}");
        if (currentMode == "ONLINE")
        {
            if (webSocket == null || !webSocket.IsOpen)
            {
                Connect();
            }
        }
        else
        {
            currentUIState = UIState.Hidden;
            ApplyCurrentUIState();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        string currentMode = PlayerPrefs.GetString("GameMode", "ONLINE");
        bool isOnline = (currentMode == "ONLINE");

        if (enableDebugLogs)
        {
            Debug.Log($"[WSS] Scene '{scene.name}' loaded. OnlineMode: {isOnline}");
        }

        if (isOnline)
        {
            if (webSocket == null || !webSocket.IsOpen)
            {
                Connect();
            }
            else
            {
                // 如果已经在后台连着了，在新场景载入时直接展示“连接成功”并渐隐
                StartTransition(TransitionToConnectedSuccessRoutine(false));
            }
        }
        else
        {
            CloseConnection();
            currentUIState = UIState.Hidden;
            ApplyCurrentUIState();
        }
    }

    [System.Serializable]
    private class AppConfig
    {
        public string machineId = "machine-1";
    }

    public void LoadConfig()
    {
        string configPath = System.IO.Path.Combine(Application.streamingAssetsPath, "config.json");
        if (System.IO.File.Exists(configPath))
        {
            try
            {
                string json = System.IO.File.ReadAllText(configPath);
                AppConfig config = JsonUtility.FromJson<AppConfig>(json);
                if (config != null && !string.IsNullOrEmpty(config.machineId))
                {
                    machineId = config.machineId;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[WSS] Loaded machineId from config.json: {machineId}");
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WSS] Error reading config.json: {ex.Message}");
            }
        }
        else
        {
            try
            {
                AppConfig defaultConfig = new AppConfig();
                defaultConfig.machineId = "machine-1";
                string json = JsonUtility.ToJson(defaultConfig, true);

                string directory = System.IO.Path.GetDirectoryName(configPath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                System.IO.File.WriteAllText(configPath, json);
                machineId = "machine-1";
                if (enableDebugLogs)
                {
                    Debug.Log($"[WSS] config.json not found. Created a default config file with machineId='machine-1' at: {configPath}");
                }
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WSS] Failed to create default config.json: {ex.Message}");
            }
        }

        InitializeMachineIdFallback();
    }

    private void InitializeMachineIdFallback()
    {
        if (string.IsNullOrEmpty(machineId))
        {
            string rawId = SystemInfo.deviceUniqueIdentifier;
            string lastSix = rawId.Length >= 6 ? rawId.Substring(rawId.Length - 6) : rawId;
            machineId = "machine-" + lastSix.ToUpper();
        }
        if (enableDebugLogs)
        {
            Debug.Log($"[WSS] Fallback Machine ID initialized as: {machineId}");
        }
    }

    public void UpdateUIReferences(Canvas canvas, TextMeshProUGUI title, TextMeshProUGUI content, TextMeshProUGUI reconnecting)
    {
        this.wssCanvas = canvas;
        this.titleTMP = title;
        this.contentTMP = content;
        this.reconnectingTMP = reconnecting;

        if (enableDebugLogs)
        {
            Debug.Log("[WSS] UI references updated.");
        }

        ApplyCurrentUIState();
    }

    public void Connect()
    {
        ConnectWithUIStatus(isFirstTimeWSSConnect);
    }

    public void ConnectWithUIStatus(bool showConnectionUI)
    {
        isIntentionalClose = false;

        if (webSocket != null && webSocket.IsOpen)
        {
            if (showConnectionUI)
            {
                StartTransition(TransitionToConnectedSuccessRoutine(true));
            }
            return;
        }

        CloseConnection();

        string url = $"wss://yandexa.bbtech.cc/ws?role=machine&machineId={machineId}";
        if (enableDebugLogs)
        {
            Debug.Log($"[WSS] Connecting to: {url}");
        }

        try
        {
            webSocket = new WebSocket(new Uri(url));
            webSocket.OnOpen += OnWebSocketOpen;
            webSocket.OnMessage += OnWebSocketMessage;
            webSocket.OnClosed += OnWebSocketClosed;
            webSocket.Open();

            if (showConnectionUI)
            {
                currentUIState = UIState.FirstConnecting;
                ApplyCurrentUIState();
            }
            // 修改：重连时保持 ErrorReconnecting，避免闪屏
            else if (currentUIState != UIState.ErrorReconnecting)
            {
                currentUIState = UIState.Hidden;
                ApplyCurrentUIState();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WSS] Exception during connect: {ex.Message}");
            HandleConnectionError(ex.Message);
        }
    }

    private void CloseConnection()
    {
        StopHeartbeat();
        StopAutoReconnect();

        if (webSocket != null)
        {
            try
            {
                isIntentionalClose = true;
                webSocket.OnOpen -= OnWebSocketOpen;
                webSocket.OnMessage -= OnWebSocketMessage;
                webSocket.OnClosed -= OnWebSocketClosed;
                webSocket.Close();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WSS] Exception during socket close: {ex.Message}");
            }
            webSocket = null;
        }
    }

    // 修复：增加实例校验，防止旧连接的延迟回调污染新连接
    private void OnWebSocketOpen(WebSocket ws)
    {
        if (this.webSocket != ws) return;

        lastReceivedMessageTime = Time.unscaledTime;
        if (enableDebugLogs)
        {
            Debug.Log("[WSS] WebSocket connection opened.");
        }

        StopAutoReconnect();
        StartHeartbeat();

        //if (!isFirstTimeWSSConnect || currentUIState == UIState.ErrorReconnecting)
        //{
        // StartTransition(TransitionToConnectedSuccessRoutine(false));
        //}
        StartTransition(TransitionToConnectedSuccessRoutine(isFirstTimeWSSConnect));
    }

    private void OnWebSocketMessage(WebSocket ws, string message)
    {
        if (this.webSocket != ws) return;

        lastReceivedMessageTime = Time.unscaledTime;
        if (enableDebugLogs)
        {
            Debug.Log($"[WSS] Server Message Received: {message}");
        }

        try
        {
            if (message.Contains("\"type\":\"connected\""))
            {
                if (isFirstTimeWSSConnect && currentUIState == UIState.FirstConnecting)
                {
                    StartTransition(DelayFirstTimeConnectedSuccessRoutine());
                }
            }
            else if (message.Contains("\"type\":\"start_game\""))
            {
                HandleStartGameMessage(message);
            }
            else if (message.Contains("\"type\":\"game_result\""))
            {
                if (YandexGameOverLeaderboard.Instance != null)
                {
                    YandexGameOverLeaderboard.Instance.DisplayGameResult(message);
                }
            }
            else if (message.Contains("\"type\":\"pong\""))
            {
                // Pong message processed
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WSS] Error parsing server message: {ex.Message}");
        }
    }

    private void OnWebSocketClosed(WebSocket ws, WebSocketStatusCodes code, string message)
    {
        // 修复：忽略非当前实例的旧连接回调
        if (this.webSocket != ws)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[WSS] Ignoring OnClosed from stale WebSocket instance.");
            }
            return;
        }

        Debug.LogWarning($"[WSS] Connection closed. Code: {code}, Message: {message}");
        webSocket = null;

        if (isIntentionalClose)
        {
            isIntentionalClose = false;
            return;
        }

        string currentMode = PlayerPrefs.GetString("GameMode", "ONLINE");
        if (currentMode != "ONLINE") return;

        HandleConnectionError($"Connection lost. Code: {code}");
    }

    private void StopTransition()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (wssCanvas != null)
        {
            CanvasGroup canvasGroup = wssCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
                canvasGroup.alpha = 1f;
            }
        }
    }

    private void HandleConnectionError(string errorDetail)
    {
        Debug.Log(errorDetail);
        StopTransition();

        lastErrorMessage = errorDetail;
        currentUIState = UIState.ErrorReconnecting;
        ApplyCurrentUIState();

        Time.timeScale = 0f;
        StartAutoReconnect();
    }

    private void ApplyCurrentUIState()
    {
        if (wssCanvas == null) return;

        CanvasGroup canvasGroup = wssCanvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = wssCanvas.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;

        switch (currentUIState)
        {
            case UIState.Hidden:
                wssCanvas.gameObject.SetActive(false);
                StopDotsAnimation();
                break;

            case UIState.FirstConnecting:
                wssCanvas.gameObject.SetActive(true);
                if (titleTMP != null) titleTMP.gameObject.SetActive(false);
                if (reconnectingTMP != null) reconnectingTMP.gameObject.SetActive(false);
                if (contentTMP != null)
                {
                    contentTMP.gameObject.SetActive(true);
                    contentTMP.text = "Connecting...";
                    contentTMP.color = Color.white;
                }
                StopDotsAnimation();
                break;

            case UIState.ErrorReconnecting:
                wssCanvas.gameObject.SetActive(true);
                if (titleTMP != null)
                {
                    titleTMP.gameObject.SetActive(true);
                    titleTMP.text = "Connection Error";
                }
                if (reconnectingTMP != null) reconnectingTMP.gameObject.SetActive(true);
                if (contentTMP != null)
                {
                    contentTMP.gameObject.SetActive(true);
                    contentTMP.text = lastErrorMessage;
                    contentTMP.color = Color.white;
                }
                StartDotsAnimation();
                break;

            case UIState.ConnectedSuccess:
                wssCanvas.gameObject.SetActive(true);
                if (titleTMP != null) titleTMP.gameObject.SetActive(false);
                if (reconnectingTMP != null) reconnectingTMP.gameObject.SetActive(false);
                if (contentTMP != null)
                {
                    contentTMP.gameObject.SetActive(true);
                    contentTMP.text = "Connected successfully";
                    contentTMP.color = Color.green;
                }
                StopDotsAnimation();
                break;
        }
    }

    private void StartDotsAnimation()
    {
        StopDotsAnimation();
        if (gameObject.activeInHierarchy)
        {
            reconnectingDotsCoroutine = StartCoroutine(ReconnectingDotsRoutine());
        }
    }

    private void StopDotsAnimation()
    {
        if (reconnectingDotsCoroutine != null)
        {
            StopCoroutine(reconnectingDotsCoroutine);
            reconnectingDotsCoroutine = null;
        }
    }

    private IEnumerator ReconnectingDotsRoutine()
    {
        int dotCount = 0;
        while (reconnectingTMP != null && reconnectingTMP.gameObject.activeInHierarchy)
        {
            string dots = new string('.', dotCount);
            reconnectingTMP.text = "Reconnecting" + dots;
            dotCount = (dotCount + 1) % 4;
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    private void StartTransition(IEnumerator routine)
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }
        transitionRoutine = StartCoroutine(routine);
    }

    private IEnumerator DelayFirstTimeConnectedSuccessRoutine()
    {
        yield return new WaitForSecondsRealtime(1f);
        yield return StartCoroutine(TransitionToConnectedSuccessRoutine(true));
    }

    private IEnumerator TransitionToConnectedSuccessRoutine(bool isFirstTime)
    {
        currentUIState = UIState.ConnectedSuccess;
        ApplyCurrentUIState();

        if (isFirstTime)
        {
            isFirstTimeWSSConnect = false;
        }

        yield return new WaitForSecondsRealtime(2f);

        if (wssCanvas != null)
        {
            CanvasGroup canvasGroup = wssCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                yield return canvasGroup.DOFade(0f, 0.5f).SetUpdate(true).WaitForCompletion();
            }
            wssCanvas.gameObject.SetActive(false);
        }

        currentUIState = UIState.Hidden;
        ApplyCurrentUIState();

        Time.timeScale = 1f;
    }

    private void StartHeartbeat()
    {
        StopHeartbeat();
        if (gameObject.activeInHierarchy)
        {
            heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());
        }
    }

    private void StopHeartbeat()
    {
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
        }
    }

    private IEnumerator HeartbeatRoutine()
    {
        while (webSocket != null && webSocket.IsOpen)
        {
            yield return new WaitForSecondsRealtime(2f);

            if (webSocket != null && webSocket.IsOpen)
            {
                float idleTime = Time.unscaledTime - lastReceivedMessageTime;
                if (idleTime > 5.5f)
                {
                    Debug.LogWarning($"[WSS] Ping-pong timeout detected (idle for {idleTime:F1}s). Reconnecting...");
                    CloseConnection();
                    HandleConnectionError("Heartbeat timeout");
                    yield break;
                }

                string pingJson = "{\"type\":\"ping\"}";
                webSocket.Send(pingJson);
            }
        }
    }

    // 修复：防止重复启动多个重连协程
    private void StartAutoReconnect()
    {
        if (isReconnecting) return;
        StopAutoReconnect();
        if (gameObject.activeInHierarchy)
        {
            autoReconnectCoroutine = StartCoroutine(AutoReconnectRoutine());
        }
    }

    private void StopAutoReconnect()
    {
        if (autoReconnectCoroutine != null)
        {
            StopCoroutine(autoReconnectCoroutine);
            autoReconnectCoroutine = null;
        }
        isReconnecting = false;
    }

    private IEnumerator AutoReconnectRoutine()
    {
        isReconnecting = true;
        yield return new WaitForSecondsRealtime(3f);

        // 修复：3秒倒计时结束后，如果仍未连接，则发起连接
        // 此时 isReconnecting 仍为 true，但 ConnectWithUIStatus 不再检查此标志
        if (webSocket == null || !webSocket.IsOpen)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[WSS] Auto reconnect trigger...");
            }
            Connect();
        }

        isReconnecting = false;
    }

    private void HandleStartGameMessage(string jsonStr)
    {
        try
        {
            StartGameMessage msg = JsonUtility.FromJson<StartGameMessage>(jsonStr);
            if (msg != null && msg.data != null)
            {
                currentSessionData = msg.data;
                if (enableDebugLogs)
                {
                    Debug.Log($"[WSS] Session Data saved. SessionId: {currentSessionData.sessionId}, Name: {currentSessionData.name}");
                }

                if (GameFlowController.Instance != null && GameFlowController.Instance.CurrentState == GameFlowController.GameState.Idle)
                {
                    if (GameModeSelector.Instance == null || !GameModeSelector.Instance.IsOpen)
                    {
                        GameFlowController.Instance.SetState(GameFlowController.GameState.LanguageSelect);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WSS] Error deserializing start_game payload: {ex.Message}");
        }
    }

    public void OnGameOverPageReached()
    {
        string currentMode = PlayerPrefs.GetString("GameMode", "ONLINE");
        if (enableDebugLogs)
        {
            Debug.Log($"[WSS] OnGameOverPageReached invoked. GameMode: {currentMode}, WebSocket status: {(webSocket != null ? (webSocket.IsOpen ? "Open" : "Closed") : "Null")}");
        }

        if (currentMode != "ONLINE") return;

        string sessionId = currentSessionData != null ? currentSessionData.sessionId : "";

        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = "test-session-" + System.Guid.NewGuid().ToString().Substring(0, 8);
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[WSS] SessionId was empty. Generated mock SessionId: '{sessionId}' for local testing.");
            }
        }

        int score = GameFlowController.Instance != null ? GameFlowController.Instance.gameScore : 0;
        int durationMs = 60000;
        if (GameFlowController.Instance != null)
        {
            durationMs = Mathf.RoundToInt(GameFlowController.Instance.gameplayDuration * 1000f);
        }

        StartCoroutine(SendGameScoreRoutine(sessionId, score, durationMs));
    }

    private IEnumerator SendGameScoreRoutine(string sessionId, int score, int durationMs)
    {
        string payload = $"{{\"type\":\"game_score\",\"data\":{{\"sessionId\":\"{sessionId}\",\"score\":{score},\"gameDurationMs\":{durationMs},\"machineMessage\":{{}}}}}}";
        int retryCount = 0;
        bool success = false;

        while (retryCount < 3 && !success)
        {
            if (webSocket != null && webSocket.IsOpen)
            {
                try
                {
                    webSocket.Send(payload);
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[WSS] Sent game score (Attempt {retryCount + 1}): {payload}");
                    }
                    success = true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WSS] Exception during sending score: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning("[WSS] WebSocket not connected. Waiting 2s for reconnection before retry...");
            }

            if (!success)
            {
                retryCount++;
                yield return new WaitForSecondsRealtime(2f);
            }
        }

        if (!success)
        {
            Debug.LogError("[WSS] Failed to send game score after 3 attempts.");
        }
    }

    [System.Serializable]
    private class StartGameMessage
    {
        public string type;
        public GameSessionData data;
    }
}