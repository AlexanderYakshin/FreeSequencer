using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Assets.Scripts.Editor;
using Assets.Scripts.Editor.Sequencer;
using FreeSequencer.Events;
using FreeSequencer.Tracks;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FreeSequencer.Editor
{
	public class SequencerEditorWindow : EditorWindow
	{
		private float x_startGrid = 205f;
		private float y_startGrid = 20f;
		private float x_endGrid = 100f;
		private float y_endGrid = 20f;
		private float x_dif;
		private int koef;


		private int _currentFrameNumber;
		private int CurrentFrameNumber
		{
			get { return _currentFrameNumber; }
			set
			{
				if (_currentFrameNumber != value)
				{
					_currentFrameNumber = value;
					DrawCursor();
					HandleAllEvents();
				}
			}
		}


		public float modifier = 1.23f;
		public float cursorTime;
		public int MeterSignature = 4;

		private int _seqIndex;
		private string _selectedSequenceName;
		private Sequence _selectedSequence;
		private BaseEvent _selectedEvent;
		private BaseTrack _selectedTrack;

		private Dictionary<string, Sequence> _sequences;
		private float _gridWidth;
		private float _gridHeight;


		#region TimeControl

		private bool _isPlaying;
		private float _playTime;

		#endregion

		#region WindowReferences

		private static SequencerEditorWindow _editorWindow;
		private static EventInspector _inspectorWindow;

		#endregion

		[MenuItem("FreeSequencer/Editor")]
		static void ShowEditor()
		{
			_editorWindow = EditorWindow.GetWindow<SequencerEditorWindow>();
			_inspectorWindow = EditorWindow.GetWindow<EventInspector>();
			_editorWindow.Init();

		}

		public void Init()
		{
			_sequences = new Dictionary<string, Sequence>();
			LoadSequences();
		}

		void OnGUI()
		{
			x_endGrid = position.width - 10f;
			y_endGrid = position.height - 35f;

			DrawSequenceRegion();
			DropAreaGUI();
			DrawGrid();
			GUILayout.FlexibleSpace();
			DrawTimeControls();
		}

		#region Drawing

		private void DrawSequenceRegion()
		{
			EditorGUILayout.BeginHorizontal(GUILayout.Height(20f));
			DrawSequences();
			GUILayout.FlexibleSpace();
			if (_selectedSequence != null)
			{
				EditorGUILayout.LabelField("Update Mode", GUILayout.Width(85), GUILayout.Height(20f));
				_selectedSequence.UpdateTypeMode = (UpdateType)EditorGUILayout.EnumPopup(_selectedSequence.UpdateTypeMode, GUILayout.Width(64), GUILayout.Height(20f));
				EditorGUILayout.LabelField("Frame Rate", GUILayout.Width(70), GUILayout.Height(20f));
				_selectedSequence.FrameRate = EditorGUILayout.IntPopup(_selectedSequence.FrameRate, new string[] { "10", "20", "30", "40", "50", "60" },
					new int[] { 10, 20, 30, 40, 50, 60 }, GUILayout.Width(40), GUILayout.Height(20f));
				EditorGUILayout.LabelField("Length", GUILayout.Width(64), GUILayout.Height(20f));
				var length = EditorGUILayout.IntField(_selectedSequence.Length, GUILayout.Width(64), GUILayout.Height(18f));
				_selectedSequence.Length = Mathf.Clamp(length, 1, 2000);
			}

			EditorGUILayout.EndHorizontal();
		}

		private void DrawSequences()
		{
			EditorGUILayout.LabelField("Sequence", GUILayout.Width(64), GUILayout.Height(20f));
			if (_sequences != null && _sequences.Any())
			{
				var sequenceNames = _sequences.Keys.ToArray<string>();
				if (_seqIndex > sequenceNames.Length - 1)
					_seqIndex = 0;
				_seqIndex = EditorGUILayout.Popup(_seqIndex, sequenceNames, GUILayout.Width(200));
				_selectedSequenceName = sequenceNames[_seqIndex];
				_selectedSequence = _sequences[_selectedSequenceName];
			}
			else
			{
				GUILayout.Label("Create first sequence", GUILayout.Width(200));
				_selectedSequence = null;
			}

			if (GUILayout.Button("Add", GUILayout.Width(35)))
			{
				var newGameObject = new GameObject("Sequence");
				var seq = newGameObject.AddComponent<Sequence>();
				seq.Length = 600;
				seq.FrameRate = 60;
				_selectedSequenceName = "Sequence";
				_selectedSequence = seq;
				LoadSequences();
			}

			if (GUILayout.Button("R", GUILayout.Width(20)))
			{
				LoadSequences();
			}
		}

		public void DropAreaGUI()
		{
			if (_selectedSequence == null)
				return;
			Event evt = Event.current;
			Rect drop_area = new Rect(0.0f, y_startGrid, x_startGrid - 5f, y_endGrid);

			switch (evt.type)
			{
				case EventType.DragUpdated:
				case EventType.DragPerform:
					if (!drop_area.Contains(evt.mousePosition))
						return;

					/*Handles.DrawSolidRectangleWithOutline(drop_area, new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b, 0.5f), Color.black);
					Handles.Label(new Vector3((x_startGrid - 5f) / 2f, y_startGrid + y_endGrid / 2f), "Drag GO there");*/

					DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

					if (evt.type == EventType.DragPerform)
					{
						DragAndDrop.AcceptDrag();

						foreach (Object dragedObject in DragAndDrop.objectReferences)
						{
							_selectedSequence.Objects.Add(new AnimatedGameObject() { GameObject = (GameObject)dragedObject });
						}
					}
					break;
			}
		}

		void DrawGrid()
		{
			if (_selectedSequence != null && _selectedSequence.Objects != null)
			{
				DrawTimeLine();
				DrawCursor();
				var objectsCount = 1;
				for (int i = 0; i < _selectedSequence.Objects.Count; i++)
				{
					var animatedGameObject = _selectedSequence.Objects[i];
					EditorGUILayout.BeginHorizontal(GUILayout.Width(190), GUILayout.Height(20f));
					//GUILayout.Box(animatedGameObject.GameObject.name, GUILayout.Width(150), GUILayout.Height(20f));
					animatedGameObject.Toggled = EditorGUILayout.Foldout(animatedGameObject.Toggled, animatedGameObject.GameObject.name);
					//GUILayout.Box("123", GUILayout.Width(150), GUILayout.Height(20f));
					//EditorGUILayout.LabelField(animatedGameObject.GameObject.name, GUILayout.Width(150), GUILayout.Height(20f));
					if (GUILayout.Button("Add", EditorStyles.toolbarDropDown, GUILayout.Width(40), GUILayout.Height(20f)))
					{
						var mPos = Event.current.mousePosition;
						GenericMenu toolsMenu = new GenericMenu();
						toolsMenu.AddItem(new GUIContent("Add animation event"), false, OnAddAnimationTrack, animatedGameObject);
						toolsMenu.DropDown(new Rect(mPos.x, mPos.y, 0, 0));
						//GUIUtility.ExitGUI();
					}

					Handles.color = Color.black;
					Handles.DrawLine(new Vector3(0, y_startGrid + objectsCount * 22f, 0f), new Vector3(x_endGrid, y_startGrid + objectsCount * 22f, 0f));
					EditorGUILayout.EndHorizontal();
					objectsCount++;
					if (animatedGameObject.Toggled && animatedGameObject.Tracks != null && animatedGameObject.Tracks.Any())
					{
						foreach (var track in animatedGameObject.Tracks)
						{
							DrawTrack(track, animatedGameObject, objectsCount * 22f);

							foreach (BaseEvent baseEvent in track.Events)
							{
								
							}
							Handles.DrawLine(new Vector3(0, y_startGrid + objectsCount * 22f, 0f), new Vector3(x_endGrid, y_startGrid + objectsCount * 22f, 0f));
							objectsCount++;
						}
					}

					//EditorGUILayout.EndToggleGroup();
				}
			}
		}

		private void DrawTimeLine()
		{
			_gridWidth = x_endGrid - x_startGrid;
			_gridHeight = y_endGrid - y_startGrid;
			x_dif = _gridWidth / _selectedSequence.Length;
			koef = x_dif < 4f ? 10 : 1;
			var framesCount = _selectedSequence.Length / koef;

			Handles.color = Color.white;
			var i = 0;
			while (i <= framesCount)
			{
				var color = i == 0
					? Color.white
					: i % 5 == 0
						? Color.white
						: new Color(Color.white.r, Color.white.g, Color.white.b, 0.4f);
				Handles.color = color;

				Handles.DrawLine(new Vector3(x_startGrid + i * x_dif * koef, y_startGrid), new Vector3(x_startGrid + i * x_dif * koef, y_endGrid));
				if (!_isPlaying && GetMouseDownRect(new Rect(x_startGrid + i * x_dif * koef - 2f, y_startGrid, 4f, _gridHeight)))
				{
					CurrentFrameNumber = i * koef;
				}
				i++;
			}
			Handles.color = Color.white;
			Handles.DrawSolidRectangleWithOutline(new Rect(x_startGrid - 5f, y_endGrid, _gridWidth + 10f, 15f), new Color(Color.gray.r, Color.gray.g, Color.gray.b, 0.2f), Color.gray);
			i = 0;
			while (i <= framesCount)
			{
				var evenFrame = false;
				evenFrame = i == 0 || i % 5 == 0;
				var width = evenFrame ? 10f : 5f;

				if (evenFrame && i != framesCount)
				{
					var rate = i * koef / _selectedSequence.FrameRate;
					var time = string.Format("{0}:{1}", Mathf.Ceil(rate), (i * koef) % _selectedSequence.FrameRate);
					Handles.Label(new Vector3(x_startGrid + i * x_dif * koef + 1f, y_endGrid + 3f), time);
				}

				Handles.DrawLine(new Vector3(x_startGrid + i * x_dif * koef, y_endGrid), new Vector3(x_startGrid + i * x_dif * koef, y_endGrid + width));
				i++;
			}

			if (!_isPlaying && GetMouseOverRect(new Rect(x_startGrid, y_startGrid, _gridWidth, _gridHeight + 35f)))
			{
				Vector2 mousePosition = Event.current.mousePosition;
				var frame = (mousePosition.x - x_startGrid) / x_dif;
				CurrentFrameNumber = Mathf.CeilToInt(frame);
			}
		}

		private void DrawCursor()
		{
			x_dif = _gridWidth / _selectedSequence.Length;
			var x = x_startGrid + x_dif * _currentFrameNumber;
			Handles.color = Color.red;

			Handles.DrawSolidDisc(new Vector3(x, y_endGrid + 2f), new Vector3(0, 0, 1f), 3f);
			Handles.color = Color.red;
			Handles.DrawLine(new Vector3(x, y_startGrid), new Vector3(x, y_endGrid));
			Repaint();
		}

		private void DrawTrack(BaseTrack track, AnimatedGameObject animatedGameObject, float startHeight)
		{
			EditorGUILayout.BeginHorizontal();
			//GUILayout.Box(track.TrackName, GUILayout.Width(150), GUILayout.Height(20f));
			EditorGUILayout.SelectableLabel(track.TrackName, GUILayout.Width(190f), GUILayout.Height(20f));
			var rect = new Rect(0f, startHeight, 190f, 23f);
			if (GetMouseDownRect(rect, 1))
			{
				var mousePos = Event.current.mousePosition;
				GenericMenu toolsMenu = new GenericMenu();
				toolsMenu.AddItem(new GUIContent("Remove Track"), false, OnRemoveTrack, new RemoveTrackHolder() { Track = track, AnimatedGameObject = animatedGameObject });
				// Offset menu from right of editor window
				toolsMenu.DropDown(new Rect(mousePos.x, mousePos.y, 0, 0));
				GUIUtility.ExitGUI();
			}
			var lastEndFrame = 0;
			var events = track.Events.OrderBy(evt => evt.StartFrame).ToList();
			for (int i = 0; i < events.Count; i++)
			{
				var evt = events[i];
				if (evt.StartFrame > lastEndFrame)
				{
					DrawEventMouseRect(lastEndFrame, evt.StartFrame - 1, startHeight, animatedGameObject, track);
				}

				DrawEventMouseRect(evt.StartFrame, evt.EndFrame, startHeight, animatedGameObject, track, evt);
				var min = lastEndFrame;
				var max = i == events.Count - 1 ? _selectedSequence.Length : events[i + 1].StartFrame;
				DrawEvent(evt, track, animatedGameObject.GameObject, startHeight, min, max);

				lastEndFrame = evt.EndFrame;
				if (i == events.Count - 1 && lastEndFrame < _selectedSequence.Length - 1)
				{
					DrawEventMouseRect(lastEndFrame, _selectedSequence.Length - 1, startHeight, animatedGameObject, track);
				}
			}
			EditorGUILayout.EndHorizontal();
		}

		private void DrawEvent(BaseEvent baseEvent, BaseTrack track, GameObject gameObject, float startHeight, int minFrame, int maxFrame)
		{
			var start = x_startGrid + x_dif * baseEvent.StartFrame;
			var width = x_dif * (baseEvent.EndFrame - baseEvent.StartFrame);
			var rect = new Rect(start, startHeight, width, 20f);
			var selectionColor = Color.white;
			var color = baseEvent.EventInnerColor;

			var cacheColor = Handles.color;
			Handles.color = Color.black;
			Handles.DrawSolidRectangleWithOutline(rect, color, color);
			Handles.color = color;
			Handles.DrawSolidRectangleWithOutline(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), color, color);
			
			if (_selectedEvent == baseEvent)
			{
				Handles.color = new Color(selectionColor.r, selectionColor.g, selectionColor.b, 0.3f);
				Handles.DrawSolidRectangleWithOutline(rect, selectionColor, selectionColor);
			}

			Handles.color = cacheColor;

			if (_selectedEvent != baseEvent && GetMouseDownRect(rect))
			{
				_selectedEvent = baseEvent;
				_selectedTrack = track;
				if (_inspectorWindow == null)
					_inspectorWindow = EditorWindow.GetWindow<EventInspector>();
				_inspectorWindow.Init(_selectedSequenceName, _selectedSequence.FrameRate,
					_selectedSequence.Length, minFrame, maxFrame, gameObject, track, baseEvent);
				_inspectorWindow.Repaint();
			}
		}

		private void DrawEventMouseRect(int start, int end, float startHeight, AnimatedGameObject animatedGameObject, BaseTrack baseTrack, BaseEvent seqEvent = null)
		{
			var st = x_startGrid + x_dif * start;
			var width = x_dif * (end - start);
			var evtRect = new Rect(st, startHeight, width, 20f);

			if (GetMouseDownRect(evtRect, 1))
			{
				var mPos = Event.current.mousePosition;
				GenericMenu toolsMenu = new GenericMenu();
				if (seqEvent == null)
				{
					var holder = new AddEventHolder() { AnimatedGameObject = animatedGameObject, StartFrame = start, EndFrame = end, Track = baseTrack };
					toolsMenu.AddItem(new GUIContent("Add animation event"), false, OnAddAnimationEvent, holder);
				}
				else
				{
					var holder = new RemoveEventHolder() { AnimatedGameObject = animatedGameObject, Track = baseTrack, Event = seqEvent };
					toolsMenu.AddItem(new GUIContent("Remove animation event"), true, OnRemoveEvent, holder);
				}

				toolsMenu.DropDown(new Rect(mPos.x, mPos.y, 0, 0));
				//GUIUtility.ExitGUI();
			}
		}

		#endregion

		private void LoadSequences()
		{
			if (_sequences == null)
				_sequences = new Dictionary<string, Sequence>();
			var sequences = FindObjectsOfType<Sequence>();
			foreach (Sequence sequance in sequences)
			{
				if (!_sequences.ContainsKey(sequance.name))
				{
					_sequences.Add(sequance.name, sequance);
				}
				else
				{
					_sequences[sequance.name] = sequance;
				}
			}
			var copy = _sequences.ToDictionary(seq => seq.Key, seq => seq.Value);
			foreach (var seq in copy.Where(kv => kv.Value == null))
			{
				_sequences.Remove(seq.Key);
			}
		}

		#region Handle Events

		private void HandleAllEvents()
		{
			if (_selectedSequence != null && _selectedSequence.Objects != null && _selectedSequence.Objects.Any())
			{
				foreach (AnimatedGameObject animatedGameObject in _selectedSequence.Objects)
				{
					if (animatedGameObject.Tracks != null && animatedGameObject.Tracks.Any())
					{
						foreach (BaseTrack baseTrack in animatedGameObject.Tracks)
						{
							if (baseTrack.Events != null && baseTrack.Events.Any())
							{
								foreach (BaseEvent @event in baseTrack.Events)
								{
									ProcessEvent(@event, animatedGameObject.GameObject);
								}
							}
						}
					}
				}
			}
		}

		private void ProcessEvent(BaseEvent baseEvent, GameObject gameObject)
		{
			if (CurrentFrameNumber < baseEvent.StartFrame || CurrentFrameNumber > baseEvent.EndFrame)
				return;

			var animEvent = baseEvent as AnimationSeqEvent;
			if (animEvent != null)
			{
				ProcessAnimationEvent(animEvent, gameObject);
			}
		}

		private void ProcessAnimationEvent(AnimationSeqEvent animEvent, GameObject gameObject)
		{
			if (animEvent.Clip != null)
			{
				if (animEvent.ControlAnimation)
				{
					animEvent.Clip.frameRate = _selectedSequence.FrameRate;
					var length = (float)(animEvent.EndFrame - animEvent.StartFrame) / (float)_selectedSequence.FrameRate;
					if (Mathf.Abs(animEvent.Clip.length - length) > 0.001f)
					{
						HandleLenghDifference(animEvent, length / animEvent.Clip.length);
					}
				}

				var frameNumber = CurrentFrameNumber - animEvent.StartFrame;
				animEvent.Clip.SampleAnimation(gameObject, (float)frameNumber / _selectedSequence.FrameRate);
			}
		}

		#endregion

		private void OnRemoveTrack(object userdata)
		{
			var holder = userdata as RemoveTrackHolder;

			if (holder == null)
				return;

			if (_selectedTrack == holder.Track)
			{
				_inspectorWindow.Init(string.Empty, 0, 0, 0, _selectedSequence.Length);
			}
			holder.AnimatedGameObject.Tracks.Remove(holder.Track);
			if (_inspectorWindow == null)
				_inspectorWindow = EditorWindow.GetWindow<EventInspector>();
			_inspectorWindow.Repaint();
			Repaint();
		}

		private void OnRemoveEvent(object userdata)
		{
			var holder = userdata as RemoveEventHolder;

			if (holder == null)
				return;

			if (_selectedEvent == holder.Event)
			{
				_inspectorWindow.Init(string.Empty, 0, 0, 0, _selectedSequence.Length);
			}

			holder.Track.Events.Remove(holder.Event);
			if (_inspectorWindow == null)
				_inspectorWindow = EditorWindow.GetWindow<EventInspector>();
			_inspectorWindow.Repaint();

			Repaint();
		}

		private void DrawTimeControls()
		{
			EditorGUILayout.BeginHorizontal();
			var prevIsPlaing = _isPlaying;
			_isPlaying = GUILayout.Toggle(_isPlaying, !_isPlaying ? ">" : "||", EditorStyles.toolbarButton, GUILayout.Width(30f));
			if (prevIsPlaing != _isPlaying)
			{
				if (_isPlaying)
				{
					_playTime = Time.realtimeSinceStartup;
				}
			}
			if (_isPlaying)
			{
				var dif = Time.realtimeSinceStartup - _playTime;
				var tick = (float)_selectedSequence.FrameRate / 3600f;
				if (dif > tick)
				{
					_playTime += tick;
					OnPlayTick();
				}
				Repaint();
			}

			if (GUILayout.Button("|>", GUILayout.Width(30f)))
			{
				if (CurrentFrameNumber < _selectedSequence.Length)
					CurrentFrameNumber++;
			}

			EditorGUILayout.LabelField(CurrentFrameNumber.ToString(), GUILayout.Width(50f));


			EditorGUILayout.EndHorizontal();
		}

		private void OnPlayTick()
		{
			if (CurrentFrameNumber < _selectedSequence.Length)
			{
				CurrentFrameNumber++;
			}
			else
			{
				_isPlaying = false;
			}
		}

		private void OnAddAnimationTrack(object userdata)
		{
			var animatedObject = (AnimatedGameObject)userdata;
			if (animatedObject.Tracks == null)
				animatedObject.Tracks = new List<BaseTrack>();

			var animationTrack = new AnimationTrack() { TrackName = "Play Animation" };
			if (animationTrack.Events == null)
				animationTrack.Events = new List<BaseEvent>();
			var animEvent = new AnimationSeqEvent() { StartFrame = 0, EndFrame = _selectedSequence.Length };
			animationTrack.Events.Add(animEvent);
			animatedObject.Tracks.Add(animationTrack);
			animatedObject.Toggled = true;
		}

		private void OnAddAnimationEvent(object userdata)
		{
			var holder = (AddEventHolder)userdata;
			var animatedObject = holder.AnimatedGameObject;

			var animationTrack = holder.Track;
			if (animationTrack.Events == null)
				animationTrack.Events = new List<BaseEvent>();
			var animEvent = new AnimationSeqEvent() { StartFrame = holder.StartFrame, EndFrame = holder.EndFrame };
			animationTrack.Events.Add(animEvent);
			animatedObject.Toggled = true;
		}

		private static bool GetMouseDownRect(Rect rect, int button = 0)
		{
			Vector2 mousePosition = Event.current.mousePosition;
			if (rect.Contains(mousePosition) && Event.current.button == button)
			{
				if (Event.current.type == EventType.mouseDown)
					return true;
			}

			return false;
		}

		private static bool GetMouseOverRect(Rect rect)
		{
			Vector2 mousePosition = Event.current.mousePosition;
			if (rect.Contains(mousePosition) && Event.current.button == 0 && Event.current.type == EventType.MouseDrag)
			{
				return true;
			}

			return false;
		}


		#region OnSceneGUI

		void OnFocus()
		{
			// Remove delegate listener if it has previously
			// been assigned.
			SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
			// Add (or re-add) the delegate.
			SceneView.onSceneGUIDelegate += this.OnSceneGUI;
		}

		void OnDestroy()
		{
			// When the window is destroyed, remove the delegate
			// so that it will no longer do any drawing.
			SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
		}

		protected void OnSceneGUI(SceneView sceneView)
		{
			if (_selectedSequence == null || _selectedSequence.Objects == null)
				return;

			foreach (var animatedGameObject in _selectedSequence.Objects)
			{
				if (animatedGameObject.Tracks != null && animatedGameObject.Tracks.Any())
				{
					foreach (var track in animatedGameObject.Tracks)
					{
						if (track.ShowTransformPath)
						{
							var animTrack = track as AnimationTrack;
							if (animTrack != null && animTrack.Controller != null && track.Events != null && track.Events.Any())
							{
								foreach (BaseEvent baseEvent in track.Events)
								{
									var animEvent = baseEvent as AnimationSeqEvent;
									if (animEvent != null && animEvent.Clip != null)
									{
										DrawTransformPath(animEvent);
										if (track.ShowKeyFrames)
											DrawKeyFrames(animEvent);
									}
								}
							}

						}
					}
				}
			}
		}

		private void DrawKeyFrames(AnimationSeqEvent animEvent)
		{
			var curveBindings = AnimationUtility.GetCurveBindings(animEvent.Clip);
			if (Tools.current == Tool.Move)
			{
				var curveBindingX = curveBindings
					.FirstOrDefault(curv => curv.propertyName.Equals("m_LocalPosition.x", StringComparison.InvariantCultureIgnoreCase));
				var curveBindingY = curveBindings
					.FirstOrDefault(curv => curv.propertyName.Equals("m_LocalPosition.y", StringComparison.InvariantCultureIgnoreCase));
				var curveBindingZ = curveBindings
					.FirstOrDefault(curv => curv.propertyName.Equals("m_LocalPosition.z", StringComparison.InvariantCultureIgnoreCase));

				if (curveBindingX.propertyName == null || curveBindingY.propertyName == null || curveBindingZ.propertyName == null)
					return;

				var curveX = AnimationUtility.GetEditorCurve(animEvent.Clip, curveBindingX);
				var curveY = AnimationUtility.GetEditorCurve(animEvent.Clip, curveBindingY);
				var curveZ = AnimationUtility.GetEditorCurve(animEvent.Clip, curveBindingZ);

				List<Keyframe> keyPointsX = new List<Keyframe>();
				List<Keyframe> keyPointsY = new List<Keyframe>();
				List<Keyframe> keyPointsZ = new List<Keyframe>();
				bool pointChanged = false;
				foreach (Keyframe keyframe in curveX.keys.OrderBy(key => key.time))
				{
					var valueX = curveX.Evaluate(keyframe.time);
					var valueY = curveY.Evaluate(keyframe.time);
					var valueZ = curveZ.Evaluate(keyframe.time);

					var vector = new Vector3(valueX, valueY, valueZ);
					EditorGUI.BeginChangeCheck();
					vector = Handles.DoPositionHandle(vector, Quaternion.identity);
					if (EditorGUI.EndChangeCheck())
					{
						pointChanged = true;
						Keyframe keyframeX = new Keyframe(keyframe.time, vector.x) {inTangent = keyframe.inTangent, outTangent = keyframe.outTangent, tangentMode = keyframe.tangentMode};
						Keyframe keyframeY;
						if (curveY.keys.Any(key => Math.Abs(key.time - keyframe.time) < 0.001f))
						{
							keyframeY = curveY.keys.FirstOrDefault(key1 => Math.Abs(key1.time - keyframe.time) < 0.001f);
							keyframeY = new Keyframe(keyframeY.time, vector.y) { inTangent = keyframeY.inTangent, outTangent = keyframeY.outTangent, tangentMode = keyframeY.tangentMode };
						}
						else
						{
							keyframeY = new Keyframe(keyframe.time, vector.y);
						}
						Keyframe keyframeZ;
						if (curveZ.keys.Any(key => Math.Abs(key.time - keyframe.time) < 0.001f))
						{
							keyframeZ = curveZ.keys.FirstOrDefault(key1 => Math.Abs(key1.time - keyframe.time) < 0.001f);
							keyframeZ = new Keyframe(keyframeZ.time, vector.z) { inTangent = keyframeZ.inTangent, outTangent = keyframeZ.outTangent, tangentMode = keyframeZ.tangentMode };
						}
						else
						{
							keyframeZ = new Keyframe(keyframe.time, vector.z);
						}

						if (keyPointsX.Any(key => Math.Abs(key.time - keyframeX.time) < 0.001f))
						{
							keyPointsX.RemoveAll(key => Math.Abs(key.time - keyframeX.time) < 0.001f);
						}
						keyPointsX.Add(keyframeX);
						if (keyPointsY.Any(key => Math.Abs(key.time - keyframeY.time) < 0.001f))
						{
							keyPointsY.RemoveAll(key => Math.Abs(key.time - keyframeY.time) < 0.001f);
						}
						keyPointsY.Add(keyframeY);
						if (!keyPointsZ.Any(key => Math.Abs(key.time - keyframeZ.time) < 0.001f))
						{
							keyPointsZ.RemoveAll(key => Math.Abs(key.time - keyframeZ.time) < 0.001f);
						}
						keyPointsZ.Add(keyframeZ);
					}
					else
					{
						keyPointsX.Add(keyframe);
					}
				}

				foreach (Keyframe keyframe in curveY.keys)
				{
					var valueX = curveX.Evaluate(keyframe.time);
					var valueY = curveY.Evaluate(keyframe.time);
					var valueZ = curveZ.Evaluate(keyframe.time);

					var vector = new Vector3(valueX, valueY, valueZ);
					EditorGUI.BeginChangeCheck();
					vector = Handles.DoPositionHandle(vector, Quaternion.identity);
					if (EditorGUI.EndChangeCheck())
					{
						pointChanged = true;
						Keyframe keyframeY = new Keyframe(keyframe.time, vector.y) { inTangent = keyframe.inTangent, outTangent = keyframe.outTangent, tangentMode = keyframe.tangentMode };
						Keyframe keyframeX;
						if (curveX.keys.Any(key => Math.Abs(key.time - keyframe.time) < 0.001f))
						{
							keyframeX = curveX.keys.FirstOrDefault(key1 => Math.Abs(key1.time - keyframe.time) < 0.001f);
							keyframeX = new Keyframe(keyframeX.time, vector.x) { inTangent = keyframeX.inTangent, outTangent = keyframeX.outTangent, tangentMode = keyframeX.tangentMode };
						}
						else
						{
							keyframeX = new Keyframe(keyframe.time, vector.x);
						}
						Keyframe keyframeZ;
						if (curveZ.keys.Any(key => Math.Abs(key.time - keyframe.time) < 0.001f))
						{
							keyframeZ = curveZ.keys.FirstOrDefault(key1 => Math.Abs(key1.time - keyframe.time) < 0.001f);
							keyframeZ = new Keyframe(keyframeZ.time, vector.z) { inTangent = keyframeZ.inTangent, outTangent = keyframeZ.outTangent, tangentMode = keyframeZ.tangentMode };
						}
						else
						{
							keyframeZ = new Keyframe(keyframe.time, vector.z);
						}

						if (!keyPointsX.Any(key => Math.Abs(key.time - keyframeX.time) < 0.001f))
						{
							keyPointsX.Add(keyframeX);
						}
						if (!keyPointsY.Any(key => Math.Abs(key.time - keyframeY.time) < 0.001f))
						{
							keyPointsY.Add(keyframeY);
						}
						if (!keyPointsZ.Any(key => Math.Abs(key.time - keyframeZ.time) < 0.001f))
						{
							keyPointsZ.Add(keyframeZ);
						}
					}
					else
					{
						keyPointsX.Add(keyframe);
					}
				}

				foreach (Keyframe keyframe in curveZ.keys)
				{
					var valueX = curveX.Evaluate(keyframe.time);
					var valueY = curveY.Evaluate(keyframe.time);
					var valueZ = curveZ.Evaluate(keyframe.time);

					var vector = new Vector3(valueX, valueY, valueZ);
					EditorGUI.BeginChangeCheck();
					vector = Handles.DoPositionHandle(vector, Quaternion.identity);
					if (EditorGUI.EndChangeCheck())
					{
						pointChanged = true;
						Keyframe keyframeZ = new Keyframe(keyframe.time, vector.z) { inTangent = keyframe.inTangent, outTangent = keyframe.outTangent, tangentMode = keyframe.tangentMode };
						Keyframe keyframeY;
						if (curveY.keys.Any(key => Math.Abs(key.time - keyframe.time) < 0.001f))
						{
							keyframeY = curveY.keys.FirstOrDefault(key1 => Math.Abs(key1.time - keyframe.time) < 0.001f);
							keyframeY = new Keyframe(keyframeY.time, vector.y) { inTangent = keyframeY.inTangent, outTangent = keyframeY.outTangent, tangentMode = keyframeY.tangentMode };
						}
						else
						{
							keyframeY = new Keyframe(keyframe.time, vector.y);
						}
						Keyframe keyframeX;
						if (curveX.keys.Any(key => Math.Abs(key.time - keyframe.time) < 0.001f))
						{
							keyframeX = curveX.keys.FirstOrDefault(key1 => Math.Abs(key1.time - keyframe.time) < 0.001f);
							keyframeX = new Keyframe(keyframeX.time, vector.z) { inTangent = keyframeX.inTangent, outTangent = keyframeX.outTangent, tangentMode = keyframeX.tangentMode };
						}
						else
						{
							keyframeX = new Keyframe(keyframe.time, vector.z);
						}

						if (!keyPointsX.Any(key => Math.Abs(key.time - keyframeX.time) < 0.001f))
						{
							keyPointsX.Add(keyframeX);
						}
						if (!keyPointsY.Any(key => Math.Abs(key.time - keyframeY.time) < 0.001f))
						{
							keyPointsY.Add(keyframeY);
						}
						if (!keyPointsZ.Any(key => Math.Abs(key.time - keyframeZ.time) < 0.001f))
						{
							keyPointsZ.Add(keyframeZ);
						}
					}
					else
					{
						keyPointsX.Add(keyframe);
					}
				}

				if (pointChanged)
				{
					curveX.keys = keyPointsX.ToArray();
					AnimationUtility.SetEditorCurve(animEvent.Clip, curveBindingX, curveX);

					curveY.keys = keyPointsX.ToArray();
					AnimationUtility.SetEditorCurve(animEvent.Clip, curveBindingY, curveY);

					curveZ.keys = keyPointsX.ToArray();
					AnimationUtility.SetEditorCurve(animEvent.Clip, curveBindingZ, curveZ);

					animEvent.Clip.EnsureQuaternionContinuity();
				}
			}
		
			var curveBindingRotX = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("localEulerAnglesRaw.x", StringComparison.InvariantCultureIgnoreCase));
			var curveBindingRotY = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("localEulerAnglesRaw.y", StringComparison.InvariantCultureIgnoreCase));
			var curveBindingRotZ = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("localEulerAnglesRaw.z", StringComparison.InvariantCultureIgnoreCase));
		}

		private void DrawTransformPath(AnimationSeqEvent seqEvent)
		{
			DrawPositionTransformPath(seqEvent);
		}

		private void DrawPositionTransformPath(AnimationSeqEvent seqEvent)
		{
			var curveBindings = AnimationUtility.GetCurveBindings(seqEvent.Clip);
			var curveBindingRotX = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("localEulerAnglesRaw.x", StringComparison.InvariantCultureIgnoreCase));
			var curveBindingRotY = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("localEulerAnglesRaw.y", StringComparison.InvariantCultureIgnoreCase));
			var curveBindingRotZ = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("localEulerAnglesRaw.z", StringComparison.InvariantCultureIgnoreCase));

			var curveBindingX = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("m_LocalPosition.x", StringComparison.InvariantCultureIgnoreCase));
			var curveBindingY = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("m_LocalPosition.y", StringComparison.InvariantCultureIgnoreCase));
			var curveBindingZ = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("m_LocalPosition.z", StringComparison.InvariantCultureIgnoreCase));
			if (curveBindingX.propertyName == null || curveBindingY.propertyName == null || curveBindingZ.propertyName == null)
				return;

			var curveX = AnimationUtility.GetEditorCurve(seqEvent.Clip, curveBindingX);
			var curveY = AnimationUtility.GetEditorCurve(seqEvent.Clip, curveBindingY);
			var curveZ = AnimationUtility.GetEditorCurve(seqEvent.Clip, curveBindingZ);

			bool skipRotation = curveBindingRotX.propertyName == null || curveBindingRotY.propertyName == null ||
				curveBindingRotZ.propertyName == null;

			var curveRotX = skipRotation ? null : AnimationUtility.GetEditorCurve(seqEvent.Clip, curveBindingRotX);
			var curveRotY = skipRotation ? null : AnimationUtility.GetEditorCurve(seqEvent.Clip, curveBindingRotY);
			var curveRotZ = skipRotation ? null : AnimationUtility.GetEditorCurve(seqEvent.Clip, curveBindingRotZ);

			List<float> keyPoints = new List<float>();
			foreach (Keyframe keyframe in curveX.keys)
			{
				if (!keyPoints.Contains(keyframe.time))
				{
					keyPoints.Add(keyframe.time);
				}
			}

			foreach (Keyframe keyframe in curveY.keys)
			{
				if (!keyPoints.Contains(keyframe.time))
				{
					keyPoints.Add(keyframe.time);
				}
			}

			foreach (Keyframe keyframe in curveZ.keys)
			{
				if (!keyPoints.Contains(keyframe.time))
				{
					keyPoints.Add(keyframe.time);
				}
			}

			keyPoints.Sort();
			var color = Handles.color;
			for (int i = 0; i < keyPoints.Count - 1; i++)
			{
				var length = keyPoints[i + 1] - keyPoints[i];
				var countOfFrames = (int)(seqEvent.Clip.frameRate * length);
				var oneframe = length / countOfFrames;
				for (int j = 0; j < countOfFrames - 1; j++)
				{
					var x = curveX.Evaluate(keyPoints[i] + oneframe * j);
					var x1 = curveX.Evaluate(keyPoints[i] + oneframe * (j + 1));
					var y = curveY.Evaluate(keyPoints[i] + oneframe * j);
					var y1 = curveY.Evaluate(keyPoints[i] + oneframe * (j + 1));
					var z = curveZ.Evaluate(keyPoints[i] + oneframe * j);
					var z1 = curveZ.Evaluate(keyPoints[i] + oneframe * (j + 1));

					if (!skipRotation)
					{
						var xRot = curveRotX.Evaluate(keyPoints[i] + oneframe * j);
						var yRot = curveRotY.Evaluate(keyPoints[i] + oneframe * j);
						var zRot = curveRotZ.Evaluate(keyPoints[i] + oneframe * j);

						Handles.color = Color.green;
						Handles.DrawLine(new Vector3(x, y, z), new Vector3(x, y, z) + Quaternion.Euler(xRot, yRot, zRot) * Vector3.forward * 0.5f);
					}

					Handles.color = Color.white;
					Handles.DrawLine(
						new Vector3(x, y, z),
						new Vector3(x1, y1, z1));
				}
			}
			Handles.color = color;
		}

		private void HandleLenghDifference(AnimationSeqEvent seqEvent, float dif)
		{
			var curveBindings = AnimationUtility.GetCurveBindings(seqEvent.Clip);
			foreach (EditorCurveBinding editorCurveBinding in curveBindings)
			{
				var curve = AnimationUtility.GetEditorCurve(seqEvent.Clip, editorCurveBinding);

				var keyframes = new Keyframe[curve.length];
				keyframes[0] = curve[0];
				for (int i = 0; i < curve.length; i++)
				{
					var keyframe = new Keyframe(curve[i].time * dif, curve[i].value);
					keyframe.inTangent = curve[i].inTangent / dif;
					keyframe.outTangent = curve[i].outTangent / dif;
					keyframe.tangentMode = curve[i].tangentMode;
					keyframes[i] = keyframe;
				}
				curve.keys = keyframes;
				AnimationUtility.SetEditorCurve(seqEvent.Clip, editorCurveBinding, curve);
				seqEvent.Clip.EnsureQuaternionContinuity();
			}
		}
		#endregion
	}
}
