using System;

using System.Text.Json.Serialization;

namespace GBV_Shield.Services
{
    public sealed class Story
    {
        [JsonIgnore]
        public string Id { get; set; } = string.Empty;
        public string Author { get; set; } = "Anonymous";
        public string Content { get; set; } = string.Empty;
        public long CreatedAtMs { get; set; }
        [JsonIgnore]
        public bool IsOwner { get; set; }
        [JsonIgnore]
        public DateTime CreatedAt => DateTimeOffset.FromUnixTimeMilliseconds(CreatedAtMs).LocalDateTime;
    }
}
