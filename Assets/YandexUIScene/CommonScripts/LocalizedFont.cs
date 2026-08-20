using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedFont : MonoBehaviour
{
    [Header("对应语言的字体素材")]
    public TMP_FontAsset cnFont;
    public TMP_FontAsset enFont;

    private TextMeshProUGUI textComponent;

    private void Awake()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        // 注册事件
        GameFlowController.OnLanguageChanged += UpdateLanguage;
        
        // 初始显示时，如果 Instance 已经就绪，则更新一次字体
        if (GameFlowController.Instance != null)
        {
            UpdateLanguage(GameFlowController.Instance.SelectedLanguage);
        }
    }

    private void OnDisable()
    {
        // 注销事件，防止内存泄漏
        GameFlowController.OnLanguageChanged -= UpdateLanguage;
    }

    private void UpdateLanguage(GameFlowController.Language language)
    {
        if (textComponent == null) return;

        TMP_FontAsset targetFont = language == GameFlowController.Language.CN ? cnFont : enFont;
        if (targetFont != null)
        {
            textComponent.font = targetFont;
        }
    }
}
