using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FreeSequencer.Editor
{
	public class AnimationWindowHelper
	{
		private static Type ANIMATION_WINDOW_TYPE = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationWindow");
		private static Type ANIMATION_WINDOW_STATE_TYPE = typeof(EditorWindow).Assembly.GetType("UnityEditorInternal.AnimationWindowState");
		private static Type ANIMATION_SELECTION_TYPE = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationSelection");
		private static Type ANIMATION_EDITOR_TYPE = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimEditor");
		private static EditorWindow _animationWindow = (EditorWindow)null;
		private static FieldInfo _animEditorField = (FieldInfo)null;
		private static ScriptableObject _animEditor = (ScriptableObject)null;
		private static FieldInfo _stateField = (FieldInfo)null;
		private static PropertyInfo _activeAnimationClipProperty = (PropertyInfo)null;
		private static FieldInfo _currentTimeField = (FieldInfo)null;
		private static PropertyInfo _frameProperty = (PropertyInfo)null;
		private static PropertyInfo _recordingProperty = (PropertyInfo)null;
		private static MethodInfo _chooseClipMethod = (MethodInfo)null;
		public static EditorWindow GetWindowIfExists(Type windowType)
		{
			foreach (var @object in Resources.FindObjectsOfTypeAll(windowType))
			{
				if (@object.GetType() == windowType)
					return (EditorWindow)@object;
			}
			return null;
		}

		public static EditorWindow AnimationWindow
		{
			get
			{
				//if (_animationWindow == null)
					_animationWindow = GetWindowIfExists(ANIMATION_WINDOW_TYPE);
				return _animationWindow;
			}
		}

		private static FieldInfo AnimEditorField
		{
			get
			{
				if (_animEditorField == null)
					_animEditorField = ANIMATION_WINDOW_TYPE.GetField("m_AnimEditor", BindingFlags.Instance | BindingFlags.NonPublic);
				return _animEditorField;
			}
		}

		private static ScriptableObject AnimEditor
		{
			get
			{
				if (_animEditor == null)
					_animEditor = (ScriptableObject)AnimEditorField.GetValue(AnimationWindow);
				return _animEditor;
			}
		}

		private static FieldInfo StateField
		{
			get
			{
				if (_stateField == null)
					_stateField = ANIMATION_EDITOR_TYPE.GetField("m_State", BindingFlags.Instance | BindingFlags.NonPublic);
				return _stateField;
			}
		}

		private static PropertyInfo ActiveAnimationClipProperty
		{
			get
			{
				if (_activeAnimationClipProperty == null)
					_activeAnimationClipProperty = ANIMATION_WINDOW_STATE_TYPE.GetProperty("activeAnimationClip", BindingFlags.Instance | BindingFlags.Public);
				return _activeAnimationClipProperty;
			}
		}

		private static FieldInfo CurrentTimeField
		{
			get
			{
				//if (_currentTimeField == null)
					_currentTimeField = ANIMATION_WINDOW_STATE_TYPE.GetField("m_CurrentTime", BindingFlags.Instance | BindingFlags.NonPublic);
				return _currentTimeField;
			}
		}

		private static PropertyInfo FrameProperty
		{
			get
			{
				if (_frameProperty == null)
					_frameProperty = ANIMATION_WINDOW_STATE_TYPE.GetProperty("frame", BindingFlags.Instance | BindingFlags.Public);
				return _frameProperty;
			}
		}

		private static PropertyInfo RecordingProperty
		{
			get
			{
				if (_recordingProperty == null)
					_recordingProperty = ANIMATION_WINDOW_STATE_TYPE.GetProperty("recording", BindingFlags.Instance | BindingFlags.Public);
				return _recordingProperty;
			}
		}

		private static MethodInfo ChooseClipMethod
		{
			get
			{
				if (_chooseClipMethod == null)
					_chooseClipMethod = ANIMATION_SELECTION_TYPE.GetMethod("ChooseClip", BindingFlags.Instance | BindingFlags.NonPublic, (Binder)null, new Type[1]
					{
			typeof (AnimationClip)
					}, null);
				return _chooseClipMethod;
			}
		}

		public static EditorWindow OpenAnimationWindow()
		{
			if (_animationWindow == null)
				_animationWindow = EditorWindow.GetWindow(ANIMATION_WINDOW_TYPE);
			return _animationWindow;
		}

		public static void StartAnimationMode()
		{
			RecordingProperty.SetValue(GetState(), true, null);
		}

		public static void StopAnimationMode()
		{
			RecordingProperty.SetValue(GetState(), false, null);
		}

		private static object GetState()
		{
			return StateField.GetValue(AnimEditor);
		}

		public static void SetCurrentFrame(int frame, float time)
		{
			if (AnimationWindow == null)
				return;
			CurrentTimeField.SetValue(GetState(), time);
			AnimationWindow.Repaint();
		}

		public static int GetCurrentFrame()
		{
			if (AnimationWindow == null)
				return -1;
			return (int)FrameProperty.GetValue(GetState(), null);
		}

		public static void SelectAnimationClip(AnimationClip clip)
		{
			if (AnimationWindow == null || clip == null)
				return;
			AnimationClip[] animationClips = AnimationUtility.GetAnimationClips(Selection.activeGameObject);
			int index = 0;
			while (index < animationClips.Length)
			{
				if (animationClips[index] == clip)
					break;

				index++;
			}
			if (index == animationClips.Length)
				Debug.LogError("Couldn't find clip " + clip.name);
			else
				ActiveAnimationClipProperty.SetValue(GetState(), clip, null);
			AnimationWindow.Repaint();
		}
	}
}
