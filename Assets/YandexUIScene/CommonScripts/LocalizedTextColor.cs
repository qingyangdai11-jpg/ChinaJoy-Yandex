using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// LocalizedTextColor 脚本
/// 作用：在中文 (CN) 与英文 (EN) 模式下，分别设置 TextMeshProUGUI 或 Graphic/Text 的字体/组件颜色。
/// </summary>
[DisallowMultipleComponent]
public class LocalizedTextColor : MonoBehaviour
{
    [Header("对应语言的字体颜色")]
    public Color cnColor = Color.white;
    public Color enColor = Color.white;

    private TextMeshProUGUI tmpComponent;
    private Graphic graphicComponent;

    private void Awake()
    {
        tmpComponent = GetComponent<TextMeshProUGUI>();
        if (tmpComponent == null)
        {
            graphicComponent = GetComponent<Graphic>();
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
        if (tmpComponent == null && graphicComponent == null)
        {
            Awake();
        }

        Color targetColor = (language == GameFlowController.Language.CN) ? cnColor : enColor;

        if (tmpComponent != null)
        {
            tmpComponent.color = targetColor;
        }
        else if (graphicComponent != null)
        {
            graphicComponent.color = targetColor;
        }
    }
}
