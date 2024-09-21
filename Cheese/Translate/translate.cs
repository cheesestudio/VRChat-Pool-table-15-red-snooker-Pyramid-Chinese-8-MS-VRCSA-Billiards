
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using TMPro;
using UnityEngine.UI;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

public class translate : UdonSharpBehaviour
{
    [SerializeField] public Text[] Text;
    [SerializeField] public TextMeshProUGUI[] Tmp;
    [SerializeField] public string[] TextSrting;
    [SerializeField] public string[] TMPSring;
    private string language;

    void Start()
    {

        language = VRCPlayerApi.GetCurrentLanguage();
        if (language == "zh-CN") Chinese();
        else English();


    }
    public void Chinese() 
    {
        if (Text != null)
        {
            for (int i = 0; i < Text.Length; i++)
            {
                if (TextSrting[i]!=null)
                    Text[i].text = TextSrting[i];
            }
        }
        if(Tmp != null)
        {
            for(int i = 0;i < Tmp.Length; i++)
            {
                if(TMPSring[i]!=null)
                    Tmp[i].text = TMPSring[i];
            }
        }
    }
    public void English() { }
}
