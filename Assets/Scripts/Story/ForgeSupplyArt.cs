using UnityEngine;

namespace RewindNotary
{
    internal static class ForgeSupplyArt
    {
        private static Texture2D atlas;
        private static Sprite standing, offering, tea, bandage;

        internal static Sprite Ethan(bool talking) => talking
            ? (offering ??= Slice(new RectInt(746, 12, 372, 682), new Vector2(.5f, 0f)))
            : (standing ??= Slice(new RectInt(122, 12, 372, 682), new Vector2(.5f, 0f)));

        internal static Sprite Icon(SupplyItem item) => item == SupplyItem.MintTea
            ? (tea ??= Slice(new RectInt(126, 734, 406, 500), Vector2.one * .5f))
            : (bandage ??= Slice(new RectInt(668, 778, 540, 443), Vector2.one * .5f));

        private static Sprite Slice(RectInt pixels, Vector2 pivot)
        {
            atlas ??= Resources.Load<Texture2D>("GameArt/sprites/forge-supplies");
            var rect = new Rect(pixels.x, atlas.height - pixels.yMax, pixels.width, pixels.height);
            return Sprite.Create(atlas, rect, pivot, pixels.height, 0, SpriteMeshType.FullRect);
        }
    }
}
