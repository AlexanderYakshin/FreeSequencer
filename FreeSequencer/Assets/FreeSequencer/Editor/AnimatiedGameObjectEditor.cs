using System;
using System.Linq;
using FreeSequencer.Events;
using FreeSequencer.Tracks;
using UnityEditor;
using UnityEngine;

namespace FreeSequencer.Editor
{
	public static class AnimatiedGameObjectEditor
	{
		public const float RowHeight = 25f;
		public static event Action<AnimatedGameObject> AddAnimationTrack;
		public static event Action<AnimatedGameObject> RemoveAnimatedGameObject;
		public static event Action<RemoveTrackHolder> RemoveTrack;
		public static event Action<EventSelectionHolder> OnEventSelection;
		public static event Action<EventSelectionHolder> OnEventDragged;
		public static event Action<AddEventHolder> AddEvent;
		public static event Action<RemoveEventHolder> RemoveEvent;
		public static event Action<AnimationTrack> GenerateTrack;
		public static event Action<MoveHolder> Move;

		public static void OnDraw(TimeLineParameters parameters, AnimatedGameObject animatedGameObject, Rect controlRect)
		{
			Handles.color = new Color(153f / 256f, 153f / 256f, 153f / 256, 1f);
			var ctrRect1 = new Rect(0, 0, controlRect.width - 10f, RowHeight);
			var ctrRect2 = new Rect(0, 0, 10f, controlRect.height);
			Handles.DrawSolidRectangleWithOutline(ctrRect1, Color.white, new Color(0, 0, 0, 0));
			Handles.DrawSolidRectangleWithOutline(ctrRect2, Color.white, new Color(0, 0, 0, 0));
			Handles.color = Color.black;
			Handles.DrawLine(new Vector3(0, 0), new Vector3(controlRect.width - 10f, 0));
			Handles.DrawLine(new Vector3(10f, RowHeight), new Vector3(controlRect.width - 10f, RowHeight));


			EditorGUILayout.BeginHorizontal(GUILayout.Width(190), GUILayout.Height(RowHeight));
			EditorGUI.BeginChangeCheck();
			animatedGameObject.Toggled = EditorGUILayout.Foldout(animatedGameObject.Toggled, animatedGameObject.GameObject.name);
			if (EditorGUI.EndChangeCheck())
			{
				//ToggleChanged(animatedGameObject.Toggled);
			}
			if (GUILayout.Button("Add", EditorStyles.toolbarDropDown, GUILayout.Width(40), GUILayout.Height(RowHeight)))
			{
				var mPos = Event.current.mousePosition;
				GenericMenu toolsMenu = new GenericMenu();
				toolsMenu.AddItem(new GUIContent("Add animation track"), false, OnAddAnimationTrack, animatedGameObject);
				toolsMenu.DropDown(new Rect(mPos.x, mPos.y, 0, 0));
			}
			var rect = new Rect(0, 0, 190, RowHeight);
			if (EditorHelper.GetMouseDownRect(rect, 1))
			{
				var mousePos = Event.current.mousePosition;
				GenericMenu toolsMenu = new GenericMenu();
				toolsMenu.AddItem(new GUIContent("Remove GameObject"), false, RemoveGameObject, animatedGameObject);
				toolsMenu.DropDown(new Rect(mousePos.x, mousePos.y, 0, 0));
				GUIUtility.ExitGUI();
			}
			EditorGUILayout.EndHorizontal();

			if (animatedGameObject.Toggled)
			{
				var lastTrackPosition = RowHeight;
				foreach (var track in animatedGameObject.Tracks)
				{
					var trackRect = new Rect(10f, lastTrackPosition, controlRect.width, RowHeight);
					GUILayout.BeginArea(trackRect);
					DrawTrackSection(animatedGameObject, track, trackRect, parameters);
					GUILayout.EndArea();
					lastTrackPosition += RowHeight;
				}
			}

			Handles.color = Color.black;
			Handles.DrawAAPolyLine(2f, new Vector3(195f, 0), new Vector3(195f, controlRect.height));
		}

		public static void DrawTrackSection(AnimatedGameObject animatedGameObject, BaseTrack track, Rect trackRect, TimeLineParameters parameters)
		{
			EditorGUILayout.LabelField(track.TrackName);
			DrawTrackMenu(animatedGameObject, track);
			var eventsRect = new Rect(185f, 0, trackRect.width - 205f, RowHeight);
			GUILayout.BeginArea(eventsRect);
			var events = track.Events.Where(evt => evt.StartFrame < parameters.MaxFrame && evt.EndFrame > parameters.MinFrame).OrderBy(evt => evt.StartFrame).ToList();
			var lastEndFrame = parameters.MinFrame;
			for (int i = 0; i < events.Count; i++)
			{
				var trackEvent = events[i];
				if (trackEvent.StartFrame > lastEndFrame)
					DrawEmptyEvent(animatedGameObject, track, eventsRect, lastEndFrame, trackEvent.StartFrame, parameters);

				var minFrame = i == 0 ? parameters.MinFrame : events[i - 1].EndFrame;
				var maxFrame = i == events.Count - 1 ? parameters.MaxFrame : events[i + 1].StartFrame;
				DrawTrackEvent(animatedGameObject, track, trackEvent, eventsRect, parameters, minFrame, maxFrame, i);
				lastEndFrame = trackEvent.EndFrame;
				if (i == events.Count - 1 && trackEvent.EndFrame < parameters.MaxFrame)
					DrawEmptyEvent(animatedGameObject, track, eventsRect, lastEndFrame, parameters.MaxFrame, parameters);
			}
			GUILayout.EndArea();
			Handles.color = Color.black;
			Handles.DrawLine(new Vector3(0, trackRect.height - 1f), new Vector3(trackRect.width, trackRect.height - 1f));
		}

