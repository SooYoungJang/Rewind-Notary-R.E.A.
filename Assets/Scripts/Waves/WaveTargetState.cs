namespace RewindNotary
{
    // Finite material segments. Every cut really removes one segment; follow-up combo hits
    // meet the interlock until that whole originating sequence has closed.
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
        private float openUntil = float.NegativeInfinity;
        public WaveTargetState(WaveArchetype kind, WaveSpawn spawn, int setupHits = 1, bool skillOnly = false)
        {
            archetype = kind; setupSkill = spawn.setupSkill; Remaining = spawn.segments;
            this.setupHits = System.Math.Max(1, setupHits); this.skillOnly = skillOnly;
        }
        public bool NeedsSetup => archetype.gate == "skill" || !string.IsNullOrEmpty(setupSkill);
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
        public bool Cut(UseContext use, float now, bool echo = false)
        {
            if (Cleared || use == null || use.Closed || use.SkillId != SkillRules.BasicSword || !Ready(now)) return false;
            if (LastSword == use)
            {
                // The delayed wave follows its first hit only on ordinary moving enemies.
                if (!echo || echoed || archetype.mechanism || NeedsSetup || archetype.gate == "defense") return false;
                echoed = true;
            }
            else
            {
                if (LastSword != null && !LastSword.Closed) return false;
                echoed = false;
            }
            Remaining--;
            LastSword = use;
            CrossLoopFinish = NeedsSetup && prepared;
            prepared = false;
            openUntil = float.NegativeInfinity;
            return true;
        }
    }
}
