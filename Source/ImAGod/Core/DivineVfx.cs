using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ImAGod
{
    /// <summary>Light, sound and camera effects for miracles: golden light for Creation, crimson for Chaos.</summary>
    public static class DivineVfx
    {
        private static readonly Color CreationColor = new Color(1f, 0.86f, 0.5f);
        private static readonly Color ChaosColor = new Color(0.95f, 0.25f, 0.4f);
        private const float BigShake = 0.6f;
        private const float SmallShake = 0.2f;

        private static bool Chaos => GameComponent_Divinity.Instance?.path?.defName == "IAG_Path_Chaos";

        public static Color PathColor => Chaos ? ChaosColor : CreationColor;

        /// <summary>A pillar of light at the target, a ring spreading over the area, a flash and a divine sound.</summary>
        public static void Miracle(Map map, IntVec3 cell, float radius, bool big)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map))
                return;
            Color color = PathColor;
            Vector3 loc = cell.ToVector3Shifted();

            FleckCreationData beam = FleckMaker.GetDataStatic(loc + new Vector3(0f, 0f, big ? 4.5f : 3f), map, IAG_DefOf.IAG_Fleck_LightBeam);
            beam.exactScale = new Vector3(big ? 3f : 2f, 1f, big ? 10f : 7f);
            beam.instanceColor = color;
            map.flecks.CreateFleck(beam);

            FleckCreationData ring = FleckMaker.GetDataStatic(loc, map, IAG_DefOf.IAG_Fleck_Ring, Mathf.Max(2.5f, radius * 2f));
            ring.instanceColor = color;
            map.flecks.CreateFleck(ring);

            FleckMaker.ThrowLightningGlow(loc, map, big ? 4f : 2.5f);
            for (int i = 0; i < (big ? 8 : 4); i++)
                FleckMaker.ThrowMicroSparks(loc + new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f)), map);

            Sound(map, cell, big);
            if (big && Find.CurrentMap == map)
                Find.CameraDriver.shaker.DoShake(BigShake);
        }

        /// <summary>A miracle with no single target: a glow over every believer and a divine sound.</summary>
        public static void Global(Map map, bool big)
        {
            if (map == null)
                return;
            Color color = PathColor;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (!DivinityUtility.IsFollower(pawn))
                    continue;
                FleckCreationData beam = FleckMaker.GetDataStatic(pawn.DrawPos + new Vector3(0f, 0f, 1.6f), map, IAG_DefOf.IAG_Fleck_LightBeam);
                beam.exactScale = new Vector3(1f, 1f, 3.5f);
                beam.instanceColor = color;
                map.flecks.CreateFleck(beam);
            }
            Sound(map, IntVec3.Invalid, big);
            if (Find.CurrentMap == map)
                Find.CameraDriver.shaker.DoShake(big ? BigShake : SmallShake);
        }

        private static void Sound(Map map, IntVec3 cell, bool big)
        {
            if (Find.CurrentMap != map)
                return;
            (Chaos ? SoundDefOf.PsychicPulseGlobal : SoundDefOf.PsychicSootheGlobal).PlayOneShotOnCamera(map);
            if (big && cell.IsValid)
                SoundDefOf.Thunder_OnMap.PlayOneShot(new TargetInfo(cell, map));
        }
    }
}
