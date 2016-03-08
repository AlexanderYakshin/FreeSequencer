using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FreeSequencer.Events;
using UnityEngine;

namespace FreeSequencer.Tracks
{
	public enum TrackType
	{
		Animation
	}

	[Serializable]
	public class BaseTrack : ScriptableObject
	{
		public TrackType Type;
		public bool Enabled;
		public string TrackName;

		public bool SyncAnimationWindow;
		public bool ShowTransformPath;
		public bool ShowRotationNormales;
		public bool ShowKeyFrames;
		public bool ShowKeyFramesTimes;
		public Color PathColor = Color.white;

		public string ObjectsPathToShowTransform;

		public List<TrackEvent> Events;

		public BaseTrack()
		{
			Events = new List<TrackEvent>();
			PathColor = Color.white;
		}
	}
}
