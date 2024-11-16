
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.BC;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

public class Magnification : UdonSharpBehaviour
{
	// 需要加载中字符直接写在TMP里面就可以了
	// 同样，有自动绑定
	[SerializeField] private MagnificationDownload _download = null;
	// 这个要自己加
	[SerializeField] private TextMeshPro _tmp = null;

	void Start()
	{
		if (_tmp == null)
			return;

		// 尝试寻找控件
		if (_download == null)
		{
			_download = GameObject.Find("MagnificationDownload").GetComponent<MagnificationDownload>();
			if (_download == null)
			{
				return;
			}
		}

		// 30秒后开始首次调用
		SendCustomEventDelayedSeconds("TryLoad", 30);
	}

	/// <summary>
	/// 查询是否从网页上下载完成
	/// </summary>
	public void TryLoad()
	{
		if(_download._Magnification != null && _download._Magnification.Count != 0)
		{
			loadText();
			// 加载完成后关闭脚本节约资源
			this.enabled = false;
		}
		SendCustomEventDelayedSeconds("TryLoad", 30);
	}

	/// <summary>
	/// 加载字符
	/// </summary>
	private void loadText()
	{
		// 格式化字符串
		string magnificationBoardString = "";
		DataList types = _download._Magnification.GetKeys();
		DataList magnification = _download._Magnification.GetValues();
		for (int i = 0; i < _download._Magnification.Count; i++)
		{
			var typeTmp = types[i].ToString().Replace(" ", " ");
			// 转码，去除小数点，格式化，替换空格 \u0020 到 \u00A0
			magnificationBoardString +=
				(i + 1).ToString() + "."
				+ typeTmp
				+ " "
				+ magnification[i].ToString()
				+ "\n";
		}
		_tmp.text = magnificationBoardString;
	}
}
