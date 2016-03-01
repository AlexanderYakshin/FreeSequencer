using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FreeSequencer.Events
{
	[Serializable]
	public class AnimationSeqEvent:BaseEvent
	{
		public AnimationState State;
		public AnimationClip Clip;
		public bool ControlAnimation;

		public AnimationSeqEvent()
		{
			ControlAnimation = true;
		}
	}
}
