namespace Controller.AnimationControllers
{
    /// <summary>
    /// Coarse movement state the animator is currently driven with. The values are ordered by
    /// intensity (idle -> walk -> sprint), so a larger value always means "more movement".
    /// </summary>
    public enum PlayerAnimationState
    {
        /// <summary>Standing still.</summary>
        Idle = 0,

        /// <summary>Moving at walk speed (no sprint key or no stamina).</summary>
        Walk = 1,

        /// <summary>Moving at sprint speed (acceleration).</summary>
        Sprint = 2
    }
}
