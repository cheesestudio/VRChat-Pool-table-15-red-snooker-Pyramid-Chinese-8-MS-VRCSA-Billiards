/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *  启动时自动配置
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDKBase;
using WangQAQ.UdonPlug;
namespace WangQAQ.Plug {
	[InitializeOnLoad]
	public class BindButtonTagKey : MonoBehaviour
	{
		private static string baseUrl = "https://www.wangqaq.com/AspAPI/table/GetTagConfig/";
		private static string baseUrl1 = "https://oss.wangqaq.com/pool-tag-img/";
		static BindButtonTagKey()
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

