using System;
using System.Linq;
using FreeSequencer.Events;
using FreeSequencer.Tracks;
using UnityEditor;
using UnityEngine;

namespace FreeSequencer.Editor
{
	public class EventInspector : EditorWindow
	{
		private string _seqName;
		private int _frameRate;
		private int _length;
		private string _startFrameString;
		private string _endFrameString;

		private float _min;
		private float _max;

		private GameObject _selectedGameObject;
		private BaseEvent _currentSeqEvent;
		private BaseTrack _currentTrack;
		private SequencerEditorWindow _editorWindow;
		private int _minFrame;
		private int _maxFrame;

		public void Init(string seqName, int frameRate, int length, int minFrame, int maxFrame, GameObject gameObject = null, BaseTrack track = null, BaseEvent seqEvent = null)
		{
			_seqName = seqName;
			_frameRate = frameRate;
			_length = length;
			_selectedGameObject = gameObject;
			_currentSeqEvent = seqEvent;
			_currentTrack = track;
			_minFrame = minFrame;
			_maxFrame = maxFrame;
			if (_editorWindow == null)
				_editorWindow = EditorWindow.GetWindow<SequencerEditorWindow>();
		}

		void OnGUI()
		{
			this.minSize = new Vector2(370f, 350f);
			if (_selectedGameObject == null || _currentTrack == null || _currentSeqEvent == null)
				return;

			var animationEvent = _currentSeqEvent as AnimationSeqEvent;
			if (animationEvent != null)
			{
				DrawAnimationEventInspector(animationEvent);
				return;
			}
		}

		private void DrawAnimationEventInspector(AnimationSeqEvent seqEvent)
		{
			if (seqEvent == null)
				return;

			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField("Event:", GUILayout.Width(150f));
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Range", GUILayout.Width(150f));
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.LabelField("S:", GUILayout.Width(15f));
			var start = EditorGUILayout.IntField(seqEvent.StartFrame);
			seqEvent.StartFrame = Mathf.Clamp(start, _minFrame, seqEvent.EndFrame - 1);
			EditorGUILayout.LabelField("E:", GUILayout.Width(15f));
			var end = EditorGUILayout.IntField(seqEvent.EndFrame);
			seqEvent.EndFrame = Mathf.Clamp(end, seqEvent.StartFrame + 1, _length);

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(string.Empty, GUILayout.Width(150f));
			float _min = seqEvent.StartFrame;
			float _max = seqEvent.EndFrame;
			EditorGUILayout.MinMaxSlider(ref _min, ref _max, 0, _length);
			if (!((int)_min < _minFrame && (int)_max != seqEvent.EndFrame 
				|| (int)_max > _maxFrame && (int)_max != seqEvent.StartFrame))
			{
				seqEvent.StartFrame = Mathf.Clamp((int)_min, _minFrame, seqEvent.EndFrame - 1);
				seqEvent.EndFrame = Mathf.Clamp((int)_max, seqEvent.StartFrame + 1, _maxFrame);
			}
				
			EditorGUILayout.EndHorizontal();
			if (EditorGUI.EndChangeCheck())
			{
				_editorWindow.Repaint();
			}
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Title", GUILayout.Width(150f));
			seqEvent.EventTitle = EditorGUILayout.TextField(seqEvent.EventTitle);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Color", GUILayout.Width(150f));
			seqEvent.EventInnerColor = EditorGUILayout.ColorField(seqEvent.EventInnerColor);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("AnimationClip", GUILayout.Width(150f));

			var animationTrack = _currentTrack as AnimationTrack;
			if (animationTrack != null && animationTrack.Controller == null)
			{
				EditorGUILayout.LabelField("Create Controller First");
				EditorGUILayout.EndHorizontal();
			}
			else
			{
				seqEvent.Clip = (AnimationClip)EditorGUILayout.ObjectField(seqEvent.Clip, typeof(AnimationClip), true);
				EditorGUILayout.EndHorizontal();

				if (seqEvent.Clip == null)
				{
					if (GUILayout.Button("Create Clip"))
					{
						AnimationClip clip = new AnimationClip();
						clip.frameRate = _frameRate;
						var savePath = EditorUtility.SaveFilePanel("Create Clip", "Assets", string.Format("{0}_{1}", _selectedGameObject.name, _seqName),
							"clip");

						AssetDatabase.CreateAsset(clip, savePath);
						AssetDatabase.SaveAssets();

						seqEvent.Clip = clip;
						var controller = animationTrack.Controller as UnityEditor.Animations.AnimatorController;
						var layer =
							controller.layers.FirstOrDefault(
								l => l.name.Equals(animationTrack.ControllerLayer, StringComparison.InvariantCultureIgnoreCase));
						var rootStateMachine = layer.stateMachine;

						var state = rootStateMachine.AddState(clip.name);
						state.motion = clip;
					}
				}
				else
				{
					seqEvent.Clip.frameRate = _frameRate;
				}
			}

			DrawTrackSection();

			DrawTrackToolSection();
		}

