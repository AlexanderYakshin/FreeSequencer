using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FreeSequencer.Editor
{
	public class SequenceEditorArea
	{
		public event Action<Sequence> OnSequanceChanged;
		public event Action<int> OnSequenceParametersChanged;

		public void OnDraw(Sequence selectedSequance = null)
		{
			EditorGUILayout.BeginHorizontal(GUILayout.Height(20f));
			DrawSequanceControl(selectedSequance);
			GUILayout.FlexibleSpace();
			if (selectedSequance != null)
			{
				var prevLength = selectedSequance.Length;
				EditorGUILayout.LabelField("Update Mode", GUILayout.Width(85), GUILayout.Height(20f));
				EditorGUI.BeginChangeCheck();
				var updateMode = (UpdateType)EditorGUILayout.EnumPopup(selectedSequance.UpdateTypeMode, GUILayout.Width(64), GUILayout.Height(20f));
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(selectedSequance, "Change Update Mode");
					selectedSequance.UpdateTypeMode = updateMode;
					ParametersChanged(prevLength);
				}
				EditorGUILayout.LabelField("Frame Rate", GUILayout.Width(70), GUILayout.Height(20f));
				EditorGUI.BeginChangeCheck();
				var frameRate = EditorGUILayout.IntPopup(selectedSequance.FrameRate, new string[] { "10", "20", "30", "40", "50", "60" },
					new int[] { 10, 20, 30, 40, 50, 60 }, GUILayout.Width(40), GUILayout.Height(20f));
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(selectedSequance, "Change Frame Rate");
					selectedSequance.FrameRate = frameRate;
					ParametersChanged(prevLength);
				}
				EditorGUILayout.LabelField("Length", GUILayout.Width(64), GUILayout.Height(20f));
				EditorGUI.BeginChangeCheck();

				var length = EditorGUILayout.IntField(selectedSequance.Length, GUILayout.Width(64), GUILayout.Height(18f));
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(selectedSequance, "Change Length");
					selectedSequance.Length = Mathf.Clamp(length, 10, 9999);
					ParametersChanged(prevLength);
				}
			}

			GUILayout.Space(3f);
			EditorGUILayout.EndHorizontal();
		}

		private void DrawSequanceControl(Sequence currentSelectedSequence)
		{
			EditorGUILayout.LabelField("Sequence", GUILayout.Width(64), GUILayout.Height(20f));
			var sequences = GetExistingSequances();
			if (GUILayout.Button(currentSelectedSequence == null ? string.Empty : currentSelectedSequence.name, EditorStyles.popup, GUILayout.MinWidth(100f), GUILayout.MaxWidth(250f)))
			{
				var mousePos = Event.current.mousePosition;
				GenericMenu toolsMenu = new GenericMenu();
				foreach (var option in sequences)
				{
					toolsMenu.AddItem(new GUIContent(option.Key), false, OnChooseSequence, option.Value);
				}
				toolsMenu.AddSeparator(string.Empty);
				toolsMenu.AddItem(new GUIContent("Create New Sequence"), false, OnCreateNewSequence);
				toolsMenu.DropDown(new Rect(mousePos.x, mousePos.y, 0, 0));
				GUIUtility.ExitGUI();
			}
		}

		private void ParametersChanged(int prevLength)
		{
			if (OnSequenceParametersChanged != null)
				OnSequenceParametersChanged(prevLength);
		}

		private void OnChooseSequence(object objSequence)
		{
			var sequence = objSequence as Sequence;
			if (OnSequanceChanged != null)
				OnSequanceChanged(sequence);
		}

		private void OnCreateNewSequence()
		{
			var newSequence = CreateNewSequance();
			if (OnSequanceChanged != null)
				OnSequanceChanged(newSequence);
		}

		private Sequence CreateNewSequance()
		{
			var newSequanceGameObject = new GameObject("Sequence");
			var sequance = newSequanceGameObject.AddComponent<Sequence>();
			sequance.Length = 600;
			sequance.FrameRate = 60;
			return sequance;
		}

		private Dictionary<string, Sequence> GetExistingSequances()
		{
			var results = new Dictionary<string, Sequence>();
			var sequences = Object.FindObjectsOfType<Sequence>();
			foreach (Sequence sequance in sequences)
			{
				if (!results.ContainsKey(sequance.name))
				{
					results.Add(sequance.name, sequance);
				}
				else
				{
					results[sequance.name] = sequance;
				}
			}

			return results;
		}
	}
}
