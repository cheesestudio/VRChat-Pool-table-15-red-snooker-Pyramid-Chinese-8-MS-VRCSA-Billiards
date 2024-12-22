/*
 *  MIT License
 *  Copyright (c) 2024 WangQAQ
 *
 *  MangerV1
 */
using UnityEngine;
using VRC.SDKBase;
namespace WangQAQ.UdonPlug
{
	public class MangerV1 : IManger
	{

		#region ValueEdit

		[Header("ConfigDownload")]
		[SerializeField] private ConfigDownload _configDownload;

		[Header("TagPrefab")]
		[SerializeField] private GameObject _tagPrefab = null;

		[Header("TagTransform")]
		[SerializeField] private Transform _tagTransform = null;

		[Header("Materials")]
		[SerializeField] private Material[] _materials = null;

		#endregion

		#region PublicAPI

		public void Start()
		{
			_configDownload._Init(this);
		}

		public override void RefreshTag()
		{
			/* NOP */
		}

		#endregion

		#region CallBack

		public override void OnPlayerJoined(VRCPlayerApi player)
		{
			var index = _configDownload.GetPlayerTag(player.displayName);
			if (index != -1)
			{
				var obj = Instantiate(_tagPrefab, _tagTransform);
				var sharpObj = obj.GetComponent<TagCore>();
				sharpObj._Init(player, _materials[index], index);
			}
		}

		public override void OnConfigDownloadDone()
		{
			var playerArray = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
			VRCPlayerApi.GetPlayers(playerArray);

			foreach (var player in playerArray)
			{
				var index = _configDownload.GetPlayerTag(player.displayName);
				if (index != -1)
				{
					var obj = Instantiate(_tagPrefab, _tagTransform);
					var sharpObj = obj.GetComponent<TagCore>();
					sharpObj._Init(player, _materials[index], index);
				}
			}
		}

		#endregion
	}

}
