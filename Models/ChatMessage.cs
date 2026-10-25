using System;

namespace GBV_Shield.Models
{
    public class ChatMessage
    {
        public string SenderId { get; set; } = string.Empty;  // ✅ Default prevents CS8618
        public string Text { get; set; } = string.Empty;      // ✅ Default prevents CS8618
        public long TimestampMs { get; set; }
        public bool IsSelf { get; set; }

        // ✅ For UI binding
        public DateTime Timestamp =>
            DateTimeOffset.FromUnixTimeMilliseconds(TimestampMs).LocalDateTime;
    }
}
