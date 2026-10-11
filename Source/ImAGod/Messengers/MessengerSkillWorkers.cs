using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace ImAGod
{
    public abstract class MessengerSkillWorker
    {
        public MessengerSkillDef def;

        /// <summary>Whether the caster could cast on the target from where it stands now.</summary>
        public virtual bool InRange(Pawn caster, LocalTargetInfo target)
        {
            if (def.target == SkillTarget.Self)
                return true;
            if (!target.IsValid || (caster.Position - target.Cell).LengthHorizontal > def.range)
                return false;
            return !def.needsLineOfSight || GenSight.LineOfSight(caster.Position, target.Cell, caster.Map, true);
        }

        public virtual bool ValidTarget(Pawn caster, LocalTargetInfo target)
        {
            switch (def.target)
            {
                case SkillTarget.Self:
                    return true;
                case SkillTarget.Enemy:
                    return target.Thing is Pawn p && !p.Dead && p.Spawned && p.Map == caster.Map;
                default:
                    return target.IsValid && target.Cell.InBounds(caster.Map) && !target.Cell.Fogged(caster.Map);
            }
        }

        /// <summary>The target the AI would cast on now, or an invalid target if the skill is not worth casting.</summary>
        public virtual LocalTargetInfo AITarget(Pawn caster, List<Pawn> enemies)
        {
            Pawn best = enemies.FirstOrDefault(e => InRange(caster, e));
            return best != null && enemies.Count >= def.aiMinTargets ? best : LocalTargetInfo.Invalid;
        }

        public abstract void Cast(Pawn caster, LocalTargetInfo target);

        /// <summary>A flash on the caster while it gathers its power.</summary>
        public virtual void WarmupEffect(Pawn caster, LocalTargetInfo target, int ticksLeft)
        {
            if (ticksLeft % 10 == 0)
                MessengerUtility.Glow(caster.Map, caster.DrawPos, def.color, 2.5f);
        }

        protected void PlaySound(Map map, IntVec3 cell)
        {
            if (def.Sound != null && Find.CurrentMap == map)
                def.Sound.PlayOneShot(new TargetInfo(cell, map));
        }

        /// <summary>Deals the skill's damage to a thing and applies its burning, hediff and stun.</summary>
        protected void Hit(Pawn caster, Thing victim, float factor = 1f)
        {
            if (victim == null || victim.Destroyed)
                return;
            if (def.damageDef != null && def.damage > 0f)
            {
                float angle = (victim.Position - caster.Position).AngleFlat;
                var dinfo = new DamageInfo(def.damageDef, def.damage * factor * DamageFactor(caster), def.ArmorPenetration, angle, caster);
                victim.TakeDamage(dinfo);
            }
            if (victim.Destroyed)
                return;
            if (def.fireChance > 0f && Rand.Chance(def.fireChance))
                victim.TryAttachFire(0.4f, caster);
            if (victim is Pawn pawn && !pawn.Dead)
            {
                if (def.hediff != null)
                {
                    Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def.hediff) ?? pawn.health.AddHediff(def.hediff);
                    if (hediff != null)
                        hediff.Severity = Mathf.Max(hediff.Severity, def.severity);
                    MessengerUtility.Refresh(hediff);
                }
                if (def.stunTicks > 0)
                    pawn.stances?.stunner?.StunFor(def.stunTicks, caster, false);
            }
        }

        /// <summary>Avatars of rival gods are a little stronger than ordinary messengers.</summary>
        protected static float DamageFactor(Pawn caster) => caster.Messenger()?.avatar == true ? 1.15f : 1f;

        protected static IEnumerable<Thing> EnemiesIn(Pawn caster, IEnumerable<IntVec3> cells)
        {
            var seen = new HashSet<Thing>();
            Map map = caster.Map;
            foreach (IntVec3 c in cells)
            {
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    if ((t is Pawn || t is Building) && t.IsEnemyOf(caster) && seen.Add(t))
                        yield return t;
                }
            }
        }
    }

    /// <summary>A straight beam that pierces every enemy in its path: the Seraphim's holy arc, a beholder's eye laser, acid breath.</summary>
    public class MessengerSkill_Beam : MessengerSkillWorker
    {
        public override LocalTargetInfo AITarget(Pawn caster, List<Pawn> enemies)
        {
            Pawn best = null;
            int bestCount = 0;
            foreach (Pawn e in enemies)
            {
                if (!InRange(caster, e))
                    continue;
                int n = EnemiesIn(caster, MessengerUtility.BeamCells(caster.Map, caster.Position, e.Position, def.range, def.width)).Count(t => t is Pawn);
                if (n > bestCount)
                {
                    best = e;
                    bestCount = n;
                }
            }
            return bestCount >= def.aiMinTargets ? best : LocalTargetInfo.Invalid;
        }

        public override void Cast(Pawn caster, LocalTargetInfo target)
        {
            Map map = caster.Map;
            List<IntVec3> cells = MessengerUtility.BeamCells(map, caster.Position, target.Cell, def.range, def.width);
            foreach (Thing t in EnemiesIn(caster, cells).ToList())
                Hit(caster, t);
            Vector3 from = caster.DrawPos;
            Vector3 dir = (target.CenterVector3 - from).Yto0().normalized;
            Vector3 to = from + dir * def.range;
            MessengerUtility.Beam(map, from, to, def.color, def.width * 1.6f);
            MessengerUtility.Beam(map, from, to, Color.white, def.width * 0.5f);
            for (int i = 0; i < cells.Count; i += 3)
                MessengerUtility.Glow(map, cells[i].ToVector3Shifted(), def.color, 1.2f);
            PlaySound(map, caster.Position);
        }
    }

    /// <summary>A cone of breath: fire, poison or frost on everything in front of the caster.</summary>
    public class MessengerSkill_Cone : MessengerSkillWorker
    {
        public override LocalTargetInfo AITarget(Pawn caster, List<Pawn> enemies)
        {
            Pawn best = null;
            int bestCount = 0;
            foreach (Pawn e in enemies)
            {
                if (!InRange(caster, e))
                    continue;
                int n = EnemiesIn(caster, MessengerUtility.ConeCells(caster.Map, caster.Position, e.Position, def.range, def.angle)).Count(t => t is Pawn);
                if (n > bestCount)
                {
                    best = e;
                    bestCount = n;
                }
            }
            return bestCount >= def.aiMinTargets ? best : LocalTargetInfo.Invalid;
        }

        public override void Cast(Pawn caster, LocalTargetInfo target)
        {
            Map map = caster.Map;
            List<IntVec3> cells = MessengerUtility.ConeCells(map, caster.Position, target.Cell, def.range, def.angle);
            foreach (Thing t in EnemiesIn(caster, cells).ToList())
                Hit(caster, t);
            foreach (IntVec3 c in cells)
            {
                if (Rand.Chance(0.45f))
                    MessengerUtility.Glow(map, c.ToVector3Shifted() + new Vector3(Rand.Range(-0.3f, 0.3f), 0f, Rand.Range(-0.3f, 0.3f)), def.color, Rand.Range(1.2f, 2.2f));
                if (def.damageDef == DamageDefOf.Flame && Rand.Chance(def.fireChance * 0.5f))
                    FireUtility.TryStartFireIn(c, map, 0.3f, caster);
                if (def.hediff != null && def.damage <= 0f && Rand.Chance(0.15f))
                    FleckMaker.ThrowSmoke(c.ToVector3Shifted(), map, 1.2f);
            }
            PlaySound(map, caster.Position);
        }
    }

    /// <summary>Lightning that leaps from enemy to enemy.</summary>
    public class MessengerSkill_Chain : MessengerSkillWorker
    {
        public override void Cast(Pawn caster, LocalTargetInfo target)
        {
            Map map = caster.Map;
            var struck = new List<Thing>();
            Thing current = target.Thing;
            Vector3 from = caster.DrawPos;
            for (int i = 0; i <= def.jumps && current != null; i++)
            {
                MessengerUtility.Beam(map, from, current.DrawPos, def.color, 0.9f);
                MessengerUtility.Beam(map, from, current.DrawPos, Color.white, 0.3f);
                FleckMaker.ThrowLightningGlow(current.DrawPos, map, 1.5f);
                FleckMaker.ThrowMicroSparks(current.DrawPos, map);
                struck.Add(current);
                from = current.DrawPos;
                Hit(caster, current, i == 0 ? 1f : 0.75f);
                IntVec3 at = current.Position;
                current = MessengerUtility.EnemiesNear(caster, at, def.jumpRange)
                    .Where(p => !struck.Contains(p))
                    .OrderBy(p => (p.Position - at).LengthHorizontalSquared)
                    .FirstOrDefault();
            }
            if (Find.CurrentMap == map)
                SoundDefOf.Thunder_OnMap.PlayOneShot(new TargetInfo(target.Cell, map));
        }
    }

    /// <summary>A single strike on one enemy: a paralysing gaze.</summary>
    public class MessengerSkill_Strike : MessengerSkillWorker
    {
        public override void Cast(Pawn caster, LocalTargetInfo target)
        {
            Map map = caster.Map;
            MessengerUtility.Beam(map, caster.DrawPos, target.CenterVector3, def.color, 0.5f);
            MessengerUtility.Ring(map, target.CenterVector3, def.color, 1.8f);
            Hit(caster, target.Thing);
            PlaySound(map, target.Cell);
        }
    }

    /// <summary>The caster vanishes and reappears next to its target.</summary>
    public class MessengerSkill_Blink : MessengerSkillWorker
    {
        private const float MinDistance = 8f;

        public override LocalTargetInfo AITarget(Pawn caster, List<Pawn> enemies)
        {
            // Only worth it when the nearest enemy is far: close the distance at once.
            Pawn nearest = enemies.FirstOrDefault();
            if (nearest == null || (nearest.Position - caster.Position).LengthHorizontal < MinDistance || !InRange(caster, nearest))
                return LocalTargetInfo.Invalid;
            return nearest;
        }

        public override void Cast(Pawn caster, LocalTargetInfo target)
        {
            if (!MessengerUtility.TryFindLandingCell(caster, target.Cell, 2, out IntVec3 cell))
                return;
            MessengerUtility.Teleport(caster, cell, def.color);
            PlaySound(caster.Map, cell);
            if (def.damage > 0f || def.stunTicks > 0)
            {
                foreach (Pawn e in MessengerUtility.EnemiesNear(caster, cell, Mathf.Max(1.5f, def.radius)))
                    Hit(caster, e);
            }
        }
    }

    /// <summary>Hurls the enemies around the caster far away and leaves them dazed.</summary>
    public class MessengerSkill_Banish : MessengerSkillWorker
    {
        public override LocalTargetInfo AITarget(Pawn caster, List<Pawn> enemies) =>
            MessengerUtility.EnemiesNear(caster, caster.Position, def.radius).Count >= def.aiMinTargets ? caster : LocalTargetInfo.Invalid;

        public override void Cast(Pawn caster, LocalTargetInfo target)
        {
            Map map = caster.Map;
            MessengerUtility.Ring(map, caster.DrawPos, def.color, def.radius * 2f);
            foreach (Pawn e in MessengerUtility.EnemiesNear(caster, caster.Position, def.radius).Take(def.count))
            {
                IntVec3 origin = e.Position;
                if (CellFinder.TryFindRandomCellNear(origin, map, 25,
                        c => c.Standable(map) && !c.Fogged(map) && c.GetFirstPawn(map) == null && (c - origin).LengthHorizontal >= 12f, out IntVec3 cell))
                {
                    MessengerUtility.Beam(map, e.DrawPos, cell.ToVector3Shifted(), def.color, 0.4f);
                    MessengerUtility.Teleport(e, cell, def.color);
                }
                Hit(caster, e);
            }
            PlaySound(map, caster.Position);
        }
    }

    /// <summary>Calls lesser spawn to fight beside the caster: winged beholders, the brood of Tiamat.</summary>
    public class MessengerSkill_Summon : MessengerSkillWorker
    {
        public static int AliveSummons(Pawn caster, PawnKindDef kind) =>
            caster.Map.mapPawns.AllPawnsSpawned.Count(p => p.kindDef == kind && !p.Dead && p.Messenger()?.master == caster);

        public override LocalTargetInfo AITarget(Pawn caster, List<Pawn> enemies)
        {
            if (enemies.Count < def.aiMinTargets || AliveSummons(caster, def.pawnKind) + def.count > def.maxSummons)
                return LocalTargetInfo.Invalid;
            return caster;
        }

        public override void Cast(Pawn caster, LocalTargetInfo target)
        {
            Map map = caster.Map;
            int n = Mathf.Min(def.count, def.maxSummons - AliveSummons(caster, def.pawnKind));
            Lord lord = caster.GetLord();
            for (int i = 0; i < n; i++)
            {
                if (!CellFinder.TryFindRandomCellNear(caster.Position, map, 4, c => c.Standable(map) && c.GetFirstPawn(map) == null, out IntVec3 cell))
                    continue;
                CompMessenger master = caster.Messenger();
                Pawn spawn = GameComponent_Divinity.Instance.SpawnMessenger(def.pawnKind, caster.Faction, cell, map, def.lifetimeDays, caster, false, master?.rivalIdeo);
                lord?.AddPawn(spawn);
                MessengerUtility.Pillar(map, cell.ToVector3Shifted(), def.color, 1.5f, 5f);
            }
            MessengerUtility.Ring(map, caster.DrawPos, def.color, 6f);
            PlaySound(map, caster.Position);
        }
    }

    /// <summary>Heals and wards the caster's allies around it.</summary>
    public class MessengerSkill_Aura : MessengerSkillWorker
    {
        public override LocalTargetInfo AITarget(Pawn caster, List<Pawn> enemies)
        {
            if (enemies.Count == 0)
                return LocalTargetInfo.Invalid;
            bool hurt = MessengerUtility.AlliesNear(caster, caster.Position, def.radius).Any(p => p.health.hediffSet.hediffs.Any(h => h is Hediff_Injury));
            return hurt || enemies.Count >= def.aiMinTargets ? caster : LocalTargetInfo.Invalid;
        }

        public override void Cast(Pawn caster, LocalTargetInfo target)
        {
            Map map = caster.Map;
            foreach (Pawn ally in MessengerUtility.AlliesNear(caster, caster.Position, def.radius).ToList())
            {
                float left = def.heal;
                foreach (Hediff_Injury injury in ally.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
                {
                    if (left <= 0f)
                        break;
                    float amount = Mathf.Min(left, injury.Severity);
                    injury.Heal(amount);
                    left -= amount;
                }
                if (def.hediff != null)
                {
                    Hediff ward = ally.health.hediffSet.GetFirstHediffOfDef(def.hediff) ?? ally.health.AddHediff(def.hediff);
                    MessengerUtility.Refresh(ward);
                }
                MessengerUtility.Glow(map, ally.DrawPos, def.color, 2f);
                FleckMaker.ThrowMetaIcon(ally.Position, map, FleckDefOf.HealingCross);
            }
            MessengerUtility.Ring(map, caster.DrawPos, def.color, def.radius * 2f);
            MessengerUtility.Pillar(map, caster.DrawPos, def.color, 3f, 9f);
            PlaySound(map, caster.Position);
        }
    }

    /// <summary>Marks a spot and, after a short delay, brings a pillar of light (or of chaos) down on it.</summary>
    public class MessengerSkill_Pillar : MessengerSkillWorker
    {
        public override LocalTargetInfo AITarget(Pawn caster, List<Pawn> enemies)
        {
            Pawn best = null;
            int bestCount = 0;
            foreach (Pawn e in enemies)
            {
                if (!InRange(caster, e))
                    continue;
                int n = MessengerUtility.EnemiesNear(caster, e.Position, def.radius).Count;
                bool friendsNear = MessengerUtility.AlliesNear(caster, e.Position, def.radius + 1f).Any(a => a != caster && !a.IsMessenger());
                if (!friendsNear && n > bestCount)
                {
                    best = e;
                    bestCount = n;
                }
            }
            return bestCount >= def.aiMinTargets ? best : LocalTargetInfo.Invalid;
        }

        public override void Cast(Pawn caster, LocalTargetInfo target)
        {
            Map map = caster.Map;
            IntVec3 cell = target.Cell;
            MessengerUtility.Ring(map, cell.ToVector3Shifted(), def.color, def.radius * 2f);
            MessengerUtility.Pillar(map, cell.ToVector3Shifted(), def.color, 0.6f, 12f);
            GameComponent_Divinity.Instance.ScheduleStrike(new PendingStrike
            {
                map = map,
                cell = cell,
                tick = Find.TickManager.TicksGame + def.delayTicks,
                skill = def,
                caster = caster
            });
        }

        public void Strike(Pawn caster, Map map, IntVec3 cell)
        {
            MessengerUtility.Pillar(map, cell.ToVector3Shifted(), def.color, def.radius * 1.4f, 14f);
            MessengerUtility.Pillar(map, cell.ToVector3Shifted(), Color.white, def.radius * 0.5f, 14f);
            var ignored = new List<Thing>();
            if (caster != null)
                ignored.AddRange(MessengerUtility.AlliesNear(caster, cell, def.radius + 2f));
            float damage = def.damage * (caster != null ? DamageFactor(caster) : 1f);
            GenExplosion.DoExplosion(cell, map, def.radius, def.damageDef ?? DamageDefOf.Bomb, caster, Mathf.RoundToInt(damage), def.ArmorPenetration,
                ignoredThings: ignored, chanceToStartFire: def.fireChance, screenShakeFactor: 2f);
            if (Find.CurrentMap == map)
                SoundDefOf.Thunder_OnMap.PlayOneShot(new TargetInfo(cell, map));
        }
    }

    /// <summary>A terrible roar: the enemies around the caster are stunned and some flee in terror.</summary>
    public class MessengerSkill_Roar : MessengerSkillWorker
    {
        public override LocalTargetInfo AITarget(Pawn caster, List<Pawn> enemies) =>
            MessengerUtility.EnemiesNear(caster, caster.Position, def.radius).Count >= def.aiMinTargets ? caster : LocalTargetInfo.Invalid;

        public override void Cast(Pawn caster, LocalTargetInfo target)
        {
            Map map = caster.Map;
            foreach (Pawn e in MessengerUtility.EnemiesNear(caster, caster.Position, def.radius))
            {
                Hit(caster, e);
                if (def.mentalState != null && !e.RaceProps.IsMechanoid && !e.Dead && Rand.Chance(def.chance))
                    e.mindState?.mentalStateHandler?.TryStartMentalState(def.mentalState, def.LabelCap, true);
            }
            MessengerUtility.Ring(map, caster.DrawPos, def.color, def.radius * 2f);
            MessengerUtility.Ring(map, caster.DrawPos, def.color, def.radius);
            if (Find.CurrentMap == map)
            {
                Find.CameraDriver.shaker.DoShake(1.2f);
                SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(map);
            }
        }
    }

    /// <summary>A strike scheduled by a pillar skill, waiting for its moment.</summary>
    public class PendingStrike : IExposable
    {
        public Map map;
        public IntVec3 cell;
        public int tick;
        public MessengerSkillDef skill;
        public Pawn caster;

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Defs.Look(ref skill, "skill");
            Scribe_References.Look(ref caster, "caster");
        }
    }
}
