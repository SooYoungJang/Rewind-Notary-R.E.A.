namespace RewindNotary
{
    public enum DefensePhase { Ready, Dash }

    public sealed class DefenseState
    {
        public DefensePhase Phase { get; private set; }
        public bool AirDashUsed { get; private set; }
        public int DashDirection { get; private set; } = 1;
        public float DashReadyAt { get; private set; } = float.NegativeInfinity;
        private float phaseUntil, dodgeUntil = float.NegativeInfinity;
        public bool BlocksActions => Phase == DefensePhase.Dash;
        public bool Evading(float now) => Phase == DefensePhase.Dash && now < dodgeUntil;

        public void Tick(float now)
        {
            if (Phase == DefensePhase.Dash && now >= phaseUntil) Phase = DefensePhase.Ready;
        }

        public bool TryDash(float now, bool grounded, int direction)
        {
            Tick(now);
            if (BlocksActions || now < DashReadyAt || (!grounded && AirDashUsed)) return false;
            Phase = DefensePhase.Dash;
            DashDirection = direction < 0 ? -1 : 1;
            AirDashUsed |= !grounded;
            phaseUntil = now + DefenseTuning.DashSeconds;
            dodgeUntil = now + DefenseTuning.DashInvulnerableSeconds;
            DashReadyAt = now + DefenseTuning.DashCooldown;
            return true;
        }

        public void Land() { AirDashUsed = false; }

        public void ObserveAirborne()
        {
            if (Phase == DefensePhase.Dash) AirDashUsed = true;
        }

        public void Interrupt()
        {
            Phase = DefensePhase.Ready;
            phaseUntil = dodgeUntil = float.NegativeInfinity;
        }

        public void Reset()
        {
            Interrupt();
            AirDashUsed = false;
            DashReadyAt = float.NegativeInfinity;
            DashDirection = 1;
        }
    }
}
