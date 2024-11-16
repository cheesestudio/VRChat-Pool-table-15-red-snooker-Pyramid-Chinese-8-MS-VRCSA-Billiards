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
			byte[] rng = UdonRng.GetRngSha256();
			IVb64 = Convert.ToBase64String(rng);

			var t = _hc256.Process(Encoding.UTF8.GetBytes(InText), Encoding.ASCII.GetBytes(Key), rng);
			Outb64 = Convert.ToBase64String(t);
		}

		public void _test2()
		{
			var rng = Convert.FromBase64String(IVb64);
			var t = Convert.FromBase64String(Outb64);

			Done = Encoding.UTF8.GetString(_hc256.Process(t, Encoding.ASCII.GetBytes(Key), rng));
		}

	}
}

#endif