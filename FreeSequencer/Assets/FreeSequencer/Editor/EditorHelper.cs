using UnityEngine;

namespace FreeSequencer.Editor
{
	public static class EditorHelper
	{
		public static bool GetMouseDownRect(Rect rect, int button = 0)
		{
			Vector2 mousePosition = Event.current.mousePosition;
			if (rect.Contains(mousePosition) && Event.current.button == button)
			{
				if (Event.current.type == EventType.mouseDown)
					return true;
			}

			return false;
		}

		public static bool GetMouseDrag()
		{
			return Event.current.type == EventType.mouseDrag;
		}

		public static bool GetMouseUp(int button = 0)
		{
			return Event.current.rawType == EventType.mouseUp;
		}

		public static bool GetMouseDown(int button = 0)
		{
			return Event.current.type == EventType.mouseDown;
		}

		public static bool GetMouseUpRect(Rect rect, int button = 0)
		{
			Vector2 mousePosition = Event.current.mousePosition;
			if (rect.Contains(mousePosition) && Event.current.button == button)
			{
				if (Event.current.type == EventType.mouseDown)
					return true;
			}

			return false;
		}

		public static bool GetMouseOverRect(Rect rect)
		{
			Vector2 mousePosition = Event.current.mousePosition;
			if (rect.Contains(mousePosition) && Event.current.button == 0 && Event.current.type == EventType.MouseDrag)
			{
				return true;
			}

			return false;
		}
	}
}
