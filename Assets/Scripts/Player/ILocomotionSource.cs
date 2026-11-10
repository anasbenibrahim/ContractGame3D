namespace Contract.Player
{
    /// <summary>
    /// Movement state an animated body reads from - implemented by the player controller and by
    /// NPC navigation, so the same rig/animator drives both.
    /// </summary>
    public interface ILocomotionSource
    {
        float PlanarSpeed { get; }
        bool Sprinting { get; }
        float CrouchAmount { get; }
        bool Grounded { get; }
    }
}
