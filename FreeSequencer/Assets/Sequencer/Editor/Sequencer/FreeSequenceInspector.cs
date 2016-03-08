using System;
using System.Linq;
using FreeSequencer.Events;
using FreeSequencer.Tracks;
using UnityEditor;
using UnityEngine;

namespace FreeSequencer.Editor
{
	public class FreeSequenceInspector : EditorWindow
	{
		private AnimatedGameObject _animatedGameObject;
		private BaseTrack _track;
		private TrackEvent _trackEvent;
		private string _sequenceName;

		private EditorParameters _lastEditorParameters;
		private FreeSequencerEditor _freeSequencerEditorWindow;
		private Sequence _selectedSequence;
		public event Action<TrackEvent> EventChanged;
		private FreeSequencerEditor FreeSequanceEditor
		{
			get
			{
				if (_freeSequencerEditorWindow == null)
					_freeSequencerEditorWindow = GetWindow<FreeSequencerEditor>();
				return _freeSequencerEditorWindow;
			}
		}

		public void Init(EditorParameters parameters, Sequence selectedSequence = null, AnimatedGameObject animatedGameObject = null, BaseTrack track = null, TrackEvent trackEvent = null)
		{
			_selectedSequence = selectedSequence;
			_sequenceName = selectedSequence == null ? string.Empty : selectedSequence.name;
			_animatedGameObject = animatedGameObject;
			_track = track;
			_trackEvent = trackEvent;

			_lastEditorParameters = parameters;
		}

		private void OnGUI()
		{
			if (_lastEditorParameters == null)
				return;
			if (_trackEvent != null)
			{
				DrawTrackEvent();
				GUILayout.Space(10f);
			}

			if (_track != null)
			{
				DrawTrack();
				GUILayout.Space(10f);
				DrawTrackTools();
			}
		}

		private void DrawTrackTools()
		{
			EditorGUILayout.BeginVertical(GUI.skin.textArea);
			EditorGUILayout.LabelField("Track Tools:", EditorStyles.boldLabel, GUILayout.Width(150f));

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Show Transform Path", GUILayout.Width(150f));
			EditorGUI.BeginChangeCheck();
			var trackShowTransform = EditorGUILayout.Toggle(_track.ShowTransformPath);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(_track, "Track Path Changed");
				_track.ShowTransformPath = trackShowTransform;
				FreeSequanceEditor.Repaint();
				SceneView.RepaintAll();
			}

			EditorGUILayout.EndHorizontal();

			_track.ObjectsPathToShowTransform = string.Empty;

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Show Rotation Normales", GUILayout.Width(150f));
			_track.ShowRotationNormales = EditorGUILayout.Toggle(_track.ShowRotationNormales);

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Show Key Frames", GUILayout.Width(150f));
			EditorGUI.BeginChangeCheck();
			var trackShowKeyFrames = EditorGUILayout.Toggle(_track.ShowKeyFrames);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(_track, "Track Key Changed");
				_track.ShowKeyFrames = trackShowKeyFrames;
				FreeSequanceEditor.Repaint();
				SceneView.RepaintAll();
			}
			EditorGUILayout.EndHorizontal();

			if (_track.ShowTransformPath)
			{
				EditorGUILayout.BeginHorizontal();

				EditorGUILayout.LabelField("Path Color", GUILayout.Width(150f));
				EditorGUI.BeginChangeCheck();
				_track.PathColor = EditorGUILayout.ColorField(_track.PathColor);
				if (EditorGUI.EndChangeCheck())
				{
					FreeSequanceEditor.Repaint();
					SceneView.RepaintAll();
				}
				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.EndVertical();
		}

		private void DrawTrack()
		{
			EditorGUILayout.BeginVertical(GUI.skin.textArea);
			EditorGUILayout.LabelField("Track:", EditorStyles.boldLabel, GUILayout.Width(150f));

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Enabled", GUILayout.Width(150f));
			EditorGUI.BeginChangeCheck();
			var trackEnabled = EditorGUILayout.Toggle(_track.Enabled);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(_track, "Track Enabled Changed");
				_track.Enabled = trackEnabled;
				FreeSequanceEditor.Repaint();
				SceneView.RepaintAll();
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Name", GUILayout.Width(150f));
			EditorGUI.BeginChangeCheck();
			var trackName = EditorGUILayout.TextField(_track.TrackName);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(_track, "Track Enabled Changed");
				_track.TrackName = trackName;
				FreeSequanceEditor.Repaint();
				SceneView.RepaintAll();
			}
			EditorGUILayout.EndHorizontal();