		private void DrawTrackToolSection()
		{
			EditorGUILayout.Separator();

			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField("Track Tools:", GUILayout.Width(150f));

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Sync Animation Window", GUILayout.Width(150f));
			_currentTrack.SyncAnimationWindow = EditorGUILayout.Toggle(_currentTrack.SyncAnimationWindow);

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Show Transform Path", GUILayout.Width(150f));
			_currentTrack.ShowTransformPath = EditorGUILayout.Toggle(_currentTrack.ShowTransformPath);

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Show Key Frames", GUILayout.Width(150f));
			_currentTrack.ShowKeyFrames = EditorGUILayout.Toggle(_currentTrack.ShowKeyFrames);

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical();
		}

		private void DrawTrackSection()
		{
			EditorGUILayout.Separator();

			EditorGUILayout.LabelField("Track:", GUILayout.Width(150f));

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Enabled", GUILayout.Width(150f));
			_currentTrack.Enabled = EditorGUILayout.Toggle(_currentTrack.Enabled);

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Name", GUILayout.Width(150f));
			_currentTrack.TrackName = EditorGUILayout.TextField(_currentTrack.TrackName);

			EditorGUILayout.EndHorizontal();

			var animationTrack = _currentTrack as AnimationTrack;

			if (animationTrack != null)
				DrawAnimationTrack(animationTrack);

			EditorGUILayout.EndVertical();
		}

		private void DrawAnimationTrack(AnimationTrack track)
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Animator Ctrl", GUILayout.Width(150f));
			var animator = _selectedGameObject.GetComponent<Animator>();
			if (animator == null)
			{
				if (GUILayout.Button("Create Animator"))
					animator = _selectedGameObject.AddComponent<Animator>();
				EditorGUILayout.EndHorizontal();
			}
			else
			{
				track.Controller = (RuntimeAnimatorController)EditorGUILayout.ObjectField(track.Controller, typeof(RuntimeAnimatorController), true);
				EditorGUILayout.EndHorizontal();
				if (animator.runtimeAnimatorController != null)
				{
					track.Controller = animator.runtimeAnimatorController;
				}
				if (track.Controller == null && animator.runtimeAnimatorController == null)
				{
					if (GUILayout.Button("Create Ctrl"))
					{
						CreateController(track);
						animator.runtimeAnimatorController = track.Controller;
					}
				}

				EditorGUILayout.BeginHorizontal();

				EditorGUILayout.LabelField("Animator Layer", GUILayout.Width(150f));
				track.ControllerLayer = EditorGUILayout.TextField(track.ControllerLayer);

				EditorGUILayout.EndHorizontal();
			}
		}

		private void CreateController(AnimationTrack track)
		{
			var savePath = EditorUtility.SaveFilePanel("Create Animator Controller", "Assets", string.Format("{0}_{1}", _selectedGameObject.name, _seqName),
							   "controller");
			track.Controller =
				UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(savePath);
		}
	}
}
