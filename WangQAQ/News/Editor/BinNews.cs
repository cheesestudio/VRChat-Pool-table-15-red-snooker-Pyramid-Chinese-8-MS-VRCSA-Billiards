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

[InitializeOnLoad]
public class BinNews : MonoBehaviour
{
	private static string baseUrl = "https://www.wangqaq.com/AspAPI/table/GetNews/";
	static BinNews()
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
			var uploadOBJ = Resources.FindObjectsOfTypeAll<GetNewsList>().ToList();
			var uploadOBJ1 = Resources.FindObjectsOfTypeAll<GetMainContext>().ToList();
			var tmp = File.ReadAllText(path).Split("||");

			if (tmp.Length != 2)
			{
				return;
			}

			var tmpKey = tmp[0];
			var tmpGuid = tmp[1];
			foreach (var obj in uploadOBJ)
			{
				obj.key = Encoding.UTF8.GetBytes(tmpKey);
				obj.url = new VRCUrl(baseUrl + tmpGuid);
			}

			foreach (var obj in uploadOBJ1)
			{
				obj.key = Encoding.UTF8.GetBytes(tmpKey);
				obj.urls = new VRCUrl[100];
				for (var i = 0; i < 100; i++)
				{
					obj.urls[i] = new VRCUrl(baseUrl + tmpGuid + $"/{i}");
				}
			}
		}
	}
}
