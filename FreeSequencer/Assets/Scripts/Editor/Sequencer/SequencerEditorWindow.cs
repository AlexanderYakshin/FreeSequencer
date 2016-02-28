using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Assets.Scripts.Editor;
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
		private int _ticks;

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
		}

		void OnGUI()
		{
			x_endGrid = position.width - 10f;
			y_endGrid = position.height - 35f;

			EditorGUILayout.BeginHorizontal();

			DrawSequences();
			GUILayout.FlexibleSpace();

			if (_selectedSequence != null)
			{
				EditorGUILayout.LabelField("Update Mode", GUILayout.Width(85), GUILayout.Height(20f));
				_selectedSequence.UpdateTypeMode = (UpdateType)EditorGUILayout.EnumPopup(_selectedSequence.UpdateTypeMode, GUILayout.Width(64));
				EditorGUILayout.LabelField("Frame Rate", GUILayout.Width(70));
				_selectedSequence.FrameRate = EditorGUILayout.IntPopup(_selectedSequence.FrameRate, new string[] { "10", "20", "30", "40", "50", "60" },
					new int[] { 10, 20, 30, 40, 50, 60 }, GUILayout.Width(40));
				EditorGUILayout.LabelField("Length", GUILayout.Width(64));
				var length = EditorGUILayout.IntField(_selectedSequence.Length, GUILayout.Width(64));
				_selectedSequence.Length = Mathf.Clamp(length, 1, 2000);
			}

			EditorGUILayout.EndHorizontal();
			DropAreaGUI();
			DrawGrid();
			GUILayout.FlexibleSpace();
			DrawTimeControls();
		}

		private void DrawSequences()
		{
			EditorGUILayout.LabelField("Sequence", GUILayout.Width(64), GUILayout.Height(20f));
			LoadSequences();
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
			}
		}

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
					EditorGUILayout.BeginHorizontal();
					//animatedGameObject.Toggled = EditorGUILayout(animatedGameObject.GameObject.name, animatedGameObject.Toggled);
					EditorGUILayout.LabelField(animatedGameObject.GameObject.name, GUILayout.Width(150), GUILayout.Height(20f));
					if (GUILayout.Button("Add", EditorStyles.toolbarDropDown, GUILayout.Width(40), GUILayout.Height(20f)))
					{
						var mPos = Event.current.mousePosition;
						GenericMenu toolsMenu = new GenericMenu();
						toolsMenu.AddItem(new GUIContent("Add animation event"), false, OnAddAnimationEvent, animatedGameObject);
						// Offset menu from right of editor window
						toolsMenu.DropDown(new Rect(mPos.x, mPos.y, 0, 0));
						GUIUtility.ExitGUI();
					}
					Handles.color = Color.black;
					Handles.DrawLine(new Vector3(0, y_startGrid + objectsCount * 22f, 0f), new Vector3(x_endGrid, y_startGrid + objectsCount * 22f, 0f));
					EditorGUILayout.EndHorizontal();
					objectsCount++;
					if (animatedGameObject.Tracks != null && animatedGameObject.Tracks.Any())
					{
						foreach (var track in animatedGameObject.Tracks)
						{
							DrawTrack(track, animatedGameObject, objectsCount * 22f);

							foreach (BaseEvent baseEvent in track.Events)
							{
								DrawEvent(baseEvent, track, animatedGameObject.GameObject, objectsCount * 22f);
								HandledEvent(baseEvent, animatedGameObject.GameObject);
							}
							Handles.DrawLine(new Vector3(0, y_startGrid + objectsCount * 22f, 0f), new Vector3(x_endGrid, y_startGrid + objectsCount * 22f, 0f));
							objectsCount++;
						}
					}

					//EditorGUILayout.EndToggleGroup();
				}
			}
		}

		private void HandledEvent(BaseEvent baseEvent, GameObject gameObject)
		{
			var animEvent = baseEvent as AnimationSeqEvent;
			if (animEvent != null)
			{
				if (animEvent.Clip != null)
				{
					animEvent.Clip.frameRate = _selectedSequence.FrameRate;
					var length = (float)(animEvent.EndFrame - animEvent.StartFrame) / (float)_selectedSequence.FrameRate;
					if (Mathf.Abs(animEvent.Clip.length - length) > 0.001f)
					{
						HandleLenghDifference(animEvent, length / animEvent.Clip.length);
					}
					if (_currentFrameNumber >= animEvent.StartFrame)
					{
						var frameNumber = _currentFrameNumber - animEvent.StartFrame;
						animEvent.Clip.SampleAnimation(gameObject, (float)frameNumber / _selectedSequence.FrameRate);
					}
				}
			}
		}

		private void DrawEvent(BaseEvent baseEvent, BaseTrack track, GameObject gameObject, float startHeight)
		{
			var start = x_startGrid + x_dif * baseEvent.StartFrame;
			var width = x_dif * (baseEvent.EndFrame - baseEvent.StartFrame);
			var rect = new Rect(start, startHeight, width, 20f);
			var color = _selectedEvent == baseEvent ? new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b, 0.3f) : baseEvent.EventInnerColor;
			Handles.DrawSolidRectangleWithOutline(rect, color, Color.black);

			if (_selectedEvent != baseEvent && GetMouseDownRect(rect))
			{
				var same = _selectedEvent == baseEvent;
				_selectedEvent = baseEvent;
				_selectedTrack = track;

				if (!same)
				{
					if (_inspectorWindow == null)
						_inspectorWindow = EditorWindow.GetWindow<EventInspector>();
					_inspectorWindow.Init(_selectedSequenceName, _selectedSequence.FrameRate, _selectedSequence.Length, gameObject,
						track, baseEvent);
					_inspectorWindow.Repaint();
				}
			}
		}

		private void DrawTrack(BaseTrack track, AnimatedGameObject animatedGameObject, float startHeight)
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.SelectableLabel(track.TrackName, GUILayout.Width(150f), GUILayout.Height(20f));
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
			EditorGUILayout.EndHorizontal();
		}

		private void OnRemoveTrack(object userdata)
		{
			var holder = userdata as RemoveTrackHolder;

			if (holder == null)
				return;

			if (_selectedTrack == holder.Track)
			{
				_inspectorWindow.Init(string.Empty, 0, 0);
			}
			holder.AnimatedGameObject.Tracks.Remove(holder.Track);
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
					_ticks = 0;
				}
			}
			if (_isPlaying)
			{
				Debug.Log(Time.realtimeSinceStartup);
				var dif = Time.realtimeSinceStartup - _playTime;
				var tick = (float)_selectedSequence.FrameRate/3600f;
				if (dif > tick)
				{
					Debug.Log(_ticks + "   :  " + tick);
					_ticks++;
					   _playTime += tick;
					OnPlayTick();
				}
				Repaint();
			}

			if (GUILayout.Button("|>", GUILayout.Width(30f)))
			{
				if (_currentFrameNumber < _selectedSequence.Length)
					_currentFrameNumber++;
			}

			EditorGUILayout.LabelField(_currentFrameNumber.ToString(), GUILayout.Width(50f));


			EditorGUILayout.EndHorizontal();
		}

		private void OnPlayTick()
		{
			if (_currentFrameNumber < _selectedSequence.Length)
			{
				_currentFrameNumber++;
			}
			else
			{
				_isPlaying = false;
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
					_currentFrameNumber = i * koef;
					Repaint();
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
				_currentFrameNumber = Mathf.CeilToInt(frame);
				Repaint();
			}
		}

		private void OnAddAnimationEvent(object userdata)
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

		private void DrawCursor()
		{
			x_dif = _gridWidth / _selectedSequence.Length;
			var x = x_startGrid + x_dif * _currentFrameNumber;
			Handles.color = Color.red;

			Handles.DrawSolidDisc(new Vector3(x, y_endGrid + 2f), new Vector3(0, 0, 1f), 3f);
			Handles.color = Color.red;
			Handles.DrawLine(new Vector3(x, y_startGrid), new Vector3(x, y_endGrid));
		}

		public void DropAreaGUI()
		{
			if (_selectedSequence == null)
				return;
			Event evt = Event.current;
			Rect drop_area = new Rect(0.0f, y_startGrid, x_startGrid - 5f, y_endGrid);
			var mousePosition = evt.mousePosition;
			if (drop_area.Contains(mousePosition))
			{
				Handles.DrawSolidRectangleWithOutline(drop_area, new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b, 0.05f),
					Color.black);
				if (_selectedSequence.Objects == null || !_selectedSequence.Objects.Any())
					Handles.Label(new Vector3((x_startGrid - 5f)/2f - 35f, y_startGrid + y_endGrid/2f), "Drag GO there");
			}
			//GUI.Box(drop_area, "Add Trigger");

			switch (evt.type)
			{
				case EventType.DragUpdated:
				/*if (!drop_area.Contains(evt.mousePosition))
					return;*/

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
										DrawTransformPath(animEvent);
								}
							}

						}
					}
				}
			}
		}

		private void DrawTransformPath(AnimationSeqEvent seqEvent)
		{
			DrawPositionTransformPath(seqEvent);
		}

		private void DrawRotationTransformNotmals(AnimationSeqEvent seqEvent)
		{
			var curveBindings = AnimationUtility.GetCurveBindings(seqEvent.Clip);
			var curveBindingX = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("m_LocalRotation.x", StringComparison.InvariantCultureIgnoreCase));
			var curveBindingY = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("m_LocalRotation.y", StringComparison.InvariantCultureIgnoreCase));
			var curveBindingZ = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("m_LocalRotation.z", StringComparison.InvariantCultureIgnoreCase));
			if (curveBindingX.propertyName == null || curveBindingY.propertyName == null || curveBindingZ.propertyName == null)
				return;

			var curveX = AnimationUtility.GetEditorCurve(seqEvent.Clip, curveBindingX);
			var curveY = AnimationUtility.GetEditorCurve(seqEvent.Clip, curveBindingY);
			var curveZ = AnimationUtility.GetEditorCurve(seqEvent.Clip, curveBindingZ);

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
				}
				/*var length = curveX[i + 1].time - curveX[i].time;
				var countOfFrames = (int)(seqEvent.Clip.frameRate * length);
				var oneframe = length / countOfFrames;
				for (int j = 0; j < countOfFrames - 1; j++)
				{
					var x = curveX.Evaluate(curveX[i].time + oneframe * j);
					var x1 = curveX.Evaluate(curveX[i].time + oneframe * (j + 1));
					var y = curveY.Evaluate(curveY[i].time + oneframe * j);
					var y1 = curveY.Evaluate(curveY[i].time + oneframe * (j + 1));
					var z = curveZ.Evaluate(curveZ[i].time + oneframe * j);
					var z1 = curveZ.Evaluate(curveZ[i].time + oneframe * (j + 1));

					Handles.DrawLine(
						new Vector3(x, y, z),
						new Vector3(x1, y1, z1));
				}*/
			}
		}

		private void DrawPositionTransformPath(AnimationSeqEvent seqEvent)
		{
			var curveBindings = AnimationUtility.GetCurveBindings(seqEvent.Clip);
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

					Handles.DrawLine(
						new Vector3(x, y, z),
						new Vector3(x1, y1, z1));
				}
			}
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
