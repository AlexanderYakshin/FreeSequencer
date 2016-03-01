using System;
using UnityEngine;

namespace FreeSequencer.Events
{
	[Serializable]
	public class BaseEvent : ScriptableObject
	{
		public string EventTitle;
		public int StartFrame;
		public int EndFrame;

		public int Length { get { return EndFrame - StartFrame; } }

		public Color EventInnerColor;
		public Color EventTitleColor;

		public BaseEvent()
		{
			EventInnerColor = Color.blue;
			EventTitleColor = Color.white;
		}
	}
}
