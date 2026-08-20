using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PageSwitcher : MonoBehaviour
{
    [Header("页面设置")]
    [Tooltip("所有需要控制的CanvasGroup页面")]
    public CanvasGroup[] pages;

    [Header("切换动画设置")]
    [Tooltip("淡入淡出时间")]
    public float fadeDuration = 0.5f;

    [Tooltip("初始显示的页面索引")]
    public int defaultPageIndex = 0;

    public int currentPageIndex=-1;

    [Header("Panel5 模式元素")]
    [Tooltip("离线图片")]
    public GameObject imageOffline;
    [Tooltip("在线图片")]
    public GameObject imageOnline;
    [Tooltip("排行榜")]
    public GameObject ranking1;

    private Sequence activeSwitchSequence;


    public static PageSwitcher Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // 销毁重复的新对象
            return;
        }
        Instance = this;

        // 初始化所有页面
        InitializePages();
    }

    void Start()
    {
        // 显示默认页面
        SwitchToPage(defaultPageIndex);
    }

    void InitializePages()
    {
        // 确保所有页面初始状态正确，默认页提前全显就绪，防止黑幕淡出时出现 0.1s 缺屏闪现
        for (int i = 0; i < pages.Length; i++)
        {
            var page = pages[i];
            if (page == null) continue;

            if (i == defaultPageIndex)
            {
                page.alpha = 1f;
                page.interactable = true;
                page.blocksRaycasts = true;
                page.gameObject.SetActive(true);
            }
            else
            {
                page.alpha = 0f;
                page.interactable = false;
                page.blocksRaycasts = false;
                page.gameObject.SetActive(false);
            }
        }
        currentPageIndex = defaultPageIndex;
    }

    // 切换到指定索引的页面（高级柔和渐变过渡）
    public void SwitchToPage(int pageIndex, bool instant)
    {
        // 检查索引是否有效
        if (pageIndex < 0 || pageIndex >= pages.Length || pages[pageIndex] == null)
        {
            Debug.LogWarning($"无效的页面索引: {pageIndex}");
            return;
        }

        // 当切换到 panel5 (index 5) 时，自动检测用户选择的模式并控制 image-offline / image-online 绝对互斥
        if (pageIndex == 5 && pages.Length > 5 && pages[5] != null)
        {
            UpdatePanel5ModeVisibility(pages[5]);
        }

        // 如果已经是当前页面且目标页面已完全显示，不做任何操作
        if (pageIndex == currentPageIndex && pages[pageIndex].gameObject.activeSelf && Mathf.Approximately(pages[pageIndex].alpha, 1f))
        {
            return;
        }

        // Kill active transition if any
        if (activeSwitchSequence != null)
        {
            activeSwitchSequence.Kill();//在开始新的页面切换动画前，强行停止（掐断）当前正在播放的切换动画
        }


        //创建并配置当前页面切换的动画序列，并将其记录下来
        Sequence switchSequence = DOTween.Sequence().SetLink(gameObject).SetUpdate(true);
        activeSwitchSequence = switchSequence;


        //遍历所有页面，把目标页面“淡入显示”，把当前显示的页面“淡出隐藏”，并把其他无关页面完全关闭
        float fadeOutTime = fadeDuration * 0.5f;
        float fadeInTime = fadeDuration * 0.6f;

        for (int i = 0; i < pages.Length; i++)
        {
            var page = pages[i];
            if (page == null) continue;
            page.DOKill();

            if (i == pageIndex)//分支一：目标显示页面
            {
                page.alpha = 0f;
                page.gameObject.SetActive(true);
                page.interactable = true;
                page.blocksRaycasts = false;

                switchSequence.Insert(0, page.DOFade(1f, fadeInTime).SetEase(Ease.OutCubic).SetUpdate(true)
                    .OnComplete(() => {
                        page.blocksRaycasts = true;
                    }));
            }
            else if (page.gameObject.activeSelf || page.alpha > 0f)//分支二：需要隐藏的当前页面
            {
                page.interactable = false;
                page.blocksRaycasts = false;
                var tempPage = page;

                switchSequence.Insert(0, page.DOFade(0f, fadeOutTime).SetEase(Ease.OutQuad).SetUpdate(true)
                    .OnComplete(() => {
                        tempPage.gameObject.SetActive(false);
                    }));
            }
            else//分支三：其他本就隐藏的页面
            {
                page.alpha = 0f;
                page.interactable = false;
                page.blocksRaycasts = false;
                page.gameObject.SetActive(false);
            }
        }

        currentPageIndex = pageIndex;
    }

    public void UpdatePanel5ModeVisibility(CanvasGroup panel5)
    {
        if (panel5 == null) return;

        // 检测用户选择的是离线模式还是在线模式
        string gameMode = PlayerPrefs.GetString("GameMode", "ONLINE");
        if (GameFlowController.Instance != null && !string.IsNullOrEmpty(GameFlowController.Instance.ActiveGameMode))
        {
            gameMode = GameFlowController.Instance.ActiveGameMode;
        }

        bool isOffline = (gameMode == "OFFLINE");

        if (isOffline)
        {
            // 离线模式：显示 panel5 的 image-offline，强行关闭 image-online 和 Ranking (1)
            if (imageOffline != null)
            {
                imageOffline.SetActive(true);
                var img = imageOffline.GetComponent<Image>();
                if (img != null) img.enabled = true;
            }

            if (imageOnline != null)
            {
                var img = imageOnline.GetComponent<Image>();
                if (img != null) img.enabled = false;
                imageOnline.SetActive(false);
            }

            if (ranking1 != null)
            {
                ranking1.SetActive(false);
            }
        }
        else
        {
            // 在线模式：显示 panel5 的 image-online 和 Ranking (1)，强行关闭 image-offline
            if (imageOnline != null)
            {
                imageOnline.SetActive(true);
                var img = imageOnline.GetComponent<Image>();
                if (img != null) img.enabled = true;
            }

            if (imageOffline != null)
            {
                var img = imageOffline.GetComponent<Image>();
                if (img != null) img.enabled = false;
                imageOffline.SetActive(false);
            }

            if (ranking1 != null)
            {
                ranking1.SetActive(true);
            }
        }
    }



    // 保留原始的单参数方法，默认非瞬间切换
    public void SwitchToPage(int pageIndex)
    {
        SwitchToPage(pageIndex, false);
    }



    // 通过按钮直接调用的方法
    public void SwitchToPageByButton(int pageIndex)
    {
        SwitchToPage(pageIndex);
    }

    // 切换到下一页
    public void NextPage()
    {
        int nextIndex = (currentPageIndex + 1) % pages.Length;
        SwitchToPage(nextIndex);
    }

    // 切换到上一页
    public void PreviousPage()
    {
        int prevIndex = (currentPageIndex - 1 + pages.Length) % pages.Length;
        SwitchToPage(prevIndex);
    }

    void OnDestroy()
    {
        // 清理DOTween动画
        DOTween.KillAll();
    }
}
