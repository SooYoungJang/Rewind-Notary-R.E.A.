using UnityEngine;

namespace RewindNotary
{
    internal sealed class SwordSwipe : MonoBehaviour
    {
        private readonly struct Sweep
        {
            internal readonly Vector2 Start, Curve, End;
            internal readonly float Width, Lifetime;

            internal Sweep(Vector2 start, Vector2 curve, Vector2 end, float width, float lifetime)
            {
                Start = start;
                Curve = curve;
                End = end;
                Width = width;
                Lifetime = lifetime;
            }
        }

        private static readonly Sweep[] Ground =
        {
            new Sweep(new Vector2(.36f, 1.05f), new Vector2(1.47f, 1.04f), new Vector2(1.30f, .60f), .062f, .115f),
            new Sweep(new Vector2(.46f, .32f), new Vector2(1.48f, .57f), new Vector2(1.10f, 1.47f), .070f, .131f),
            new Sweep(new Vector2(.49f, .79f), new Vector2(1.17f, .90f), new Vector2(1.78f, .75f), .050f, .110f),
            new Sweep(new Vector2(.52f, 1.72f), new Vector2(1.99f, 1.17f), new Vector2(1.12f, .20f), .103f, .172f)
        };
        private static readonly Sweep[] Air =
        {
            new Sweep(new Vector2(.44f, 1.22f), new Vector2(1.48f, 1.13f), new Vector2(1.25f, .69f), .065f, .119f),
            new Sweep(new Vector2(.40f, .40f), new Vector2(1.50f, .72f), new Vector2(1.07f, 1.48f), .077f, .135f),
            new Sweep(new Vector2(.52f, 1.30f), new Vector2(1.68f, 1.02f), new Vector2(1.16f, .30f), .072f, .142f),
            new Sweep(new Vector2(.41f, 1.88f), new Vector2(1.94f, 1.39f), new Vector2(1.10f, .04f), .109f, .180f)
        };
        private static readonly Sweep GroundUpward = new Sweep(
            new Vector2(.68f, 1.05f), new Vector2(.91f, 2.97f), new Vector2(-.60f, 2.24f), .108f, .158f);
        private static readonly Sweep AirUpward = new Sweep(
            new Vector2(.71f, 1.43f), new Vector2(.75f, 3.02f), new Vector2(-.52f, 2.49f), .094f, .146f);
        private static readonly Sweep AirDownward = new Sweep(
            new Vector2(.83f, .48f), new Vector2(.56f, -1.16f), new Vector2(-.54f, -1.05f), .121f, .151f);
        private static readonly int Tint = Shader.PropertyToID("_Color");
        private static Material material;
        private CombatMotor2D owner;
        private int facing;
        private float started, lifetime;
        private Mesh mesh;
        private MeshRenderer view;
        private MaterialPropertyBlock properties;

        internal static void Show(CombatMotor2D owner, int step, bool airborne)
        {
            Sweep sweep;
            if (owner.AttackDirectionY > 0) sweep = airborne ? AirUpward : GroundUpward;
            else if (owner.AttackDirectionY < 0) sweep = AirDownward;
            else sweep = (airborne ? Air : Ground)[step - 1];
            var go = new GameObject("Sword swipe");
            go.transform.SetParent(owner.transform, false);
            go.transform.localScale = new Vector3(owner.Facing, 1f, 1f);
            go.AddComponent<SwordSwipe>().Initialize(owner, sweep);
        }

        private void Initialize(CombatMotor2D actor, Sweep sweep)
        {
            owner = actor;
            facing = actor.Facing;
            started = Time.time;
            lifetime = sweep.Lifetime;
            properties = new MaterialPropertyBlock();
            if (material == null) material = new Material(Resources.Load<Shader>("Shaders/AdditiveSprite"));
            mesh = MakeRibbon(sweep);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            view = gameObject.AddComponent<MeshRenderer>();
            view.sharedMaterial = material;
            view.sortingOrder = 12;
        }

        private static Mesh MakeRibbon(Sweep sweep)
        {
            const int sections = 20;
            var vertices = new Vector3[(sections + 1) * 3];
            var colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var indices = new int[sections * 12];
            var silver = new Color(.72f, .90f, 1f, .82f);
            for (int sample = 0; sample <= sections; sample++)
            {
                float t = sample / (float)sections;
                Vector2 first = Vector2.Lerp(sweep.Start, sweep.Curve, t);
                Vector2 second = Vector2.Lerp(sweep.Curve, sweep.End, t);
                Vector2 point = Vector2.Lerp(first, second, t);
                float taper = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)));
                Vector2 edge = Vector2.Perpendicular((second - first).normalized) * (sweep.Width * taper);
                int v = sample * 3;
                vertices[v] = point - edge;
                vertices[v + 1] = point;
                vertices[v + 2] = point + edge;
                colors[v] = new Color(silver.r, silver.g, silver.b, 0f);
                colors[v + 1] = new Color(silver.r, silver.g, silver.b, silver.a * taper);
                colors[v + 2] = colors[v];
                uv[v] = uv[v + 1] = uv[v + 2] = Vector2.one * .5f;
                if (sample == sections) continue;
                for (int side = 0; side < 2; side++)
                {
                    int j = sample * 12 + side * 6;
                    int lower = v + side;
                    indices[j] = lower; indices[j + 1] = lower + 3; indices[j + 2] = lower + 1;
                    indices[j + 3] = lower + 1; indices[j + 4] = lower + 3; indices[j + 5] = lower + 4;
                }
            }
            var trail = new Mesh { vertices = vertices, colors = colors, uv = uv, triangles = indices };
            trail.RecalculateBounds();
            return trail;
        }

        private void Update()
        {
            float t = (Time.time - started) / lifetime;
            if (t >= 1f || owner.Facing != facing || !owner.Attacking)
            {
                Destroy(gameObject);
                return;
            }
            properties.SetColor(Tint, new Color(1f, 1f, 1f, 1f - t * t));
            view.SetPropertyBlock(properties);
        }

        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
