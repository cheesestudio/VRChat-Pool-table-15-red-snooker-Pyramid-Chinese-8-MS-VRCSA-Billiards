/*
 脚本来自 https://www.bilibili.com/video/BV1bg1oYaEQk
 */
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace WangQAQ.UdonPlug
{
	public class LookatPlayer : UdonSharpBehaviour
	{
		public float trackingSpeed = 2;

		private VRCPlayerApi localPlayer;

		public void Start()
		{
			localPlayer = Networking.LocalPlayer;
		}

		public void Update()
		{
			VRCPlayerApi.TrackingData data = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
			Quaternion targetRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(data.position - transform.position, Vector3.up), Vector3.up);
			transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, trackingSpeed * Time.deltaTime);
		}

	}
}
