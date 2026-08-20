using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LanguageDatabase", menuName = "Localization/Language Database")]
public class LanguageDatabase : ScriptableObject
{
    [System.Serializable]
    public struct Translation
    {
        public string key;          // 翻译的唯一标识 Key，例如 "rule_title"
        [TextArea] public string cn; // 中文内容
        [TextArea] public string en; // 英文内容
    }

    public List<Translation> translations = new List<Translation>();

    public string GetText(string key, GameFlowController.Language language)
    {
        if (translations == null) return key;
        
        var item = translations.Find(t => t.key == key);
        if (string.IsNullOrEmpty(item.key)) 
        {
            return key; // 如果找不到，直接返回 key 作为默认值
        }
        
        return language == GameFlowController.Language.CN ? item.cn : item.en;
    }
}
