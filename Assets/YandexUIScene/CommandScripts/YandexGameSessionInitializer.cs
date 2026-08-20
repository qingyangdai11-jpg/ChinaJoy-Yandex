using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class YandexGameSessionInitializer : MonoBehaviour
{
    private static YandexGameSessionInitializer _instance;
    public static YandexGameSessionInitializer Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("YandexGameSessionInitializer");
                _instance = go.AddComponent<YandexGameSessionInitializer>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [SerializeField] private bool enableDebugLogs = true;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void InitializeSession()
    {
        string machineId = "machine-1";
        if (YandexWebSocketClient.Instance != null && !string.IsNullOrEmpty(YandexWebSocketClient.Instance.MachineId))
        {
            machineId = YandexWebSocketClient.Instance.MachineId;
        }
        else
        {
            // Fallback: try reading config from StreamingAssets
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
                catch (Exception ex)
                {
                    if (enableDebugLogs)
                    {
                        Debug.LogWarning($"[SessionInitializer] Failed to read fallback config: {ex.Message}");
                    }
                }
            }
        }

        StartCoroutine(PostResetSessionRoutine(machineId));
    }

    [System.Serializable]
    private class AppConfig
    {
        public string machineId = "machine-1";
    }

    [System.Serializable]
    private class ResetPayload
    {
        public string machineId;
    }

    private IEnumerator PostResetSessionRoutine(string machineId)
    {
        string url = "https://yandexa.bbtech.cc/api/machine/reset";
        ResetPayload payload = new ResetPayload { machineId = machineId };
        string jsonPayload = JsonUtility.ToJson(payload);

        if (enableDebugLogs)
        {
            Debug.Log($"[SessionInitializer] Sending POST request to {url} with body: {jsonPayload}");
        }

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 5;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[SessionInitializer] POST reset successful! Response: {request.downloadHandler.text}");
                }
            }
            else
            {
                Debug.LogError($"[SessionInitializer] POST reset failed: {request.error}. Response: {request.downloadHandler?.text}");
            }
        }
    }
}