		private static void DrawEmptyEvent(AnimatedGameObject animatedGameObject, BaseTrack track, Rect eventsRect, int startFrame, int endFrame, TimeLineParameters parameters)
		{
			var frameLength = parameters.MaxFrame - parameters.MinFrame;
			var x_dif = eventsRect.width / frameLength;

			var startFr = startFrame;
			var start = Mathf.Clamp(startFr, 0, frameLength);
			var end = Mathf.Clamp(endFrame - startFr, 0, frameLength);
			var rect = new Rect(start * x_dif, 0, end * x_dif, RowHeight);
			if (EditorHelper.GetMouseDownRect(rect, 1))
			{
				var mousePos = Event.current.mousePosition;
				GenericMenu toolsMenu = new GenericMenu();
				toolsMenu.AddItem(new GUIContent("Add new event"), false, OnAddEvent, new AddEventHolder() { StartFrame = startFr, EndFrame = endFrame, Track = track, AnimatedGameObject = animatedGameObject });
				toolsMenu.DropDown(new Rect(mousePos.x, mousePos.y, 0, 0));
				GUIUtility.ExitGUI();
			}
		}

		private static void OnAddEvent(object userdata)
		{
			var holder = userdata as AddEventHolder;
			if (AddEvent != null)
			{
				AddEvent(holder);
			}
		}

		private static void DrawTrackMenu(AnimatedGameObject animatedGameObject, BaseTrack track)
		{
			var rect = new Rect(0, 0, 185f, RowHeight);
			if (EditorHelper.GetMouseDownRect(rect, 1))
			{
				var mousePos = Event.current.mousePosition;
				GenericMenu toolsMenu = new GenericMenu();
				toolsMenu.AddItem(new GUIContent("Remove Track"), false, OnRemoveTrack, new RemoveTrackHolder() { Track = track, AnimatedGameObject = animatedGameObject });
				var animationTrack = track as AnimationTrack;
				if (animationTrack != null)
					toolsMenu.AddItem(new GUIContent("Generate State Machine"), false, OnGenerateTrack, animationTrack);
				toolsMenu.DropDown(new Rect(mousePos.x, mousePos.y, 0, 0));
				GUIUtility.ExitGUI();
			}
		}

		private static void OnGenerateTrack(object track)
		{
			var animationTrack = track as AnimationTrack;
			if (animationTrack != null && GenerateTrack != null)
				GenerateTrack(animationTrack);
		}

		private static void OnRemoveTrack(object userdata)
		{
			var holder = userdata as RemoveTrackHolder;
			if (RemoveTrack != null)
			{
				RemoveTrack(holder);
			}
		}

