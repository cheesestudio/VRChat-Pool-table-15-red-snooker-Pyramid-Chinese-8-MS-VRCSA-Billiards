/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *	Udon BLAKE2b
 */

using System;
using UdonSharp;

namespace WangQAQ.ED
{
	public class BLAKE2b : UdonSharpBehaviour
	{
		// 核心 G 函数
		private static void G(ref ulong a, ref ulong b, ref ulong c, ref ulong d, ulong x, ulong y)
		{
			a = a + b + x;
			d = RotateRight(d ^ a, 32);
			c = c + d;
			b = RotateRight(b ^ c, 24);
			a = a + b + y;
			d = RotateRight(d ^ a, 16);
			c = c + d;
			b = RotateRight(b ^ c, 63);
		}

		private static void Compress(ulong[] h, ulong[] m, ulong t0, ulong t1, bool isFinal, byte[][] sigma, ulong[] iv)
		{
			ulong[] v = new ulong[16];
			Array.Copy(h, v, 8);
			Array.Copy(iv, 0, v, 8, 8);

			v[12] ^= t0;
			v[13] ^= t1;
			if (isFinal)
				v[14] = ~v[14];

			for (int i = 0; i < 12; i++) // 12 轮
			{
				G(ref v[0], ref v[4], ref v[8], ref v[12], m[sigma[i][0]], m[sigma[i][1]]);
				G(ref v[1], ref v[5], ref v[9], ref v[13], m[sigma[i][2]], m[sigma[i][3]]);
				G(ref v[2], ref v[6], ref v[10], ref v[14], m[sigma[i][4]], m[sigma[i][5]]);
				G(ref v[3], ref v[7], ref v[11], ref v[15], m[sigma[i][6]], m[sigma[i][7]]);
				G(ref v[0], ref v[5], ref v[10], ref v[15], m[sigma[i][8]], m[sigma[i][9]]);
				G(ref v[1], ref v[6], ref v[11], ref v[12], m[sigma[i][10]], m[sigma[i][11]]);
				G(ref v[2], ref v[7], ref v[8], ref v[13], m[sigma[i][12]], m[sigma[i][13]]);
				G(ref v[3], ref v[4], ref v[9], ref v[14], m[sigma[i][14]], m[sigma[i][15]]);
			}

			for (int i = 0; i < 8; i++)
			{
				h[i] ^= v[i] ^ v[i + 8];
			}
		}

		private static byte[] ComputeHash(byte[] input, int outputSize = 64)
		{
			ulong[] IV = new ulong[]
			{
				0x6a09e667f3bcc908, 0xbb67ae8584caa73b,
				0x3c6ef372fe94f82b, 0xa54ff53a5f1d36f1,
				0x510e527fade682d1, 0x9b05688c2b3e6c1f,
				0x1f83d9abfb41bd6b, 0x5be0cd19137e2179
			};

			byte[][] Sigma = new byte[][]
			{
				new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
				new byte[] { 14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3 },
				new byte[] { 11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4 },
				new byte[] { 7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8 },
				new byte[] { 9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13 },
				new byte[] { 2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9 },
				new byte[] { 12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11 },
				new byte[] { 13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10 },
				new byte[] { 6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5 },
				new byte[] { 10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0 },
				new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
				new byte[] { 14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3 }
			};

			if (outputSize <= 0 || outputSize > 64)
				return null;

			ulong[] h = (ulong[])IV.Clone();
			h[0] ^= 0x01010000 | (ulong)outputSize;

			int fullBlocks = input.Length / 128;
			ulong t0 = 0, t1 = 0;

			for (int i = 0; i < fullBlocks; i++)
			{
				byte[] block = new byte[128];
				Array.Copy(input, i * 128, block, 0, 128);
				ulong[] m = new ulong[16];
				for (int j = 0; j < 16; j++)
				{
					m[j] = BitConverter.ToUInt64(block, j * 8);
				}

				t0 += 128;
				Compress(h, m, t0, t1, false, Sigma, IV);
			}

			byte[] lastBlock = new byte[128];
			int remaining = input.Length % 128;
			Array.Copy(input, fullBlocks * 128, lastBlock, 0, remaining);

			t0 += (ulong)remaining;
			ulong[] finalM = new ulong[16];
			for (int j = 0; j < 16; j++)
			{
				if (j * 8 < remaining)
					finalM[j] = BitConverter.ToUInt64(lastBlock, j * 8);
			}

			Compress(h, finalM, t0, t1, true, Sigma, IV);

			byte[] output = new byte[outputSize];
			for (int i = 0; i < outputSize; i++)
			{
				output[i] = (byte)((h[i / 8] >> (8 * (i % 8))) & 0xFF);
			}

			return output;
		}

		private static byte[] ComputeHMAC(byte[] key, byte[] input, int outputSize = 64)
		{
			byte[] paddedKey = new byte[128];
			if (key.Length > 128)
			{
				key = ComputeHash(key, 64);
			}
			Array.Copy(key, 0, paddedKey, 0, key.Length);

			byte[] innerPad = new byte[128];
			byte[] outerPad = new byte[128];

			for (int i = 0; i < 128; i++)
			{
				innerPad[i] = (byte)(paddedKey[i] ^ 0x36);
				outerPad[i] = (byte)(paddedKey[i] ^ 0x5C);
			}

			byte[] innerInput = new byte[innerPad.Length + input.Length];
			Array.Copy(innerPad, 0, innerInput, 0, innerPad.Length);
			Array.Copy(input, 0, innerInput, innerPad.Length, input.Length);
			byte[] innerHash = ComputeHash(innerInput, 64);

			byte[] outerInput = new byte[outerPad.Length + innerHash.Length];
			Array.Copy(outerPad, 0, outerInput, 0, outerPad.Length);
			Array.Copy(innerHash, 0, outerInput, outerPad.Length, innerHash.Length);

			byte[] finalHash = ComputeHash(outerInput, 64);
			byte[] result = new byte[outputSize];
			Array.Copy(finalHash, result, outputSize);
			return result;
		}

		// 128 位

		public static byte[] BLAKE2b_128(byte[] input)
		{
			return ComputeHash(input, 16);
		}

		public static byte[] HMAC_BLAKE2b_128(byte[] key, byte[] input)
		{
			return ComputeHMAC(key, input,16);
		}

		// 256 位

		public static byte[] BLAKE2b_256(byte[] input)
		{
			return ComputeHash(input, 32);
		}

		public static byte[] HMAC_BLAKE2b_256(byte[] key, byte[] input)
		{
			return ComputeHMAC(key, input, 32);
		}

		// 512 位

		public static byte[] BLAKE2b_512(byte[] input)
		{
			return ComputeHash(input, 64);
		}

		public static byte[] HMAC_BLAKE2b_512(byte[] key, byte[] input)
		{
			return ComputeHMAC(key, input, 64);
		}

		private static ulong RotateRight(ulong x, int n) => (x >> n) | (x << (64 - n));
	}
}