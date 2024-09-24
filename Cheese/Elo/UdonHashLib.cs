
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

// -*- coding: utf-8 -*-
/*
 * Code modified by Oikki
 * check original at https://github.com/Gorialis/vrchat-udon-hashlib/blob/main/Assets/Gorialis/UdonHashLib/UdonSharp/UdonHashLib.cs
MIT License

Copyright (c) 2021-present Devon (Gorialis) R

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

//cheese using it now

using System;
using System.Linq;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
#endif


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class UdonHashLib : UdonSharpBehaviour
{
    // Udon does not support UTF-8 or expose System.Text.Encoding so we must implement this ourselves
    private static byte[] ToUTF8(char[] characters)
    {
        byte[] buffer = new byte[characters.Length * 4];

        int writeIndex = 0;
        for (int i = 0; i < characters.Length; i++)
        {
            uint character = characters[i];

            if (character < 0x80)
            {
                buffer[writeIndex++] = (byte)character;
            }
            else if (character < 0x800)
            {
                buffer[writeIndex++] = (byte)(0b11000000 | ((character >> 6) & 0b11111));
                buffer[writeIndex++] = (byte)(0b10000000 | (character & 0b111111));
            }
            else if (character < 0x10000)
            {
                buffer[writeIndex++] = (byte)(0b11100000 | ((character >> 12) & 0b1111));
                buffer[writeIndex++] = (byte)(0b10000000 | ((character >> 6) & 0b111111));
                buffer[writeIndex++] = (byte)(0b10000000 | (character & 0b111111));
            }
            else
            {
                buffer[writeIndex++] = (byte)(0b11110000 | ((character >> 18) & 0b111));
                buffer[writeIndex++] = (byte)(0b10000000 | ((character >> 12) & 0b111111));
                buffer[writeIndex++] = (byte)(0b10000000 | ((character >> 6) & 0b111111));
                buffer[writeIndex++] = (byte)(0b10000000 | (character & 0b111111));
            }
        }

        // We do this to truncate off the end of the array
        // This would be a lot easier with Array.Resize, but Udon once again does not allow access to it.
        byte[] output = new byte[writeIndex];

        for (int i = 0; i < writeIndex; i++)
            output[i] = buffer[i];

        return output;
    }
    private string BytesToString(byte[] bytes)
    {
        string output = "";
        foreach (var item in bytes)
        {
            output += item.ToString("x2");
        }
        return output;
    }


    private static string MD5_Core(byte[] payload_bytes, ulong[] init, ulong[] constants, int[] shifts, ulong size_mask, int word_size, int chunk_modulo, int appended_length, int round_count, string output_format, int output_segments)
    {
        int word_bytes = word_size / 8;

        // Working variables a0->d0
        ulong[] working_variables = new ulong[4];
        init.CopyTo(working_variables, 0);

        byte[] input = new byte[chunk_modulo];
        ulong[] message_schedule = new ulong[16];

        // Each 64-byte/512-bit chunk
        // 64 bits/8 bytes are required at the end for the bit size
        for (int chunk_index = 0; chunk_index < payload_bytes.Length + appended_length + 1; chunk_index += chunk_modulo)
        {
            int chunk_size = Mathf.Min(chunk_modulo, payload_bytes.Length - chunk_index);
            int schedule_index = 0;

            // Buffer message
            for (; schedule_index < chunk_size; ++schedule_index)
                input[schedule_index] = payload_bytes[chunk_index + schedule_index];
            // Append a 1-bit if not an even chunk
            if (schedule_index < chunk_modulo && chunk_size >= 0)
                input[schedule_index++] = 0b10000000;
            // Pad with zeros until the end
            for (; schedule_index < chunk_modulo; ++schedule_index)
                input[schedule_index] = 0x00;
            // If the chunk is less than 56 bytes, this will be the final chunk containing the data size in bits
            if (chunk_size < chunk_modulo - appended_length)
            {
                ulong bit_size = (ulong)payload_bytes.Length * 8ul;
                input[chunk_modulo - 8] = Convert.ToByte((bit_size >> 0x00) & 0xFFul);
                input[chunk_modulo - 7] = Convert.ToByte((bit_size >> 0x08) & 0xFFul);
                input[chunk_modulo - 6] = Convert.ToByte((bit_size >> 0x10) & 0xFFul);
                input[chunk_modulo - 5] = Convert.ToByte((bit_size >> 0x18) & 0xFFul);
                input[chunk_modulo - 4] = Convert.ToByte((bit_size >> 0x20) & 0xFFul);
                input[chunk_modulo - 3] = Convert.ToByte((bit_size >> 0x28) & 0xFFul);
                input[chunk_modulo - 2] = Convert.ToByte((bit_size >> 0x30) & 0xFFul);
                input[chunk_modulo - 1] = Convert.ToByte((bit_size >> 0x38) & 0xFFul);
            }

            // Copy into w[0..15]
            int copy_index = 0;
            for (; copy_index < 16; copy_index++)
            {
                message_schedule[copy_index] = 0ul;
                for (int i = 0; i < word_bytes; i++)
                {
                    message_schedule[copy_index] = message_schedule[copy_index] | ((ulong)input[(copy_index * word_bytes) + i] << (i * 8));
                }

                message_schedule[copy_index] = message_schedule[copy_index] & size_mask;
            }

            // temp vars
            ulong f, g;
            // work is equivalent to A, B, C, D
            // This copies work from a0, b0, c0, d0
            ulong[] work = new ulong[4];
            working_variables.CopyTo(work, 0);

            // Compression function main loop
            for (copy_index = 0; copy_index < round_count; copy_index++)
            {
                if (copy_index < 16)
                {
                    f = ((work[1] & work[2]) | ((size_mask ^ work[1]) & work[3])) & size_mask;
                    g = (ulong)copy_index;
                }
                else if (copy_index < 32)
                {
                    f = ((work[3] & work[1]) | ((size_mask ^ work[3]) & work[2])) & size_mask;
                    g = (ulong)(((5 * copy_index) + 1) % 16);
                }
                else if (copy_index < 48)
                {
                    f = work[1] ^ work[2] ^ work[3];
                    g = (ulong)(((3 * copy_index) + 5) % 16);
                }
                else
                {
                    f = (work[2] ^ (work[1] | (size_mask ^ work[3]))) & size_mask;
                    g = (ulong)(7 * copy_index % 16);
                }

                f = (f + work[0] + constants[copy_index] + message_schedule[g]) & size_mask;
                work[0] = work[3];
                work[3] = work[2];
                work[2] = work[1];
                work[1] = (work[1] + ((f << shifts[copy_index]) | (f >> word_size - shifts[copy_index]))) & size_mask;
            }

            for (copy_index = 0; copy_index < 4; copy_index++)
                working_variables[copy_index] = (working_variables[copy_index] + work[copy_index]) & size_mask;
        }

        // Finalization
        string output = "";

        for (int character_index = 0; character_index < output_segments; character_index++)
        {
            ulong value = working_variables[character_index];
            output += string.Format(output_format,
                ((value & 0x000000FFul) << 0x18) |
                ((value & 0x0000FF00ul) << 0x08) |
                ((value & 0x00FF0000ul) >> 0x08) |
                ((value & 0xFF000000ul) >> 0x18)
            );
        }

        return output;
    }

    public static string MD5_UTF8(string text)
    {
        int[] md5_shifts =
            {
            7, 12, 17, 22,  7, 12, 17, 22,  7, 12, 17, 22,  7, 12, 17, 22,
            5,  9, 14, 20,  5,  9, 14, 20,  5,  9, 14, 20,  5,  9, 14, 20,
            4, 11, 16, 23,  4, 11, 16, 23,  4, 11, 16, 23,  4, 11, 16, 23,
            6, 10, 15, 21,  6, 10, 15, 21,  6, 10, 15, 21,  6, 10, 15, 21,
        };
        ulong[] md5_constants = {
            0xd76aa478, 0xe8c7b756, 0x242070db, 0xc1bdceee, 0xf57c0faf, 0x4787c62a, 0xa8304613, 0xfd469501,
            0x698098d8, 0x8b44f7af, 0xffff5bb1, 0x895cd7be, 0x6b901122, 0xfd987193, 0xa679438e, 0x49b40821,
            0xf61e2562, 0xc040b340, 0x265e5a51, 0xe9b6c7aa, 0xd62f105d, 0x02441453, 0xd8a1e681, 0xe7d3fbc8,
            0x21e1cde6, 0xc33707d6, 0xf4d50d87, 0x455a14ed, 0xa9e3e905, 0xfcefa3f8, 0x676f02d9, 0x8d2a4c8a,
            0xfffa3942, 0x8771f681, 0x6d9d6122, 0xfde5380c, 0xa4beea44, 0x4bdecfa9, 0xf6bb4b60, 0xbebfbc70,
            0x289b7ec6, 0xeaa127fa, 0xd4ef3085, 0x04881d05, 0xd9d4d039, 0xe6db99e5, 0x1fa27cf8, 0xc4ac5665,
            0xf4292244, 0x432aff97, 0xab9423a7, 0xfc93a039, 0x655b59c3, 0x8f0ccc92, 0xffeff47d, 0x85845dd1,
            0x6fa87e4f, 0xfe2ce6e0, 0xa3014314, 0x4e0811a1, 0xf7537e82, 0xbd3af235, 0x2ad7d2bb, 0xeb86d391,
        };
        ulong[] md5_init = { 0x67452301, 0xefcdab89, 0x98badcfe, 0x10325476 };

        return MD5_Core(ToUTF8(text.ToCharArray()), md5_init, md5_constants, md5_shifts, 0xFFFFFFFFul, 32, 64, 8, 64, "{0:x8}", 4);
    }
}



