#if UNITY_EDITOR
using System.Linq;
using System;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Editor;
using VRC.SDKBase;
using VRC.SDKBase.Editor.Api;
using System.Security.Cryptography;
using System.Text;
using System.IO;

public class MagnificationDownloadBind : MonoBehaviour,IEditorOnly
{
	private static string baseUrl = "https://www.wangqaq.com/AspAPI/table/GetMagnification/";
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
		// 初始化对象
		var pipelineOBJ = FindObjectsOfType<PipelineManager>().SingleOrDefault();
		var GUID = "";

		// 获取世界GUID
		if (pipelineOBJ != null)
		{
			if (pipelineOBJ.GetType() == typeof(PipelineManager))
			{
				var tmp = pipelineOBJ.blueprintId.Split("_", StringSplitOptions.RemoveEmptyEntries);

				if (tmp.Length == 2)
					GUID = tmp[1];
				else
					return;
			}
			else
			{
				return;
			}
		}
		else
		{
			return;
		}

		string path = "Assets/" + GUID + "VRChatPoolMapKey.txt";
		if (File.Exists(path))
		{
			var uploadOBJ = Resources.FindObjectsOfTypeAll<MagnificationDownload>().ToList();
			var tmp = File.ReadAllText(path).Split("||");

			if (tmp.Length != 2)
			{
				return;
			}

			var tmpKey = tmp[0];
			var tmpGuid = tmp[1];
			using (SHA256 sha256 = SHA256.Create())
			{
				foreach (var obj in uploadOBJ)
				{
					obj.key = sha256.ComputeHash(Encoding.UTF8.GetBytes(tmpKey));
					obj.url = new VRCUrl(baseUrl + tmpGuid);
				}
			}
		}
	}
}
#endif