using System;
using UnityEngine;

namespace FreeSequencer.Events
{
	[Serializable]
	public class AnimationTrackEvent:TrackEvent
	{
		public AnimationState State;
		public AnimationClip Clip;
		public bool ControlAnimation;

		public AnimationTrackEvent()
		{
			ControlAnimation = false;
		}
	}
}
