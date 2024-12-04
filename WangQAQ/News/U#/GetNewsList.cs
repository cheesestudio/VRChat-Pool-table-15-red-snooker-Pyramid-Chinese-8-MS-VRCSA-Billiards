
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
using System.Linq;

namespace WangQAQ.UdonPlug
{
	public class GetNewsList : UdonSharpBehaviour
	{
		[SerializeField] private GameObject _Prefab;
		[SerializeField] private Transform ObjParent;

		[SerializeField] public VRCUrl url;

		[HideInInspector] public byte[] key = null;

		[HideInInspector] public HC256 _hc256;

		[HideInInspector] public DataDictionary NewsList;

		private bool isLoading = false;

		public void Start()
		{
			if (url == null ||
				key == null)
				return;

			_hc256 = GameObject.Find("HC256").GetComponent<HC256>();
			key = BLAKE2b.BLAKE2b_256(key);

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
				if (VRCJson.TryDeserializeFromJson(stringContext, out var json1))
				{
					NewsList = json1.DataDictionary;
					buildList();
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

		#region FUNC
		private void buildList()
		{
			var title = NewsList.GetKeys().ToArray();
			var description = NewsList.GetValues().ToArray();

			for (int i = 0; i < NewsList.Count; i++)
			{
				var a = Instantiate(_Prefab, ObjParent);
				var cmpObj = a.GetComponent<UdonBehaviour>();
				cmpObj.SetProgramVariable("Title", title[i].ToString());
				cmpObj.SetProgramVariable("Description", description[i].ToString());
				cmpObj.SetProgramVariable("UrlID", i);
				cmpObj.SendCustomEvent("_Init");
			}	
		}
		#endregion
	}
}
