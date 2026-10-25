using SQLite;

namespace GBV_Shield.Models
{
    public class Report
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [NotNull]
        public string Text { get; set; } = string.Empty;

        [NotNull]
        public string Type { get; set; } = "general";

        // ✅ Always store raw numeric coordinates
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public long CreatedAtMs { get; set; }

        // Tracks whether the API accepted this locally queued report.
        public bool Synced { get; set; } = false;
    }
}
