using UnityEngine;

namespace RewindNotary
{
    // Temporary encounter -> reward mapping. The skill's owner remains Mikhail, never Janhyang.
    public sealed class EncounterRewardBridge : MonoBehaviour
    {
        public const string PrototypeEncounter = "prototype_janhyang_duel";
        public string EncounterId = PrototypeEncounter;
        public string RewardSkillId = SkillRules.SilverStitch;
        private Health health;
        private SkillProgressionService progression;
        private bool pending;
        public void Initialize(Health source, SkillProgressionService service)
        { health = source; progression = service; health.Died += QueueReward; }
        private void QueueReward() { pending = true; }
        private void LateUpdate() { Flush(); }
        public void Flush()
        {
            if (!pending) return;
            pending = false;
            // Health.Died runs inside Hit. Wait until the lethal hit has credited its use.
            progression.CompleteEncounter(EncounterId, RewardSkillId);
        }
        private void OnDestroy() { if (health != null) health.Died -= QueueReward; }
    }
}
