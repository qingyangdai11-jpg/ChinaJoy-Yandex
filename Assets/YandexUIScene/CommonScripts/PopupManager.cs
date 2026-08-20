using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

//负责管理游戏中所有飘字与事件弹窗。包含跟随角色的小弹窗（Small Popup）与居中显示的主事件弹窗（Main Popup）
public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance { get; private set; }

    [Header("Popup References")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private List<GameObject> popupItems = new List<GameObject>();

    [Header("Small Popup Settings")]
    [SerializeField] private Vector3 smallPopupWorldOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private float smallPopupFollowSpeed = 15f;
    [SerializeField] private float smallPopupEnterDuration = 0.3f;
    [SerializeField] private float smallPopupStayDuration = 0.8f;
    [SerializeField] private float smallPopupExitDuration = 0.4f;
    [SerializeField] private float smallPopupQuickExitDuration = 0.15f;
    [SerializeField] private Ease smallPopupEnterEase = Ease.OutBack;

    [Header("Main Popup Settings")]
    [SerializeField] private float normalStayDuration = 1.2f;
    [SerializeField] private float enterScaleDuration = 0.3f;
    [SerializeField] private float exitFadeDuration = 0.2f;
    [SerializeField] private float previousExitUpOffset = 150f;
    [SerializeField] private float previousExitDuration = 0.3f;
    [SerializeField] private float startScale = 0.5f;
    [SerializeField] private Ease mainEnterEase = Ease.OutBack;
    [SerializeField] private Ease mainPreviousExitEase = Ease.OutQuad;

    [System.Serializable]
    public class ActiveMainPopup
    {
        public GameObject popupInstance;
        public CanvasGroup canvasGroup;
        public string popupName;
        public float startTime;
        public bool isShortened;
        public Vector3 originalLocalPos;
        public bool isExiting;
    }

    private List<ActiveMainPopup> activeMainPopups = new List<ActiveMainPopup>();
    private SmallPopupFollowPlayer activeSmallPopupScript = null;
    private Coroutine pendingSmallPopupCoroutine = null;

    private static readonly HashSet<string> smallPopupKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        "coin", "barrier", "5sbuffx2", "coin10", "coin35", "coin50", "buff30", "buff60", "light1", "light3"
    };

    private static readonly Dictionary<string, string> popupDisplayNames = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        { "coin", "COIN\n+1 分！" },
        { "5sbuffx2", "5s BUFF x2\n+2 分！" },
        { "coin10", "COIN 10\n+2 分！" },
        { "coin35", "COIN 35\n+10 分！" },
        { "coin50", "COIN 50\n+2 分！" },
        { "buff30", "BUFF 30s\n+10 分！" },
        { "buff60", "BUFF 60s\n+25 分！" },
        { "light1", "闪电连击\n+5 分！" },
        { "light2", "闪电爆发\n5秒双倍分！" },
        { "light3", "闪电暴击\n+10 分！" },
        { "light4", "闪电降临\n生成15个金币！" },
        { "light5", "雷霆时刻\n3秒双倍分！" },
        { "light6", "电磁磁铁\n吸附周围金币！" }
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        InitializePopupReferences();
    }

    private void Update()
    {
        // 任何菜单弹出/暂停/非Gameplay状态时，隐藏并清理当前所有的弹幕
        bool isPaused = (SimpleUIManager.Instance != null && SimpleUIManager.Instance.IsPaused);
        bool isNotGameplay = (GameFlowController.Instance != null && GameFlowController.Instance.CurrentState != GameFlowController.GameState.Gameplay);
        if (isPaused || isNotGameplay)
        {
            HideAllPopupsImmediately();
        }
    }

    private void InitializePopupReferences()
    {
        if (popupRoot == null)
        {
            popupRoot = System.Array.Find(Resources.FindObjectsOfTypeAll<GameObject>(), go => go.name == "PopUp" && !string.IsNullOrEmpty(go.scene.name));
        }

        if (popupRoot != null)
        {
            popupRoot.transform.localScale = Vector3.one;
            popupItems.Clear();
            Transform canvasTransform = popupRoot.transform.Find("Canvas") ?? GameObject.Find("Canvas")?.transform;
            if (canvasTransform != null)
            {
                canvasTransform.gameObject.SetActive(true);
                canvasTransform.localScale = Vector3.one;
                foreach (Transform child in canvasTransform)
                {
                    popupItems.Add(child.gameObject);
                }
            }
            else
            {
                AddAllChildren(popupRoot.transform);
            }

            foreach (var item in popupItems)
            {
                if (item != null) item.SetActive(false);
            }
        }
        else
        {
            Debug.LogWarning("[PopupManager] Could not find GameObject named 'PopUp' in root hierarchy!");
        }
    }

    private void AddAllChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            popupItems.Add(child.gameObject);
            AddAllChildren(child);
        }
    }

    public void ShowPopup(string popupName)//判断弹窗名称，若是金币或特定Buff飘字（如 coin、barrier、5sbuffx2）则调用小弹窗逻辑；若是大事件则调用主弹窗。
    {
        if (this == null) return;

        if (popupRoot == null)
        {
            InitializePopupReferences();
        }

        if (popupRoot == null)
        {
            Debug.LogError("[PopupManager] Cannot show popup because root 'PopUp' is missing!");
            return;
        }

        if (popupName.StartsWith("+") || smallPopupKeys.Contains(popupName))
        {
            ShowSmallPopup(popupName);
            return;
        }

        ShowMainPopup(popupName);
    }

    /// <summary>
    /// Shows a score popup by cloning the existing coin small-popup template.
    /// The source template and the normal named-popup flow are left unchanged.
    /// </summary>
    public void ShowScoreCoinPopup(int scoreAmount)
    {
        if (scoreAmount <= 0) return;

        ShowSmallPopup("coin", "+" + scoreAmount);
    }

    //当玩家短时间内连续吃金币时，若已有活跃小弹窗，会先调用它的 QuickExit() 快速淡出，
    //并在 smallPopupQuickExitDuration 延迟后生成下一个小弹窗，防止所有飘字叠加在猫咪头顶。
    private void ShowSmallPopup(string popupName, string textOverride = null)
    {
        if (popupRoot == null)
        {
            InitializePopupReferences();
        }

        if (popupRoot == null)
        {
            Debug.LogError("[PopupManager] Cannot show small popup because root 'PopUp' is missing!");
            return;
        }

        if (pendingSmallPopupCoroutine != null)
        {
            StopCoroutine(pendingSmallPopupCoroutine);
            pendingSmallPopupCoroutine = null;
        }

        // Let the current popup finish its quick fade before creating the next one.
        // This keeps small popups from visibly overlapping at the cat's head.
        if (activeSmallPopupScript != null)
        {
            activeSmallPopupScript.QuickExit();
            activeSmallPopupScript = null;
            pendingSmallPopupCoroutine = StartCoroutine(ShowSmallPopupAfterExit(popupName, textOverride));
            return;
        }

        CreateSmallPopup(popupName, textOverride);
    }

    private IEnumerator ShowSmallPopupAfterExit(string popupName, string textOverride)
    {
        yield return new WaitForSeconds(smallPopupQuickExitDuration);
        pendingSmallPopupCoroutine = null;
        CreateSmallPopup(popupName, textOverride);
    }

    private void CreateSmallPopup(string popupName, string textOverride)
    {
        GameObject template = popupItems.Find(x => x.name.Equals(popupName, System.StringComparison.OrdinalIgnoreCase));
        if (template == null)
        {
            template = CreatePopupDynamically(popupName);
        }

        if (template == null)
        {
            Debug.LogError($"[PopupManager] Small popup template '{popupName}' not found!");
            return;
        }

        // Activate root
        popupRoot.SetActive(true);

        // Instantiate a copy under the Canvas so they don't share/overwrite the same GameObject
        Transform canvasTransform = popupRoot.transform.Find("Canvas");
        GameObject copy = Instantiate(template, canvasTransform);
        copy.name = popupName + "_Instance";
        if (!string.IsNullOrEmpty(textOverride))
        {
            var texts = copy.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                text.text = textOverride;
            }
        }
        copy.SetActive(true);

        // Attach the smooth follow script with parameters
        var follow = copy.AddComponent<SmallPopupFollowPlayer>();
        follow.worldOffset = smallPopupWorldOffset;
        follow.followSpeed = smallPopupFollowSpeed;
        follow.enterDuration = smallPopupEnterDuration;
        follow.stayDuration = smallPopupStayDuration;
        follow.exitDuration = smallPopupExitDuration;
        follow.quickExitDuration = smallPopupQuickExitDuration;
        follow.enterEase = smallPopupEnterEase;

        activeSmallPopupScript = follow;
    }

    private void ShowMainPopup(string popupName)
    {
        // Prevent showing main popups if finish popup is active to avoid overlap conflicts
        if (GameFlowController.Instance != null && GameFlowController.Instance.finishImagePopup != null && GameFlowController.Instance.finishImagePopup.activeSelf)
        {
            return;
        }

        GameObject template = popupItems.Find(x => x.name.Equals(popupName, System.StringComparison.OrdinalIgnoreCase));
        if (template == null)
        {
            template = CreatePopupDynamically(popupName);
        }

        if (template == null)
        {
            Debug.LogError($"[PopupManager] Main popup template '{popupName}' not found!");
            return;
        }

        // Activate root
        popupRoot.SetActive(true);

        // Instantiate copy
        Transform canvasTransform = popupRoot.transform.Find("Canvas");
        GameObject copy = Instantiate(template, canvasTransform);
        copy.name = popupName + "_Instance";
        copy.SetActive(true);

        // Ensure canvas group is present for fade transitions
        CanvasGroup cg = copy.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = copy.AddComponent<CanvasGroup>();
        }

        // Shorten stay time for all preceding active main popups
        foreach (var active in activeMainPopups)
        {
            if (active != null && !active.isExiting && !active.isShortened)
            {
                active.isShortened = true;
            }
        }

        ActiveMainPopup newRecord = new ActiveMainPopup
        {
            popupInstance = copy,
            canvasGroup = cg,
            popupName = popupName,
            startTime = Time.time,
            isShortened = false,
            originalLocalPos = template.transform.localPosition,
            isExiting = false
        };

        copy.transform.localPosition = template.transform.localPosition;
        activeMainPopups.Add(newRecord);

        StartCoroutine(MainPopupRoutine(newRecord, template.transform.localScale));
    }


    //主弹窗显示期间，若有新的主弹窗生成，会将旧的标记为 isShortened。
    //旧弹窗会使用 DOTween 平滑向上滑动并迅速淡出，为新弹窗让出屏幕中央位置。
    private IEnumerator MainPopupRoutine(ActiveMainPopup info, Vector3 originalScale)
    {
        // Enter transition: Fade-in and scale-up from startScale to original scale
        info.popupInstance.transform.localScale = originalScale * startScale;
        info.canvasGroup.alpha = 0f;

        info.popupInstance.transform.DOScale(originalScale, enterScaleDuration).SetEase(mainEnterEase).SetLink(info.popupInstance);
        info.canvasGroup.DOFade(1f, enterScaleDuration).SetLink(info.popupInstance);

        yield return new WaitForSeconds(enterScaleDuration);

        // Wait loop: checks if another popup appeared (isShortened) or if normal stay duration expired
        while (true)
        {
            if (info.popupInstance == null) yield break;

            // If a new popup is triggered, immediately break the loop to exit and slide up
            if (info.isShortened)
            {
                break;
            }

            float elapsed = Time.time - info.startTime;
            if (elapsed >= normalStayDuration)
            {
                break;
            }
            yield return null;
        }

        info.isExiting = true;
        activeMainPopups.Remove(info);

        if (info.popupInstance == null) yield break;

        info.popupInstance.transform.DOKill();
        info.canvasGroup.DOKill();

        if (info.isShortened)
        {
            // Previous popup: drift upward and fade out
            info.popupInstance.transform.DOLocalMoveY(info.originalLocalPos.y + previousExitUpOffset, previousExitDuration).SetEase(mainPreviousExitEase).SetLink(info.popupInstance);
            info.canvasGroup.DOFade(0f, previousExitDuration).SetLink(info.popupInstance).OnComplete(() => {
                if (info.popupInstance != null) Destroy(info.popupInstance);
                CheckAndDeactivateRoot();
            });
        }
        else
        {
            // Last popup: fade out at its normal position
            info.canvasGroup.DOFade(0f, exitFadeDuration).SetLink(info.popupInstance).OnComplete(() => {
                if (info.popupInstance != null) Destroy(info.popupInstance);
                CheckAndDeactivateRoot();
            });
        }
    }

    public void HideAllMainPopupsQuickly()
    {
        foreach (var active in activeMainPopups)
        {
            if (active != null && !active.isExiting)
            {
                active.isShortened = true;
                if (active.popupInstance != null)
                {
                    active.popupInstance.transform.DOKill();
                    active.canvasGroup.DOKill();
                    active.isExiting = true;

                    // Instantly slide up and fade out within 0.15 seconds
                    active.popupInstance.transform.DOLocalMoveY(active.originalLocalPos.y + previousExitUpOffset, 0.15f).SetEase(mainPreviousExitEase).SetLink(active.popupInstance);
                    active.canvasGroup.DOFade(0f, 0.15f).SetLink(active.popupInstance).OnComplete(() => {
                        if (active.popupInstance != null) Destroy(active.popupInstance);
                        CheckAndDeactivateRoot();
                    });
                }
            }
        }
        activeMainPopups.Clear();
    }

    /// <summary>
    /// 隐藏并销毁当前所有弹幕（包括 Small Popup 与 Main Popups），用于菜单弹出或状态切换
    /// 当游戏暂停或退出 Gameplay 阶段时，立即中断所有 DOTween 动画并销毁激活的实例，防止弹窗残留。
    /// </summary>
    public void HideAllPopupsImmediately()
    {
        if (pendingSmallPopupCoroutine != null)
        {
            StopCoroutine(pendingSmallPopupCoroutine);
            pendingSmallPopupCoroutine = null;
        }

        foreach (var active in activeMainPopups)
        {
            if (active != null && active.popupInstance != null)
            {
                active.popupInstance.transform.DOKill();
                if (active.canvasGroup != null) active.canvasGroup.DOKill();
                Destroy(active.popupInstance);
            }
        }
        activeMainPopups.Clear();

        if (activeSmallPopupScript != null && activeSmallPopupScript.gameObject != null)
        {
            activeSmallPopupScript.gameObject.transform.DOKill();
            Destroy(activeSmallPopupScript.gameObject);
            activeSmallPopupScript = null;
        }

        if (popupRoot != null)
        {
            Transform canvasTransform = popupRoot.transform.Find("Canvas");
            if (canvasTransform != null)
            {
                for (int i = canvasTransform.childCount - 1; i >= 0; i--)
                {
                    Transform child = canvasTransform.GetChild(i);
                    if (child != null && child.name.EndsWith("_Instance"))
                    {
                        child.DOKill();
                        Destroy(child.gameObject);
                    }
                }
            }
            popupRoot.SetActive(false);
        }
    }

    private void CheckAndDeactivateRoot()
    {
        if (activeMainPopups.Count == 0 && popupRoot != null)
        {
            Transform canvasTransform = popupRoot.transform.Find("Canvas");
            if (canvasTransform != null)
            {
                bool hasActiveInstances = false;
                for (int i = 0; i < canvasTransform.childCount; i++)
                {
                    Transform child = canvasTransform.GetChild(i);
                    if (child.name.EndsWith("_Instance") && child.gameObject.activeSelf)
                    {
                        hasActiveInstances = true;
                        break;
                    }
                }
                if (!hasActiveInstances)
                {
                    popupRoot.SetActive(false);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (popupRoot != null)
        {
            popupRoot.transform.DOKill();
        }
        foreach (var item in popupItems)
        {
            if (item != null)
            {
                item.transform.DOKill();
            }
        }
    }

    //如果所需的弹窗模板在初始列表中找不到，会基于第一个有效模板克隆一个新实体，并通过 TMPro 动态改写其文字内容。(小弹窗，加分)
    private GameObject CreatePopupDynamically(string popupName)
    {
        if (popupItems == null || popupItems.Count == 0 || popupRoot == null)
        {
            Debug.LogWarning("[PopupManager] Cannot create popup dynamically: template list is empty or popupRoot is null.");
            return null;
        }

        Transform canvasTransform = popupRoot.transform.Find("Canvas");
        if (canvasTransform == null)
        {
            Debug.LogWarning("[PopupManager] Cannot create popup dynamically: Canvas child not found under PopUp root.");
            return null;
        }

        GameObject template = popupItems.Find(x => x != null);
        if (template == null)
        {
            Debug.LogWarning("[PopupManager] Cannot create popup dynamically: no valid template found in popupItems.");
            return null;
        }

        GameObject newPopup = Instantiate(template, canvasTransform);
        newPopup.name = popupName;
        newPopup.SetActive(false);

        var tmpTexts = newPopup.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
        if (tmpTexts.Length > 0)
        {
            if (!popupDisplayNames.TryGetValue(popupName, out string displayName))
            {
                displayName = popupName.ToUpper();
            }
            foreach (var tmp in tmpTexts)
            {
                tmp.text = displayName;
            }
        }

        popupItems.Add(newPopup);
        Debug.Log($"[PopupManager] Dynamically created missing popup '{popupName}' using template '{template.name}'");
        return newPopup;
    }
}
