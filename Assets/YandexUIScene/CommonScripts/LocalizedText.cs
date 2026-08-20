using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedText : MonoBehaviour
{
    public LanguageDatabase database; // 拖入创建的数据库
    public string translationKey;     // 在 Inspector 中填写对应的 Key

    private TextMeshProUGUI textComponent;

    private void Awake()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        // 注册事件
        GameFlowController.OnLanguageChanged += UpdateLanguage;
        
        // 初始显示时，如果 Instance 已经就绪，则更新一次语言
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
        if (textComponent != null && database != null)
        {
            textComponent.text = database.GetText(translationKey, language);
        }
    }
}
