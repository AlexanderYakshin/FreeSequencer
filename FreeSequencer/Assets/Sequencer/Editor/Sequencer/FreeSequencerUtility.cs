using System.IO;
using UnityEditor;
using UnityEngine;

namespace FreeSequencer.Editor
{
	public static class FreeSequencerUtility
	{
		private static string _path;
		private static string _editorPath;
		private static string _skinPath;
		private static GUISkin _skin;
		private static GUIStyle _evtStyle;

		public static string FindDirectory()
		{
			var directories = Directory.GetDirectories("Assets", "Sequencer", SearchOption.AllDirectories);
			return directories.Length > 0 ? directories[0] : string.Empty;
		}

		public static string GetPath()
		{
			if (_path == null)
			{
				_path = FindDirectory() + (object)'\\';
				_editorPath = _path + "Editor\\";
				_skinPath = _editorPath + "Skin\\";
			}
			return _path;
		}

		public static string GetEditorPath()
		{
			if (_editorPath == null)
				GetPath();
			return _editorPath;
		}

		public static string GetSkinPath()
		{
			//if (_skinPath == null)
				GetPath();
			return _skinPath;
		}

		public static GUISkin GetSkin()
		{
			if (_skin==null)
				_skin = (GUISkin)AssetDatabase.LoadAssetAtPath(GetSkinPath() + "FreeSequencer.guiskin", typeof(GUISkin));
			return _skin;
		}

		public static GUIStyle GetEventStyle()
		{
			return _evtStyle ?? (_evtStyle = GetSkin().GetStyle("Event"));
		}
	}
}
