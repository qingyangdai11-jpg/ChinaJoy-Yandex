using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class LocalizedImage : MonoBehaviour
{
    [Header("对应语言的图片素材")]
    public Sprite cnSprite;
    public Sprite enSprite;

    private Image imageComponent;

    private void Awake()
    {
        imageComponent = GetComponent<Image>();
    }

    private void OnEnable()
    {
        GameFlowController.OnLanguageChanged += UpdateLanguage;
        
        if (GameFlowController.Instance != null)
        {
            UpdateLanguage(GameFlowController.Instance.SelectedLanguage);
        }
    }

    private void OnDisable()
    {
        GameFlowController.OnLanguageChanged -= UpdateLanguage;
    }

    private void UpdateLanguage(GameFlowController.Language language)
    {
        if (imageComponent == null) return;

        Sprite targetSprite = language == GameFlowController.Language.CN ? cnSprite : enSprite;
        if (targetSprite != null)
        {
            imageComponent.sprite = targetSprite;
        }
    }
}
