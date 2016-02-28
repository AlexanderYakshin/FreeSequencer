using System.Collections.Generic;
using UnityEngine;

namespace FreeSequencer
{
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
	}
}
