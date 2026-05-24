namespace Stint.Models
{
    public class TimeEvent
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Category { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? StoppedAt { get; set; }
        public bool AutoStopped { get; set; }

        public bool IsRunning => StoppedAt == null;
        public TimeSpan Elapsed => (StoppedAt ?? DateTime.Now) - StartedAt;
    }
}
