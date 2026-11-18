using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [Serializable] public sealed class WaveArchetype
    {
        public string Label => Loc.Get(label);
        public string id, label, gate;
        public bool mechanism;
        public float openingSeconds, telegraphSeconds, attackInterval, attackReach;
        public int attackDamage;
    }
    [Serializable] public sealed class WaveSpawn
    {
        public string archetypeId, setupSkill;
        public int count, segments;
        public float x;
    }
    [Serializable] public sealed class WaveDefinition
    {
        public string id;
        public WaveSpawn[] spawns;
    }
    [Serializable] public sealed class WaveZone
    {
        public string Label => Loc.Get(label);
        public string id, label;
        public float left, right, entryX;
        public int gate;
        public WaveDefinition[] waves;
    }
    [Serializable] public sealed class WaveCatalog
    {
        public int schemaVersion, initialSwordUses;
        public float supplyRatio, handoffSeconds, crossLoopSpeed;
        public WaveArchetype[] archetypes;
        public WaveZone[] zones;
        public WaveArchetype Archetype(string id) => Array.Find(archetypes, item => item.id == id);
        public static WaveCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("Waves/test-stage");
            if (asset == null) throw new InvalidOperationException(Loc.Get("wave.missing_data"));
            var data = JsonUtility.FromJson<WaveCatalog>(asset.text);
            data.Validate(SkillCatalog.Load());
            return data;
        }
        public void Validate(SkillCatalog skills)
        {
            Require(schemaVersion == 1 && initialSwordUses == 120 && zones != null && zones.Length == 3,
                Loc.Get("wave.invalid_header"));
            Require(supplyRatio >= 1.35f && handoffSeconds > 0 && crossLoopSpeed == 1.8f, Loc.Get("wave.invalid_supply"));
            var ids = new HashSet<string>();
            Require(archetypes != null && archetypes.Length == 4, Loc.Get("wave.invalid_archetype_count"));
            foreach (var a in archetypes)
            {
                Require(a != null && !string.IsNullOrEmpty(a.id) && ids.Add(a.id), Loc.Get("wave.invalid_archetype_id"));
                Require(a.gate == "skill" || a.gate == "armor" || a.gate == "defense" || a.gate == "device", Loc.Get("wave.invalid_gate"));
                Require(a.openingSeconds > 0 && a.telegraphSeconds > 0 && a.attackInterval > 0
                    && a.attackReach > 0 && a.attackDamage > 0, Loc.Get("wave.invalid_combat_values"));
            }
            ids.Clear();
            int total = initialSwordUses;
            for (int z = 0; z < zones.Length; z++)
            {
                var zone = zones[z];
                Require(zone != null && ids.Add(zone.id) && zone.left < zone.entryX && zone.entryX < zone.right
                    && zone.waves != null && zone.waves.Length > 0, Loc.Get("wave.invalid_zone"));
                if (z > 0) Require(zone.left == zones[z - 1].right, Loc.Get("wave.invalid_connection"));
                int supply = 0;
                foreach (var wave in zone.waves)
                {
                    Require(wave != null && !string.IsNullOrEmpty(wave.id) && ids.Add(wave.id)
                        && wave.spawns != null && wave.spawns.Length > 0, Loc.Get("wave.invalid_definition"));
                    foreach (var spawn in wave.spawns)
                    {
                        Require(spawn != null, Loc.Get("wave.invalid_spawn"));
                        var a = Archetype(spawn.archetypeId);
                        Require(a != null && spawn.count > 0 && spawn.segments > 0
                            && spawn.x > zone.left + 2 && spawn.x < zone.right - 2, Loc.Get("wave.invalid_spawn_range"));
                        bool setup = !string.IsNullOrEmpty(spawn.setupSkill);
                        Require(a.gate != "skill" || setup, Loc.Get("wave.setup_required"));
                        Require(!setup || ((a.gate == "skill" || a.gate == "device")
                            && Array.IndexOf(BenefactorIds.All, spawn.setupSkill) >= 0
                            && skills.Get(spawn.setupSkill).implemented), Loc.Get("wave.invalid_setup_skill"));
                        supply += spawn.count * spawn.segments;
                    }
                }
                total += supply;
                if (z == 0) Require(supply >= Mathf.CeilToInt((151 - initialSwordUses) * supplyRatio), Loc.Get("wave.invalid_zone1_supply"));
                Require(total >= zone.gate, Loc.Get("wave.invalid_gate_supply"));
            }
            Require(total - initialSwordUses >= Mathf.CeilToInt((400 - initialSwordUses) * supplyRatio), Loc.Get("wave.invalid_mastery_supply"));
        }
        private static void Require(bool ok, string message)
        { if (!ok) throw new InvalidOperationException(Loc.Get("wave.data_error", message)); }
    }
}
