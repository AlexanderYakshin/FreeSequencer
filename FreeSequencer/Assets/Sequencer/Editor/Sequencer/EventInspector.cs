using System.Collections.Generic;
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
		private TrackEvent _currentTrackEvent;
		private BaseTrack _currentTrack;
		private SequencerEditorWindow _editorWindow;
		private int _minFrame;
		private int _maxFrame;
		private int _layerIndex;
		private int _objectIndex;

		public void Init(string seqName, int frameRate, int length, int minFrame, int maxFrame, GameObject gameObject = null, BaseTrack track = null, TrackEvent seqEvent = null)
		{
			_seqName = seqName;
			_frameRate = frameRate;
			_length = length;
			_selectedGameObject = gameObject;
			_currentTrackEvent = seqEvent;
			_currentTrack = track;
			_minFrame = minFrame;
			_maxFrame = maxFrame;
			if (_editorWindow == null)
				_editorWindow = EditorWindow.GetWindow<SequencerEditorWindow>();
		}

		void OnGUI()
		{
			this.minSize = new Vector2(370f, 350f);
			if (_selectedGameObject == null || _currentTrack == null || _currentTrackEvent == null)
				return;

			var animationEvent = _currentTrackEvent as AnimationTrackEvent;
			if (animationEvent != null)
			{
				DrawAnimationEventInspector(animationEvent);
				return;
			}
		}

		private void DrawAnimationEventInspector(AnimationTrackEvent trackEvent)
		{
			if (trackEvent == null)
				return;

			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField("Event:", GUILayout.Width(150f));
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Range", GUILayout.Width(150f));
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.LabelField("S:", GUILayout.Width(15f));
			var start = EditorGUILayout.IntField(trackEvent.StartFrame);
			trackEvent.StartFrame = Mathf.Clamp(start, _minFrame, trackEvent.EndFrame - 1);
			EditorGUILayout.LabelField("E:", GUILayout.Width(15f));
			var end = EditorGUILayout.IntField(trackEvent.EndFrame);
			trackEvent.EndFrame = Mathf.Clamp(end, trackEvent.StartFrame + 1, _length);

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(string.Empty, GUILayout.Width(150f));
			float _min = trackEvent.StartFrame;
			float _max = trackEvent.EndFrame;
			EditorGUILayout.MinMaxSlider(ref _min, ref _max, 0, _length);
			if (!((int)_min < _minFrame && (int)_max != trackEvent.EndFrame
				|| (int)_max > _maxFrame && (int)_max != trackEvent.StartFrame))
			{
				trackEvent.StartFrame = Mathf.Clamp((int)_min, _minFrame, trackEvent.EndFrame - 1);
				trackEvent.EndFrame = Mathf.Clamp((int)_max, trackEvent.StartFrame + 1, _maxFrame);
			}

			EditorGUILayout.EndHorizontal();
			if (EditorGUI.EndChangeCheck())
			{
				_editorWindow.Repaint();
			}
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Title", GUILayout.Width(150f));
			EditorGUI.BeginChangeCheck();
			var title = EditorGUILayout.TextField(trackEvent.EventTitle);
			if (EditorGUI.EndChangeCheck() && !string.IsNullOrEmpty(title))
			{
				trackEvent.EventTitle = title;
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Color", GUILayout.Width(150f));
			trackEvent.EventInnerColor = EditorGUILayout.ColorField(trackEvent.EventInnerColor);
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
				trackEvent.Clip = (AnimationClip)EditorGUILayout.ObjectField(trackEvent.Clip, typeof(AnimationClip), true);
				EditorGUILayout.EndHorizontal();

				if (trackEvent.Clip == null)
				{
					if (GUILayout.Button("Create Clip"))
					{
						AnimationClip clip = new AnimationClip();
						clip.frameRate = _frameRate;
						var savePath = EditorUtility.SaveFilePanel("Create Clip", "Assets", string.Format("{0}_{1}", _selectedGameObject.name, _seqName),
							"anim");
						if (!string.IsNullOrEmpty(savePath))
						{
							savePath = FileUtil.GetProjectRelativePath(savePath);
							AssetDatabase.CreateAsset(clip, savePath);
							AssetDatabase.SaveAssets();

							trackEvent.Clip = clip;
							/*var controller = animationTrack.Controller as UnityEditor.Animations.AnimatorController;
							var layer =
								controller.layers.FirstOrDefault(
									l => l.name.Equals(animationTrack.ControllerLayer, StringComparison.InvariantCultureIgnoreCase));
							var rootStateMachine = layer.stateMachine;
							var state = rootStateMachine.AddState(clip.name);
							state.motion = clip;*/
						}
					}
				}
				else
				{
					trackEvent.Clip.frameRate = _frameRate;
					//AnimationMode.StartAnimationMode();
				}
			}
			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Control Animation", GUILayout.Width(150f));
			trackEvent.ControlAnimation = EditorGUILayout.Toggle(trackEvent.ControlAnimation);

			EditorGUILayout.EndHorizontal();
			DrawTrackSection();

			DrawTrackToolSection();
		}

		private void DrawTrackToolSection()
		{
			EditorGUILayout.Separator();

			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField("Track Tools:", GUILayout.Width(150f));

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Show Transform Path", GUILayout.Width(150f));
			_currentTrack.ShowTransformPath = EditorGUILayout.Toggle(_currentTrack.ShowTransformPath);

			EditorGUILayout.EndHorizontal();
			var names = GetObjectPaths(_selectedGameObject);
			if (names.Count > 1)
			{
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField("Path To Object", GUILayout.Width(150f));
				if (_objectIndex > names.Count - 1)
					_objectIndex = 0;
				_objectIndex = EditorGUILayout.Popup(_objectIndex, names.ToArray());

				_currentTrack.ObjectsPathToShowTransform = names[_objectIndex] == " " ? "" : names[_objectIndex]; ;

				EditorGUILayout.EndHorizontal();
			}
			else
			{
				_currentTrack.ObjectsPathToShowTransform = string.Empty;
			}
			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Show Rotation Normales", GUILayout.Width(150f));
			_currentTrack.ShowRotationNormales = EditorGUILayout.Toggle(_currentTrack.ShowRotationNormales);

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.LabelField("Show Key Frames", GUILayout.Width(150f));
			_currentTrack.ShowKeyFrames = EditorGUILayout.Toggle(_currentTrack.ShowKeyFrames);

			EditorGUILayout.EndHorizontal();

			if (_currentTrack.ShowTransformPath)
			{
				EditorGUILayout.BeginHorizontal();

				EditorGUILayout.LabelField("Path Color", GUILayout.Width(150f));
				_currentTrack.PathColor = EditorGUILayout.ColorField(_currentTrack.PathColor);

				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.EndVertical();
		}

		private List<string> GetObjectPaths(GameObject selectedGameObject, string prevPath = null)
		{
			var result = new List<string>();

			var path = prevPath + "\\" + selectedGameObject.name;
			if (prevPath == null)
			{
				result.Add(" ");
				path = "";
			}
			else if (prevPath == string.Empty)
			{
				path = selectedGameObject.name;
				result.Add(path);
			}
			else
			{
				path = prevPath + "\\" + selectedGameObject.name;
				result.Add(path);
			}

			for (int i = 0; i < selectedGameObject.transform.childCount; i++)
			{
				var child = selectedGameObject.transform.GetChild(i);
				result.AddRange(GetObjectPaths(child.gameObject, path));
			}

			return result;
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
				if (track.Controller != null)
				{
					EditorGUILayout.BeginHorizontal();

					EditorGUILayout.LabelField("Animator Layer", GUILayout.Width(150f));
					track.ControllerLayer = EditorGUILayout.TextField(track.ControllerLayer);
					var controllerEditor = track.Controller as UnityEditor.Animations.AnimatorController;
					var layers = controllerEditor.layers.Select(l => l.name).ToArray();
					if (_layerIndex > layers.Length - 1)
						_layerIndex = 0;
					_layerIndex = EditorGUILayout.Popup(_layerIndex, layers, GUILayout.Width(200));
					track.ControllerLayer = layers[_layerIndex];
					EditorGUILayout.EndHorizontal();
				}

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
