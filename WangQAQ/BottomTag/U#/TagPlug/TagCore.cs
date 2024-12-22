/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *  标签脚本
 */
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using WangQAQ.UdonPlug;

public class TagCore : UdonSharpBehaviour
{
	#region ValueEdit

	[SerializeField] private Renderer _renderer;

	#endregion

	#region PrivateValue

	private ImageDownload _imageDownload = null;
	private VRCPlayerApi _vrcPlayerApi = null;

	private Material _material = null;

	#endregion

	public void _Init(VRCPlayerApi playerApi,Material material,int index)
	{
		_vrcPlayerApi = playerApi;
		_imageDownload = transform.Find("../../U#/Downloads/ImageDownload").GetComponent<ImageDownload>();
		_material = material;

		if (_vrcPlayerApi == null ||
			_imageDownload == null||
			_material == null)
			return;

		_imageDownload.TrySetMatTexture(index , _material);
		_renderer.material = _material;
	}

	public void Update()
	{
		if(_vrcPlayerApi.IsValid())
		{
			transform.position = _vrcPlayerApi.GetPosition();
			transform.rotation = _vrcPlayerApi.GetRotation();
		}
		else
		{
			Destroy(gameObject);
		}
	}
}
