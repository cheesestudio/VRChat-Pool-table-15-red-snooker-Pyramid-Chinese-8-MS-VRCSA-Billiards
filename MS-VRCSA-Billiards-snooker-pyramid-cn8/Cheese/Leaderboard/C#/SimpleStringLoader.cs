using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace DrBlackRat
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SimpleStringLoader : UdonSharpBehaviour
    {
        [Header("Download Link")]
        public VRCUrl url;

        [Header("Settings")]
        [Tooltip("Load String when you enter the World")]
        [SerializeField] private bool loadOnStart = true;

        [Space(10)]
        [Tooltip("Automatically reload String after a certain amount of time (Load On Start should be enabled for this)")]
        [SerializeField] bool autoReload = false;
        [Tooltip("Time in minutes after which the String should be downloaded again")]
        [SerializeField] [Range(1, 60)] int autoReloadTime = 10;

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
        [Tooltip("String used while the String is Loading")] [TextArea]
        [SerializeField] private string loadingString = "Loading...";
        [Space(10)]
        [Tooltip("Use the Error String when the String couldn't be Loaded")]
        [SerializeField] private bool useErrorString = true;
        [Tooltip("String used when the String couldn't be Loaded")] [TextArea]
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
            // Start Loading
            if (loadOnStart) _LoadString();

        }
        public void _LoadString()
        {
            // Check if it's already loading
            if (loading)
            {
                return;
            }
            // Load String
            if (url == null || string.IsNullOrEmpty(url.Get()))
            {
                if (useErrorString) ApplyString(errorString);
                return;
            }
            loading = true;
            // Loading String
            if (useLoadingString && timesRun == 0 || useLoadingString && timesRun >= 1 && !skipLoadingStringOnReload)
            {
                ApplyString(loadingString);
            }
            VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
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
            SendCustomEventDelayedSeconds("_LoadString", autoReloadTime * 60);
        }
        public override void OnStringLoadSuccess(IVRCStringDownload result)
        {
            timesRun++;
            loading = false;
            ApplyString(result.Result);
            AutoReload();
        }
        public override void OnStringLoadError(IVRCStringDownload result)
        {
            timesRun++;
            loading = false;
            if (useErrorString) ApplyString(errorString);
            AutoReload();
        }
    }
}

