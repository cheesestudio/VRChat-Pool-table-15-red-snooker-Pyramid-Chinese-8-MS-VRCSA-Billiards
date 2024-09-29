using BestHTTP.Extensions;
using System;
using System.Text;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class RankingSystem : UdonSharpBehaviour
{
    [Header("References")]
    public InputField copyField;
    public VRCUrlInputField pasteField;
    public ScoreManagerV2 scoreManager;
    public Text errorText;
    private string Player1 = "";
    private string Player2 = "";

    private string hashKey = "CheeseIsTheHashKeyForNoReason";
    private string ScoreUploadBaseURL = "https://wangqaq.com/api/eol/upload_score.php";

    public void UpdateCopyData(String player1,String player2,string score1,string score2)
    {
        Player1 = player1;
        Player2 = player2;
        if (score1 == score2)
        {
            copyField.text = "null";
            return;
        }
        string hash = UdonHashLib.MD5_UTF8(player1 + player2 + score1 + score2 + hashKey);
        copyField.text = $"{ScoreUploadBaseURL}?player1={player1}&player2={player2}&score1={score1}&score2={score2}&hash={hash}";
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
            errorText.text = ("不是你的比赛你上传???,don't upload others score");
            return;
        }

       // noteDownloader.newNoteButton.interactable = false;
        VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);

        scoreManager.M_Score_Reset();
        copyField.text = "Upload Starting";
        errorText.text = "loading";
    }
    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        copyField.text = "Upload Finished";
        errorText.text = "Upload connected" + result.Result;
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
    void Start()
    {
        //UpdateCopyData("测试3", "测试4", "36", "0");
        //TryToUploadNote();
    }
}
