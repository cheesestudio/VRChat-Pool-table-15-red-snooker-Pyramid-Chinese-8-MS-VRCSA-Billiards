using OnlinePinboard;
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
    public ScoreManager scoreManager;

    private string hashKey = "CheeseIsTheHashKeyForNoReason";
    private string ScoreUploadBaseURL = "http://106.14.156.156/upload_score.php";

    public void UpdateCopyData(String player1,String player2,string score1,string score2)
    {
        string hash = UdonHashLib.MD5_UTF8(player1 + player2 + score1 + score2 + hashKey);
        copyField.text = $"{ScoreUploadBaseURL}?player1={player1}&player2={player2}&score1={score1}&score2={score2}&hash={hash}";
        Debug.Log($"copyField =  {copyField.text}");
    }
    public void TryToUploadNote()
    {
        VRCUrl url = pasteField.GetUrl();

        pasteField.SetUrl(VRCUrl.Empty);
        if (url.ToString() != copyField.text)
        {
            return;
        }
       // noteDownloader.newNoteButton.interactable = false;
        VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);

        scoreManager.M_Score_Reset();
        copyField.text = "Upload Finished";
        //ActivateNoteUploadAnimation(true);
        //notePickup.ResetNote();
    }
    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        string resultJson = result.Result;
        Debug.Log(resultJson);
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        //ActivateNoteUploadAnimation(false);
        Debug.LogError($"Error loading string: {result.ErrorCode} - {result.Error}");
        //errorText.text = $"{result.ErrorCode} - {result.Error}";

        if (result.ErrorCode == 401)
        {
            //unauthorizedErrorInfo.SetActive(true);
        }
    }
    void Start()
    {
        //UpdateCopyData("cheese2", "fuckoff2", "111", "222");
        //TryToUploadNote();

    }
}
