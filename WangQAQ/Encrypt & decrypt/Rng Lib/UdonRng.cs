/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *	Udon随机数系统
 */
using UnityEngine;
using UdonSharp;
using VRC.SDKBase;
using System.Security.Policy;
using System;
using BestHTTP.Extensions;

namespace WangQAQ.ED
{
	// 若作为加密用随机数，建议使用前先预热（玩家进入5-10秒后再调用）
	public class UdonRng : UdonSharpBehaviour
	{
		public static string GetRngSha256S()
		{
			return UdonHashLib.SHA256_Bytes(longToBytes(GetRng()));
		}

		public static byte[] GetRngSha256()
		{
			string rngSha = UdonHashLib.SHA256_Bytes(longToBytes(GetRng()));
			byte[] sha256Bytes = new byte[rngSha.Length / 2];

			for (int i = 0; i < sha256Bytes.Length; i++)
			{
				sha256Bytes[i] = Convert.ToByte(rngSha.Substring(i * 2, 2), 16);
			}

			return sha256Bytes;
		}

		public static long GetRng()
		{
			long ret = 0;
			long last = 1;
			float time = Networking.GetServerTimeInMilliseconds();
			VRCPlayerApi papi = Networking.LocalPlayer;
			Vector3 pos = Networking.LocalPlayer.GetPosition();

			var lastTag = papi.GetPlayerTag("WUdonRng");
			if(!string.IsNullOrWhiteSpace(lastTag))
			{
				last = Convert.ToInt64(lastTag);
			}

			int i = ((int)((pos.x + pos.z + pos.y) % 256)) * (int)(time % 16777210);
			long p = i * (long)(papi.GetAvatarEyeHeightAsMeters() * (time % 1024));
			ret = p * (long)time;

			papi.SetPlayerTag("WUdonRng",ret.ToString());

			return ret;
		}

		private static byte[] longToBytes(long value)
		{
			byte[] bytes = new byte[8];

			for (int i = 0; i < 8; i++)
			{
				bytes[i] = (byte)((value >> (i * 8)) & 0xFF);
			}

			return bytes;
		}
	}
}