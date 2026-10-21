namespace UI.Settings
{
    /// <summary>
    /// Every key the player is allowed to rebind in the settings menu.
    /// Add new values here and give them a default in <see cref="KeyBindingManager"/>.
    /// </summary>
    public enum GameAction
    {
        MoveForward,
        MoveBackward,
        MoveLeft,
        MoveRight,
        Sprint,
        Fire,
        Reload,
        Interact,
        Pause
    }
}
