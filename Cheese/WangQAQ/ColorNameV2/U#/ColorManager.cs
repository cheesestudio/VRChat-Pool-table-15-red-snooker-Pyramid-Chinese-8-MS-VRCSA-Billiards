/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *	第二版彩色名称
 */
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace WangQAQ.UdonPlug
{
	public class ColorManager : UdonSharpBehaviour
	{
		[HideInInspector] public string inOwner;
		[HideInInspector] public string outColor;

		//指向ColorDownloadV2获取彩色名称
		private ColorDownloaderV2 _colorDownloaderV2;

		void Start()
		{
			//尝试从世界查找Download 脚本
			_colorDownloaderV2 = GameObject.Find("ColorDownloaderV2").GetComponent<ColorDownloaderV2>();
		}

		//API 提供到台球彩色名称 （获取彩名）
		public void _GetNameColor()
		{

			if(_colorDownloaderV2 != null)
			{
				if (_colorDownloaderV2._colors != null)
				{
					outColor = _colorDownloaderV2._colors[inOwner].String;
					return;
				}
			}

			//查询不到，返回空
			outColor = "";
			return;
		}
	}
}

