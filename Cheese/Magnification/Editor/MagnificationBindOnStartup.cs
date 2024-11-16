using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDKBase;

[InitializeOnLoad]
public class MagnificationBindOnStartup : MonoBehaviour
{
	private static string baseUrl = "https://www.wangqaq.com/AspAPI/table/GetMagnification/";
	static MagnificationBindOnStartup()
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
