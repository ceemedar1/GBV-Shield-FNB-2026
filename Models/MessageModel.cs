using System;

namespace GBV_Shield.Models
{
    public class MessageModel
    {
        public string SenderId { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool IsSelf { get; set; }
    }
}
