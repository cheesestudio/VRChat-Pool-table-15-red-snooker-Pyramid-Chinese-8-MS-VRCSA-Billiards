
using BestHTTP.JSON;
using System;
using System.Security.Policy;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace WangQAQ.Web
{
	public class WebConfig : UdonSharpBehaviour
	{
		[SerializeField] private EloDownload _eloDownloads;

		private VRCUrl url = new VRCUrl("https://wangqaq.com/config/table/table.json");

		void Start()
		{
			if (!_eloDownloads)
			{
				enabled = false;
				return;
			}

			VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);

		}

		#region URL
		// 字符串下载成功回调
		public override void OnStringLoadSuccess(IVRCStringDownload result)
		{
			if (VRCJson.TryDeserializeFromJson(result.Result, out var json))
			{
				_eloDownloads.ReloadSecond = Convert.ToInt32(json.DataDictionary["ReloadSec"].String);
			}
			enabled = false;
		}

		//字符串下载失败回调
		public override void OnStringLoadError(IVRCStringDownload result)
		{
			// 出错默认走15分钟刷新一次
			_eloDownloads.ReloadSecond = 15 * 60;
			enabled = false;
		}
		#endregion

	}
}
