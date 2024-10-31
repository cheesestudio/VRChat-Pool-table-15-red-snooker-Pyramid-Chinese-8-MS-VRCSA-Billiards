using BestHTTP.Extensions;
using System;
using System.Text;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class RankingSystem : UdonSharpBehaviour
{
    public bool useV2API = false;
    [Header("References")]
    public InputField copyField;
    public VRCUrlInputField pasteField;
    [UdonSynced] private string errorString;
    public TextMeshProUGUI errorText;

    // 台球名称
    public string[] TableName = null;

    public string WorldGUID = null;
	public string hashKey = "CheeseIsTheHashKeyForNoReason";
	public string ScoreUploadBaseURL = "https://wangqaq.com/api/eol/upload_score.php";

	private string Player1 = "";
	private string Player2 = "";

	// Ioc 
	[HideInInspector] public ScoreManagerV4 _scoreManager;

#if DEBUG
	//Debug
	[HideInInspector] public string scoreDebugA = "";
	[HideInInspector] public string scoreDebugB = "";
	public void DebugUpload()
    {
        UpdateCopyData("testa", "testb", scoreDebugA, scoreDebugB,0);
	}
#endif

    public void _Init(ScoreManagerV4 scoreManager)
    {
        _scoreManager = scoreManager;
    }

    public void UpdateCopyData(string player1, string player2,string score1,string score2,uint ballMode)
    {
        Player1 = player1;
        Player2 = player2;
        if (score1 == score2                || 
            string.IsNullOrEmpty(player1)   ||
            string.IsNullOrEmpty(player2))
        {
            copyField.text = "null";
            errorText.text = "";
            return;
        }

		if (useV2API)
        {
            var modeString = mapModeName(ballMode);
			// 新API
			string hash = UdonHashLib.MD5_UTF8(player1 + player2 + score1 + score2 + modeString + WorldGUID + DateTime.UtcNow.ToString("yyyy/MM/dd HH:mm:ss") + hashKey);
			copyField.text = $"{ScoreUploadBaseURL}?Player1={player1}&Player2={player2}&PlayerScore1={score1}&PlayerScore2={score2}&mode={modeString}&WorldGUID={WorldGUID}&time={DateTime.UtcNow.ToString()}&MD5={hash}";
		}
        else
        {
			// 老API
			string hash = UdonHashLib.MD5_UTF8(player1 + player2 + score1 + score2 + hashKey);
			copyField.text = $"{ScoreUploadBaseURL}?player1={player1}&player2={player2}&score1={score1}&score2={score2}&hash={hash}";
		}

        //Debug.Log($"copyField =  {copyField.text}");
    }
    public void TryToUploadNote()
    {
        VRCUrl url = pasteField.GetUrl();

        pasteField.SetUrl(VRCUrl.Empty);
        if (url.ToString() != copyField.text)
        {
            errorText.text = ("not equal to copy text! ! !");
            return;
        }
        string localPlayer =Networking.LocalPlayer.displayName;
        if (localPlayer != Player1 && localPlayer != Player2)
        {
            errorText.text = ("不是你的比赛你上传???Don't upload others score");
            return;
        }

       // noteDownloader.newNoteButton.interactable = false;
        VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);

        _scoreManager.gameResetLocal();
        copyField.text = "Starting";
        errorText.text = "loading";
    }
    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        copyField.text = "Finished";
        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        errorString = "connected  " + result.Result;
        errorText.text = errorString;
        RequestSerialization();
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        //ActivateNoteUploadAnimation(false);
        copyField.text = "failed";
        Debug.LogError($"Error loading string: {result.ErrorCode} - {result.Error}");
        errorText.text = $"{result.ErrorCode} - {result.Error}";

        if (result.ErrorCode == 401)
        {
            errorText.text += "Please allow untrusted rul";
            //unauthorizedErrorInfo.SetActive(true);
        }
    }


    public override void OnDeserialization()
    {
        errorText.text = errorString;
    }

    void Start()
    {
        //UpdateCopyData("测试3", "urara․", "0", "720");
        //TryToUploadNote();
    }

    // 找台球名称（如果有的话）
    private string mapModeName(uint mode)
    {
        if (!string.IsNullOrEmpty(TableName[mode]))
            return TableName[mode];
        return "NotFind";
	}
}
