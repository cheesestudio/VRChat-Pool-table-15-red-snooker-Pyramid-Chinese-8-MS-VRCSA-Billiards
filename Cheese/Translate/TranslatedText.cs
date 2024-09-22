using TMPro;

using UdonSharp;
using UnityEngine;
using UnityEngine.UI;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TranslatedText : UdonSharpBehaviour
{
    public string key;

    public TextMeshPro text;
    public TextMeshProUGUI textUI;
    public Text textLegacy;

    private Translations _translations;
    private Translations Translations
    {
        get
        {
            if (_translations == null) _translations = GetComponentInParent<Translations>();
            return _translations;
        }
    }

    private void Start() => Translations.AddTranslatedText(this);

    public void UpdateText() => SetText(Translations.Get(key));

    private void SetText(string value)
    {
        if (text != null) text.text = value;
        if (textUI != null) textUI.text = value;
        if (textLegacy != null) textLegacy.text = value;
    }
}