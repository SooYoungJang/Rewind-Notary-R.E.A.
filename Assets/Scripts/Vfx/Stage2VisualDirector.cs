using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class Stage2VisualDirector : MonoBehaviour
    {
        private CombatMotor2D player;
        private readonly HashSet<WaveTarget2D> attached = new HashSet<WaveTarget2D>();

        public void Initialize(CombatMotor2D playerActor)
        {
            player = playerActor;
        }

        private void LateUpdate()
        {
            var targets = WaveCombatTargets.All;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || attached.Contains(t)) continue;
                attached.Add(t);
                if (t.GetComponent<EnemyMotionPolish2D>() == null)
                    t.gameObject.AddComponent<EnemyMotionPolish2D>().Initialize(t, player);
            }
        }
    }
}
