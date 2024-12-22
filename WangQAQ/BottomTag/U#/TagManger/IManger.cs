/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *  MangerV1
 */
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
namespace WangQAQ.UdonPlug
{
	public class IManger : UdonSharpBehaviour
	{
		#region PublicAPI

		public virtual void RefreshTag()
		{
			/* NOP */
		}

		#endregion

		#region CallBack

		public virtual void OnConfigDownloadDone()
		{
			/* NOP */
		}

		#endregion
	}
}

