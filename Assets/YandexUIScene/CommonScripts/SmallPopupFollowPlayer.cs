using UnityEngine;
using DG.Tweening;

public class SmallPopupFollowPlayer : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform playerTransform;
    [Tooltip("Offset in world space relative to the player")]
    public Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    [Tooltip("Smoothing factor for following the player (0 means instant follow)")]
    public float followSpeed = 15f;

    [Header("Animation Settings")]
    [Tooltip("Scale and fade-in duration on enter")]
    public float enterDuration = 0.3f;
    [Tooltip("How long the popup stays fully visible")]
    public float stayDuration = 0.8f;
    [Tooltip("Fade-out duration on exit (only fades, no scaling down)")]
    public float exitDuration = 0.4f;
    [Tooltip("Quick fade-out duration when interrupted by another small popup")]
    public float quickExitDuration = 0.15f;
    
    [Tooltip("Easing curve for scaling up")]
    public Ease enterEase = Ease.OutBack;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas parentCanvas;
    private Vector3 originalScale;
    
    private Sequence normalSequence;
    private bool isExiting = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        parentCanvas = GetComponentInParent<Canvas>();
        originalScale = transform.localScale;
        
        // Handle case where template scale starts as zero
        if (originalScale == Vector3.zero)
        {
            originalScale = Vector3.one;
        }
    }

    private void Start()
    {
        // Try to automatically find the player controller if not assigned
        if (playerTransform == null)
        {
            var player = FindObjectOfType<SimplePlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        // Snap immediately to the player's position on first frame to prevent UI jumping
        UpdatePosition(true);

        // Reset scale and alpha for enter transition
        transform.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;

        // Enter scale & fade, stay, and normal exit fade
        normalSequence = DOTween.Sequence().SetLink(gameObject);
        normalSequence.Join(transform.DOScale(originalScale, enterDuration).SetEase(enterEase));
        normalSequence.Join(canvasGroup.DOFade(1f, enterDuration));
        normalSequence.AppendInterval(stayDuration);
        normalSequence.AppendCallback(() => isExiting = true);
        normalSequence.Append(canvasGroup.DOFade(0f, exitDuration));
        normalSequence.OnComplete(() => {
            if (gameObject != null) Destroy(gameObject);
        });
    }

    private void LateUpdate()
    {
        // Continuous smooth update following the player
        UpdatePosition(false);
    }

    private void UpdatePosition(bool instant)
    {
        if (playerTransform == null) return;
        if (parentCanvas == null) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        // Calculate world target position with offset
        Vector3 targetWorldPos = playerTransform.position + worldOffset;
        
        // Project to screen space
        Vector3 screenPoint = mainCam.WorldToScreenPoint(targetWorldPos);

        // Do not render or update if target is behind camera
        if (screenPoint.z < 0) return;

        if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            if (instant || followSpeed <= 0f)
            {
                rectTransform.position = screenPoint;
            }
            else
            {
                rectTransform.position = Vector3.Lerp(rectTransform.position, screenPoint, Time.deltaTime * followSpeed);
            }
        }
        else
        {
            // Convert screen point to canvas local point for Screen Space - Camera or World Space
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                screenPoint,
                parentCanvas.worldCamera,
                out localPoint
            );
            if (instant || followSpeed <= 0f)
            {
                rectTransform.anchoredPosition = localPoint;
            }
            else
            {
                rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, localPoint, Time.deltaTime * followSpeed);
            }
        }
    }

    public void QuickExit()
    {
        if (isExiting) return;
        isExiting = true;

        // Kill the running sequence to avoid animation conflicts
        if (normalSequence != null)
        {
            normalSequence.Kill();
        }
        
        transform.DOKill();
        canvasGroup.DOKill();

        // Perform a quick fade out at the current scale
        canvasGroup.DOFade(0f, quickExitDuration).SetLink(gameObject).OnComplete(() => {
            if (gameObject != null) Destroy(gameObject);
        });
    }

    private void OnDestroy()
    {
        if (normalSequence != null)
        {
            normalSequence.Kill();
        }
        transform.DOKill();
        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
        }
    }
}
