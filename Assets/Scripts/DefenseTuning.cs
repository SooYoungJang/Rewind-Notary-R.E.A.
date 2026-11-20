namespace RewindNotary
{
    // Distances are world units; gameplay timers use scaled seconds, hit-stop uses real seconds.
    // Basic actions deliberately live outside the skill catalog and its mastery boundaries.
    public static class DefenseTuning
    {
        public const float DashDistance = 3.2f;
        public const float DashSeconds = 0.16f;
        public const float DashSpeed = DashDistance / DashSeconds;
        public const float DashInvulnerableSeconds = 0.10f;
        public const float DashCooldown = 0.60f;
        public const float TrailInterval = 0.04f;
        public const float TrailSeconds = 0.12f;
        public const float TrailLength = 0.65f;
        public const float TrailHeight = 0.9f;
    }
}
