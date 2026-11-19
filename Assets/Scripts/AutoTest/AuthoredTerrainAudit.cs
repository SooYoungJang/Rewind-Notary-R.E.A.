#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class RepresentativeArea
    {
        [Serializable] private sealed class TileReading
        {
            public string room, surface, error;
            public StoryRect authored;
            public Vector2 expectedCenter, expectedSize, boxCenter, boxSize, drawnCenter, drawnSize;
            public Vector2 localBox, localTile, localDrawn, childScale, spriteUnits;
            public Vector3 boxScale;
            public bool autoTiling, directChild;
        }

        [Serializable] private sealed class TerrainReadings
        {
            public string unityVersion = Application.unityVersion;
            public List<TileReading> tiles = new List<TileReading>();
        }

        // At authoring time every bridge is visible and moving platforms are at phase zero.
        // Build the same surfaces as a new room, without running story, input or save code.
        public static void AuditAuthoredTerrain(string output)
        {
            var readings = new TerrainReadings();
            var host = new GameObject("Authored terrain audit");
            var area = host.AddComponent<RepresentativeArea>();
            int failures = 0;
            try
            {
                area.InitArt();
                foreach (var room in RepresentativeScript.Load().rooms)
                {
                    if (room.terrain == null || room.terrain.Length == 0) continue;
                    area.Room = room;
                    area.layout = new GameObject(room.id);
                    area.layout.transform.SetParent(host.transform, false);
                    area.BuildSurfaces();
                    Physics2D.SyncTransforms();
                    foreach (var surface in room.terrain)
                    {
                        var reading = new TileReading { room = room.id, surface = surface.id, authored = surface.rect };
                        readings.tiles.Add(reading);
                        try { area.ReadTile(surface, reading); }
                        catch (Exception e) { reading.error = e.Message; failures++; }
                    }
                    area.fixedFloors.Clear();
                    DestroyImmediate(area.layout);
                }
            }
            finally
            {
                // Edit Mode has no end-of-frame Destroy queue.
                foreach (var sprite in area.rosterArt.Values) DestroyImmediate(sprite);
                area.rosterArt.Clear();
                DestroyImmediate(host);
                File.WriteAllText(output, JsonUtility.ToJson(readings, true));
            }
            if (failures != 0 || readings.tiles.Count == 0)
                throw new InvalidOperationException($"Terrain audit: {failures} errors in {readings.tiles.Count} tiles. See {output}");
        }

        private void ReadTile(StorySurface s, TileReading reading)
        {
            var r = s.rect;
            var tangent = new Vector2(r.width, s.rampRise);
            var thickness = Vector2.Perpendicular(tangent.normalized) * r.height;
            reading.expectedCenter = new Vector2(r.x, r.y + r.height) + (tangent - thickness) * .5f;
            reading.expectedSize = new Vector2(Mathf.Abs(tangent.x) + Mathf.Abs(thickness.x),
                Mathf.Abs(tangent.y) + Mathf.Abs(thickness.y));
            var box = (BoxCollider2D)surfaces[s.id];
            var tile = box.GetComponentInChildren<SpriteRenderer>();
            reading.localBox = box.size;
            reading.boxScale = box.transform.lossyScale;
            reading.autoTiling = box.autoTiling;
            reading.boxCenter = box.bounds.center;
            reading.boxSize = box.bounds.size;
            if (tile == null || tile.sprite == null || !tile.enabled)
                throw new InvalidOperationException("Missing visible terrain sprite");
            reading.localTile = tile.size;
            reading.directChild = tile.transform.parent == box.transform;
            reading.spriteUnits = tile.sprite.bounds.size;
            reading.localDrawn = tile.localBounds.size;
            reading.childScale = tile.transform.localScale;
            reading.drawnCenter = tile.bounds.center;
            reading.drawnSize = tile.bounds.size;
            if (Room.stage == "clock" || Room.stage == "opera" || Room.stage == "bell" || Room.stage == "gallery")
                VerifyGalleryTile(s);
            if (!WithinTileTolerance(reading.boxSize, reading.expectedSize) ||
                !WithinTileTolerance(reading.boxCenter, reading.expectedCenter) ||
                !WithinTileTolerance(reading.drawnSize, reading.boxSize) ||
                !WithinTileTolerance(reading.drawnCenter, reading.boxCenter))
                throw new InvalidOperationException("Authored, rendered and collision rectangles diverged");
        }

        private static bool WithinTileTolerance(Vector2 a, Vector2 b) =>
            (a - b).sqrMagnitude <= .000025f;
    }
}
#endif
