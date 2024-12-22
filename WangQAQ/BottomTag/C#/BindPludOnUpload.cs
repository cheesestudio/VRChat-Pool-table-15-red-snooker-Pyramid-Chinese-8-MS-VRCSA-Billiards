/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *  上传时自动配置
 */
#if UNITY_EDITOR
using System.Linq;
using System;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Editor;
using VRC.SDKBase;
using System.Text;
using System.IO;
using WangQAQ.UdonPlug;
namespace WangQAQ.Plug
{
	public class BindPludOnUpload : MonoBehaviour, IEditorOnly
	{
		private static string baseUrl = "https://www.wangqaq.com/AspAPI/table/GetTagConfig/";
		private static string baseUrl1 = "https://oss.wangqaq.com/pool-tag-img/";
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
				var uploadOBJ = Resources.FindObjectsOfTypeAll<ConfigDownload>().ToList();
				var uploadOBJ1 = Resources.FindObjectsOfTypeAll<ImageDownload>().ToList();
				var tmp = File.ReadAllText(path).Split("||");

				if (tmp.Length != 2)
				{
					return;
				}

				var tmpKey = tmp[0];
				var tmpGuid = tmp[1];
				foreach (var obj in uploadOBJ)
				{
					obj.MapKey = Encoding.UTF8.GetBytes(tmpKey);
					obj.ConfigUrl = new VRCUrl(baseUrl + tmpGuid);
				}

				foreach (var obj in uploadOBJ1)
				{
					obj.ImageUrlArray = new VRCUrl[100];
					for (var i = 0; i < 100; i++)
					{
						obj.ImageUrlArray[i] = new VRCUrl(baseUrl1 + $"{i}");
					}
				}
			}
		}
	}
}
#endif
