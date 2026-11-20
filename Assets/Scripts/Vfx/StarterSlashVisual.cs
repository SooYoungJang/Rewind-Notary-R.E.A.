using UnityEngine;

namespace RewindNotary
{
    internal sealed class StarterSlashVisual : MonoBehaviour
    {
        private static Sprite[] strikes, accents;
        private static Material glow;
        private static readonly Color[] Colors =
        {
            new Color(1f, .67f, .24f), new Color(.35f, 1f, .94f),
            new Color(1f, .60f, .24f), new Color(.42f, .76f, 1f)
        };
        private SpriteRenderer view;
        private Vector3 size;
        private Color tint;
        private float age, duration, startScale, endScale;

        private static Sprite[] LoadAtlas(string resource)
        {
            var texture = Resources.Load<Texture2D>(resource);
            if (texture == null) return null;
            var sprites = new Sprite[4];
            int width = texture.width / 2, height = texture.height / 2;
            for (int i = 0; i < sprites.Length; i++)
            {
                var cell = new Rect(i % 2 * width, (1 - i / 2) * height, width, height);
                sprites[i] = Sprite.Create(texture, cell, Vector2.one * .5f, width, 0, SpriteMeshType.FullRect);
                sprites[i].name = texture.name + "_" + i;
            }
            return sprites;
        }

        private static Material Glow
        {
            get
            {
                if (glow == null)
                {
                    var shader = Resources.Load<Shader>("Shaders/AdditiveSprite") ?? Shader.Find("Sprites/Default");
                    glow = new Material(shader);
                }
                return glow;
            }
        }

        internal static SpriteRenderer Create(int index, Vector3 position, Vector2 dimensions, int facing)
        {
            strikes ??= LoadAtlas("GameArt/sprites/rea-starter-effects");
            return strikes == null ? null : Layer(strikes[index], position, dimensions, facing, 15);
        }

        private static SpriteRenderer Layer(Sprite sprite, Vector3 position, Vector2 dimensions, int facing, int order)
        {
            var go = new GameObject("Rea sword effect");
            if (SkillStroke.WorldRoot != null) go.transform.SetParent(SkillStroke.WorldRoot, false);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.flipX = facing < 0;
            renderer.sharedMaterial = Glow;
            var nativeSize = sprite.bounds.size;
            go.transform.localScale = new Vector3(dimensions.x / nativeSize.x, dimensions.y / nativeSize.y, 1f);
            return renderer;
        }

        internal static void Burst(int index, Vector3 position, Vector2 dimensions, int facing, bool hit)
        {
            var renderer = Create(index, position, dimensions * 1.55f, facing);
            var color = Colors[index];
            if (renderer == null)
            {
                Vector3 reach = index == QuickSkills.Rising ? Vector3.up * dimensions.y : Vector3.right * dimensions.x;
                SkillStroke.Show(position - reach * .5f, position + reach * .5f, color, .20f);
                return;
            }

            Animate(renderer, Color.white, .29f, .84f, 1.15f);
            accents ??= LoadAtlas("GameArt/sprites/rea-starter-impacts");
            if (accents != null)
            {
                var flash = Layer(accents[index], position, dimensions * (hit ? 1.95f : 1.65f), facing, 16);
                Animate(flash, new Color(1f, 1f, 1f, hit ? .93f : .62f), .34f, .78f, 1.18f);
                EmitSparks(flash.gameObject, position, color, index, facing, hit ? 14 : 8);
            }
            if (hit)
            {
                AtmospherePostEffect.PulseImpact(index == QuickSkills.Sweep || index == QuickSkills.BladeWave ? .40f : .28f);
                ImpactFeedback.StarterHit(index);
            }
        }

        internal static void Wake(Vector2 position, int direction)
        {
            var after = Create(QuickSkills.BladeWave, position, new Vector2(1.39f, 1.85f), direction);
            if (after != null)
            {
                after.sortingOrder = 14;
                Animate(after, new Color(.38f, .74f, 1f, .36f), .14f, 1f, .65f);
            }
        }

        private static void EmitSparks(GameObject source, Vector3 at, Color color, int index, int facing, int count)
        {
            var system = source.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.gravityModifier = .4f;
            var emission = system.emission;
            emission.enabled = false;
            var sparks = source.GetComponent<ParticleSystemRenderer>();
            sparks.sharedMaterial = Glow;
            sparks.sortingOrder = 17;
            sparks.renderMode = ParticleSystemRenderMode.Stretch;
            sparks.velocityScale = .095f;
            sparks.lengthScale = 1.3f;
            system.Play();

            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.39996f;
                float spread = .62f + .17f * (i % 5);
                Vector3 velocity = new Vector3(Mathf.Cos(angle) * 3.2f, Mathf.Sin(angle) * 2.1f, 0f) * spread;
                if (index == QuickSkills.Rising) { velocity.x *= .62f; velocity.y += 3.2f; }
                else if (index != QuickSkills.Sweep) velocity.x += facing * 2.7f;
                system.Emit(new ParticleSystem.EmitParams
                {
                    position = at, velocity = velocity, startSize = .035f + .013f * (i % 3),
                    startColor = Color.Lerp(color, Color.white, (i % 4) / 4f), startLifetime = .15f + spread * .12f
                }, 1);
            }
        }

        private static void Animate(SpriteRenderer renderer, Color color, float seconds, float from, float to)
        {
            var effect = renderer.gameObject.AddComponent<StarterSlashVisual>();
            effect.view = renderer;
            effect.size = renderer.transform.localScale;
            effect.duration = seconds;
            effect.startScale = from;
            effect.endScale = to;
            renderer.color = effect.tint = color;
            renderer.transform.localScale *= from;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            transform.localScale = size * Mathf.Lerp(startScale, endScale, 1f - (1f - t) * (1f - t));
            Color color = tint;
            color.a *= 1f - t * t;
            view.color = color;
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
