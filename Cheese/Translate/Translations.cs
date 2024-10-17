using Newtonsoft.Json;
using UdonSharp;
using UnityEngine;

using VRC.SDK3.Data;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Translations : UdonSharpBehaviour
{
    //[SerializeField]public bool test=false;
    public string currentLanguage;
    public string fallbackLanguage = "en";

    public TextAsset[] translations;
    private DataDictionary _translations = new DataDictionary();
    private DataDictionary _currentLanguageDict;
    private DataDictionary _fallbackLanguageDict;

    private bool _initialized;
    private DataList _translatedTexts = new DataList();

    private void Initialize()
    {
        if (_initialized) return;

        _initialized = true;

        foreach (var t in translations)
        {
            if (VRCJson.TryDeserializeFromJson(t.text, out var json))
            {
                _translations[t.name] = json.DataDictionary;
            }
            else Debug.LogError($"Failed to parse translation file {json.Error}");
        }

        SetLanguage(VRCPlayerApi.GetCurrentLanguage().Substring(0, 2).ToLower());
        //SetLanguage("ja");
        _fallbackLanguageDict = _translations.ContainsKey(fallbackLanguage) ? _translations[fallbackLanguage].DataDictionary : null;
    }

    public void AddTranslatedText(TranslatedText text)
    {
        _translatedTexts.Add(text);
        text.UpdateText();
    }

    public void SetLanguage(string language)
    {
        Initialize();

        if (!_translations.ContainsKey(language))
        {
            Debug.LogError($"Language {language} not found, falling back to {fallbackLanguage}");
            language = fallbackLanguage;
        }
        
        currentLanguage = language;
        _currentLanguageDict = _translations[language].DataDictionary;

        StartTextUpdate();
    }
    // 当前更新的索引
    private int currentIndex = 0;
    // 延迟时间（以秒为单位）
    public float delay = 0.1f;

    public void StartTextUpdate()
    {
        currentIndex = 0; // 重置索引
        SendCustomEventDelayedSeconds(nameof(UpdateNextText), delay);
    }

    public void UpdateNextText()
    {
        if (currentIndex < _translatedTexts.Count)
        {
            ((TranslatedText)_translatedTexts[currentIndex].Reference).UpdateText(); // 更新文本
            currentIndex++; // 增加索引
            // 再次调用，添加延迟
            SendCustomEventDelayedSeconds(nameof(UpdateNextText), delay);
        }
    }

    public string Get(string key)
    {
        Initialize();

        if (TryGet(_currentLanguageDict, key, out var result)) return result;
        else if (TryGet(_fallbackLanguageDict, key, out result)) return result;

        return key;
    }

    private bool TryGet(DataDictionary language, string key, out string result)
    {
        if (language == null)
        {
            result = key;
            return false;
        }

        result = language[key].String;
        return true;
    }
}