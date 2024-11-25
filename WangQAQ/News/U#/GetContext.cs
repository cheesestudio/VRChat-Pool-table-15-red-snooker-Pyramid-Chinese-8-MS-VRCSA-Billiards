
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace WangQAQ.UdonPlug
{
	public class GetContext : UdonSharpBehaviour
	{
		[HideInInspector] public string Title;
		[HideInInspector] public string Description;

		[HideInInspector] public int UrlID;

		public UdonBehaviour _ContextDownloader;

		[SerializeField] private TextMeshProUGUI _titleTMP;
		[SerializeField] private TextMeshProUGUI _contextTMP;

		public void _Init()
		{
			_titleTMP.text = Title;
			_contextTMP.text = Description;

			_ContextDownloader = transform.Find("../../../../../../ContextDownloader").GetComponent<UdonBehaviour>();
		}

		public void OnChick()
		{
			_ContextDownloader.SetProgramVariable("_UrlID",(uint)UrlID);
			_ContextDownloader.SendCustomEvent("GetContext");
		}
	}
}

