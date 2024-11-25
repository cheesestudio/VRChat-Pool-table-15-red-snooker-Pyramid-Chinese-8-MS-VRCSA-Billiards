
using System.Security.Policy;
using System.Text;
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.InputSystem;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using WangQAQ.ED;
using UnityEngine.TerrainTools;

namespace WangQAQ.UdonPlug
{
	public class GetMainContext : UdonSharpBehaviour
	{
		[SerializeField] private TextMeshProUGUI MainContext;

		[HideInInspector] public VRCUrl[] urls;
		[HideInInspector] public byte[] key = null;
		[HideInInspector] public HC256 _hc256;

		[HideInInspector] public uint _UrlID = 0;

		private bool isLoad = false;

		void Start()
		{
			if (urls == null ||
				key == null)
				return;

			_hc256 = GameObject.Find("HC256").GetComponent<HC256>();
			key = BLAKE2b.BLAKE2b_256(key);
		}

		public void GetContext()
		{
			if (!isLoad)
            {
				MainContext.text = "<size=40>Loading...</size>";
				VRCStringDownloader.LoadUrl(urls[_UrlID], (IUdonEventReceiver)this);
			}
			isLoad = true;
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
				MainContext.text = stringContext;
			}
			isLoad = false;
		}

		//字符串下载失败回调
		public override void OnStringLoadError(IVRCStringDownload result)
		{
			MainContext.text = "加载失败";
			isLoad = false;
		}

		#endregion
	}
}

