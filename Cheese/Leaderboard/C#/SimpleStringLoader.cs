using System;
using System.Collections.Generic;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace DrBlackRat
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SimpleStringLoader : UdonSharpBehaviour
    {

        [Header("Settings")]
        [Tooltip("Load String when you enter the World")]
        [SerializeField] private bool loadOnStart = true;

        [Space(10)]
        [Tooltip("Automatically reload String after a certain amount of time (Load On Start should be enabled for this)")]
        [SerializeField] bool autoReload = false;
        [Tooltip("Time in second after which the String should be downloaded again")]
        [SerializeField] [Range(1, 60)] int autoReloadTime = 10;

        [Header("EloPlug")]
        [Tooltip("Plug EloDonwload")]
        [SerializeField] public EloDownload _eloDownload = null;

        [Header("Text Components")]
        [Tooltip("Text component the string should be applied to, if left empty it tires to use the one it's attached to")]
        [SerializeField] private Text text;
        [Tooltip("Text Mesh Pro component the string should be applied to, if left empty it tires to use the one it's attached to")]
        [SerializeField] private TextMeshPro textMeshPro;
        [Tooltip("Text Mesh Pro UGUI component the string should be applied to, if left empty it tires to use the one it's attached to")]
        [SerializeField] private TextMeshProUGUI textMeshProUGUI;

        [Header("Loading & Error String")]
        [Tooltip("Use the Loading String while it waits for the String to Load")]
        [SerializeField] private bool useLoadingString = true;
        [Tooltip("Skips the Loading String when reloading the String (e.g. Auto Reload or Manually Loading it again)")]
        [SerializeField] private bool skipLoadingStringOnReload = true;
        [Tooltip("String used while the String is Loading")]
        [TextArea]
        [SerializeField] private string loadingString = "Loading...";
        [Space(10)]
        [Tooltip("Use the Error String when the String couldn't be Loaded")]
        [SerializeField] private bool useErrorString = true;
        [Tooltip("String used when the String couldn't be Loaded")]
        [TextArea]
        [SerializeField] private string errorString = "Error: String couldn't be loaded, view logs for more info";

        // Internals
        private bool loading = false;
        private int timesRun = 0;

        private void Start()
        {
            // Get Components
            if (text == null) text = GetComponent<Text>();
            if (textMeshPro == null) textMeshPro = GetComponent<TextMeshPro>();
            if (textMeshProUGUI == null) textMeshProUGUI = GetComponent<TextMeshProUGUI>();

            // 尝试寻找控件
            if (_eloDownload == null)
            {
                _eloDownload = GameObject.Find("EloDownload").GetComponent<EloDownload>();
                if (_eloDownload == null)
                {
                    this.enabled = false;
                    return;
                }
            }

            // Start Loading
            if (loadOnStart) _LoadString();

        }
        public void _LoadString()
        {
            if(_eloDownload._eloData == null || _eloDownload._eloData.Count == 0) 
            {
                ApplyString(loadingString);
                AutoReload();
                return;
            }
            // 格式化字符串
            string leaderBoardString = "";
            DataList names = _eloDownload._eloData.GetKeys();
            DataList scores = _eloDownload._eloData.GetValues();
            for (int i = 0; i < _eloDownload._eloData.Count; i++)
            {
                // 转码，去除小数点，格式化，替换空格 \u0020 到 \u00A0 
                leaderBoardString += 
                    (i+1).ToString()+"."
                    + names[i].ToString().Replace(" ", " ")
                    + " "
                    + ((int)float.Parse(scores[i].ToString())).ToString() 
                    + "\n";
            }
            // Loading String
            ApplyString(leaderBoardString);
            AutoReload();
        }
        private void ApplyString(string useString)
        {
            if (text != null) text.text = useString;
            if (textMeshPro != null) textMeshPro.text = useString;
            if (textMeshProUGUI != null) textMeshProUGUI.text = useString;
        }
        private void AutoReload()
        {
            if (!autoReload) return;
            SendCustomEventDelayedSeconds("_LoadString", autoReloadTime);
        }
    }
}

