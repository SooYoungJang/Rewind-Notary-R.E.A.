using UnityEngine;

namespace RewindNotary
{
    public sealed class DistrictAtmosphere2D : MonoBehaviour
    {
        private Camera targetCamera;
        private RepresentativeArea area;
        private ParticleSystem ambientParticles;
        private SpriteRenderer depthVeil;
        private SpriteRenderer[] lightHalos;
        private Texture2D softDotTexture;
        private Sprite softDotSprite;
        private Sprite veilSprite;
        private Material particleMaterial;
        private Material additiveLightMaterial;
        private string activeRoomId;
        private string activeDistrict;
        private bool forceDistrict;

        public void Initialize(Camera camera, RepresentativeArea storyArea, string explicitDistrict = null)
        {
            targetCamera = camera;
            area = storyArea;
            forceDistrict = !string.IsNullOrEmpty(explicitDistrict);
            activeDistrict = forceDistrict ? explicitDistrict : (storyArea?.Room?.stage ?? "hub");
            CreateTexturesAndMaterials();
            BuildDepthVeil();
            BuildAmbientParticles();
            BuildLightHalos();
            ApplyDistrict(activeDistrict, true);
            activeRoomId = area?.Room?.id;
        }

        private void CreateTexturesAndMaterials()
        {
            const int size = 64;
            softDotTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            softDotTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * a;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            softDotTexture.SetPixels(pixels);
            softDotTexture.Apply();
            softDotSprite = Sprite.Create(softDotTexture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            veilSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);

            Shader spriteShader = Shader.Find("Sprites/Default");
            particleMaterial = new Material(spriteShader);
            additiveLightMaterial = new Material(Resources.Load<Shader>("Shaders/AdditiveSprite") ?? spriteShader);
        }

        private void BuildDepthVeil()
        {
            var go = new GameObject("Atmospheric depth veil");
            go.transform.SetParent(transform, false);
            depthVeil = go.AddComponent<SpriteRenderer>();
            depthVeil.sprite = veilSprite;
            depthVeil.sortingOrder = -3;
            depthVeil.transform.localScale = new Vector3(44f, 26f, 1f);
        }

        private void BuildLightHalos()
        {
            lightHalos = new SpriteRenderer[3];
            for (int i = 0; i < lightHalos.Length; i++)
            {
                var go = new GameObject("Ambient light halo " + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = softDotSprite;
                sr.sharedMaterial = additiveLightMaterial;
                sr.sortingOrder = 16;
                sr.transform.localScale = new Vector3(9f + i * 2.5f, 7f + i * 2f, 1f);
                lightHalos[i] = sr;
            }
        }

        private void BuildAmbientParticles()
        {
            var go = new GameObject("District ambient particles");
            go.transform.SetParent(transform, false);
            ambientParticles = go.AddComponent<ParticleSystem>();
            var main = ambientParticles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 140;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 7.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);

            var emission = ambientParticles.emission;
            emission.rateOverTime = 18f;

            var shape = ambientParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 13f, 0.1f);

            var colorOverLife = ambientParticles.colorOverLifetime;
            colorOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.25f), new GradientAlphaKey(0.65f, 0.75f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLife.color = grad;

            var noise = ambientParticles.noise;
            noise.enabled = true;
            noise.strength = 0.28f;
            noise.frequency = 0.35f;

            var renderer = ambientParticles.GetComponent<ParticleSystemRenderer>();
            renderer.material = additiveLightMaterial;
            renderer.material.mainTexture = softDotTexture;
            renderer.sortingOrder = 17;
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;
            Vector3 cam = targetCamera.transform.position;
            if (depthVeil != null) depthVeil.transform.position = new Vector3(cam.x, cam.y, 4f);
            if (ambientParticles != null) ambientParticles.transform.position = new Vector3(cam.x, cam.y, 0f);

            float t = Time.unscaledTime;
            for (int i = 0; i < lightHalos.Length; i++)
            {
                if (lightHalos[i] == null) continue;
                float ox = (i - 1) * 7.2f + Mathf.Sin(t * (0.35f + i * 0.12f)) * 1.1f;
                float oy = 3.8f + Mathf.Cos(t * (0.28f + i * 0.09f)) * 0.7f;
                lightHalos[i].transform.position = new Vector3(cam.x + ox, cam.y + oy, 0f);
            }

            if (!forceDistrict && area?.Room != null)
            {
                string nextRoom = area.Room.id;
                string nextDistrict = area.Room.stage;
                if (nextRoom != activeRoomId)
                {
                    activeRoomId = nextRoom;
                    ApplyDistrict(nextDistrict, true);
                }
            }
        }

