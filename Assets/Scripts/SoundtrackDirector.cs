using UnityEngine;

namespace RewindNotary
{
    // Music observes the game's state; it never advances a room, encounter or save.
    public sealed class SoundtrackDirector : MonoBehaviour
    {
        private GameBootstrap game;
        private AudioManager.Ost? lastCue;
        private bool requested;

        private void Awake() => game = GetComponentInParent<GameBootstrap>();

        private void LateUpdate()
        {
            if (game == null) return;
            AudioManager.Ost? cue = Resolve(game);
            if (requested && cue == lastCue) return;
            requested = true;
            lastCue = cue;
            if (!cue.HasValue)
            {
                AudioManager.UseLegacyMusic();
                return;
            }

            var track = cue.Value;
            if (!AudioManager.SetOst(track, track == AudioManager.Ost.EndingNormal || track == AudioManager.Ost.EndingTrue))
            {
                AudioManager.UseLegacyMusic();
                return;
            }
            if (Debug.isDebugBuild || Application.isEditor)
                Debug.Log("REWA OST cue: " + track, this);
        }

        private static AudioManager.Ost? Resolve(GameBootstrap game)
        {
            var session = game.ActiveSkills;
            if (session == null) return AudioManager.Ost.Title;
            var record = session.Progression.Record;
            var area = game.Area;
            var room = area != null ? area.Room : null;
            if (room != null && (room.stage == "janhyang" || room.id == "hub_attic"))
            {
                if (record.storyFlags.Contains("endingScene:True")) return AudioManager.Ost.EndingTrue;
                if (record.storyFlags.Contains("endingScene:Basic")) return AudioManager.Ost.EndingNormal;
            }

            var cassel = game.ActiveCassel;
            if (cassel != null)
                return cassel.Phase >= 2 ? AudioManager.Ost.CasselPhase2 : AudioManager.Ost.CasselPhase1;
            if (record.lastStageId == "echo_duel") return AudioManager.Ost.Janhyang;

            if (room != null)
            {
                var bossCue = area.ActiveSoundtrackBoss();
                if (bossCue.HasValue) return bossCue;
                if (room.id == "hub_forge") return AudioManager.Ost.Forge;
                switch (room.stage)
                {
                    case "hub": return AudioManager.Ost.Attic;
                    case "velvet": return AudioManager.Ost.Belvet;
                    case "clock": return AudioManager.Ost.Clocktower;
                    case "opera": return AudioManager.Ost.Opera;
                    case "bell": return AudioManager.Ost.BellTower;
                    case "gallery": return AudioManager.Ost.Gallery;
                }
            }

            switch (record.lastStageId)
            {
                case "trial_mikhail": return AudioManager.Ost.Mikhail;
                case "trial_julius": return AudioManager.Ost.Julius;
                case "trial_valeria": return AudioManager.Ost.Valeria;
                case "trial_gabriel": return AudioManager.Ost.Gabriel;
                case "trial_irina": return AudioManager.Ost.Irina;
                default: return null;
            }
        }
    }

    // Adapter in a separate file so the active combat implementation is not edited.
    public sealed partial class RepresentativeArea
    {
        internal AudioManager.Ost? ActiveSoundtrackBoss()
        {
            if (opponent == null || !opponent.gameObject.activeInHierarchy || opponent.Health.Dead) return null;
            if (Room.id == "court_02_echo_arena")
                return courtEcho != null && courtEcho.Active ? AudioManager.Ost.Janhyang : (AudioManager.Ost?)null;
            switch (Room.stage)
            {
                case "velvet": return AudioManager.Ost.Mikhail;
                case "clock": return AudioManager.Ost.Julius;
                case "opera": return AudioManager.Ost.Valeria;
                case "bell": return AudioManager.Ost.Gabriel;
                case "gallery": return AudioManager.Ost.Irina;
                default: return null;
            }
        }
    }
}
