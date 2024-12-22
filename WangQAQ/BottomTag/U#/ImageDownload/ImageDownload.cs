/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *  图片下载器
 */
using System;
using System.Numerics;
using System.Reflection;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.Image;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

/// <summary>
/// 由于UDON无法动态构建URL
///	所以URL全部为预存
///	以下四个数组大小，下标一一对应
///	ImageUrlArray
///	imageDownloads
///	matBindMap
///	reloadCount
/// </summary>
namespace WangQAQ.UdonPlug
{
	public class ImageDownload : UdonSharpBehaviour
	{
		#region PubilcValueEdit

		public VRCUrl[] ImageUrlArray;

		#endregion

		#region ValueEdit

		[SerializeField] private int maxReloadTry = 3;

		#endregion

		#region PubilcValueAPI

		public IVRCImageDownload[] VRCImage => imageDownloads;

		#endregion

		#region PrivateValue

		private IVRCImageDownload[] imageDownloads;
		private VRCImageDownloader imageDownloader;

		private DataList[] matBindMap;
		private int[] reloadCount;

		private TextureInfo rgbInfo = new TextureInfo();

		#endregion

		#region PubilcFunc

		public void Start()
		{
			imageDownloads = new IVRCImageDownload[100];
			imageDownloader = new VRCImageDownloader();
			matBindMap = new DataList[100];
			reloadCount = new int[100];

			rgbInfo.GenerateMipMaps = true;
		}

		/// <summary>
		/// -1 ERROR
		/// 0  Done
		/// 1  WaitDownload
		/// 2  StartDownload
		/// </summary>
		/// <param name="index"></param>
		/// <param name="mat"></param>
		/// <returns></returns>
		public short TrySetMatTexture(int index, Material mat)
		{
			var imageObj = imageDownloads[index];

			if (ImageUrlArray[index] == null)
				return -1;

			if (imageObj != null && imageObj.State == VRCImageDownloadState.Complete)
			{
				mat.mainTexture = imageObj.Result;
				return 0;
			}
			else if (imageObj != null && imageObj.State == VRCImageDownloadState.Pending)
			{
				bindMatTextureOnCallback(index, mat);
				return 1;
			}
			else
			{
				imageDownloads[index] = downloadTexture(index);
				bindMatTextureOnCallback(index, mat);
				return 2;
			}
		}
		#endregion

		#region CallBack

		public override void OnImageLoadSuccess(IVRCImageDownload result)
		{
			int index = Array.IndexOf(ImageUrlArray, result.Url);

			if (index == -1)
				return;

			var matList = matBindMap[index];

			for (int i = 0; i < matList.Count; i++)
			{
				((Material)matList[i].Reference).mainTexture = result.Result;
			}

			matBindMap[index] = null;

		}

		public override void OnImageLoadError(IVRCImageDownload result)
		{
			int index = Array.IndexOf(ImageUrlArray, result.Url);

			if (index == -1)
				return;

			if (reloadCount[index] > maxReloadTry)
				return;

			if (ImageUrlArray[index] == null)
				return;

			imageDownloads[index] =
				imageDownloader.DownloadImage(
					ImageUrlArray[index],
					null,
					(IUdonEventReceiver)this,
					rgbInfo);

			reloadCount[index]++;
		}

		#endregion

		#region Func

		private IVRCImageDownload downloadTexture(int index)
		{
			if (ImageUrlArray[index] == null)
				return null;

			return imageDownloader.DownloadImage(ImageUrlArray[index], null, (IUdonEventReceiver)this, rgbInfo);
		}

		private void bindMatTextureOnCallback(int index, Material mat)
		{
			DataToken obj = mat;

			if (matBindMap[index] == null)
				matBindMap[index] = new DataList();

			matBindMap[index].Add(obj);
		}

		#endregion
	}
}