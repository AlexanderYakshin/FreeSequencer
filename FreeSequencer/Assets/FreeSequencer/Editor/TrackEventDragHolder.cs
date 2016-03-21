using FreeSequencer.Events;
using FreeSequencer.Tracks;

namespace FreeSequencer.Editor
{
	public class TrackEventDragHolder
	{
		public BaseTrack Track;
		public AnimatedGameObject AnimatedGameObject;
		public TrackEvent TrackEvent;
		public int Step;
		public int Min;
		public int Max;
	}
}
