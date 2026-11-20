namespace RewindNotary
{
    // Finite material segments. Every cut really removes one segment; follow-up combo hits
    // can hit ordinary enemies while guarded targets keep their interlock.
    public sealed class WaveTargetState
    {
        public int Remaining { get; private set; }
        public bool Cleared => Remaining == 0;
        public UseContext LastSword { get; private set; }
        public bool CrossLoopFinish { get; private set; }
        public bool EffectsClosed => (LastSword == null || LastSword.Closed) && (lastSetup == null || lastSetup.Closed);
        private readonly WaveArchetype archetype;
        private readonly string setupSkill;
        private UseContext lastSetup;
        private readonly int setupHits;
        private readonly bool skillOnly;
        private int hits;
        private bool prepared;
        private bool echoed;
        private int lastComboStep;
        private float openUntil = float.NegativeInfinity;
        public WaveTargetState(WaveArchetype kind, WaveSpawn spawn, int setupHits = 1, bool skillOnly = false)
        {
            archetype = kind; setupSkill = spawn.setupSkill; Remaining = spawn.segments;
            this.setupHits = System.Math.Max(1, setupHits); this.skillOnly = skillOnly;
            if (TakesComboHits && Remaining >= 3) Remaining = System.Math.Max(Remaining, 5);
        }
        public bool NeedsSetup => archetype.gate == "skill" || !string.IsNullOrEmpty(setupSkill);
        internal bool TakesComboHits => !archetype.mechanism && !NeedsSetup && archetype.gate != "defense";
        public bool Ready(float now) => NeedsSetup ? prepared
            : archetype.gate != "defense" || now < openUntil;
        public bool Setup(UseContext use)
        {
            if (Cleared || !NeedsSetup || prepared || use == null || use.Closed
                || use.SkillId != setupSkill || use == lastSetup || (LastSword != null && !LastSword.Closed)) return false;
            lastSetup = use;
            if (skillOnly) Remaining--;
            else if (++hits == setupHits) { prepared = true; hits = 0; }
            return true;
        }
        public bool RearmSwitch()
        {
            if (!skillOnly || !Cleared || lastSetup == null || !lastSetup.Closed) return false;
            Remaining = 1;
            return true;
        }

        // A support strike removes material without a sword use or an ordinary-combat receipt.
        public void SupportHit(int damage) { Remaining = System.Math.Max(0, Remaining - damage); }

        public bool OpenDefense(float now)
        {
            if (Cleared || archetype.gate != "defense" || now < openUntil) return false;
            openUntil = now + archetype.openingSeconds;
            return true;
        }
        public bool Cut(UseContext use, float now, bool echo = false, int comboStep = 0)
        {
            if (Cleared || use == null || use.Closed || use.SkillId != SkillRules.BasicSword || !Ready(now)) return false;
            if (LastSword == use)
            {
                if (!TakesComboHits || (echo ? echoed : comboStep <= lastComboStep)) return false;
                if (echo) echoed = true;
            }
            else
            {
                if (LastSword != null && !LastSword.Closed) return false;
                echoed = false;
            }
            lastComboStep = comboStep;
            Remaining--;
            LastSword = use;
            CrossLoopFinish = NeedsSetup && prepared;
            prepared = false;
            openUntil = float.NegativeInfinity;
            return true;
        }
    }
}
