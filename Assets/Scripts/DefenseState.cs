namespace RewindNotary
{
    public enum DefensePhase { Ready, Dash, ParryWindow, ParryRecovery }

    // Clock-injected state only: no Unity input/physics, health or progression side effects.
    public sealed class DefenseState
    {
        public DefensePhase Phase { get; private set; }
        public bool AirDashUsed { get; private set; }
        public int DashDirection { get; private set; } = 1;
        public float DashReadyAt { get; private set; } = float.NegativeInfinity;
        private float phaseUntil, dodgeUntil = float.NegativeInfinity;
        private float counterUntil = float.NegativeInfinity, guardUntil = float.NegativeInfinity;
        public bool BlocksActions => Phase != DefensePhase.Ready;
        public bool CounterReady(float now) => now < counterUntil;
        public bool Evading(float now) => Phase == DefensePhase.Dash && now < dodgeUntil;
        public bool SuccessGuard(float now) => now < guardUntil;

        public void Tick(float now)
        {
            if (Phase == DefensePhase.ParryWindow && now >= phaseUntil)
            {
                Phase = DefensePhase.ParryRecovery;
                phaseUntil += DefenseTuning.ParryRecoverySeconds;
            }
            if ((Phase == DefensePhase.Dash || Phase == DefensePhase.ParryRecovery) && now >= phaseUntil)
                Phase = DefensePhase.Ready;
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
        public bool TryStartParry(float now)
        {
            Tick(now);
            if (BlocksActions || SuccessGuard(now)) return false;
            Phase = DefensePhase.ParryWindow;
            phaseUntil = now + DefenseTuning.ParryWindowSeconds;
            return true;
        }
        public bool TryParry(float now)
        {
            Tick(now); // Exact expiry belongs to recovery even if the attacker's Update ran first.
            if (Phase != DefensePhase.ParryWindow) return false;
            Phase = DefensePhase.Ready;
            counterUntil = now + DefenseTuning.CounterSeconds;
            guardUntil = now + DefenseTuning.ParrySuccessGuardSeconds;
            return true;
        }
        public float ConsumeCounter(float now)
        {
            float multiplier = CounterReady(now) ? DefenseTuning.CounterDamageMultiplier : 1f;
            counterUntil = float.NegativeInfinity;
            return multiplier;
        }
        public void Land() { AirDashUsed = false; }
        public void ObserveAirborne()
        {
            // A ground dash that crosses a ledge uses this flight's one dash too.
            if (Phase == DefensePhase.Dash) AirDashUsed = true;
        }
        public void Interrupt()
        {
            Phase = DefensePhase.Ready;
            phaseUntil = dodgeUntil = counterUntil = guardUntil = float.NegativeInfinity;
            // Taking damage cannot refund the airborne charge or the dash cooldown.
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
