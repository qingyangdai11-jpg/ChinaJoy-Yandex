using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// LocalizedTextStyle 脚本
/// 作用：一站式设置中英文模式下的字体素材 (Font)、颜色 (Color)、以及字号大小 (Font Size)。
/// 可在 Inspector 面板中按需勾选/开关单个属性的覆盖控制。
/// </summary>
[DisallowMultipleComponent]
public class LocalizedTextStyle : MonoBehaviour
{
    [Header("1. 字体配置 (Font Asset)")]
    public bool overrideFont = true;
    public TMP_FontAsset cnFont;
    public TMP_FontAsset enFont;

    [Header("2. 颜色配置 (Font Color)")]
    public bool overrideColor = true;
    public Color cnColor = Color.white;
    public Color enColor = Color.white;

    [Header("3. 字号大小配置 (Font Size)")]
    public bool overrideFontSize = true;
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

        bool isCN = (language == GameFlowController.Language.CN);

        if (tmpComponent != null)
        {
            // 1. 字体 Asset
            if (overrideFont)
            {
                TMP_FontAsset targetFont = isCN ? cnFont : enFont;
                if (targetFont != null)
                {
                    tmpComponent.font = targetFont;
                }
            }

            // 2. 字体颜色
            if (overrideColor)
            {
                tmpComponent.color = isCN ? cnColor : enColor;
            }

            // 3. 字体大小
            if (overrideFontSize)
            {
                tmpComponent.fontSize = isCN ? cnFontSize : enFontSize;
            }
        }
        else if (textComponent != null)
        {
            // 传统 Text 支持颜色与字号大小
            if (overrideColor)
            {
                textComponent.color = isCN ? cnColor : enColor;
            }

            if (overrideFontSize)
            {
                textComponent.fontSize = Mathf.RoundToInt(isCN ? cnFontSize : enFontSize);
            }
        }
    }
}
