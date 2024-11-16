/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *  CC20 加密
 */

// WIP
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
namespace WangQAQ.ED
{
	public class CC20 : UdonSharpBehaviour
	{
		private const int BlockSize = 64;
		private uint[] state = new uint[16];
		private int index;

		public bool _Init(byte[] key, byte[] nonce, uint counter = 0)
		{
			if (key.Length != 32)
				return false;
			if (nonce.Length != 12)
				return false;

			// Initialize state
			state[0] = 0x61707865;
			state[1] = 0x3320646e;
			state[2] = 0x79622d32;
			state[3] = 0x6b206574;
			for (int i = 0; i < 8; i++)
				state[4 + i] = BitConverter.ToUInt32(key, i * 4);

			state[12] = counter;
			for (int i = 0; i < 3; i++)
				state[13 + i] = BitConverter.ToUInt32(nonce, i * 4);

			index = 0;

			return true;
		}

		private void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
		{
			a += b; d ^= a; d = (d << 16) | (d >> 16);
			c += d; b ^= c; b = (b << 12) | (b >> 20);
			a += b; d ^= a; d = (d << 8) | (d >> 24);
			c += d; b ^= c; b = (b << 7) | (b >> 25);
		}

		private void Salsa20Hash()
		{
			var x = (uint[])state.Clone();
			for (int i = 0; i < 10; i++)
			{
				QuarterRound(ref x[0], ref x[4], ref x[8], ref x[12]);
				QuarterRound(ref x[1], ref x[5], ref x[9], ref x[13]);
				QuarterRound(ref x[2], ref x[6], ref x[10], ref x[14]);
				QuarterRound(ref x[3], ref x[7], ref x[11], ref x[15]);
				QuarterRound(ref x[0], ref x[5], ref x[10], ref x[15]);
				QuarterRound(ref x[1], ref x[6], ref x[11], ref x[12]);
				QuarterRound(ref x[2], ref x[7], ref x[8], ref x[13]);
				QuarterRound(ref x[3], ref x[4], ref x[9], ref x[14]);
			}
			for (int i = 0; i < 16; i++)
				x[i] += state[i];
			Buffer.BlockCopy(x, 0, state, 0, BlockSize);
		}

		public byte[] Process(byte[] data)
		{
			var output = new byte[data.Length];
			for (int i = 0; i < data.Length; i++)
			{
				if (index == 0)
					Salsa20Hash();

				output[i] = (byte)(data[i] ^ (state[index / 4] >> (8 * (index % 4)) & 0xff));
				index++;
				if (index >= BlockSize)
				{
					index = 0;
					state[12]++; // Increment the counter
				}
			}
			return output;
		}
	}

}