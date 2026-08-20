using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// LocalizedFontSize 脚本
/// 作用：在中文 (CN) 与英文 (EN) 模式下，分别设置 TextMeshProUGUI 或 Text 的字体大小 (Font Size)。
/// </summary>
[DisallowMultipleComponent]
public class LocalizedFontSize : MonoBehaviour
{
    [Header("对应语言的字体大小")]
    public float cnFontSize = 36f;
    public float enFontSize = 28f;

    private TextMeshProUGUI tmpComponent;
    private Text textComponent;

    private void Awake()
    {
        tmpComponent = GetComponent<TextMeshProUGUI>();
        if (tmpComponent == null)
        {
            textComponent = GetComponent<Text>();
        }
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

    public void UpdateLanguage(GameFlowController.Language language)
    {
        if (tmpComponent == null && textComponent == null)
        {
            Awake();
        }

        float targetSize = (language == GameFlowController.Language.CN) ? cnFontSize : enFontSize;

        if (tmpComponent != null)
        {
            tmpComponent.fontSize = targetSize;
        }
        else if (textComponent != null)
        {
            textComponent.fontSize = Mathf.RoundToInt(targetSize);
        }
    }
}
