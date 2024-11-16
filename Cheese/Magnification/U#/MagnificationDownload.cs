using System;
using System.Linq;
using System.Text;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using WangQAQ.ED;

public class MagnificationDownload : UdonSharpBehaviour
{
	/// <summary>
	/// 下面参数不需要手动添加，会自动绑定
	/// </summary>
	[HideInInspector] public VRCUrl url = null;
	[HideInInspector] public byte[] key = null;

	[HideInInspector] public HC256 _hc256;

	// Magnification字典对象
	[HideInInspector] public DataDictionary _Magnification = null;

	private bool isLoading = false;

	private void Start()
	{
		if (url == null ||
			key == null)
			return;

		_hc256 = GameObject.Find("HC256").GetComponent<HC256>();

		VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
		isLoading = true;
	}

	#region URL
	// 字符串下载成功回调
	public override void OnStringLoadSuccess(IVRCStringDownload result)
	{
		if (VRCJson.TryDeserializeFromJson(result.Result, out var json))
		{
			var data = json.DataDictionary["data"].DataDictionary;
			var i = data["i"].ToString();
			var context = data["context"].ToString();
			var decodeContext = _hc256.Process(Convert.FromBase64String(context), key, Convert.FromBase64String(i));
			var stringContext = Encoding.UTF8.GetString(decodeContext);
			Debug.Log(stringContext);
			if (VRCJson.TryDeserializeFromJson(stringContext, out var json1))
			{
				_Magnification = json1.DataDictionary;
			}
		}
	}

	//字符串下载失败回调
	public override void OnStringLoadError(IVRCStringDownload result)
	{
		isLoading = false;
		SendCustomEventDelayedSeconds("_AutoReload", 60);
	}

	//重新加载字符串函数
	public void _AutoReload()
	{
		//VRC下载API
		if (!isLoading)
		{
			VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
			isLoading = true;
		}
	}

	#endregion
}
