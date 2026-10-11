using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>
    /// A miracle that affects pawns: the targeted pawn (Pawn mode, no radius), the pawns around a cell (Cell mode),
    /// or every matching pawn on the map (wholeMap). Which pawns is set by <c>affects</c>.
    /// </summary>
    public abstract class DivinePowerWorker_Area : DivinePowerWorker
    {
        private bool SingleTarget => def.targetMode == DivineTargetMode.Pawn && def.radius <= 0f && !def.wholeMap;

        protected virtual bool Eligible(Pawn pawn) => DivinityUtility.Matches(pawn, def.affects);

        protected List<Pawn> Affected(LocalTargetInfo target, Map map)
        {
            if (SingleTarget)
                return target.Thing is Pawn p && Eligible(p) ? new List<Pawn> { p } : new List<Pawn>();
            bool everywhere = def.wholeMap || def.targetMode == DivineTargetMode.None;
            return map.mapPawns.AllPawnsSpawned
                .Where(p => Eligible(p) && (everywhere || p.Position.InHorDistOf(target.Cell, def.radius)))
                .ToList();
        }

        public override AcceptanceReport CanApplyOn(LocalTargetInfo target, Map map)
        {
            if (map == null)
                return "IAG_Reason_NoMap".Translate();
            AcceptanceReport report = base.CanApplyOn(target, map);
            if (!report.Accepted)
                return report;
            if (SingleTarget && !(target.Thing is Pawn pawn && Eligible(pawn)))
                return "IAG_Reason_WrongTarget".Translate(("IAG_Affects_" + def.affects).Translate());
            return true;
        }

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            int affected = 0;
            foreach (Pawn pawn in Affected(target, map))
            {
                if (Affect(pawn, map))
                {
                    affected++;
                    GameComponent_Divinity.Instance?.Notify_PawnBlessed(pawn, def);
                    FleckMaker.ThrowMetaIcon(pawn.Position, map, def.affects == PawnFilter.Enemies ? FleckDefOf.IncapIcon : FleckDefOf.Heart);
                }
            }
            if (affected == 0)
            {
                Messages.Message("IAG_Reason_NoOneAffected".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            Messages.Message("IAG_PowerAffected".Translate(def.LabelCap, affected), MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        protected abstract bool Affect(Pawn pawn, Map map);

        public override string EffectSummary()
        {
            string who = ("IAG_Affects_" + def.affects).Translate();
            if (def.wholeMap || def.targetMode == DivineTargetMode.None)
                return "IAG_Effect_WholeMap".Translate(who);
            if (def.radius > 0f)
                return "IAG_Effect_AreaWho".Translate(who, def.radius.ToString("0"));
            return who;
        }
    }

    /// <summary>Gives a hediff (a blessing, a curse or a disease). Severity is the scaled amount, if set.</summary>
    public class DivinePowerWorker_AreaHediff : DivinePowerWorker_Area
    {
        protected override bool Affect(Pawn pawn, Map map)
        {
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def.hediffDef);
            float severity = ScaledAmount;
            if (existing != null)
            {
                if (def.hediffDef.makesSickThought || existing.TryGetComp<HediffComp_Disappears>() == null)
                {
                    if (severity > 0f)
                        existing.Severity = Mathf.Max(existing.Severity, severity);
                    return true;
                }
                pawn.health.RemoveHediff(existing);
            }
            Hediff hediff = HediffMaker.MakeHediff(def.hediffDef, pawn);
            if (severity > 0f)
                hediff.Severity = severity;
            pawn.health.AddHediff(hediff);
            return true;
        }

        public override string EffectSummary() => def.hediffDef.LabelCap + " · " + base.EffectSummary();
    }

    /// <summary>Starts a mental state, or with no mental state given, ends any mental break.</summary>
    public class DivinePowerWorker_AreaMentalState : DivinePowerWorker_Area
    {
        protected override bool Eligible(Pawn pawn) =>
            base.Eligible(pawn) && pawn.RaceProps.Humanlike && !pawn.Downed && (def.mentalState != null || pawn.InMentalState);

        protected override bool Affect(Pawn pawn, Map map)
        {
            if (def.mentalState == null)
            {
                pawn.MentalState?.RecoverFromState();
                return true;
            }
            return pawn.mindState.mentalStateHandler.TryStartMentalState(def.mentalState, "IAG_MiracleReason".Translate(def.label), forced: true);
        }
    }

    /// <summary>Heals injuries and optionally cures diseases and infections.</summary>
    public class DivinePowerWorker_AreaHeal : DivinePowerWorker_Area
    {
        protected override bool Eligible(Pawn pawn) =>
            base.Eligible(pawn) && pawn.health.hediffSet.hediffs.Any(h => h is Hediff_Injury || (def.cureDiseases && IsDisease(h)));

        private static bool IsDisease(Hediff h) => h.def.makesSickThought || h.def.isInfection;

        protected override bool Affect(Pawn pawn, Map map)
        {
            float remaining = ScaledAmount;
            foreach (Hediff_Injury injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>()
                         .OrderByDescending(h => h.Bleeding ? 1 : 0).ThenByDescending(h => h.Severity).ToList())
            {
                if (remaining <= 0f)
                    break;
                float heal = Mathf.Min(remaining, injury.Severity);
                injury.Heal(heal);
                remaining -= heal;
            }
            if (def.cureDiseases)
            {
                foreach (Hediff disease in pawn.health.hediffSet.hediffs.Where(IsDisease).ToList())
                    pawn.health.RemoveHediff(disease);
            }
            FleckMaker.ThrowMetaIcon(pawn.Position, map, FleckDefOf.HealingCross);
            return true;
        }

        public override string EffectSummary() =>
            (ScaledAmount > 0f ? "IAG_Effect_Mend".Translate(ScaledAmount.ToString("0")) + " · " : "") +
            (def.cureDiseases ? "IAG_Effect_Cure".Translate() + " · " : "") + base.EffectSummary();
    }

    /// <summary>Restores health the way a healer mech serum does, several times over.</summary>
    public class DivinePowerWorker_Restore : DivinePowerWorker_Area
    {
        protected override bool Affect(Pawn pawn, Map map)
        {
            int times = Mathf.Max(1, Mathf.RoundToInt(ScaledAmount));
            for (int i = 0; i < times; i++)
                HealthUtility.FixWorstHealthCondition(pawn);
            FleckMaker.ThrowMetaIcon(pawn.Position, map, FleckDefOf.HealingCross);
            return true;
        }

        public override string EffectSummary() => "IAG_Effect_Restore".Translate(Mathf.Max(1, Mathf.RoundToInt(ScaledAmount)));
    }

    /// <summary>Damages pawns directly, ignoring armor.</summary>
    public class DivinePowerWorker_AreaDamage : DivinePowerWorker_Area
    {
        protected override bool Eligible(Pawn pawn) => base.Eligible(pawn) && !pawn.Dead;

        protected override bool Affect(Pawn pawn, Map map)
        {
            pawn.TakeDamage(new DamageInfo(def.damageDef ?? DamageDefOf.Cut, ScaledAmount, armorPenetration: 2f));
            return true;
        }

        public override string EffectSummary() =>
            "IAG_Effect_Damage".Translate(ScaledAmount.ToString("0"), (def.damageDef ?? DamageDefOf.Cut).label) + " · " + base.EffectSummary();
    }

    /// <summary>Gives believers a thought, such as rapture.</summary>
    public class DivinePowerWorker_AreaThought : DivinePowerWorker_Area
    {
        protected override bool Eligible(Pawn pawn) => base.Eligible(pawn) && pawn.needs?.mood != null;

        protected override bool Affect(Pawn pawn, Map map)
        {
            DivinityUtility.GainMemory(pawn, def.thought);
            return true;
        }

        public override string EffectSummary() => def.thought.stages[0].LabelCap + " · " + base.EffectSummary();
    }

    /// <summary>Wears down a prisoner's resistance and will.</summary>
    public class DivinePowerWorker_Allure : DivinePowerWorker_Area
    {
        protected override bool Eligible(Pawn pawn) => pawn.IsPrisonerOfColony && pawn.guest != null;

        protected override bool Affect(Pawn pawn, Map map)
        {
            pawn.guest.resistance = Mathf.Max(0f, pawn.guest.resistance - ScaledAmount);
            pawn.guest.will = Mathf.Max(0f, pawn.guest.will - ScaledAmount * 0.5f);
            return true;
        }

        public override string EffectSummary() => "IAG_Effect_Allure".Translate(ScaledAmount.ToString("0.#"));
    }

    /// <summary>Wins over a prisoner or a downed enemy at once.</summary>
    public class DivinePowerWorker_Enthrall : DivinePowerWorker_Area
    {
        protected override bool Eligible(Pawn pawn) =>
            pawn.RaceProps.Humanlike && (pawn.IsPrisonerOfColony || (pawn.Downed && pawn.HostileTo(Faction.OfPlayer)));

        protected override bool Affect(Pawn pawn, Map map)
        {
            RecruitUtility.Recruit(pawn, Faction.OfPlayer, DivinityUtility.Prophet);
            return true;
        }
    }

    /// <summary>An explosion at the target.</summary>
    public class DivinePowerWorker_Explosion : DivinePowerWorker
    {
        public override bool Apply(LocalTargetInfo target, Map map)
        {
            GenExplosion.DoExplosion(target.Cell, map, def.radius, def.damageDef ?? DamageDefOf.Bomb, null, Mathf.RoundToInt(ScaledAmount), chanceToStartFire: def.fireChance);
            return true;
        }

        public override string EffectSummary() =>
            "IAG_Effect_Explosion".Translate(ScaledAmount.ToString("0"), def.radius.ToString("0.#"));
    }

    /// <summary>Sets fires around the target.</summary>
    public class DivinePowerWorker_Fire : DivinePowerWorker
    {
        private int Count => Mathf.Max(1, Mathf.RoundToInt(ScaledAmount));

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            List<IntVec3> cells = GenRadial.RadialCellsAround(target.Cell, def.radius, true).Where(c => c.InBounds(map) && !c.Filled(map)).InRandomOrder().Take(Count).ToList();
            int started = cells.Count(c => FireUtility.TryStartFireIn(c, map, Rand.Range(0.4f, 0.9f), null));
            if (started == 0)
            {
                Messages.Message("IAG_Reason_NothingBurns".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }

        public override string EffectSummary() => "IAG_Effect_Fire".Translate(Count, def.radius.ToString("0"));
    }

    /// <summary>Calls down a meteorite.</summary>
    public class DivinePowerWorker_Meteor : DivinePowerWorker
    {
        public override AcceptanceReport CanApplyOn(LocalTargetInfo target, Map map)
        {
            AcceptanceReport report = base.CanApplyOn(target, map);
            if (!report.Accepted)
                return report;
            if (target.Cell.Roofed(map))
                return "IAG_Reason_Roofed".Translate();
            return true;
        }

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            SkyfallerMaker.SpawnSkyfaller(ThingDefOf.MeteoriteIncoming, def.thingDef ?? ThingDefOf.ChunkSlagSteel, target.Cell, map);
            return true;
        }
    }

    /// <summary>Changes the weather.</summary>
    public class DivinePowerWorker_Weather : DivinePowerWorker
    {
        public override AcceptanceReport CanApplyOn(LocalTargetInfo target, Map map) =>
            map == null ? "IAG_Reason_NoMap".Translate() : base.CanApplyOn(target, map);

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            map.weatherManager.TransitionTo(def.weather);
            return true;
        }

        public override string EffectSummary() => def.weather.LabelCap;
    }

    /// <summary>Ends harmful game conditions such as toxic fallout or a cold snap.</summary>
    public class DivinePowerWorker_EndConditions : DivinePowerWorker
    {
        public override AcceptanceReport CanApplyOn(LocalTargetInfo target, Map map) =>
            map == null ? "IAG_Reason_NoMap".Translate() : base.CanApplyOn(target, map);

        public override bool Apply(LocalTargetInfo target, Map map)
        {
            var ended = new List<string>();
            foreach (GameConditionManager manager in new[] { map.gameConditionManager, Find.World.gameConditionManager })
            {
                foreach (GameCondition condition in manager.ActiveConditions.Where(c => def.conditions.Contains(c.def)).ToList())
                {
                    ended.Add(condition.LabelCap);
                    condition.End();
                }
            }
            if (ended.Count == 0)
            {
                Messages.Message("IAG_Reason_NoConditions".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            Messages.Message("IAG_ConditionsEnded".Translate(ended.ToCommaList()), MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public override string EffectSummary() => def.conditions.Select(c => c.LabelCap.ToString()).ToCommaList();
    }

    /// <summary>Turns poor ground into rich soil.</summary>
    public class DivinePowerWorker_Terrain : DivinePowerWorker
    {
        public override bool Apply(LocalTargetInfo target, Map map)
        {
            int changed = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(target.Cell, def.radius, true))
            {
                if (!cell.InBounds(map))
                    continue;
                TerrainDef terrain = cell.GetTerrain(map);
                if (terrain.IsWater || terrain.IsFloor || terrain.fertility <= 0f || terrain.fertility >= def.terrain.fertility)
                    continue;
                map.terrainGrid.SetTerrain(cell, def.terrain);
                changed++;
            }
            if (changed == 0)
            {
                Messages.Message("IAG_Reason_NoGround".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }

        public override string EffectSummary() => "IAG_Effect_Terrain".Translate(def.terrain.label, def.radius.ToString("0"));
    }

    /// <summary>Repairs the colony's buildings.</summary>
    public class DivinePowerWorker_Repair : DivinePowerWorker
    {
        public override bool Apply(LocalTargetInfo target, Map map)
        {
            int repaired = 0;
            foreach (Building building in map.listerBuildings.allBuildingsColonist.ToList())
            {
                if (!building.def.useHitPoints || building.HitPoints >= building.MaxHitPoints)
                    continue;
                if (!def.wholeMap && !building.Position.InHorDistOf(target.Cell, def.radius))
                    continue;
                building.HitPoints = Mathf.Min(building.MaxHitPoints, building.HitPoints + Mathf.CeilToInt(building.MaxHitPoints * ScaledAmount));
                repaired++;
            }
            if (repaired == 0)
            {
                Messages.Message("IAG_Reason_NothingToRepair".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            Messages.Message("IAG_Repaired".Translate(repaired), MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public override string EffectSummary() => "IAG_Effect_Repair".Translate(ScaledAmount.ToStringPercent()).ToString() +
                                                  (def.wholeMap ? "" : " · " + "IAG_Effect_Radius".Translate(def.radius.ToString("0")).ToString());
    }

    /// <summary>Finishes construction frames whose materials have all been delivered.</summary>
    public class DivinePowerWorker_CompleteFrames : DivinePowerWorker
    {
        public override bool Apply(LocalTargetInfo target, Map map)
        {
            Pawn builder = DivinityUtility.Prophet;
            if (builder == null || builder.MapHeld != map)
                builder = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (builder == null)
                return false;
            List<Frame> frames = GenRadial.RadialDistinctThingsAround(target.Cell, map, def.radius, true).OfType<Frame>()
                .Where(f => f.Faction == Faction.OfPlayer && f.TotalMaterialCost().All(c => f.resourceContainer.TotalStackCountOfDef(c.thingDef) >= c.count))
                .ToList();
            foreach (Frame frame in frames)
                frame.CompleteConstruction(builder);
            if (frames.Count == 0)
            {
                Messages.Message("IAG_Reason_NoFrames".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            Messages.Message("IAG_FramesCompleted".Translate(frames.Count), MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public override string EffectSummary() => "IAG_Effect_Radius".Translate(def.radius.ToString("0"));
    }
}
