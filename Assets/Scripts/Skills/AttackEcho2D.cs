using UnityEngine;

namespace RewindNotary
{
    // Visual replay uses the recorded pose/position, not the player's pose at delayed delivery.
    public sealed class AttackEcho2D : MonoBehaviour
    {
        public struct Pose
        {
            public Sprite sprite;
            public Vector3 position, scale;
            public Quaternion rotation;
            public bool flip;
            public static Pose Capture(SpriteRenderer source) => new Pose { sprite = source.sprite,
                position = source.transform.position, rotation = source.transform.rotation,
                scale = source.transform.lossyScale, flip = source.flipX };
        }
        private float until;
        public static void Show(Pose pose)
        {
            var go = new GameObject("Canvas portrait echo"); go.transform.SetParent(SkillStroke.WorldRoot, false);
            go.transform.SetPositionAndRotation(pose.position, pose.rotation); go.transform.localScale = pose.scale;
            var render = go.AddComponent<SpriteRenderer>(); render.sprite = pose.sprite; render.flipX = pose.flip;
            render.sortingOrder = 11; render.color = new Color(0.3f, 1f, 0.8f, 0.55f);
            go.AddComponent<AttackEcho2D>().until = Time.time + SkillGeometry.EchoVisualSeconds;
        }
        private void Update() { if (Time.time >= until) Destroy(gameObject); }
    }
}
