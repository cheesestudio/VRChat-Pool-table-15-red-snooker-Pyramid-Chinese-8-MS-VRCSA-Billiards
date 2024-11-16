/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *  HC256 对称加密
 */
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace WangQAQ.ED
{
	public class HC256 : UdonSharpBehaviour
	{
		private uint[] p = new uint[1024];
		private uint[] q = new uint[1024];
		public uint ctr = 0;

		private void init(byte[] k, byte[] iv)
		{
			uint[] temp = new uint[2560];

			for (int i = 0; i < 8; i++)
			{
				temp[i] = BitConverter.ToUInt32(k, i * 4);
				temp[i + 8] = BitConverter.ToUInt32(iv, i * 4);
			}

			for (int i = 16; i < 2560; i++)
			{
				temp[i] = (temp[i - 16] + temp[i - 9] + RotateRight(temp[i - 3], 7)) ^ RotateRight(temp[i - 13], 10) + temp[i - 6];
			}

			Array.Copy(temp, 512, p, 0, 1024);
			Array.Copy(temp, 1536, q, 0, 1024);

			ctr = 0;
		}

		private uint RotateRight(uint v, int n)
		{
			return (v >> n) | (v << (32 - n));
		}

		private uint GenerateKeystream()
		{
			uint j = ctr & 1023;
			uint output;
			if (ctr < 1024)
			{
				p[j] += p[(j - 10 + 1024) & 1023];
				output = q[(p[(j - 12 + 1024) & 1023] & 255)] ^ p[(p[(j - 23 + 1024) & 1023] >> 8) & 255];
				p[j] = output;
			}
			else
			{
				q[j] += q[(j - 10 + 1024) & 1023];
				output = p[(q[(j - 12 + 1024) & 1023] & 255)] ^ q[((q[(j - 23 + 1024) & 1023] >> 8) & 255)];
				q[j] = output;
			}

			ctr = (ctr + 1) & 2047;
			return output;
		}

		public byte[] Process(byte[] input, byte[] k, byte[] iv)
		{
			init(k, iv);

			Debug.Log(input.Length);

			byte[] output = new byte[input.Length];
			for (int i = 0; i < input.Length; i += 4)
			{
				uint stream = GenerateKeystream();
				byte[] streamBytes = BitConverter.GetBytes(stream);
				for (int j = 0; j < 4 && (i + j) < input.Length; j++)
				{
					output[i + j] = (byte)(input[i + j] ^ streamBytes[j]);
				}
			}
			return output;
		}
	}
}