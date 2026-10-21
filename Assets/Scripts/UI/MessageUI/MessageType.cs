namespace UI.MessageUI
{
    /// <summary>
    /// Tone of a UI message. The manager only uses it to pick the text colour, so new values
    /// can be added without touching the queue logic.
    /// </summary>
    public enum MessageType
    {
        /// <summary>Neutral information (default).</summary>
        Info = 0,

        /// <summary>Something good happened (pickup, level up).</summary>
        Success = 1,

        /// <summary>Something needs attention (low ammo, low health).</summary>
        Warning = 2,

        /// <summary>Action failed or cannot be performed right now.</summary>
        Error = 3
    }
}
