using System;
using UnityEditor;
using UnityEngine;

namespace FreeSequencer.Editor
{
	public class TimeLineParameters
	{
		public int CurrentFrame;
		public int MinFrame;
		public int MaxFrame;
		public int FrameRate;
		public float StartWidth;
		public float EndWidth;
		public float StartHeight;
		public float EndHeight;
		public bool IsPlaying;

	}

	public class TimeLineArea
	{
		public event Action<int> OnChangeCurrentFrame;
		private const float MinDif = 20f;

		private float _gridWidth;
		private float _gridHeight;
		private int _koef;
		private float x_dif;


		public void OnDraw(TimeLineParameters parameters, Rect timelineRect)
		{
			var framesArea = new Rect(5f, 0f, timelineRect.width - 20f, timelineRect.height - 30f);
			GUILayout.BeginArea(framesArea);
			_gridWidth = framesArea.width;
			_gridHeight = framesArea.height;
			var frameLength = parameters.MaxFrame - parameters.MinFrame;
			//if (frameLength > )
			x_dif = _gridWidth / frameLength;
			_koef = 1;
			if (x_dif < MinDif)
			{
				_koef = Mathf.FloorToInt(MinDif / x_dif);
				x_dif *= _koef;
			}
			var linesCount = frameLength / _koef;

			Handles.color = Color.white;
			var i = 0;
			while (i <= linesCount)
			{
				bool evenFrame = i == 0 || i % 5 == 0;
				var color = evenFrame
					? Color.black
					: new Color(Color.white.r, Color.white.g, Color.white.b, 0.4f);
				if (evenFrame)
				{
					Handles.color = color;
					var x = i*x_dif;

					Handles.DrawLine(new Vector3(x, 0), new Vector3(x, framesArea.height));
					if (!parameters.IsPlaying && EditorHelper.GetMouseDownRect(new Rect(x - 2f, 0, 4f, _gridHeight)))
					{
						ChangeCurrentFrame(i*_koef, parameters);
					}
				}
				i++;
			}
			GUILayout.EndArea();
			DrawFrameBar(parameters, timelineRect);
			DrawCursor(parameters, timelineRect);
		}

		private void DrawFrameBar(TimeLineParameters parameters, Rect timeLineRect)
		{
			var frameLength = parameters.MaxFrame - parameters.MinFrame;
			var start = timeLineRect.height - 30f;
			Handles.color = Color.white;
			Handles.DrawSolidRectangleWithOutline(new Rect(0, start, timeLineRect.width - 10f, 30f), new Color(Color.gray.r, Color.gray.g, Color.gray.b, 0.2f), Color.gray);
			Handles.color = Color.black;
			var i = 0;
			var linesCount = frameLength / _koef;
			while (i <= linesCount)
			{
				var evenFrame = i == 0 || i % 5 == 0;
				var width = evenFrame ? 10f : 5f;

				if (evenFrame && i != linesCount)
				{
					var rate = (parameters.MinFrame + (i * _koef)) / parameters.FrameRate;
					var time = string.Format("{0}:{1}", Mathf.Ceil(rate), (parameters.MinFrame + (i * _koef)) % parameters.FrameRate);
					Handles.Label(new Vector3(i * x_dif + 6f, start + 3f), time);
				}

				Handles.DrawLine(new Vector3(i * x_dif + 5, start), new Vector3(i * x_dif + 5, start + width));
				i++;
			}

			if (!parameters.IsPlaying && EditorHelper.GetMouseOverRect(new Rect(5f, start, timeLineRect.width - 9f, 30f)))
			{
				Vector2 mousePosition = Event.current.mousePosition;
				var dif = x_dif / _koef;
				var frame = (mousePosition.x - 5f) / dif;
				ChangeCurrentFrame(Mathf.RoundToInt(frame), parameters);
			}
		}

		private void DrawCursor(TimeLineParameters parameters, Rect timelineRect)
		{
			if (parameters.CurrentFrame < parameters.MinFrame || parameters.CurrentFrame > parameters.MaxFrame)
				return;

			var dif = x_dif / _koef;
			var x = dif * (parameters.CurrentFrame - parameters.MinFrame) + 5;
			Handles.color = Color.red;
			var start = timelineRect.height - 30f;
			Handles.DrawSolidDisc(new Vector3(x, start + 2f), new Vector3(0, 0, 1f), 3f);
			Handles.color = Color.red;
			Handles.DrawAAPolyLine(2f, new Vector3(x, 0), new Vector3(x, start));
		}

		private void ChangeCurrentFrame(int i, TimeLineParameters parameters)
		{
			var frame = Mathf.Clamp(parameters.MinFrame + i, parameters.MinFrame, parameters.MaxFrame);

			if (OnChangeCurrentFrame != null)
				OnChangeCurrentFrame(frame);
		}
	}
}
