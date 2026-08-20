using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class YandexQRCodeGenerator : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Target RawImage where the QR code will be generated. If left empty, it will auto-find any RawImage with 'QR' in its name.")]
    public RawImage targetRawImage;

    [Header("Settings")]
    [Tooltip("QR code refresh frequency in seconds")]
    public float refreshInterval = 120f;

    private Coroutine qrCodeRoutine;

    public static YandexQRCodeGenerator Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        FindTargetRawImage();

        // Immediately hide QR image on init (no fade). 
        // ConnectionObserverRoutine will activate it when connected.
        if (targetRawImage != null)
        {
            targetRawImage.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        StartQRGenerator();
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        StopQRGenerator();
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Force-null the reference so FindTargetRawImage always re-finds the new scene's RawImage
        targetRawImage = null;
        FindTargetRawImage();

        // Immediately hide QR (no fade) until ConnectionObserverRoutine activates it
        if (targetRawImage != null)
        {
            targetRawImage.gameObject.SetActive(false);
        }

        // Restart generator, which resets the timer and forces an immediate QR code generation
        StartQRGenerator();
    }

    private void FindTargetRawImage()
    {
        if (targetRawImage == null)
        {
            RawImage[] rawImages = FindObjectsOfType<RawImage>(true);
            foreach (var img in rawImages)
            {
                if (img.gameObject.name.Contains("QR") || img.gameObject.name.Contains("qr"))
                {
                    targetRawImage = img;
                    Debug.Log($"[QR] Auto-linked targetRawImage to GameObject: '{img.gameObject.name}'");
                    break;
                }
            }
        }
    }

    public void StartQRGenerator()
    {
        StopQRGenerator();

        string currentMode = GameFlowController.Instance != null ? GameFlowController.Instance.ActiveGameMode : "OFFLINE";
        if (currentMode != "ONLINE")
        {
            return;
        }

        if (gameObject.activeInHierarchy)
        {
            qrCodeRoutine = StartCoroutine(GenerateQRRoutine());
        }
    }

    public void StopQRGenerator()
    {
        if (qrCodeRoutine != null)
        {
            StopCoroutine(qrCodeRoutine);
            qrCodeRoutine = null;
        }
    }

    private IEnumerator GenerateQRRoutine()
    {
        while (true)
        {
            // Only generate QR code when GameFlowController is in the Idle state.
            // If the game has started, stop the generator and exit the coroutine.
            if (GameFlowController.Instance == null || GameFlowController.Instance.CurrentState == GameFlowController.GameState.Idle)
            {
                GenerateQRCode();
            }
            else
            {
                Debug.Log("[QR] Game is active (not Idle). Stopping QR generator coroutine.");
                StopQRGenerator();
                yield break;
            }
            yield return new WaitForSecondsRealtime(refreshInterval);
        }
    }

    [ContextMenu("Generate QR Code Now")]
    public void GenerateQRCode()
    {
        // Re-verify RawImage target existence
        if (targetRawImage == null)
        {
            FindTargetRawImage();
        }

        if (targetRawImage == null)
        {
            Debug.LogError("[QR] Target RawImage is not assigned and could not be auto-located in the scene!");
            return;
        }

        // Retrieve the machine ID
        string machineId = "";
        if (YandexWebSocketClient.Instance != null && !string.IsNullOrEmpty(YandexWebSocketClient.Instance.MachineId))
        {
            machineId = YandexWebSocketClient.Instance.MachineId;
        }
        else
        {
            // Decoupled fallback: directly attempt to load from StreamingAssets config file if WSS Client is not yet initialized
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
                    }
                }
                catch (Exception)
                {
                    // Ignore parsing error, fall through to default
                }
            }
        }

        // Fallback to hardware unique ID if all else fails
        if (string.IsNullOrEmpty(machineId))
        {
            string rawId = SystemInfo.deviceUniqueIdentifier;
            string lastSix = rawId.Length >= 6 ? rawId.Substring(rawId.Length - 6) : rawId;
            machineId = "machine-" + lastSix.ToUpper();
        }

        // Generate timestamp in yyyyMMddHHmmss format
        string timeStr = DateTime.Now.ToString("yyyyMMddHHmmss");
        string qrContent = $"https://yandexcj2026.bbtech.cc?time={timeStr}&id={machineId}";

        Debug.Log($"[QR] Generating QR code with content: {qrContent}");

        if (CreateQRcore.Instance != null)
        {
            CreateQRcore.Instance.Create(qrContent, targetRawImage, false);
        }
        else
        {
            // Fallback search in the active scene
            var creator = FindObjectOfType<CreateQRcore>();
            if (creator != null)
            {
                creator.Create(qrContent, targetRawImage, false);
            }
            else
            {
                Debug.LogError("[QR] CreateQRcore script instance not found in scene!");
            }
        }
    }

    public void SetQRImageActive(bool active)
    {
        if (targetRawImage != null)
        {
            CanvasGroup canvasGroup = targetRawImage.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = targetRawImage.gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.DOKill();

            float targetAlpha = active ? 1f : 0f;
            float duration = 0.5f;

            if (active)
            {
                // If it was inactive, activate it first with 0 alpha and then fade in
                if (!targetRawImage.gameObject.activeSelf)
                {
                    canvasGroup.alpha = 0f;
                    targetRawImage.gameObject.SetActive(true);
                }
                canvasGroup.DOFade(targetAlpha, duration).SetUpdate(true);
            }
            else
            {
                // If it is already inactive, do nothing
                if (!targetRawImage.gameObject.activeSelf)
                {
                    canvasGroup.alpha = 0f;
                    return;
                }

                canvasGroup.DOFade(targetAlpha, duration).SetUpdate(true).OnComplete(() =>
                {
                    if (targetRawImage != null && canvasGroup.alpha == 0f)
                    {
                        targetRawImage.gameObject.SetActive(false);
                    }
                });
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    [System.Serializable]
    private class AppConfig
    {
        public string machineId = "machine-1";
    }
}
