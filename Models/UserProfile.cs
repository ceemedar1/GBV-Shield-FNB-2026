namespace GBV_Shield.Models
{
    /// <summary>
    /// Represents a survivor/user profile stored in Firebase.
    /// </summary>
    public class UserProfile
    {
        /// <summary>
        /// Firebase UID (unique per user).
        /// </summary>
        public string Uid { get; set; } = string.Empty;

        /// <summary>
        /// Email address of the user.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Full name of the user.
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Contact phone number.
        /// </summary>
        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// Unix timestamp (ms) when profile was created.
        /// </summary>
        public long CreatedAt { get; set; }

        /// <summary>
        /// Unix timestamp (ms) when profile was last updated.
        /// </summary>
        public long UpdatedAt { get; set; }
    }
}