        private void ApplyDistrict(string district, bool fade)
        {
            activeDistrict = string.IsNullOrEmpty(district) ? "hub" : district;
            AtmospherePostEffect.SetDistrict(activeDistrict, fade);

            Color veilColor;
            Color particleA, particleB;
            Color haloColor;
            float gravityY;
            float rate;

            switch (activeDistrict)
            {
                case "velvet":
                    veilColor = new Color(0.14f, 0.05f, 0.07f, 0.16f);
                    particleA = new Color(1.0f, 0.74f, 0.42f, 0.65f);
                    particleB = new Color(0.95f, 0.42f, 0.35f, 0.55f);
                    haloColor = new Color(1.0f, 0.68f, 0.35f, 0.10f);
                    gravityY = -0.04f;
                    rate = 20f;
                    break;
                case "clock":
                    veilColor = new Color(0.11f, 0.09f, 0.05f, 0.15f);
                    particleA = new Color(1.0f, 0.85f, 0.48f, 0.70f);
                    particleB = new Color(0.88f, 0.72f, 0.36f, 0.50f);
                    haloColor = new Color(1.0f, 0.82f, 0.45f, 0.11f);
                    gravityY = -0.02f;
                    rate = 22f;
                    break;
                case "opera":
                    veilColor = new Color(0.08f, 0.06f, 0.14f, 0.16f);
                    particleA = new Color(1.0f, 0.88f, 0.64f, 0.75f);
                    particleB = new Color(0.95f, 0.68f, 0.82f, 0.55f);
                    haloColor = new Color(1.0f, 0.86f, 0.58f, 0.13f);
                    gravityY = 0.02f;
                    rate = 24f;
                    break;
                case "bell":
                    veilColor = new Color(0.03f, 0.10f, 0.15f, 0.18f);
                    particleA = new Color(0.48f, 0.92f, 1.0f, 0.70f);
                    particleB = new Color(0.98f, 0.85f, 0.52f, 0.55f);
                    haloColor = new Color(0.40f, 0.82f, 0.95f, 0.12f);
                    gravityY = -0.07f;
                    rate = 24f;
                    break;
                case "gallery":
                    veilColor = new Color(0.12f, 0.12f, 0.14f, 0.09f);
                    particleA = new Color(1.0f, 0.96f, 0.86f, 0.60f);
                    particleB = new Color(0.65f, 0.88f, 1.0f, 0.45f);
                    haloColor = new Color(1.0f, 0.95f, 0.82f, 0.09f);
                    gravityY = 0.01f;
                    rate = 16f;
                    break;
                case "cassel":
                    veilColor = new Color(0.05f, 0.07f, 0.14f, 0.22f);
                    particleA = new Color(0.82f, 0.92f, 1.0f, 0.80f);
                    particleB = new Color(1.0f, 0.84f, 0.48f, 0.65f);
                    haloColor = new Color(0.75f, 0.88f, 1.0f, 0.13f);
                    gravityY = 0.03f;
                    rate = 26f;
                    break;
                default:
                    veilColor = new Color(0.10f, 0.07f, 0.05f, 0.12f);
                    particleA = new Color(1.0f, 0.86f, 0.58f, 0.62f);
                    particleB = new Color(0.92f, 0.78f, 0.56f, 0.45f);
                    haloColor = new Color(1.0f, 0.80f, 0.50f, 0.10f);
                    gravityY = -0.015f;
                    rate = 16f;
                    break;
            }

            if (depthVeil != null) depthVeil.color = veilColor;
            for (int i = 0; i < lightHalos.Length; i++)
                if (lightHalos[i] != null) lightHalos[i].color = haloColor;

            if (ambientParticles != null)
            {
                var main = ambientParticles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(particleA, particleB);
                main.gravityModifier = gravityY;
                var emission = ambientParticles.emission;
                emission.rateOverTime = rate;
            }
        }

        private void OnDestroy()
        {
            if (softDotSprite != null) Destroy(softDotSprite);
            if (veilSprite != null) Destroy(veilSprite);
            if (softDotTexture != null) Destroy(softDotTexture);
            if (particleMaterial != null) Destroy(particleMaterial);
            if (additiveLightMaterial != null) Destroy(additiveLightMaterial);
        }
    }
}
