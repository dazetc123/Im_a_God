using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    public enum SkillTarget
    {
        Enemy,
        Cell,
        Self
    }

    /// <summary>
    /// A skill of a divine messenger (a Seraphim, Tiamat and their spawn). Skills are cast by the messenger's AI, or
    /// ordered by the player through the messenger's gizmos. Fields are interpreted by the worker class.
    /// </summary>
    public class MessengerSkillDef : Def
    {
        public Type workerClass;
        public string iconPath;
        public SkillTarget target = SkillTarget.Enemy;

        public float range = 20f;
        public bool needsLineOfSight = true;
        public int cooldownTicks = 600;
        public int warmupTicks = 30;

        // Shape
        public float radius;
        public float width = 0.9f;
        public float angle = 45f;
        public int delayTicks;

        // Effect
        public DamageDef damageDef;
        public float damage;
        public float armorPenetration;
        /// <summary>Armor penetration used instead when Combat Extended is active (mm RHA for sharp, MPa for blunt).</summary>
        public float armorPenetrationCE = -1f;
        public float fireChance;
        public HediffDef hediff;
        public float severity = 1f;
        public int stunTicks;
        public int count = 1;
        public int jumps;
        public float jumpRange = 6f;
        public float heal;
        public MentalStateDef mentalState;
        public float chance = 1f;
        public PawnKindDef pawnKind;
        public float lifetimeDays = 1f;
        public int maxSummons = 4;

        // Presentation
        public Color color = Color.white;
        public string sound;

        // AI
        /// <summary>The AI casts the skill only when at least this many enemies would be caught by it.</summary>
        public int aiMinTargets = 1;
        public float aiPriority = 1f;

        [Unsaved] private MessengerSkillWorker workerInt;
        [Unsaved] private Texture2D iconInt;
        [Unsaved] private SoundDef soundInt;
        [Unsaved] private bool soundResolved;

        public MessengerSkillWorker Worker
        {
            get
            {
                if (workerInt == null)
                {
                    workerInt = (MessengerSkillWorker)Activator.CreateInstance(workerClass);
                    workerInt.def = this;
                }
                return workerInt;
            }
        }

        public Texture2D Icon => iconInt ??= DivinityUtility.LoadIcon(iconPath);

        public SoundDef Sound
        {
            get
            {
                if (!soundResolved)
                {
                    soundResolved = true;
                    soundInt = sound.NullOrEmpty() ? null : DefDatabase<SoundDef>.GetNamedSilentFail(sound);
                }
                return soundInt;
            }
        }

        public float ArmorPenetration => MessengerUtility.CombatExtended && armorPenetrationCE >= 0f ? armorPenetrationCE : armorPenetration;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors())
                yield return e;
            if (workerClass == null || !typeof(MessengerSkillWorker).IsAssignableFrom(workerClass))
                yield return "workerClass must be a subclass of ImAGod.MessengerSkillWorker";
        }
    }

    /// <summary>Makes a pawn a divine messenger: its skills, how fast it heals, and whether it is a lesser summoned spawn.</summary>
    public class CompProperties_Messenger : CompProperties
    {
        public List<MessengerSkillDef> skills = new List<MessengerSkillDef>();
        public float regenPerDay = 120f;
        /// <summary>Lesser spawn summoned by a greater messenger, which do not count against the one-messenger limit.</summary>
        public bool minion;

        public CompProperties_Messenger()
        {
            compClass = typeof(CompMessenger);
        }
    }

    public static class MessengerUtility
    {
        private static bool? combatExtended;
        private static readonly string[] ChaosMemes = { "Raider", "Supremacist", "PainIsVirtue", "Cannibal", "Bloodfeeding", "Darkness", "Inhuman" };

        public static bool CombatExtended => combatExtended ??= ModsConfig.IsActive("CETeam.CombatExtended");

        /// <summary>The form a rival god's avatar takes: Tiamat for cruel faiths, a Seraphim otherwise.</summary>
        public static PawnKindDef AvatarKindFor(RivalGod rival)
        {
            Ideo ideo = rival.ideo;
            bool chaos = ideo != null && (ideo.memes.Any(m => ChaosMemes.Contains(m.defName)) || ideo.id % 3 == 0);
            return chaos ? IAG_DefOf.IAG_Tiamat : IAG_DefOf.IAG_Seraphim;
        }

        public static CompMessenger Messenger(this Pawn pawn) => pawn?.TryGetComp<CompMessenger>();

        public static bool IsMessenger(this Pawn pawn) => pawn.Messenger() != null;

        public static bool IsEnemyOf(this Thing thing, Pawn caster) =>
            thing != null && thing != caster && thing.Spawned && thing.HostileTo(caster) && !(thing is Pawn p && (p.Dead || p.Downed));

        /// <summary>Hostile pawns that can still fight, nearest first.</summary>
        public static List<Pawn> EnemiesNear(Pawn caster, IntVec3 center, float radius)
        {
            float r2 = radius * radius;
            return caster.Map.mapPawns.AllPawnsSpawned
                .Where(p => p.IsEnemyOf(caster) && (p.Position - center).LengthHorizontalSquared <= r2)
                .OrderBy(p => (p.Position - caster.Position).LengthHorizontalSquared)
                .ToList();
        }

        public static IEnumerable<Pawn> AlliesNear(Pawn caster, IntVec3 center, float radius)
        {
            float r2 = radius * radius;
            return caster.Map.mapPawns.AllPawnsSpawned.Where(p => !p.Dead && p.Faction != null && p.Faction == caster.Faction && (p.Position - center).LengthHorizontalSquared <= r2);
        }

        /// <summary>Cells of a straight beam from the caster towards a target, out to a length, of the given half width.</summary>
        public static List<IntVec3> BeamCells(Map map, IntVec3 from, IntVec3 towards, float length, float halfWidth)
        {
            var cells = new List<IntVec3>();
            Vector3 a = from.ToVector3Shifted();
            Vector3 dir = (towards.ToVector3Shifted() - a).Yto0();
            if (dir.sqrMagnitude < 0.01f)
                return cells;
            dir.Normalize();
            foreach (IntVec3 c in GenRadial.RadialCellsAround(from, length, false))
            {
                if (!c.InBounds(map))
                    continue;
                Vector3 v = c.ToVector3Shifted() - a;
                float along = Vector3.Dot(v, dir);
                if (along <= 0.5f || along > length)
                    continue;
                if ((v - dir * along).magnitude <= halfWidth)
                    cells.Add(c);
            }
            return cells;
        }

        /// <summary>Cells of a cone of breath from the caster towards a target, which the caster can see.</summary>
        public static List<IntVec3> ConeCells(Map map, IntVec3 from, IntVec3 towards, float length, float angle)
        {
            var cells = new List<IntVec3>();
            Vector3 a = from.ToVector3Shifted();
            Vector3 dir = (towards.ToVector3Shifted() - a).Yto0();
            if (dir.sqrMagnitude < 0.01f)
                return cells;
            float facing = dir.AngleFlat();
            foreach (IntVec3 c in GenRadial.RadialCellsAround(from, length, false))
            {
                if (!c.InBounds(map))
                    continue;
                Vector3 v = (c.ToVector3Shifted() - a).Yto0();
                if (Mathf.Abs(Mathf.DeltaAngle(facing, v.AngleFlat())) > angle / 2f)
                    continue;
                if (!GenSight.LineOfSight(from, c, map, true))
                    continue;
                cells.Add(c);
            }
            return cells;
        }

        /// <summary>Restarts the countdown of a hediff that wears off.</summary>
        public static void Refresh(Hediff hediff)
        {
            HediffComp_Disappears comp = hediff?.TryGetComp<HediffComp_Disappears>();
            if (comp != null)
                comp.ticksToDisappear = comp.disappearsAfterTicks;
        }

        /// <summary>A glowing line between two points, tinted.</summary>
        public static void Beam(Map map, Vector3 from, Vector3 to, Color color, float width)
        {
            if (map == null)
                return;
            Vector3 d = (to - from).Yto0();
            if (d.sqrMagnitude < 0.01f)
                return;
            FleckCreationData data = FleckMaker.GetDataStatic((from + to) / 2f, map, IAG_DefOf.IAG_Fleck_Beam);
            data.exactScale = new Vector3(width, 1f, d.magnitude);
            data.rotation = d.AngleFlat();
            data.instanceColor = color;
            map.flecks.CreateFleck(data);
        }

        /// <summary>A soft tinted glow.</summary>
        public static void Glow(Map map, Vector3 loc, Color color, float scale)
        {
            if (map == null || !loc.ToIntVec3().InBounds(map))
                return;
            FleckCreationData data = FleckMaker.GetDataStatic(loc, map, IAG_DefOf.IAG_Fleck_Glow, scale);
            data.instanceColor = color;
            data.rotation = Rand.Range(0f, 360f);
            map.flecks.CreateFleck(data);
        }

        public static void Ring(Map map, Vector3 loc, Color color, float scale)
        {
            if (map == null)
                return;
            FleckCreationData data = FleckMaker.GetDataStatic(loc, map, IAG_DefOf.IAG_Fleck_Ring, scale);
            data.instanceColor = color;
            map.flecks.CreateFleck(data);
        }

        public static void Pillar(Map map, Vector3 loc, Color color, float width, float height)
        {
            if (map == null)
                return;
            FleckCreationData data = FleckMaker.GetDataStatic(loc + new Vector3(0f, 0f, height / 2f - 0.5f), map, IAG_DefOf.IAG_Fleck_LightBeam);
            data.exactScale = new Vector3(width, 1f, height);
            data.instanceColor = color;
            map.flecks.CreateFleck(data);
        }

        /// <summary>Moves a pawn to a cell at once, with a flash where it left and where it arrived.</summary>
        public static void Teleport(Pawn pawn, IntVec3 cell, Color color)
        {
            Map map = pawn.Map;
            Glow(map, pawn.DrawPos, color, 3f);
            Ring(map, pawn.DrawPos, color, 2.5f);
            pawn.Position = cell;
            pawn.Notify_Teleported(true, true);
            Glow(map, cell.ToVector3Shifted(), color, 3f);
            Ring(map, cell.ToVector3Shifted(), color, 2.5f);
            FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(), map, 2f);
        }

        public static bool TryFindLandingCell(Pawn pawn, IntVec3 near, int radius, out IntVec3 cell)
        {
            Map map = pawn.Map;
            return CellFinder.TryFindRandomCellNear(near, map, radius,
                c => c.Standable(map) && !c.Fogged(map) && c.GetFirstPawn(map) == null && pawn.CanReach(c, Verse.AI.PathEndMode.OnCell, Danger.Deadly), out cell);
        }
    }
}