			DrawSpecificTrack();

			EditorGUILayout.EndVertical();
		}

		private void DrawSpecificTrack()
		{
			var animationTrack = _track as AnimationTrack;

			if (animationTrack != null)
				DrawAnimationTrack(animationTrack);
		}

		private void DrawAnimationTrack(AnimationTrack track)
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Animator Ctrl", GUILayout.Width(150f));
			var animator = _animatedGameObject.GameObject.GetComponent<Animator>();
			if (animator == null)
			{
				if (GUILayout.Button("Create Animator"))
					animator = _animatedGameObject.GameObject.AddComponent<Animator>();
				EditorGUILayout.EndHorizontal();
			}
			else
			{
				EditorGUI.BeginChangeCheck();
				var controller = (RuntimeAnimatorController)EditorGUILayout.ObjectField(track.Controller, typeof(RuntimeAnimatorController), true);
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(_track, "Track Controller Changed");
					track.Controller = controller;
				}
				EditorGUILayout.EndHorizontal();
				if (animator.runtimeAnimatorController != null)
				{
					Undo.RecordObject(_track, "Track Controller Changed");
					track.Controller = animator.runtimeAnimatorController;
				}

				if (track.Controller == null && animator.runtimeAnimatorController == null)
				{
					if (GUILayout.Button("Create Ctrl"))
					{
						Undo.RecordObject(_track, "Track Controller Changed");
						CreateController(track);
						animator.runtimeAnimatorController = track.Controller;
					}
				}
				if (track.Controller != null)
				{
					EditorGUILayout.BeginHorizontal();

					EditorGUILayout.LabelField("Animator Layer", GUILayout.Width(150f));
					//track.ControllerLayer = EditorGUILayout.TextField(track.ControllerLayer);
					var controllerEditor = track.Controller as UnityEditor.Animations.AnimatorController;
					var layers = controllerEditor.layers.Select(l => l.name).ToList();
					var layerIndex = 0;
					if (layers.Any(l => l == track.ControllerLayer))
					{
						layerIndex = layers.IndexOf(track.ControllerLayer);
					}
					if (layerIndex > layers.Count - 1)
						layerIndex = 0;
					EditorGUI.BeginChangeCheck();
					layerIndex = EditorGUILayout.Popup(layerIndex, layers.ToArray(), GUILayout.Width(200));
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(_track, "Track Layer Changed");
						track.ControllerLayer = layers[layerIndex];
					}
					EditorGUILayout.EndHorizontal();
				}
			}
		}

		private void DrawTrackEvent()
		{
			EditorGUILayout.BeginVertical(GUI.skin.textArea);
			EditorGUILayout.LabelField("Event:", EditorStyles.boldLabel, GUILayout.Width(150f));
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Range", GUILayout.Width(150f));
			EditorGUILayout.LabelField("S:", GUILayout.Width(15f));
			EditorGUI.BeginChangeCheck();
			var start = EditorGUILayout.IntField(_trackEvent.StartFrame);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(_trackEvent, "Track Event Changed");
				var prevStartFrame = _trackEvent.StartFrame;
				_trackEvent.StartFrame = Mathf.Clamp(start, _lastEditorParameters.MinFrame, _trackEvent.EndFrame - 1);
				if (_trackEvent.StartFrame != prevStartFrame)
					OnEventChanged(_trackEvent);
			}

			EditorGUILayout.LabelField("E:", GUILayout.Width(15f));
			EditorGUI.BeginChangeCheck();
			var end = EditorGUILayout.IntField(_trackEvent.EndFrame);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(_trackEvent, "Track Event Changed");
				var prevEndFrame = _trackEvent.EndFrame;
				_trackEvent.EndFrame = Mathf.Clamp(end, _trackEvent.StartFrame + 1, _lastEditorParameters.MaxFrame);
				if (_trackEvent.EndFrame != prevEndFrame)
					OnEventChanged(_trackEvent);
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(string.Empty, GUILayout.Width(150f));
			float _min = _trackEvent.StartFrame;
			float _max = _trackEvent.EndFrame;
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.MinMaxSlider(ref _min, ref _max, 0, _lastEditorParameters.Length);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(_trackEvent, "Track Event Changed");
				if (!((int)_min < _lastEditorParameters.MinFrame && (int)_max != _trackEvent.EndFrame
				|| (int)_max > _lastEditorParameters.MaxFrame && (int)_max != _trackEvent.StartFrame))
				{
					_trackEvent.StartFrame = Mathf.Clamp((int)_min, _lastEditorParameters.MinFrame, _trackEvent.EndFrame - 1);
					_trackEvent.EndFrame = Mathf.Clamp((int)_max, _trackEvent.StartFrame + 1, _lastEditorParameters.MaxFrame);
					OnEventChanged(_trackEvent);
				}
			}


			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Title", GUILayout.Width(150f));
			EditorGUI.BeginChangeCheck();
			var title = EditorGUILayout.TextField(_trackEvent.EventTitle);
			if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(title))
			{
				Undo.RecordObject(_trackEvent, "Track Event Changed");
				_trackEvent.EventTitle = title;
				OnEventChanged(_trackEvent);
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Color", GUILayout.Width(150f));
			_trackEvent.EventInnerColor = EditorGUILayout.ColorField(_trackEvent.EventInnerColor);
			EditorGUILayout.EndHorizontal();

			DrawSpecificEvent();

			EditorGUILayout.EndVertical();
		}

		private void DrawSpecificEvent()
		{
			var animationEvent = _trackEvent as AnimationTrackEvent;
			if (animationEvent != null)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("AnimationClip", GUILayout.Width(150f));

				var animationTrack = _track as AnimationTrack;
				if (animationTrack != null && animationTrack.Controller == null)
				{
					EditorGUILayout.LabelField("Create Controller First");
					EditorGUILayout.EndHorizontal();
				}
				else
				{
					EditorGUI.BeginChangeCheck();
					animationEvent.Clip = (AnimationClip)EditorGUILayout.ObjectField(animationEvent.Clip, typeof(AnimationClip), true);
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(_trackEvent, "Track Event Changed");
						if (animationEvent.Clip != null)
							animationEvent.EventTitle = animationEvent.Clip.name;
						OnEventChanged(animationEvent);
					}
					EditorGUILayout.EndHorizontal();

					if (animationEvent.Clip == null)
					{
						if (GUILayout.Button("Create Clip"))
						{
							AnimationClip clip = new AnimationClip();
							clip.frameRate = _lastEditorParameters.FrameRate;
							var savePath = EditorUtility.SaveFilePanel("Create Clip", "Assets", string.Format("{0}_{1}", animationEvent.EventTitle, _sequenceName),
								"anim");
							if (!string.IsNullOrEmpty(savePath))
							{
								savePath = FileUtil.GetProjectRelativePath(savePath);
								AssetDatabase.CreateAsset(clip, savePath);
								AssetDatabase.SaveAssets();
								Undo.RecordObject(_trackEvent, "Track Event Changed");
								animationEvent.Clip = clip;
								animationEvent.EventTitle = animationEvent.Clip.name;
								OnEventChanged(animationEvent);
							}
						}
					}
				}
				EditorGUILayout.BeginHorizontal();

				EditorGUILayout.LabelField("Control Animation", GUILayout.Width(150f));
				EditorGUI.BeginChangeCheck();
				var control = EditorGUILayout.Toggle(animationEvent.ControlAnimation);
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(_trackEvent, "Track Event Changed");
					animationEvent.ControlAnimation = control;
					OnEventChanged(animationEvent);
				}

				EditorGUILayout.EndHorizontal();
			}
		}

		private void CreateController(AnimationTrack track)
		{
			var savePath = EditorUtility.SaveFilePanel("Create Animator Controller", "Assets", string.Format("{0}_{1}",
				_animatedGameObject.GameObject.name, _sequenceName), "controller");
			track.Controller =
				UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(savePath);
		}

		private void OnEventChanged(TrackEvent trackEvent)
		{
			if (EventChanged != null)
				EventChanged(trackEvent);
		}
	}
}
