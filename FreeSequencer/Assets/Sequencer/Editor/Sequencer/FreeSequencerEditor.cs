using System;
using System.Collections.Generic;
using System.Linq;
using FreeSequencer.Events;
using FreeSequencer.Tracks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FreeSequencer.Editor
{
	public class FreeSequencerEditor : EditorWindow
	{
		private const float START_TIMELINE_X = 205f;
		private const float END_TIMELINE_OFFSET_X = 10f;
		private const float START_TIMELINE_Y = 200f;

		private int CurrentFrameNumber
		{
			get { return _currentFrame; }
			set
			{
				if (_currentFrame != value)
				{
					CurrentFrameChanged(value);
				}
			}
		}

		private int _currentFrame;
		private int _minFrame;
		private int _maxFrame;
		private bool _isPlaying;
		private float _speed;

		private Sequence _selectedSequence;
		private TrackEvent _selectedEvent;
		private SequenceEditorArea _sequenceEditorArea;

		private FreeSequenceInspector FreeSequanceInspector
		{
			get
			{
				if (_freeSequencerInspectorWindow == null)
					_freeSequencerInspectorWindow = GetWindow<FreeSequenceInspector>();
				return _freeSequencerInspectorWindow;
			}
		}

		private static FreeSequencerEditor _freeSequencerEditorWindow;
		private static FreeSequenceInspector _freeSequencerInspectorWindow;
		private TimeLineArea _timeLineEditorArea;
		private Vector2 _scrollPosition;
		private bool _prevIsPlaing;
		private float _playTime;

		void OnEnable()
		{
			_freeSequencerEditorWindow = this;
			_minFrame = 0;
			_maxFrame = 1;
			_speed = 1f;

			if (_selectedSequence != null)
			{
				_maxFrame = _selectedSequence.Length;
			}

			if (_sequenceEditorArea == null)
			{
				_sequenceEditorArea = new SequenceEditorArea();
				_sequenceEditorArea.OnSequanceChanged += OnSequenceChanged;
				_sequenceEditorArea.OnSequenceParametersChanged += OnSequenceParametersChanged;
			}

			if (_timeLineEditorArea == null)
			{
				_timeLineEditorArea = new TimeLineArea();
				_timeLineEditorArea.OnChangeCurrentFrame += CurrentFrameChanged;
			}

			AnimatiedGameObjectEditor.RemoveAnimatedGameObject += RemoveAnimatedGameObject;
			AnimatiedGameObjectEditor.AddAnimationTrack += OnAddAnimationTrack;
			AnimatiedGameObjectEditor.RemoveTrack += OnRemoveTrack;
			AnimatiedGameObjectEditor.OnEventSelection += OnEventSelection;
			AnimatiedGameObjectEditor.AddEvent += OnAddEvent;
			AnimatiedGameObjectEditor.RemoveEvent += OnRemoveEvent;
			AnimatiedGameObjectEditor.OnEventDragged += OnEventDragged;
			AnimatiedGameObjectEditor.GenerateTrack += GenerateAnimatorStateMachine;
			AnimatiedGameObjectEditor.Move += OnMove;

			FreeSequanceInspector.EventChanged += OnTrackEventChanged;
		}

		void OnFocus()
		{
			SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
			SceneView.onSceneGUIDelegate += this.OnSceneGUI;
		}

		void OnDestroy()
		{
			SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
		}

		private EditorParameters GetEditorParameters()
		{
			return new EditorParameters()
			{
				MinFrame = _selectedSequence == null ? 0 : _minFrame,
				MaxFrame = _selectedSequence == null ? 1 : _maxFrame,
				Length = _selectedSequence == null ? 1 : _selectedSequence.Length,
				FrameRate = _selectedSequence == null ? 60 : _selectedSequence.FrameRate,
				CurrentFrame = _currentFrame,
				IsPlaying = _isPlaying
			};
		}

		void OnDisable()
		{
			if (_sequenceEditorArea != null)
			{
				_sequenceEditorArea.OnSequanceChanged -= OnSequenceChanged;
			}

			if (_timeLineEditorArea != null)
			{
				_timeLineEditorArea.OnChangeCurrentFrame += CurrentFrameChanged;
			}

			AnimatiedGameObjectEditor.RemoveAnimatedGameObject -= RemoveAnimatedGameObject;
			AnimatiedGameObjectEditor.AddAnimationTrack -= OnAddAnimationTrack;
			AnimatiedGameObjectEditor.RemoveTrack -= OnRemoveTrack;
			AnimatiedGameObjectEditor.OnEventSelection -= OnEventSelection;
			AnimatiedGameObjectEditor.AddEvent -= OnAddEvent;
			AnimatiedGameObjectEditor.RemoveEvent -= OnRemoveEvent;
			AnimatiedGameObjectEditor.OnEventDragged -= OnEventDragged;

			FreeSequanceInspector.EventChanged -= OnTrackEventChanged;
		}

		[MenuItem("FreeSequencer/Editor1")]
		static void ShowEditor()
		{
			GetWindow<FreeSequencerEditor>();
		}

		public void Update()
		{
			if (!_isPlaying && _selectedEvent != null)
			{
				var animationEvent = _selectedEvent as AnimationTrackEvent;
				if (animationEvent != null && AnimationWindowHelper.AnimationWindow != null &&
					EditorWindow.focusedWindow == AnimationWindowHelper.AnimationWindow)
				{
					var frame = animationEvent.StartFrame + AnimationWindowHelper.GetCurrentFrame();
					CurrentFrameNumber = frame;
				}
			}
		}

		private void OnGUI()
		{
			var width = position.width;
			var height = position.height;
			GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
			GUILayout.Space(3f);
			GUILayout.EndVertical();
			_sequenceEditorArea.OnDraw(_selectedSequence);
			var timeLineParameters = new TimeLineParameters()
			{
				CurrentFrame = _currentFrame,
				MinFrame = _selectedSequence != null ? _minFrame : 0,
				MaxFrame = _selectedSequence != null ? _maxFrame : 1,
				StartWidth = 20f,
				EndWidth = 200f,
				StartHeight = 30f,
				EndHeight = 200f,
				FrameRate = _selectedSequence != null ? _selectedSequence.FrameRate : 60,
				IsPlaying = _isPlaying
			};

			GUILayout.BeginArea(new Rect(5, 50f, 200, position.height - 100f));
			DrawDragArea();
			GUILayout.EndArea();

			GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.Height(25f));
			GUILayout.Label("");
			GUILayout.EndHorizontal();
			if (_selectedSequence != null)
			{
				var color = new Color(83f / 256f, 83f / 256f, 83f / 256, 1f);
				Handles.DrawSolidRectangleWithOutline(new Rect(5, 25f, position.width - 30f, 25f), color, color);
				Handles.DrawSolidRectangleWithOutline(new Rect(5, 25f, 10f, height - 105f), color, color);
				GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.Height(height - 130f));
				GUILayout.Space(5f);
				_scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, false);
				if (_selectedSequence.Objects != null && _selectedSequence.Objects.Any())
				{
					int countOfRows = 0;
					var lastGoHeightPosition = 0f;
					foreach (AnimatedGameObject animatedGameObject in _selectedSequence.Objects)
					{
						countOfRows++;
						if (animatedGameObject.Toggled)
							countOfRows += animatedGameObject.Tracks.Count;

						var goHeightCount = 1;
						if (animatedGameObject.Tracks != null && animatedGameObject.Toggled)
							goHeightCount += animatedGameObject.Tracks.Count;
						var goHeight = goHeightCount * AnimatiedGameObjectEditor.RowHeight;
						var rect = new Rect(10, lastGoHeightPosition, position.width - 30f, goHeight);

						GUILayout.BeginArea(rect);
						AnimatiedGameObjectEditor.OnDraw(timeLineParameters, animatedGameObject, rect);
						GUILayout.EndArea();
						lastGoHeightPosition += goHeight;
					}
					GUILayout.Label("", GUILayout.Height(25f * countOfRows));
				}
				//GUILayout.EndArea();
				GUILayout.EndScrollView();
				GUILayout.Space(5f);
				GUILayout.EndHorizontal();
			}

			var timelineRect = new Rect(START_TIMELINE_X, 50f, position.width - START_TIMELINE_X - END_TIMELINE_OFFSET_X, position.height - 100f);
			GUILayout.BeginArea(timelineRect);
			_timeLineEditorArea.OnDraw(timeLineParameters, timelineRect);
			GUILayout.EndArea();

			var framesRect = new Rect(15f, position.height - 50f, position.width - 30f, 25f);
			GUILayout.BeginArea(framesRect);
			GUILayout.Space(3f);
			EditorGUILayout.BeginHorizontal(GUILayout.Height(25f));
			float start = _minFrame;
			float end = _maxFrame;

			EditorGUILayout.MinMaxSlider(ref start, ref end, 0, _selectedSequence != null ? _selectedSequence.Length : 1);
			_minFrame = (int)start;
			_maxFrame = (int)end;
			GUILayout.Space(3f);
			EditorGUILayout.EndHorizontal();

			GUILayout.EndArea();
			var controlsRect = new Rect(15f, position.height - 25f, position.width - 30f, 25f);
			GUILayout.BeginArea(controlsRect);
			DrawTimeControls();
			GUILayout.EndArea();

			HandlePlayingAnimation();
		}

		private void DrawTimeControls()
		{
			EditorGUILayout.BeginHorizontal(GUILayout.Height(15f));
			EditorGUILayout.BeginHorizontal(GUILayout.Width(3f));
			EditorGUILayout.Space();
			EditorGUILayout.EndHorizontal();
			if (GUILayout.Button("<<", EditorStyles.toolbarButton, GUILayout.Width(30f)))
			{
				if (_currentFrame > _minFrame)

					_currentFrame = _minFrame;
			}
			if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(30f)))
			{
				if (CurrentFrameNumber > _minFrame)
					CurrentFrameNumber--;
			}

			CurrentFrameNumber = EditorGUILayout.IntField(_currentFrame, GUILayout.Width(40f));

			if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(30f)))
			{
				if (CurrentFrameNumber < _maxFrame)
					CurrentFrameNumber++;
			}
			if (GUILayout.Button(">>", EditorStyles.toolbarButton, GUILayout.Width(30f)))
			{
				if (CurrentFrameNumber < _maxFrame)
					CurrentFrameNumber = _maxFrame;
			}

			EditorGUILayout.BeginHorizontal(GUILayout.Width(3f));
			EditorGUILayout.Space();
			EditorGUILayout.EndHorizontal();

			_isPlaying = GUILayout.Toggle(_isPlaying, !_isPlaying ? "Play" : "Pause", EditorStyles.toolbarButton, GUILayout.Width(40f));

			GUILayout.FlexibleSpace();

			EditorGUILayout.LabelField("Speed", GUILayout.Width(50f));
			_speed = EditorGUILayout.Slider(_speed, 0.25f, 3f, GUILayout.Width(120f));
			_speed = (int) (_speed/0.25f)*0.25f;
			if (_speed < 0.01f)
				_speed = 0.25f;
			GUILayout.FlexibleSpace();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("View Range", GUILayout.Width(70f));
			var min = EditorGUILayout.IntField(_minFrame, GUILayout.Width(50f));
			_minFrame = Mathf.Clamp(min, 0, _maxFrame - 1);
			EditorGUILayout.LabelField("-", GUILayout.Width(10f));
			var max = EditorGUILayout.IntField(_maxFrame, GUILayout.Width(50f));
			_maxFrame = Mathf.Clamp(max, _minFrame + 1, _selectedSequence != null ? _selectedSequence.Length : 1);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal(GUILayout.Width(3f));
			EditorGUILayout.Space();
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal(GUILayout.Height(4f));
			EditorGUILayout.Space();
			EditorGUILayout.EndHorizontal();

			if (_selectedSequence != null)
			{
				HandlePlayingAnimation();
				if (!_isPlaying && EditorWindow.focusedWindow == AnimationWindowHelper.AnimationWindow)
				{
					var selectedAnimationEvent = _selectedEvent as AnimationTrackEvent;
					if (selectedAnimationEvent != null)
					{
						var frame = AnimationWindowHelper.GetCurrentFrame();
						CurrentFrameNumber = _selectedEvent.StartFrame + frame;
					}
				}
			}
		}

		private void DrawDragArea()
		{
			Event evt = Event.current;
			Rect drop_area = GUILayoutUtility.GetRect(0.0f, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

			switch (evt.type)
			{
				case EventType.DragUpdated:
				case EventType.DragPerform:
					if (!drop_area.Contains(evt.mousePosition))
						return;

					DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

					if (evt.type == EventType.DragPerform)
					{
						DragAndDrop.AcceptDrag();

						foreach (Object dragged_object in DragAndDrop.objectReferences)
						{
							GameObject gameObject = dragged_object as GameObject;
							if (gameObject == null)
								continue;

							AddAnimatedGameObject(gameObject);
						}
					}
					break;
			}
		}

		#region Events handlers

		private void OnMove(MoveHolder holder)
		{
			Undo.RecordObject(holder.Event, "Event moved");
			if (holder.MoveRight)
			{
				var nextEvent = holder.Track.Events
					.OrderBy(evt => evt.StartFrame).FirstOrDefault(evt => evt.StartFrame > holder.Event.StartFrame);
				Undo.RecordObject(nextEvent, "Event moved");
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
				Undo.RecordObject(nextEvent, "Event moved");
				var gap = holder.Event.StartFrame - nextEvent.EndFrame;
				var lengthNextEvt = nextEvent.Length;
				var holderEvtLength = holder.Event.Length;

				holder.Event.StartFrame = nextEvent.StartFrame;
				holder.Event.EndFrame = nextEvent.StartFrame + holderEvtLength;

				nextEvent.StartFrame = holder.Event.EndFrame + gap;
				nextEvent.EndFrame = nextEvent.StartFrame + lengthNextEvt;
			}
			FreeSequanceInspector.Repaint();
		}

		private void OnSequenceParametersChanged(int prevLength)
		{
			if (prevLength != _selectedSequence.Length)
			{
				var dif = _selectedSequence.Length / (float)prevLength;
				foreach (AnimatedGameObject animatedGameObject in _selectedSequence.Objects)
				{
					foreach (var baseTrack in animatedGameObject.Tracks)
					{
						foreach (var trackEvent in baseTrack.Events)
						{
							trackEvent.StartFrame = (int)(trackEvent.StartFrame * dif);
							trackEvent.EndFrame = (int)(trackEvent.EndFrame * dif);
						}
					}
				}
				_minFrame = 0;
				_maxFrame = _selectedSequence.Length;
			}
		}

		private void OnEventDragged(EventSelectionHolder obj)
		{
			Undo.RecordObject(obj.Event, "Drag event");
			Repaint();
			FreeSequanceInspector.Repaint();
			OnTrackEventChanged(obj.Event);
		}

		private void OnTrackEventChanged(TrackEvent trackEvent)
		{
			Undo.RecordObject(_selectedSequence, "Track Changed");
			var animTrack = trackEvent as AnimationTrackEvent;
			if (animTrack != null)
			{
				if (animTrack.ControlAnimation && animTrack.Clip != null)
				{
					HandleControllAnimation(animTrack);
				}

				if (AnimationWindowHelper.AnimationWindow != null)
					AnimationWindowHelper.AnimationWindow.Repaint();
			}
			Repaint();
		}

		private void OnSequenceChanged(Sequence sequence)
		{
			_selectedSequence = sequence;
			if (_selectedSequence != null)
			{
				_minFrame = 0;
				_maxFrame = _selectedSequence.Length;
				_speed = 1f;
			}
			Repaint();
		}

		private void AddAnimatedGameObject(GameObject gameObject)
		{
			if (_selectedSequence == null)
				return;

			if (_selectedSequence.Objects == null)
				_selectedSequence.Objects = new List<AnimatedGameObject>();

			if (_selectedSequence.Objects.Any(go => go.GameObject == gameObject))
				return;

			var newObject = new AnimatedGameObject() { GameObject = gameObject };
			_selectedSequence.Objects.Add(newObject);
			Repaint();
		}

		private void OnAddAnimationTrack(AnimatedGameObject obj)
		{
			if (_selectedSequence == null)
				return;
			Undo.RecordObject(obj, "Added new track");
			if (obj.Tracks == null)
				obj.Tracks = new List<BaseTrack>();

			var newTrack = ScriptableObject.CreateInstance<AnimationTrack>();
			newTrack.TrackName = "Play Animation";
			newTrack.Enabled = true;

			if (newTrack.Events == null)
				newTrack.Events = new List<TrackEvent>();

			var newEvent = ScriptableObject.CreateInstance<AnimationTrackEvent>();
			newEvent.StartFrame = 0;
			newEvent.EndFrame = _selectedSequence.Length;
			newEvent.EventTitle = "Anim";

			newTrack.Events.Add(newEvent);
			obj.Tracks.Add(newTrack);
		}

		private void RemoveAnimatedGameObject(AnimatedGameObject obj)
		{
			if (_selectedSequence == null)
				return;

			_selectedSequence.Objects.Remove(obj);
		}


		private void OnRemoveEvent(RemoveEventHolder holder)
		{
			if (_selectedSequence != null)
			{
				Undo.RecordObject(holder.Track, "Remove event");
				if (holder.Track.Events.Any(evt => evt == _selectedEvent))
				{
					FreeSequanceInspector.Init(GetEditorParameters());
					FreeSequanceInspector.Repaint();
				}

				holder.Track.Events.Remove(holder.Event);
				DestroyImmediate(holder.Event);
				Repaint();
			}
		}

		private void OnAddEvent(AddEventHolder holder)
		{
			if (_selectedSequence != null)
			{
				Undo.RecordObject(holder.Track, "Add new event");
				if (holder.Track.Events.Any(evt => evt == _selectedEvent))
				{
					FreeSequanceInspector.Init(GetEditorParameters());
					FreeSequanceInspector.Repaint();
				}

				var animationTrack = holder.Track as AnimationTrack;
				if (animationTrack != null)
				{
					var newEvent = CreateInstance<AnimationTrackEvent>();

					newEvent.StartFrame = holder.StartFrame;
					newEvent.EndFrame = holder.EndFrame;
					newEvent.EventTitle = "Animation";

					holder.Track.Events.Add(newEvent);
					FreeSequanceInspector.Init(GetEditorParameters(), _selectedSequence, holder.AnimatedGameObject, holder.Track, newEvent);

					Repaint();
					FreeSequanceInspector.Repaint();
				}

			}
		}

		private void OnRemoveTrack(RemoveTrackHolder holder)
		{
			if (_selectedSequence != null)
			{
				Undo.RecordObject(holder.AnimatedGameObject, "Remove track");
				if (holder.Track.Events.Any(evt => evt == _selectedEvent))
				{
					FreeSequanceInspector.Init(GetEditorParameters());
					FreeSequanceInspector.Repaint();
				}
				holder.AnimatedGameObject.Tracks.Remove(holder.Track);
				DestroyImmediate(holder.Track);
				Repaint();
				FreeSequanceInspector.Repaint();
			}
		}

		private void OnEventSelection(EventSelectionHolder holder)
		{
			if (_selectedSequence != null)
			{
				foreach (var animatedGameObject in _selectedSequence.Objects)
				{
					foreach (var baseTrack in animatedGameObject.Tracks)
					{
						foreach (var trackEvent in baseTrack.Events)
						{
							trackEvent.IsActive = false;
						}
					}
				}

				holder.Event.IsActive = true;
				_selectedEvent = holder.Event;
				FreeSequanceInspector.Init(GetEditorParameters(), _selectedSequence, holder.AnimatedGameObject, holder.Track, holder.Event);
				FreeSequanceInspector.Repaint();

				var animationEvent = _selectedEvent as AnimationTrackEvent;
				if (animationEvent != null)
				{
					if (animationEvent.Clip != null)
					{
						if (AnimationWindowHelper.AnimationWindow != null)
						{
							Selection.activeGameObject = holder.AnimatedGameObject.GameObject;
							AnimationWindowHelper.SelectAnimationClip(animationEvent.Clip);
						}
					}
				}
			}
		}

		#endregion

		private void CurrentFrameChanged(int frame)
		{
			if (Mathf.Abs(CurrentFrameNumber - frame) > 0)
			{
				if (CurrentFrameNumber > frame)
				{
					for (int i = CurrentFrameNumber - 1; i >= frame; i--)
					{
						_currentFrame = i;
						ProcessEvents();
						Repaint();
					}
				}
				else
				{
					for (int i = CurrentFrameNumber + 1; i <= frame; i++)
					{
						_currentFrame = i;
						ProcessEvents();
						Repaint();
					}
				}

				if (_selectedEvent != null && AnimationWindowHelper.AnimationWindow != null)
				{
					var animationEvent = _selectedEvent as AnimationTrackEvent;
					if (animationEvent != null)
					{
						if (animationEvent.Clip != null)
						{
							var animationFrame = CurrentFrameNumber - animationEvent.StartFrame;
							var currentTime = animationFrame / animationEvent.Clip.frameRate;
							if (animationEvent.ControlAnimation)
							{
								if (currentTime < 0 || currentTime > animationEvent.Clip.length)
									AnimationWindowHelper.StopAnimationMode();
								else
									AnimationWindowHelper.StartAnimationMode();
							}

							AnimationWindowHelper.SetCurrentFrame(0,
								Mathf.Clamp(currentTime, 0f, animationEvent.Clip.length));
						}
					}
				}
			}
			if (_isPlaying && AnimationWindowHelper.AnimationWindow != null)
				AnimationWindowHelper.StopAnimationMode();
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
				var tick = (float)(100f / _selectedSequence.FrameRate) * 0.01f / _speed;
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
			if (CurrentFrameNumber < _maxFrame)
			{
				CurrentFrameNumber++;
			}
			else
			{
				_isPlaying = false;
			}
		}

		private void ProcessEvents()
		{
			foreach (var animatedGameObject in _selectedSequence.Objects)
			{
				foreach (var baseTrack in animatedGameObject.Tracks.Where(track => track.Enabled))
				{
					foreach (var trackEvent in baseTrack.Events)
					{
						if (trackEvent.StartFrame > CurrentFrameNumber || trackEvent.EndFrame < CurrentFrameNumber)
							continue;

						ProcessEvent(trackEvent, animatedGameObject.GameObject);
					}
				}
			}
		}

		private void ProcessEvent(TrackEvent trackEvent, GameObject gameObject)
		{
			var animationEvent = trackEvent as AnimationTrackEvent;
			if (animationEvent != null)
			{
				ProcessAnimationEvent(animationEvent, gameObject);
			}
		}

		private void ProcessAnimationEvent(AnimationTrackEvent animationTrackEvent, GameObject gameObject)
		{
			if (animationTrackEvent.Clip != null)
			{
				if (animationTrackEvent.ControlAnimation)
				{
					animationTrackEvent.Clip.frameRate = _selectedSequence.FrameRate;
					var length = (float)(animationTrackEvent.EndFrame - animationTrackEvent.StartFrame) / (float)_selectedSequence.FrameRate;
					if (Mathf.Abs(animationTrackEvent.Clip.length - length) > 0.001f)
					{
						HandleLenghDifference(animationTrackEvent, length / animationTrackEvent.Clip.length);
					}
				}

				var frameNumber = CurrentFrameNumber - animationTrackEvent.StartFrame;
				animationTrackEvent.Clip.SampleAnimation(gameObject, (float)frameNumber / animationTrackEvent.Clip.frameRate);
			}
		}

		private void HandleControllAnimation(AnimationTrackEvent animationTrackEvent)
		{
			animationTrackEvent.Clip.frameRate = _selectedSequence.FrameRate;
			var length = (float)(animationTrackEvent.EndFrame - animationTrackEvent.StartFrame) / (float)_selectedSequence.FrameRate;
			if (Mathf.Abs(animationTrackEvent.Clip.length - length) > 0.001f)
			{
				HandleLenghDifference(animationTrackEvent, length / animationTrackEvent.Clip.length);
			}
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

		#region OnSceneGUI

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
										var path = track.ObjectsPathToShowTransform;
										DrawTransformPath(animEvent, track.ShowRotationNormales, track.PathColor, path);
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
		}

		private void DrawTransformPath(AnimationTrackEvent trackEvent, bool showPositionNormales, Color pathColor, string path)
		{
			DrawPositionTransformPath(trackEvent, showPositionNormales, pathColor, path);
		}

		private void DrawPositionTransformPath(AnimationTrackEvent trackEvent, bool showPositionNormales, Color pathColor, string path)
		{
			var curveBindings = AnimationUtility.GetCurveBindings(trackEvent.Clip).Where(curve => curve.path.Equals(path));
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
			for (int i = 0; i < keyPoints.Count - 1; i++)
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
		#endregion

		#region Generation

		private void GenerateAnimatorStateMachine(AnimationTrack animationTrack)
		{
			if (_selectedSequence == null || animationTrack.Controller == null || !animationTrack.Events.Any() || animationTrack.Events.Any(ev=> ((AnimationTrackEvent)ev).Clip == null))
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
				.FirstOrDefault(sm => sm.name.Equals(_selectedSequence.name));
			if (seqStateMachine != null)
				rootStateMAchine.RemoveStateMachine(seqStateMachine);

			seqStateMachine = rootStateMAchine.AddStateMachine(_selectedSequence.name);

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

					if (i != 0)
					{
						var exitTime = 1f;
						var prevEvt = events[i - 1] as AnimationTrackEvent;
						if (!prevEvt.ControlAnimation)
						{
							var prevClipFrames = prevEvt.Clip.length * prevEvt.Clip.frameRate;
							var prevEventLength = prevEvt.Length;
							exitTime = prevEventLength / prevClipFrames;
						}
						var tran1 = prevState.AddTransition(prevEvtState);
						tran1.duration = 0f;
						tran1.exitTime = exitTime;
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

				if (i == 0)
				{
					var firstState = prevState ?? evtState;
					seqStateMachine.AddEntryTransition(firstState);
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

						var exitTime = 1f;
						var prevEvt = events[i] as AnimationTrackEvent;
						if (!prevEvt.ControlAnimation)
						{
							var prevClipFrames = prevEvt.Clip.length * prevEvt.Clip.frameRate;
							var prevEventLength = prevEvt.Length;
							exitTime = prevEventLength / prevClipFrames;
						}
						var tran2 = prevState.AddTransition(nexEvtState);
						tran2.duration = 0f;
						tran2.exitTime = exitTime;
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

			string paramName = _selectedSequence.name;
			var param = controller.parameters.FirstOrDefault(par =>
				par.type == UnityEngine.AnimatorControllerParameterType.Trigger
					&& par.name.Equals(paramName));

			if (param == null)
				controller.AddParameter(paramName, UnityEngine.AnimatorControllerParameterType.Trigger);

			var transition = rootStateMAchine.AddAnyStateTransition(seqStateMachine);
			transition.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, paramName);
		}

		#endregion
	}
}