		private static void DrawTrackEvent(AnimatedGameObject animatedGameObject, BaseTrack track, TrackEvent trackEvent, Rect eventsRect, TimeLineParameters parameters, int minFrame, int maxFrame, int index)
		{
			var frameLength = parameters.MaxFrame - parameters.MinFrame;
			var x_dif = eventsRect.width / frameLength;
			var backgroundColor = GUI.backgroundColor;
			GUI.backgroundColor = trackEvent.EventInnerColor;
			var evtStyle = FreeSequencerUtility.GetEventStyle();

			var startFr = trackEvent.StartFrame - parameters.MinFrame;
			var start = Mathf.Clamp(startFr, 0, frameLength);
			var endFr = trackEvent.EndFrame - parameters.MinFrame;
			var end = endFr > 0 && frameLength > endFr ? endFr : frameLength;
			var rect = new Rect(start * x_dif, 0, (end - start) * x_dif, RowHeight);
			if (Event.current.type == EventType.Repaint)
			{
				evtStyle.Draw(rect, false, trackEvent.IsActive, false, false);
			}

			GUILayout.BeginArea(rect);
			GUILayout.Label(trackEvent.EventTitle);
			GUILayout.EndArea();

			var minRect = new Rect(start*x_dif, 0, 5f, RowHeight);
			var maxRect = new Rect(start*x_dif + end*x_dif - 5f, 0, 5f, RowHeight);

			if (EditorHelper.GetMouseDown())
			{
				var wasDrag = false;
				if (EditorHelper.GetMouseDownRect(minRect) && !trackEvent.IsDragged)
					wasDrag = trackEvent.IsMinDragged = true;

				if (EditorHelper.GetMouseDownRect(maxRect) && !trackEvent.IsDragged)
					wasDrag = trackEvent.IsMaxDragged = true;

				if (EditorHelper.GetMouseDownRect(rect))
				{
					if (OnEventSelection != null)
					{
						var holder = new EventSelectionHolder()
						{
							AnimatedGameObject = animatedGameObject,
							Event = trackEvent,
							Track = track
						};
						OnEventSelection(holder);
					}
					trackEvent.InitialDraggedPosition = Event.current.mousePosition;
					if (!trackEvent.IsMinDragged && !trackEvent.IsMaxDragged)
						wasDrag = trackEvent.IsDragged = true;
				}

				if (!wasDrag)
				{
					trackEvent.IsDragged = false;
					trackEvent.IsMinDragged = false;
					trackEvent.IsMaxDragged = false;
				}
			}

			if (EditorHelper.GetMouseUp())
			{
				trackEvent.IsDragged = false;
				trackEvent.IsMinDragged = false;
				trackEvent.IsMaxDragged = false;
			}

			if (trackEvent.IsMinDragged || trackEvent.IsMaxDragged || trackEvent.IsDragged)
			{
				if (EditorHelper.GetMouseDrag())
				{
					var position = Event.current.mousePosition;
					var initialPosition = trackEvent.InitialDraggedPosition;

					var difX = initialPosition.x - position.x;
					int framesDif = (int) (difX/x_dif)*-1;
					if (Mathf.Abs(framesDif) > 0)
					{
						var newStartFrame = trackEvent.StartFrame + framesDif;
						var newEndFrame = trackEvent.EndFrame + framesDif;

						if (trackEvent.IsMinDragged)
							trackEvent.StartFrame = Mathf.Clamp(newStartFrame, minFrame, trackEvent.EndFrame - 1);

						if (trackEvent.IsMaxDragged)
							trackEvent.EndFrame = Mathf.Clamp(newEndFrame, trackEvent.StartFrame + 1, maxFrame);

						if (trackEvent.IsDragged)
						{
							var eventLenth = trackEvent.Length;
							trackEvent.StartFrame = Mathf.Clamp(newStartFrame, minFrame, maxFrame - eventLenth);
							trackEvent.EndFrame = Mathf.Clamp(newEndFrame, trackEvent.StartFrame + eventLenth, maxFrame);
						}

						if (OnEventDragged != null)
						{
							OnEventDragged(new EventSelectionHolder()
							{
								AnimatedGameObject = animatedGameObject,
								Event = trackEvent,
								Track = track
							});
						}
						trackEvent.InitialDraggedPosition = position;
					}
				}

				var holder = new EventSelectionHolder()
				{
					AnimatedGameObject = animatedGameObject,
					Event = trackEvent,
					Track = track
				};
				if (OnEventSelection != null)
					OnEventSelection(holder);
			}

			if (EditorHelper.GetMouseDownRect(rect, 1))
			{
				var mousePos = Event.current.mousePosition;
				GenericMenu toolsMenu = new GenericMenu();
				toolsMenu.AddItem(new GUIContent("Remove event"), false, OnRemoveEvent, new RemoveEventHolder() { Event = trackEvent, Track = track, AnimatedGameObject = animatedGameObject });
				if (index > 0)
				{
					var moveLeftHolder = new MoveHolder()
					{
						AnimatedGameObject = animatedGameObject,
						Track = track,
						Event = trackEvent,
						MoveRight = false
					};

					
					toolsMenu.AddSeparator(string.Empty);
					toolsMenu.AddItem(new GUIContent("Move left"), false, OnMove, moveLeftHolder);
				}

				if (index < track.Events.Count - 1)
				{
					var moveRightHolder = new MoveHolder()
					{
						AnimatedGameObject = animatedGameObject,
						Track = track,
						Event = trackEvent,
						MoveRight = true
					};
					toolsMenu.AddSeparator(string.Empty);
					toolsMenu.AddItem(new GUIContent("Move right"), false, OnMove, moveRightHolder);
				}

				toolsMenu.DropDown(new Rect(mousePos.x, mousePos.y, 0, 0));
				GUIUtility.ExitGUI();
			}

			GUI.backgroundColor = backgroundColor;
		}

		private static void OnMove(object userdata)
		{
			var holder = userdata as MoveHolder;
			if (holder != null && Move != null)
			{
				Move(holder);
			}
		}

		private static void OnRemoveEvent(object userdata)
		{
			var holder = userdata as RemoveEventHolder;
			if (RemoveEvent != null)
			{
				RemoveEvent(holder);
			}
		}

		private static void RemoveGameObject(object userdata)
		{
			var animatedGameObject = userdata as AnimatedGameObject;
			if (RemoveAnimatedGameObject != null)
			{
				RemoveAnimatedGameObject(animatedGameObject);
			}

		}

		private static void OnAddAnimationTrack(object userdata)
		{
			var animatedGameObject = userdata as AnimatedGameObject;
			if (animatedGameObject != null && AddAnimationTrack != null)
			{
				AddAnimationTrack(animatedGameObject);
			}
		}
	}
}
