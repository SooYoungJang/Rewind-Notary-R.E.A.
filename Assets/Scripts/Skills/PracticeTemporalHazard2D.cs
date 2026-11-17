using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class PracticeTemporalHazard2D : MonoBehaviour
    {
        public static readonly List<PracticeTemporalHazard2D> All = new List<PracticeTemporalHazard2D>();
        private CombatMotor2D player;
        private TemporalActor2D temporal;
        private Vector2 origin;
        private float phase;
        private Sprite sprite;
        public void Initialize(CombatMotor2D actor)
        {
            player = actor; origin = transform.position; temporal = gameObject.AddComponent<TemporalActor2D>();
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            var visual = gameObject.AddComponent<SpriteRenderer>(); visual.sprite = sprite; visual.color = Color.red; visual.sortingOrder = 5;
            transform.localScale = Vector3.one * SkillGeometry.LampRadius;
        }
        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }
        private void Update()
        {
            if (!GameInput.GameplayEnabled || temporal.Frozen) return;
            phase += Time.deltaTime * SkillGeometry.HazardSpeed;
            transform.position = origin + Vector2.right * Mathf.Sin(phase) * SkillGeometry.HazardSpan;
            if (!player.Health.Dead && SkillGeometry.Area(transform.position, SkillGeometry.LampRadius).Intersects(player.GetComponent<Collider2D>().bounds))
                player.Health.Hit(SkillGeometry.ProjectileDamage, transform.position);
        }
        public void ResetAttempt() { temporal.Release(); phase = 0; transform.position = origin; }
        private void OnDestroy() { if (sprite != null) Destroy(sprite); }
    }
}
