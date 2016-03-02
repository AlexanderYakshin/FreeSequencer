using System.Collections.Generic;
using FreeSequencer.Tracks;
using UnityEngine;

namespace FreeSequencer
{
	public enum StartMode
	{
		OnStart,
		OnButton
	}

	public enum UpdateType
	{
		Normal,
		Fixed
	}

	public class Sequence : MonoBehaviour
	{
		public int FrameRate;
		public int Length;
		public UpdateType UpdateTypeMode;
		public List<AnimatedGameObject> Objects = new List<AnimatedGameObject>();

		public StartMode StartMode;

		private void OnStart()
		{
			if (StartMode == StartMode.OnStart)
			{
				StartSequence();
			}
		}

		public void StartSequence()
		{
			foreach (AnimatedGameObject animatedGameObject in Objects)
			{
				foreach (BaseTrack baseTrack in animatedGameObject.Tracks)
				{
					var animationTrack = baseTrack as AnimationTrack;

					if (animationTrack != null)
					{
						StartAnimationTrack(animationTrack, animatedGameObject.GameObject);
					}
				}
			}
		}

		private void StartAnimationTrack(AnimationTrack animationTrack, GameObject trackGameObject)
		{
			var animator = trackGameObject.GetComponent<Animator>();
			animator.SetTrigger(this.gameObject.name);
		}
	}
}
