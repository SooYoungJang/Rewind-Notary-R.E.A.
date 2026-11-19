using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class RepresentativeArea
    {
        private readonly List<SpriteRenderer> unpainted = new List<SpriteRenderer>();

        private SpriteRenderer framedLandscape;
        private void DrawGalleryPassage(float x, float y)
        {
            var frame = Place("passage_frame", x, y).AddComponent<SpriteRenderer>();
            frame.sprite = OperaSprite("gallery-frame", 2.25f);
            frame.sortingOrder = -1;
            framedLandscape = Place("passage_view", x, y).AddComponent<SpriteRenderer>();
            framedLandscape.sprite = OperaSprite("gallery-meadow", 2.05f);
            framedLandscape.sortingOrder = -2;
        }

        private void BuildGallery()
        {
            unpainted.Clear();
            framedLandscape = null;
            if (Room.stage != "gallery") return;
            // Pencil marks live on the wall above the plaster. A swing colours each mark once.
            foreach (var face in Room.terrain)
            {
                var r = face.rect;
                var mark = Place(face.id + "_sketch", r.x + r.width / 2, Room.SurfaceTop(face.id) + 1f)
                    .AddComponent<SpriteRenderer>();
                mark.sprite = OperaSprite("gallery-drybrush", 2.8f);
                mark.color = new Color(.45f, .43f, .42f, .22f);
                mark.sortingOrder = -2;
                unpainted.Add(mark);
            }
            if (Room.viewingRoutes != null)
                foreach (var route in Room.viewingRoutes)
                {
                    bool upper = route.id == "upper_frames";
                    for (int i = 1; i < route.surfaces.Length - 1; i++)
                    {
                        var face = Room.Surface(route.surfaces[i]);
                        var art = OperaSprite(upper ? "gallery-frame" : "gallery-plaster", upper ? 2.25f : 1.15f);
                        var prop = Place(face.id + "_exhibit", face.rect.x + face.rect.width / 2,
                            Room.SurfaceTop(face.id) + art.bounds.extents.y + (upper ? .55f : 0f)).AddComponent<SpriteRenderer>();
                        prop.sprite = art;
                        prop.color = new Color(.72f,.70f,.68f,.74f);
                        prop.sortingOrder = -1;
                        unpainted.Add(prop);
                    }
                }
            if (Room.stationMarks == null) return;
            foreach (var m in Room.stationMarks)
            {
                PlaceMarker(m.id + "_support_mark", m.x + 1.16f, Room.SurfaceTop(m.surface),
                    RoomArt.Picture("paint-support-marker"), .50f);
                // Two small pigment dabs mark the banks; the opening stays empty until painted.
                foreach (int side in new[] { -1, 1 })
                {
                    var dab = Place(m.id + "_pigment", m.x + side * 1.16f, Room.SurfaceTop(m.surface) + .06f)
                        .AddComponent<SpriteRenderer>();
                    dab.sprite = OperaSprite("gallery-oil", 2.4f);
                    dab.transform.localScale = new Vector3(.22f, .35f, 1);
                    dab.sortingOrder = 2;
                }
            }
        }

        private void RevealGalleryOutline(Bounds blade)
        {
            for (int i = unpainted.Count - 1; i >= 0; i--)
            {
                var sketch = unpainted[i];
                if (!blade.Intersects(sketch.bounds)) continue;
                sketch.color = Color.white;
                unpainted.RemoveAt(i);
            }
        }

        private void ColorGalleryStroke(PaintSurface2D paint)
        {
            if (Room.stage != "gallery") return;
            Physics2D.SyncTransforms();
            var b = paint.Bounds;
            RevealGalleryOutline(b);
            string station = null;
            float center = 0;
            if (!paint.Wall && Room.stationMarks != null)
                for (int i = 0; i < Room.stationMarks.Length; i++)
                {
                    var m = Room.stationMarks[i];
                    if (m.x < b.min.x || m.x > b.max.x ||
                        Mathf.Abs(b.max.y - Room.SurfaceTop(m.surface) - .25f) > .16f) continue;
                    station = Room.id + ":station_" + i;
                    center = m.x;
                    break;
                }
            paint.SetBrush(OperaSprite("gallery-oil", 2.4f), station, center);
        }
    }
}
