/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *	第二版彩色名称下载
 */
using System.Text;
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.InputSystem;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using WangQAQ.ED;

namespace WangQAQ.UdonPlug
{
	public class ColorDownloaderV2 : UdonSharpBehaviour
	{

		[HideInInspector] public VRCUrl url = null;
		[HideInInspector] public byte[] key = null;
		[HideInInspector] public HC256 _hc256;

		/// <summary>
		/// name,color
		/// </summary>
		[HideInInspector] public DataDictionary _colors;

		private bool isLoading = false;
		void Start()
		{
			if (url == null ||
				key == null	)
				return;

			_hc256 = GameObject.Find("HC256").GetComponent<HC256>();
			key = BLAKE2b.BLAKE2b_256(key);

			VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
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
				if (VRCJson.TryDeserializeFromJson(stringContext, out var json1))
				{
					_colors = json1.DataDictionary;
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
}

