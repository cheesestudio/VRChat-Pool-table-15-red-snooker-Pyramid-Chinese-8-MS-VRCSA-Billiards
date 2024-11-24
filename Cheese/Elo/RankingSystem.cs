using System;
using System.Text;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using WangQAQ.ED;
using static System.Net.Mime.MediaTypeNames;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class RankingSystem : UdonSharpBehaviour
{
	[Header("References")]
	public InputField copyField;
	public VRCUrlInputField pasteField;
	[UdonSynced] private string errorString;
	public TextMeshProUGUI errorText;

	// 台球名称
	public string[] TableName = null;

	public string WorldGUID = null;
	public string Key = "CheeseIsTheHashKeyForNoReason";
	public string ScoreUploadBaseURL = "https://www.wangqaq.com/AspAPI/table/UploadScore/v2.1";

	private string Player1 = "";
	private string Player2 = "";

	private CC20 _cc20;

	// Ioc 
	[HideInInspector] public ScoreManagerV4 _scoreManager;

#if DEBUG
	//Debug
	[HideInInspector] public string scoreDebugA = "";
	[HideInInspector] public string scoreDebugB = "";
	public void DebugUpload()
	{
		UpdateCopyData("testa", "testb", scoreDebugA, scoreDebugB, 0, DateTime.UtcNow.ToString("o"));
	}
#endif

	public void _Init(ScoreManagerV4 scoreManager)
	{
		_scoreManager = scoreManager;
	}

	void Start()
	{
		_cc20 = GameObject.Find("CC20").GetComponent<CC20>();
	}

	#region upload
	public void UpdateCopyData(string player1, string player2, string score1, string score2, uint ballMode, string Date)
	{
		Player1 = player1;
		Player2 = player2;
		if (score1 == score2 ||
			string.IsNullOrEmpty(player1) ||
			string.IsNullOrEmpty(player2))
		{
			copyField.text = "null";
			errorText.text = "";
			return;
		}

		if (string.IsNullOrEmpty(WorldGUID) || string.IsNullOrEmpty(Key))
		{
			return;
		}

		byte[] key = BLAKE2b.BLAKE2b_256(Encoding.UTF8.GetBytes(Key));
		byte[] iv32 = UdonRng.GetRngBLAKE2b256();   // 假设已有 32 字节的数组
		byte[] iv12 = new byte[12];             // 假设已有 32 字节的数组

		Array.Copy(iv32, iv12, 12);

		_cc20._Init(key, iv12);

		var modeString = mapModeName(ballMode);
		// 新API
		DataDictionary data = new DataDictionary();
		data.Add("Player1", player1);
		data.Add("Player2", player2);
		data.Add("PlayerScore1", score1);
		data.Add("PlayerScore2", score2);
		data.Add("mode", modeString);
		data.Add("date", Date);
		string eData;
		string base64Guid = ToUrl(Convert.ToBase64String(Guid.Parse(WorldGUID).ToByteArray()));
		string base64IV = ToUrl(Convert.ToBase64String(iv12));
		string base64ID = string.Empty;
		string base64HMAC = string.Empty;
		if (VRCJson.TrySerializeToJson(data, JsonExportType.Minify, out DataToken json))
		{
			eData = ToUrl(Convert.ToBase64String(_cc20.Process(Encoding.UTF8.GetBytes(json.String))));
			base64ID = ToUrl(Convert.ToBase64String(BLAKE2b.BLAKE2b_128(Encoding.UTF8.GetBytes(json.String))));
		}
		else
		{
			return;
		}
		var context = $"{base64Guid}.{base64IV}.{eData}.{base64ID}";
		base64HMAC = ToUrl(Convert.ToBase64String(BLAKE2b.HMAC_BLAKE2b_256(key, Encoding.UTF8.GetBytes(context))));
		copyField.text = $"{ScoreUploadBaseURL}/{context}.{base64HMAC}";

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

#if !DEBUG
		string localPlayer = Networking.LocalPlayer.displayName;
		if (localPlayer != Player1 && localPlayer != Player2)
		{
			errorText.text = ("不是你的比赛你上传???Don't upload others score");
			return;
		}
#endif

		// noteDownloader.newNoteButton.interactable = false;
		VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);

		_scoreManager.gameResetLocal();
		copyField.text = "Starting";
		errorText.text = "loading";
	}

	public override void OnStringLoadSuccess(IVRCStringDownload result)
	{
		string context = string.Empty;

		if (VRCJson.TryDeserializeFromJson(result.Result, out var json))
		{
			var data = json.DataDictionary["data"].DataDictionary;
			if (data["stateCode"] == 0)
			{
				context = "<color=green>上传成功</color>" + $"{data["msg"]} \n";
				context += "<color=red> 玩家1历史分数" + data["p1Last"] + "</color> ";
				context += "<color=blue> 玩家2历史分数" + data["p2Last"] + "</color> \n";
				context += "<color=red> 玩家1当前分数" + data["p1Now"] + "</color> ";
				context += "<color=blue> 玩家2当前分数" + data["p2Now"] + "</color> \n";
				context += "<color=yellow> 倍率" + data["magnification"] + "</color> ";
			}
			else if (data["stateCode"] == 1)
			{
				context = "<color=yellow>平局</color>";
			}
		}
		copyField.text = "Finished";
		if (!Networking.IsOwner(gameObject))
			Networking.SetOwner(Networking.LocalPlayer, gameObject);
		errorString = "connected  " + context;
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
	#endregion

	#region Func
	// 找台球名称（如果有的话）
	private string mapModeName(uint mode)
	{
		if (!string.IsNullOrEmpty(TableName[mode]))
			return TableName[mode];
		return "NotFind";
	}

	private static string ToUrl(string base64)
	{
		return base64.Replace("+", "-").Replace("/", "_").Replace("=", "~");
	}

	public void ClearURL()
	{
		copyField.text = "null";
		errorText.text = "";
	}
	#endregion

	public override void OnDeserialization()
	{
		errorText.text = errorString;
	}

}
