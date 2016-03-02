using UnityEditor;
using UnityEngine;

namespace FreeSequencer.Editor
{
	[CustomEditor(typeof(Sequence))]
	public class SequenceEditor: UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			var sequence = (Sequence)target;

			DrawDefaultInspector();
			if (GUILayout.Button("Start sequence") && sequence.StartMode != StartMode.OnStart)
			{
				sequence.StartSequence();
			}
		}
	}
}
