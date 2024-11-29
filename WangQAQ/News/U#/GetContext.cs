
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using WangQAQ.UdonPlug;

public class GetContext : UdonSharpBehaviour
{
	[HideInInspector] public string Title;
	[HideInInspector] public string Description;

	[HideInInspector] public int UrlID;

	private GetMainContext _ContextDownloader;

	[SerializeField] public TextMeshProUGUI _titleTMP;
	[SerializeField] public TextMeshProUGUI _contextTMP;

	public void _Init()
	{
		_ContextDownloader = transform.Find("../../../../../../ContextDownloader").GetComponent<GetMainContext>();
		_titleTMP = transform.Find("frame/Title/Text").GetComponent<TextMeshProUGUI>();
		_contextTMP = transform.Find("frame/Context/Text").GetComponent<TextMeshProUGUI>();

		_titleTMP.text = Title;
		_contextTMP.text = Description;
	}

	public void OnChick()
	{
		if (_ContextDownloader != null)
		{
			_ContextDownloader._UrlID = (uint)UrlID;
			_ContextDownloader.GetContext();
		}
	}
}

