using System;
using System.Collections.Generic;
using FreeSequencer.Tracks;
using UnityEngine;

namespace FreeSequencer
{
	[Serializable]
	public class AnimatedGameObject:ScriptableObject
	{
		public bool Toggled;
		public GameObject GameObject;
		public List<BaseTrack> Tracks;

		public AnimatedGameObject()
		{
			Toggled = true;
			Tracks = new List<BaseTrack>();
		}
	}
}