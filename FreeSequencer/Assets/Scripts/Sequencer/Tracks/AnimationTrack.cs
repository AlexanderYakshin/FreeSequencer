using System;
using UnityEngine;

namespace FreeSequencer.Tracks
{
	[Serializable]
	public class AnimationTrack : BaseTrack
	{
		public RuntimeAnimatorController Controller;
		public string ControllerLayer;
	}
}
