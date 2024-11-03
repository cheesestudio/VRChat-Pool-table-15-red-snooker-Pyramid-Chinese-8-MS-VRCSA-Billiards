using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Editor;
using VRC.SDKBase;

public class BindOnUpload : MonoBehaviour , IEditorOnly
{
	public const string UrlAPI = "https://www.wangqaq.com/AspAPI/table/UploadScore";
#if UNITY_EDITOR
	[InitializeOnLoadMethod]
	public static void RegisterSDKCallback()
	{
		VRCSdkControlPanel.OnSdkPanelEnable += AddBuildHook;
	}

	private static void AddBuildHook(object sender, EventArgs e)
	{
		if (VRCSdkControlPanel.TryGetBuilder<IVRCSdkWorldBuilderApi>(out var builder))
		{
			builder.OnSdkBuildStart += OnBuildStarted;
		}
	}

	private static void OnBuildStarted(object sender, object target)
	{
		const string path = "Assets/VRChatPoolMapKey.txt";
		if (File.Exists(path))
		{
			var uploadOBJ = Resources.FindObjectsOfTypeAll<RankingSystem>().ToList();
			var tmp = File.ReadAllText(path).Split("||");

			if (tmp.Length != 2)
			{
				return;
			}

			var tmpKey = tmp[0];
			var tmpGuid = tmp[1];

			foreach (var obj in uploadOBJ)
			{
				obj.useV2API = true;
				obj.hashKey = tmpKey;
				obj.ScoreUploadBaseURL = UrlAPI;
				obj.WorldGUID = tmpGuid;
			}
		}
	}
#endif
}
