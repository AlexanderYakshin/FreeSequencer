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

		public int _minCurrentFrame;
		public int _maxCurrentFrame;

		public float modifier = 1.23f;
		public float cursorTime;
		public int MeterSignature = 4;

		private int _seqIndex;
		private string _selectedSequenceName;
		private Sequence _selectedSequence;
		private TrackEvent _selectedEvent;
		private BaseTrack _selectedTrack;

		private Dictionary<string, Sequence> _sequences;
		private float _gridWidth;
		private float _gridHeight;

		#region TimeControl

		private bool _isPlaying;
		private bool _prevIsPlaing;
		private float _playTime;
		private float _speed;

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
			y_endGrid = position.height - 60f;

			DrawSequenceRegion();
			DropAreaGUI();
			DrawGrid();
			GUILayout.FlexibleSpace();
			DrawFrameSlider();
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
				_selectedSequence.Length = Mathf.Clamp(length, 1, 9999);
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
				EditorGUI.BeginChangeCheck();
				_seqIndex = EditorGUILayout.Popup(_seqIndex, sequenceNames, GUILayout.Width(200));
				if (EditorGUI.EndChangeCheck())
				{
					_selectedSequenceName = sequenceNames[_seqIndex];
					_selectedSequence = _sequences[_selectedSequenceName];

					_maxCurrentFrame = _selectedSequence.Length;
					_speed = 1;
					_isPlaying = false;
				}
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
			var length = _maxCurrentFrame - _minCurrentFrame;
			x_dif = _gridWidth / length;
			koef = x_dif < 8f ? (int)(4f / x_dif * 4f) : (int)Mathf.Clamp(4f / x_dif * 2f, 1, 100);
			var framesCount = length / koef;

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
					CurrentFrameNumber = _minCurrentFrame + i * koef;
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
					var rate = (_minCurrentFrame + (i * koef)) / _selectedSequence.FrameRate;
					var time = string.Format("{0}:{1}", Mathf.Ceil(rate), (_minCurrentFrame + (i * koef)) % _selectedSequence.FrameRate);
					Handles.Label(new Vector3(x_startGrid + i * x_dif * koef + 1f, y_endGrid + 3f), time);
				}

				Handles.DrawLine(new Vector3(x_startGrid + i * x_dif * koef, y_endGrid), new Vector3(x_startGrid + i * x_dif * koef, y_endGrid + width));
				i++;
			}

			if (!_isPlaying && GetMouseOverRect(new Rect(x_startGrid, y_endGrid, _gridWidth, 15f)))
			{
				Vector2 mousePosition = Event.current.mousePosition;
				var frame = (mousePosition.x - x_startGrid) / x_dif;
				CurrentFrameNumber = _minCurrentFrame + Mathf.CeilToInt(frame);
			}
		}

		private void DrawCursor()
		{
			x_dif = _gridWidth / (_maxCurrentFrame - _minCurrentFrame);
			var x = x_startGrid + x_dif * (_currentFrameNumber - _minCurrentFrame);
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
				toolsMenu.AddItem(new GUIContent("Generate State Machine"), false, OnTrackChanged, track);
				// Offset menu from right of editor window
				toolsMenu.DropDown(new Rect(mousePos.x, mousePos.y, 0, 0));
				GUIUtility.ExitGUI();
			}
			var lastEndFrame = _minCurrentFrame;
			var events = track.Events.OrderBy(evt => evt.StartFrame).ToList();
			for (int i = 0; i < events.Count; i++)
			{
				var evt = events[i];
				if (evt.EndFrame > _minCurrentFrame && evt.StartFrame < _maxCurrentFrame)
				{
					if (evt.StartFrame > lastEndFrame)
					{
						DrawEventMouseRect(lastEndFrame, evt.StartFrame - 1, startHeight, animatedGameObject, track);
					}


					DrawEventMouseRect(evt.StartFrame, evt.EndFrame, startHeight, animatedGameObject, track, evt, i);
					var min = lastEndFrame;
					var max = i == events.Count - 1 ? _selectedSequence.Length : events[i + 1].StartFrame;
					DrawEvent(evt, track, animatedGameObject.GameObject, startHeight, min, max);

					lastEndFrame = evt.EndFrame;
					if (i == events.Count - 1 && lastEndFrame < _selectedSequence.Length - 1)
					{
						DrawEventMouseRect(lastEndFrame, _selectedSequence.Length - 1, startHeight, animatedGameObject, track);
					}
				}
			}
			EditorGUILayout.EndHorizontal();
		}

		private void DrawEvent(TrackEvent trackEvent, BaseTrack track, GameObject gameObject, float startHeight, int minFrame, int maxFrame)
		{
			var startFr = trackEvent.StartFrame < _minCurrentFrame ? _minCurrentFrame : trackEvent.StartFrame;
			var endFr = trackEvent.EndFrame > _maxCurrentFrame ? _maxCurrentFrame : trackEvent.EndFrame;
			var start = x_startGrid + x_dif * (startFr - _minCurrentFrame);
			var width = x_dif * (endFr - startFr);
			var rect = new Rect(start, startHeight, width, 20f);
			var selectionColor = Color.white;
			var color = trackEvent.EventInnerColor;

			var cacheColor = Handles.color;
			Handles.color = Color.black;
			Handles.DrawSolidRectangleWithOutline(rect, color, color);
			var innerRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
			Handles.color = color;
			Handles.DrawSolidRectangleWithOutline(innerRect, color, color);

			if (_selectedEvent == trackEvent)
			{
				Handles.color = new Color(selectionColor.r, selectionColor.g, selectionColor.b, 0.3f);
				Handles.DrawSolidRectangleWithOutline(rect, selectionColor, selectionColor);
			}
			Handles.color = Color.white;
			Handles.Label(new Vector3(innerRect.x, innerRect.y, 0), trackEvent.EventTitle);

			Handles.color = cacheColor;

			if (_selectedEvent != trackEvent && GetMouseDownRect(rect))
			{
				_selectedEvent = trackEvent;
				_selectedTrack = track;
				if (_inspectorWindow == null)
					_inspectorWindow = EditorWindow.GetWindow<EventInspector>();
				_inspectorWindow.Init(_selectedSequenceName, _selectedSequence.FrameRate,
					_selectedSequence.Length, minFrame, maxFrame, gameObject, track, trackEvent);
				_inspectorWindow.Repaint();
			}
		}

		private void DrawEventMouseRect(int start1, int end1, float startHeight, AnimatedGameObject animatedGameObject, BaseTrack baseTrack, TrackEvent seqEvent = null, int index = 0)
		{
			var start = start1 - _minCurrentFrame;
			var end = end1 - _minCurrentFrame;
			var st = x_startGrid + x_dif * (start);
			var width = x_dif * (end - start);
			var evtRect = new Rect(st, startHeight, width, 20f);

			if (GetMouseDownRect(evtRect, 1))
			{
				var mPos = Event.current.mousePosition;
				GenericMenu toolsMenu = new GenericMenu();
				if (seqEvent == null)
				{
					var holder = new AddEventHolder() { AnimatedGameObject = animatedGameObject, StartFrame = start1, EndFrame = end1, Track = baseTrack };
					toolsMenu.AddItem(new GUIContent("Add animation event"), false, OnAddAnimationEvent, holder);
				}
				else
				{
					var holder = new RemoveEventHolder() { AnimatedGameObject = animatedGameObject, Track = baseTrack, Event = seqEvent };
					toolsMenu.AddItem(new GUIContent("Remove animation event"), false, OnRemoveEvent, holder);
					var moveLeftHolder = new MoveHolder()
					{
						AnimatedGameObject = animatedGameObject,
						Track = baseTrack,
						Event = seqEvent,
						MoveRight = false
					};

					var moveRightHolder = new MoveHolder()
					{
						AnimatedGameObject = animatedGameObject,
						Track = baseTrack,
						Event = seqEvent,
						MoveRight = true
					};
					if (index > 0)
						toolsMenu.AddItem(new GUIContent("Move left"), false, OnMove, moveLeftHolder);
					if (index < baseTrack.Events.Count - 1)
						toolsMenu.AddItem(new GUIContent("Move right"), false, OnMove, moveRightHolder);
				}

				toolsMenu.DropDown(new Rect(mPos.x, mPos.y, 0, 0));
			}
		}

		private void DrawFrameSlider()
		{
			EditorGUILayout.BeginHorizontal(GUILayout.Height(25f));
			EditorGUILayout.BeginHorizontal(GUILayout.Width(3f));
			EditorGUILayout.Space();
			EditorGUILayout.EndHorizontal();
			float start = _minCurrentFrame;
			float end = _maxCurrentFrame;

			EditorGUILayout.MinMaxSlider(ref start, ref end, 0, _selectedSequence != null ? _selectedSequence.Length : 1);
			_minCurrentFrame = (int)start;
			_maxCurrentFrame = (int)end;
			EditorGUILayout.BeginHorizontal(GUILayout.Width(3f));
			EditorGUILayout.Space();
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.EndHorizontal();
		}

		private void DrawTimeControls()
		{
			EditorGUILayout.BeginHorizontal(GUILayout.Height(15f));
			EditorGUILayout.BeginHorizontal(GUILayout.Width(3f));
			EditorGUILayout.Space();
			EditorGUILayout.EndHorizontal();
			if (GUILayout.Button("<<", EditorStyles.toolbarButton, GUILayout.Width(30f)))
			{
				if (CurrentFrameNumber > _minCurrentFrame)
					CurrentFrameNumber = _minCurrentFrame;
			}
			if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(30f)))
			{
				if (CurrentFrameNumber > _minCurrentFrame)
					CurrentFrameNumber--;
			}

			CurrentFrameNumber = EditorGUILayout.IntField(_currentFrameNumber, GUILayout.Width(40f));

			if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(30f)))
			{
				if (CurrentFrameNumber < _maxCurrentFrame)
					CurrentFrameNumber++;
			}
			if (GUILayout.Button(">>", EditorStyles.toolbarButton, GUILayout.Width(30f)))
			{
				if (CurrentFrameNumber < _maxCurrentFrame)
					CurrentFrameNumber = _maxCurrentFrame;
			}

			EditorGUILayout.BeginHorizontal(GUILayout.Width(3f));
			EditorGUILayout.Space();
			EditorGUILayout.EndHorizontal();

			_isPlaying = GUILayout.Toggle(_isPlaying, !_isPlaying ? "Play" : "Pause", EditorStyles.toolbarButton, GUILayout.Width(40f));

			GUILayout.FlexibleSpace();

			EditorGUILayout.LabelField("Speed", GUILayout.Width(50f));
			_speed = EditorGUILayout.Slider(_speed, 0.1f, 3f, GUILayout.Width(120f));

			GUILayout.FlexibleSpace();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("View Range", GUILayout.Width(70f));
			var min = EditorGUILayout.IntField(_minCurrentFrame, GUILayout.Width(50f));
			_minCurrentFrame = Mathf.Clamp(min, 0, _maxCurrentFrame - 1);
			EditorGUILayout.LabelField("-", GUILayout.Width(10f));
			var max = EditorGUILayout.IntField(_maxCurrentFrame, GUILayout.Width(50f));
			_maxCurrentFrame = Mathf.Clamp(max, _minCurrentFrame + 1, _selectedSequence != null ? _selectedSequence.Length : 1);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal(GUILayout.Width(3f));
			EditorGUILayout.Space();
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal(GUILayout.Height(4f));
			EditorGUILayout.Space();
			EditorGUILayout.EndHorizontal();

			HandlePlayingAnimation();
		}


		#endregion

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
								foreach (TrackEvent @event in baseTrack.Events)
								{
									ProcessEvent(@event, animatedGameObject.GameObject);
								}
							}
						}
					}
				}
			}
		}

		private void ProcessEvent(TrackEvent trackEvent, GameObject gameObject)
		{
			if (CurrentFrameNumber < trackEvent.StartFrame || CurrentFrameNumber > trackEvent.EndFrame)
				return;

			var animEvent = trackEvent as AnimationTrackEvent;
			if (animEvent != null)
			{
				ProcessAnimationEvent(animEvent, gameObject);
			}
		}

		private void ProcessAnimationEvent(AnimationTrackEvent animEvent, GameObject gameObject)
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

		#region Context Menu Methods

		private void OnMove(object data)
		{
			var holder = data as MoveHolder;
			if (holder == null)
				return;

			if (holder.MoveRight)
			{
				var nextEvent = holder.Track.Events
					.OrderBy(evt => evt.StartFrame).FirstOrDefault(evt => evt.StartFrame > holder.Event.StartFrame);

				var gap = nextEvent.StartFrame - holder.Event.EndFrame;
				var lengthNextEvt = nextEvent.Length;
				var holderEvtLength = holder.Event.Length;

				nextEvent.StartFrame = holder.Event.StartFrame;
				nextEvent.EndFrame = holder.Event.StartFrame + lengthNextEvt;

				holder.Event.StartFrame = nextEvent.EndFrame + gap;
				holder.Event.EndFrame = holder.Event.StartFrame + holderEvtLength;
			}
			else
			{
				var nextEvent = holder.Track.Events
					.OrderBy(evt => evt.StartFrame).LastOrDefault(evt => evt.StartFrame < holder.Event.StartFrame);

				var gap = holder.Event.StartFrame - nextEvent.EndFrame;
				var lengthNextEvt = nextEvent.Length;
				var holderEvtLength = holder.Event.Length;

				holder.Event.StartFrame = nextEvent.StartFrame;
				holder.Event.EndFrame = nextEvent.StartFrame + holderEvtLength;

				nextEvent.StartFrame = holder.Event.EndFrame + gap;
				nextEvent.EndFrame = nextEvent.StartFrame + lengthNextEvt;
			}

			Repaint();
		}

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

		private void OnAddAnimationTrack(object userdata)
		{
			var animatedObject = (AnimatedGameObject)userdata;
			if (animatedObject.Tracks == null)
				animatedObject.Tracks = new List<BaseTrack>();

			var animationTrack = new AnimationTrack() { TrackName = "Play Animation" };
			if (animationTrack.Events == null)
				animationTrack.Events = new List<TrackEvent>();
			var animEvent = new AnimationTrackEvent() { StartFrame = 0, EndFrame = _selectedSequence.Length , EventTitle = _selectedTrack.TrackName + (animationTrack.Events.Count + 1) };
			animationTrack.Events.Add(animEvent);
			animatedObject.Tracks.Add(animationTrack);
			animatedObject.Toggled = true;
		}

		private void OnAddAnimationEvent(object userdata)
		{
			var holder = (AddEventHolder)userdata;
			var animatedObject = holder.AnimatedGameObject;

			var animationTrack = holder.Track as AnimationTrack;
			if (animationTrack.Events == null)
				animationTrack.Events = new List<TrackEvent>();
			var animEvent = new AnimationTrackEvent() { StartFrame = holder.StartFrame, EndFrame = holder.EndFrame, EventTitle = _selectedTrack.TrackName + (animationTrack.Events.Count + 1)};
			animationTrack.Events.Add(animEvent);
			animatedObject.Toggled = true;
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

		private void HandlePlayingAnimation()
		{
			if (_prevIsPlaing != _isPlaying)
			{
				if (_isPlaying)
				{
					_playTime = Time.realtimeSinceStartup;
				}
			}
			if (_isPlaying)
			{
				var dif = Time.realtimeSinceStartup - _playTime;
				var tick = (float)_selectedSequence.FrameRate / 3600f / _speed;
				if (dif > tick)
				{
					_playTime += tick;
					OnPlayTick();
				}
				Repaint();
			}
			_prevIsPlaing = _isPlaying;
		}

		private void OnPlayTick()
		{
			if (CurrentFrameNumber < _maxCurrentFrame)
			{
				CurrentFrameNumber++;
			}
			else
			{
				_isPlaying = false;
			}
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

		private void OnTrackChanged(object data)
		{
			var track = data as BaseTrack;
			var animationTrack = track as AnimationTrack;

			if (animationTrack != null)
				OnAnimationTrackChanged(animationTrack);
		}

		private void OnAnimationTrackChanged(AnimationTrack animationTrack)
		{
			if (animationTrack.Controller == null)
				return;
			var controller = animationTrack.Controller as UnityEditor.Animations.AnimatorController;
			UnityEditor.Animations.AnimatorControllerLayer layer;
			if (string.IsNullOrEmpty(animationTrack.ControllerLayer))
				layer = controller.layers[0];
			else
				layer = controller.layers.FirstOrDefault(lyr => lyr.name.Equals(animationTrack.ControllerLayer));

			if (layer == null)
				return;

			var rootStateMAchine = layer.stateMachine;
			var seqStateMachine = rootStateMAchine.stateMachines.Select(sm => sm.stateMachine)
				.FirstOrDefault(sm => sm.name.Equals(_selectedSequenceName));
			if (seqStateMachine != null)
				rootStateMAchine.RemoveStateMachine(seqStateMachine);

			seqStateMachine = rootStateMAchine.AddStateMachine(_selectedSequenceName);

			var events = animationTrack.Events.OrderBy(evt => evt.StartFrame).ToList();
			var lastEndFrame = 0;
			UnityEditor.Animations.AnimatorState prevState = null;
			for (int i = 0; i < events.Count; i++)
			{
				var evt = events[i] as AnimationTrackEvent;

				if (evt.StartFrame > lastEndFrame)
				{
					var prevEvtState = seqStateMachine.states.Select(st => st.state)
						.FirstOrDefault(st => st.name.Equals(evt.EventTitle + "_Prev"));
					if (prevEvtState == null)
					{
						prevEvtState = seqStateMachine.AddState(evt.EventTitle + "_Prev");
					}

					if (i!=0)
					{
						var tran1 = prevState.AddTransition(prevEvtState);
						tran1.duration = 0f;
						tran1.exitTime = 1f;
						tran1.hasExitTime = true;
					}

					prevState = prevEvtState;
				}

				var evtState = seqStateMachine.states.Select(st => st.state)
						.FirstOrDefault(st => st.name.Equals(evt.EventTitle));
				if (evtState == null)
				{
					evtState = seqStateMachine.AddState(evt.EventTitle);
				}
				evtState.motion = evt.Clip;

				if (i==0)
				{
					var firstState = prevState == null ? evtState : prevState;
					var firstTrans = seqStateMachine.AddEntryTransition(firstState);
				}
				else
				{
					var tran = prevState.AddTransition(evtState);
					tran.duration = 0f;
					tran.exitTime = (float)(evt.StartFrame - lastEndFrame) / _selectedSequence.FrameRate;
					tran.hasExitTime = true;
				}

				lastEndFrame = evt.EndFrame;
				prevState = evtState;

				if (i == events.Count - 1)
				{
					if (lastEndFrame < _selectedSequence.Length)
					{
						var nexEvtState = seqStateMachine.states.Select(st => st.state)
						.FirstOrDefault(st => st.name.Equals(evt.EventTitle + "_Last"));
						if (nexEvtState == null)
						{
							nexEvtState = seqStateMachine.AddState(evt.EventTitle + "_Last");
						}

						var tran2 = prevState.AddTransition(nexEvtState);
						tran2.duration = 0f;
						tran2.exitTime = 1;
						tran2.hasExitTime = true;

						var tran3 = nexEvtState.AddExitTransition();
						tran3.duration = 0f;
						tran3.exitTime = (float)(_selectedSequence.Length - lastEndFrame) / _selectedSequence.FrameRate;
						tran3.hasExitTime = true;
					}
					else
					{
						var tran3 = prevState.AddExitTransition();
						tran3.duration = 0f;
						tran3.exitTime = 1;
						tran3.hasExitTime = true;
					}
				}
			}

			string paramName = _selectedSequenceName;
			var param = controller.parameters.FirstOrDefault(par =>
				par.type == UnityEngine.AnimatorControllerParameterType.Trigger
					&& par.name.Equals(paramName));

			if (param == null)
				controller.AddParameter(paramName, UnityEngine.AnimatorControllerParameterType.Trigger);

			var transition = rootStateMAchine.AddAnyStateTransition(seqStateMachine);
			transition.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, paramName);
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
								foreach (TrackEvent baseEvent in track.Events)
								{
									var animEvent = baseEvent as AnimationTrackEvent;
									if (animEvent != null && animEvent.Clip != null)
									{
										DrawTransformPath(animEvent, track.ShowRotationNormales, track.PathColor);
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

		private void DrawKeyFrames(AnimationTrackEvent animEvent)
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

				List<float> keyTimes = new List<float>();
				foreach (Keyframe keyframe in curveX.keys.OrderBy(key => key.time))
				{
					if (keyTimes.Any(t => Math.Abs(t - keyframe.time) < 0.001f))
						continue;
					var valueX = curveX.Evaluate(keyframe.time);
					var valueY = curveY.Evaluate(keyframe.time);
					var valueZ = curveZ.Evaluate(keyframe.time);

					var oldvector = new Vector3(valueX, valueY, valueZ);
					keyTimes.Add(keyframe.time);
					EditorGUI.BeginChangeCheck();
					var vector = Handles.DoPositionHandle(oldvector, Quaternion.identity);
					if (EditorGUI.EndChangeCheck())
					{
						Keyframe keyframeX = new Keyframe(keyframe.time, vector.x) { inTangent = keyframe.inTangent, outTangent = keyframe.outTangent, tangentMode = keyframe.tangentMode };
						Keyframe keyframeY;
						if (curveY.keys.Any(key => Math.Abs(key.time - keyframe.time) < 0.001f))
						{
							keyframeY = curveY.keys.FirstOrDefault(key1 => Math.Abs(key1.time - keyframe.time) < 0.001f);
							keyframeY = new Keyframe(keyframeY.time, vector.y) { inTangent = keyframeY.inTangent, outTangent = keyframeY.outTangent, tangentMode = keyframeY.tangentMode };
						}
						else
						{
							keyframeY = new Keyframe(keyframe.time, vector.y);

							var nextPoint = curveY.keys.OrderBy(key => key.time).SkipWhile(key => key.time > keyframe.time).Skip(1).FirstOrDefault();
							var prevPoint = curveY.keys.OrderBy(key => key.time).TakeWhile(key => key.time > keyframe.time).LastOrDefault();
							if (nextPoint.time == 0f)
							{
								keyframeY.outTangent = 0f;
							}
							else
							{
								var p1 = new Vector2(nextPoint.time, nextPoint.value);
								var p2 = new Vector2(keyframeY.time, keyframeY.value);

								var delta = p1 - p2;

								keyframeY.outTangent = delta.y / delta.x;
							}

							if (prevPoint.time == 0f)
							{
								keyframeY.inTangent = 0f;
							}
							else
							{
								var p1 = new Vector2(prevPoint.time, prevPoint.value);
								var p2 = new Vector2(keyframeY.time, keyframeY.value);

								var delta = p2 - p1;

								keyframeY.inTangent = delta.y / delta.x;
							}

							keyframeY.tangentMode = 10;
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
							var nextPoint = curveZ.keys.OrderBy(key => key.time).SkipWhile(key => key.time > keyframe.time).Skip(1).FirstOrDefault();
							var prevPoint = curveZ.keys.OrderBy(key => key.time).TakeWhile(key => key.time > keyframe.time).LastOrDefault();
							if (nextPoint.time == 0f)
							{
								keyframeZ.outTangent = 0f;
							}
							else
							{
								var p1 = new Vector2(nextPoint.time, nextPoint.value);
								var p2 = new Vector2(keyframeZ.time, keyframeZ.value);

								var delta = p1 - p2;

								keyframeZ.outTangent = delta.y / delta.x;
							}

							if (prevPoint.time == 0f)
							{
								keyframeZ.inTangent = 0f;
							}
							else
							{
								var p1 = new Vector2(prevPoint.time, prevPoint.value);
								var p2 = new Vector2(keyframeZ.time, keyframeZ.value);

								var delta = p2 - p1;

								keyframeZ.inTangent = delta.y / delta.x;
							}

							keyframeY.tangentMode = 10;
						}

						if (curveX.keys.Any(key => Math.Abs(key.time - keyframeX.time) < 0.001f))
						{
							var keyX = curveX.keys
								.FirstOrDefault(key => Math.Abs(key.time - keyframeX.time) < 0.001f);
							var index = Array.IndexOf(curveX.keys, keyX);
							curveX.MoveKey(index, keyframeX);
						}
						else
						{
							curveX.AddKey(keyframeX);
						}

						if (curveY.keys.Any(key => Math.Abs(key.time - keyframeY.time) < 0.001f))
						{
							var keyY = curveY.keys
								.FirstOrDefault(key => Math.Abs(key.time - keyframeY.time) < 0.001f);
							var index = Array.IndexOf(curveY.keys, keyY);
							curveY.MoveKey(index, keyframeY);
						}
						else
						{
							curveY.AddKey(keyframeY);
						}

						if (curveZ.keys.Any(key => Math.Abs(key.time - keyframeZ.time) < 0.001f))
						{
							var keyZ = curveZ.keys
								.FirstOrDefault(key => Math.Abs(key.time - keyframeZ.time) < 0.001f);
							var index = Array.IndexOf(curveZ.keys, keyZ);
							curveZ.MoveKey(index, keyframeZ);
						}
						else
						{
							curveZ.AddKey(keyframeZ);
						}

						AnimationUtility.SetEditorCurve(animEvent.Clip, curveBindingX, curveX);
						AnimationUtility.SetEditorCurve(animEvent.Clip, curveBindingY, curveY);
						AnimationUtility.SetEditorCurve(animEvent.Clip, curveBindingZ, curveZ);

						animEvent.Clip.EnsureQuaternionContinuity();

						return;
					}
				}

				foreach (Keyframe keyframe in curveY.keys)
				{
					if (keyTimes.Any(t => Math.Abs(t - keyframe.time) < 0.001f))
						continue;
					var valueX = curveX.Evaluate(keyframe.time);
					var valueY = curveY.Evaluate(keyframe.time);
					var valueZ = curveZ.Evaluate(keyframe.time);

					var oldvector = new Vector3(valueX, valueY, valueZ);
					keyTimes.Add(keyframe.time);
					EditorGUI.BeginChangeCheck();
					var vector = Handles.DoPositionHandle(oldvector, Quaternion.identity);
					if (EditorGUI.EndChangeCheck())
					{
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

							var nextPoint = curveX.keys.OrderBy(key => key.time).SkipWhile(key => key.time > keyframe.time).Skip(1).FirstOrDefault();
							var prevPoint = curveX.keys.OrderBy(key => key.time).TakeWhile(key => key.time > keyframe.time).LastOrDefault();
							if (nextPoint.time == 0f)
							{
								keyframeX.outTangent = 0f;
							}
							else
							{
								var p1 = new Vector2(nextPoint.time, nextPoint.value);
								var p2 = new Vector2(keyframeX.time, keyframeX.value);

								var delta = p1 - p2;

								keyframeX.outTangent = delta.y / delta.x;
							}

							if (prevPoint.time == 0f)
							{
								keyframeX.inTangent = 0f;
							}
							else
							{
								var p1 = new Vector2(prevPoint.time, prevPoint.value);
								var p2 = new Vector2(keyframeX.time, keyframeX.value);

								var delta = p2 - p1;

								keyframeX.inTangent = delta.y / delta.x;
							}

							keyframeX.tangentMode = 10;
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
							var nextPoint = curveZ.keys.OrderBy(key => key.time).SkipWhile(key => key.time > keyframe.time).Skip(1).FirstOrDefault();
							var prevPoint = curveZ.keys.OrderBy(key => key.time).TakeWhile(key => key.time > keyframe.time).LastOrDefault();
							if (nextPoint.time == 0f)
							{
								keyframeZ.outTangent = 0f;
							}
							else
							{
								var p1 = new Vector2(nextPoint.time, nextPoint.value);
								var p2 = new Vector2(keyframeZ.time, keyframeZ.value);

								var delta = p1 - p2;

								keyframeZ.outTangent = delta.y / delta.x;
							}

							if (prevPoint.time == 0f)
							{
								keyframeZ.inTangent = 0f;
							}
							else
							{
								var p1 = new Vector2(prevPoint.time, prevPoint.value);
								var p2 = new Vector2(keyframeZ.time, keyframeZ.value);

								var delta = p2 - p1;

								keyframeZ.inTangent = delta.y / delta.x;
							}

							keyframeY.tangentMode = 10;
						}

						if (curveX.keys.Any(key => Math.Abs(key.time - keyframeX.time) < 0.001f))
						{
							var keyX = curveX.keys
								.FirstOrDefault(key => Math.Abs(key.time - keyframeX.time) < 0.001f);
							var index = Array.IndexOf(curveX.keys, keyX);
							curveX.MoveKey(index, keyframeX);
						}
						else
						{
							curveX.AddKey(keyframeX);
						}

						if (curveY.keys.Any(key => Math.Abs(key.time - keyframeY.time) < 0.001f))
						{
							var keyY = curveY.keys
								.FirstOrDefault(key => Math.Abs(key.time - keyframeY.time) < 0.001f);
							var index = Array.IndexOf(curveY.keys, keyY);
							curveY.MoveKey(index, keyframeY);
						}
						else
						{
							curveY.AddKey(keyframeY);
						}

						if (curveZ.keys.Any(key => Math.Abs(key.time - keyframeZ.time) < 0.001f))
						{
							var keyZ = curveZ.keys
								.FirstOrDefault(key => Math.Abs(key.time - keyframeZ.time) < 0.001f);
							var index = Array.IndexOf(curveZ.keys, keyZ);
							curveZ.MoveKey(index, keyframeZ);
						}
						else
						{
							curveZ.AddKey(keyframeZ);
						}

						AnimationUtility.SetEditorCurve(animEvent.Clip, curveBindingX, curveX);
						AnimationUtility.SetEditorCurve(animEvent.Clip, curveBindingY, curveY);
						AnimationUtility.SetEditorCurve(animEvent.Clip, curveBindingZ, curveZ);

						animEvent.Clip.EnsureQuaternionContinuity();

						return;
					}
				}

				foreach (Keyframe keyframe in curveZ.keys)
				{
					if (keyTimes.Any(t => Math.Abs(t - keyframe.time) < 0.001f))
						continue;
					var valueX = curveX.Evaluate(keyframe.time);
					var valueY = curveY.Evaluate(keyframe.time);
					var valueZ = curveZ.Evaluate(keyframe.time);

					var oldvector = new Vector3(valueX, valueY, valueZ);
					keyTimes.Add(keyframe.time);
					EditorGUI.BeginChangeCheck();
					var vector = Handles.DoPositionHandle(oldvector, Quaternion.identity);
					if (EditorGUI.EndChangeCheck())
					{
						Keyframe keyframeZ = new Keyframe(keyframe.time, vector.z) { inTangent = keyframe.inTangent, outTangent = keyframe.outTangent, tangentMode = keyframe.tangentMode };
						Keyframe keyframeX;
						if (curveX.keys.Any(key => Math.Abs(key.time - keyframe.time) < 0.001f))
						{
							keyframeX = curveX.keys.FirstOrDefault(key1 => Math.Abs(key1.time - keyframe.time) < 0.001f);
							keyframeX = new Keyframe(keyframeX.time, vector.x) { inTangent = keyframeX.inTangent, outTangent = keyframeX.outTangent, tangentMode = keyframeX.tangentMode };
						}
						else
						{
							keyframeX = new Keyframe(keyframe.time, vector.x);

							var nextPoint = curveX.keys.OrderBy(key => key.time).SkipWhile(key => key.time > keyframe.time).Skip(1).FirstOrDefault();
							var prevPoint = curveX.keys.OrderBy(key => key.time).TakeWhile(key => key.time > keyframe.time).LastOrDefault();
							if (nextPoint.time == 0f)
							{
								keyframeX.outTangent = 0f;
							}
							else
							{
								var p1 = new Vector2(nextPoint.time, nextPoint.value);
								var p2 = new Vector2(keyframeX.time, keyframeX.value);

								var delta = p1 - p2;

								keyframeX.outTangent = delta.y / delta.x;
							}

							if (prevPoint.time == 0f)
							{
								keyframeX.inTangent = 0f;
							}
							else
							{
								var p1 = new Vector2(prevPoint.time, prevPoint.value);
								var p2 = new Vector2(keyframeX.time, keyframeX.value);

								var delta = p2 - p1;

								keyframeX.inTangent = delta.y / delta.x;
							}

							keyframeX.tangentMode = 10;
						}
						Keyframe keyframeY;
						if (curveY.keys.Any(key => Math.Abs(key.time - keyframe.time) < 0.001f))
						{
							keyframeY = curveY.keys.FirstOrDefault(key1 => Math.Abs(key1.time - keyframe.time) < 0.001f);
							keyframeY = new Keyframe(keyframeY.time, vector.y) { inTangent = keyframeY.inTangent, outTangent = keyframeY.outTangent, tangentMode = keyframeY.tangentMode };
						}
						else
						{
							keyframeY = new Keyframe(keyframe.time, vector.y);

							var nextPoint = curveY.keys.OrderBy(key => key.time).SkipWhile(key => key.time > keyframe.time).Skip(1).FirstOrDefault();
							var prevPoint = curveY.keys.OrderBy(key => key.time).TakeWhile(key => key.time > keyframe.time).LastOrDefault();
							if (nextPoint.time == 0f)
							{
								keyframeY.outTangent = 0f;
							}
							else
							{
								var p1 = new Vector2(nextPoint.time, nextPoint.value);
								var p2 = new Vector2(keyframeY.time, keyframeY.value);

								var delta = p1 - p2;

								keyframeY.outTangent = delta.y / delta.x;
							}

							if (prevPoint.time == 0f)
							{
								keyframeY.inTangent = 0f;
							}
							else
							{
								var p1 = new Vector2(prevPoint.time, prevPoint.value);
								var p2 = new Vector2(keyframeY.time, keyframeY.value);

								var delta = p2 - p1;

								keyframeY.inTangent = delta.y / delta.x;
							}

							keyframeY.tangentMode = 10;
						}

						if (curveX.keys.Any(key => Math.Abs(key.time - keyframeX.time) < 0.001f))
						{
							var keyX = curveX.keys
								.FirstOrDefault(key => Math.Abs(key.time - keyframeX.time) < 0.001f);
							var index = Array.IndexOf(curveX.keys, keyX);
							curveX.MoveKey(index, keyframeX);
						}
						else
						{
							curveX.AddKey(keyframeX);
						}

						if (curveY.keys.Any(key => Math.Abs(key.time - keyframeY.time) < 0.001f))
						{
							var keyY = curveY.keys
								.FirstOrDefault(key => Math.Abs(key.time - keyframeY.time) < 0.001f);
							var index = Array.IndexOf(curveY.keys, keyY);
							curveY.MoveKey(index, keyframeY);
						}
						else
						{
							curveY.AddKey(keyframeY);
						}

						if (curveZ.keys.Any(key => Math.Abs(key.time - keyframeZ.time) < 0.001f))
						{
							var keyZ = curveZ.keys
								.FirstOrDefault(key => Math.Abs(key.time - keyframeZ.time) < 0.001f);
							var index = Array.IndexOf(curveZ.keys, keyZ);
							curveZ.MoveKey(index, keyframeZ);
						}
						else
						{
							curveZ.AddKey(keyframeZ);
						}

						AnimationUtility.SetEditorCurve(animEvent.Clip, curveBindingX, curveX);
						AnimationUtility.SetEditorCurve(animEvent.Clip, curveBindingY, curveY);
						AnimationUtility.SetEditorCurve(animEvent.Clip, curveBindingZ, curveZ);
						var settings = AnimationUtility.GetAnimationClipSettings(animEvent.Clip);
						animEvent.Clip.EnsureQuaternionContinuity();

						return;
					}
				}
			}

			var curveBindingRotX = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("localEulerAnglesRaw.x", StringComparison.InvariantCultureIgnoreCase));
			var curveBindingRotY = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("localEulerAnglesRaw.y", StringComparison.InvariantCultureIgnoreCase));
			var curveBindingRotZ = curveBindings
				.FirstOrDefault(curv => curv.propertyName.Equals("localEulerAnglesRaw.z", StringComparison.InvariantCultureIgnoreCase));
		}

		private void DrawTransformPath(AnimationTrackEvent trackEvent, bool showPositionNormales, Color pathColor)
		{
			DrawPositionTransformPath(trackEvent, showPositionNormales, pathColor);
		}

		private void DrawPositionTransformPath(AnimationTrackEvent trackEvent, bool showPositionNormales, Color pathColor)
		{
			var curveBindings = AnimationUtility.GetCurveBindings(trackEvent.Clip);
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

			var curveX = AnimationUtility.GetEditorCurve(trackEvent.Clip, curveBindingX);
			var curveY = AnimationUtility.GetEditorCurve(trackEvent.Clip, curveBindingY);
			var curveZ = AnimationUtility.GetEditorCurve(trackEvent.Clip, curveBindingZ);

			bool skipRotation = !showPositionNormales || (curveBindingRotX.propertyName == null || curveBindingRotY.propertyName == null ||
				curveBindingRotZ.propertyName == null);

			var curveRotX = skipRotation ? null : AnimationUtility.GetEditorCurve(trackEvent.Clip, curveBindingRotX);
			var curveRotY = skipRotation ? null : AnimationUtility.GetEditorCurve(trackEvent.Clip, curveBindingRotY);
			var curveRotZ = skipRotation ? null : AnimationUtility.GetEditorCurve(trackEvent.Clip, curveBindingRotZ);

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
			for (int i = 0; i < keyPoints.Count-1; i++)
			{
				var length = keyPoints[i + 1] - keyPoints[i];
				var countOfFrames = (int)(trackEvent.Clip.frameRate * length);
				var oneframe = length / countOfFrames;
				for (int j = 0; j < countOfFrames; j++)
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

					Handles.color = pathColor;
					Handles.DrawAAPolyLine(3f,
						new Vector3(x, y, z),
						new Vector3(x1, y1, z1));
				}
			}
			Handles.color = color;
		}

		private void HandleLenghDifference(AnimationTrackEvent trackEvent, float dif)
		{
			var curveBindings = AnimationUtility.GetCurveBindings(trackEvent.Clip);
			foreach (EditorCurveBinding editorCurveBinding in curveBindings)
			{
				var curve = AnimationUtility.GetEditorCurve(trackEvent.Clip, editorCurveBinding);

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
				AnimationUtility.SetEditorCurve(trackEvent.Clip, editorCurveBinding, curve);
				trackEvent.Clip.EnsureQuaternionContinuity();
			}
		}
		#endregion
	}
}
