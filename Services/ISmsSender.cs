namespace GBV_Shield.Services
{
    /// <summary>
    /// Platform SMS sender.
    /// Implementations should handle 'silent' flag appropriately:
    ///  - Android: silent => send in background (requires SEND_SMS)
    ///  - iOS: silent ignored (iOS will open composer)
    /// </summary>
    public interface ISmsSender
    {
        void Send(string phoneNumber, string message, bool silent);
    }
}
