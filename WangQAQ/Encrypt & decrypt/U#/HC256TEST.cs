//DEBUG
/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 */
#if DEBUG
using System;
using System.Text;
using UdonSharp;
using Unity.Burst.Intrinsics;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace WangQAQ.ED
{
	public class HC256TEST : UdonSharpBehaviour
	{
		public HC256 _hc256;

		public string InText = "";
		public string Key = "";

		public string IVb64 = "";
		public string Outb64 = "";

		public string Done = "";

		public void _Test()
		{
			byte[] iv32 = UdonRng.GetRngSha256();	// 假设已有 32 字节的数组
			IVb64 = Convert.ToBase64String(iv32);

			var t = _hc256.Process(Encoding.UTF8.GetBytes(InText), UdonHashLib.HexStringToByteArray(UdonHashLib.SHA256_UTF8(Key)), iv32);
			Outb64 = Convert.ToBase64String(t);
		}

		public void _test2()
		{
			var iv = Convert.FromBase64String(IVb64);
			var t = Convert.FromBase64String(Outb64);

			Done = Encoding.UTF8.GetString(_hc256.Process(t, UdonHashLib.HexStringToByteArray(UdonHashLib.SHA256_UTF8(Key)), iv));
		}

	}
}

#endif