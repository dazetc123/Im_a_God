using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ImAGod
{
    public class WorldObjectCompProperties_Mission : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_Mission()
        {
            compClass = typeof(WorldObjectComp_Mission);
        }
    }

    /// <summary>How far a settlement has come to believe in the player's god.</summary>
    public class WorldObjectComp_Mission : WorldObjectComp
    {
        public float conversion;
        public bool converted;
        public int lastMissionTick = -MissionUtility.SettlementCooldownTicks;

        private static Texture2D icon;

        public Settlement Settlement => parent as Settlement;

        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            foreach (Gizmo g in base.GetCaravanGizmos(caravan))
                yield return g;
            Settlement settlement = Settlement;
            if (settlement?.Faction == null || settlement.Faction.IsPlayer || DivinityUtility.PlayerIdeo == null)
                yield break;
            var command = new Command_Action
            {
                defaultLabel = "IAG_Preach".Translate(),
                defaultDesc = "IAG_PreachDesc".Translate(MissionUtility.MissionDays.ToString("0.#"), MissionUtility.SuccessChance(caravan, settlement).ToStringPercent()),
                icon = icon ??= DivinityUtility.LoadIcon("ImAGod/Powers/Revelation"),
                action = () => GameComponent_Divinity.Instance.StartMission(caravan, settlement)
            };
            AcceptanceReport report = MissionUtility.CanPreach(caravan, settlement);
            if (!report.Accepted)
                command.Disable(report.Reason);
            yield return command;
        }

        public override string CompInspectStringExtra()
        {
            if (converted)
                return "IAG_SettlementConverted".Translate(DivinityUtility.PlayerIdeo?.name ?? "?");
            if (conversion > 0f)
                return "IAG_SettlementConversion".Translate(conversion.ToStringPercent());
            return null;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref conversion, "iagConversion");
            Scribe_Values.Look(ref converted, "iagConverted");
            Scribe_Values.Look(ref lastMissionTick, "iagLastMissionTick", -MissionUtility.SettlementCooldownTicks);
        }
    }

    /// <summary>A caravan of believers preaching in a settlement.</summary>
    public class Mission : IExposable
    {
        public Caravan caravan;
        public Settlement settlement;
        public int startTick;
        public int endTick;

        public void ExposeData()
        {
            Scribe_References.Look(ref caravan, "caravan");
            Scribe_References.Look(ref settlement, "settlement");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref endTick, "endTick");
        }
    }

    public static class MissionUtility
    {
        public const float MissionDays = 2f;
        public const int SettlementCooldownTicks = 3 * GenDate.TicksPerDay;

        public static WorldObjectComp_Mission MissionComp(this Settlement settlement) => settlement?.GetComponent<WorldObjectComp_Mission>();

        public static bool IsConverted(Settlement settlement) => settlement.MissionComp()?.converted == true;

        public static bool CanBeMissionary(Pawn pawn) =>
            pawn.RaceProps.Humanlike && !pawn.Downed && !pawn.Dead && DivinityUtility.IsFollower(pawn) && !pawn.skills.GetSkill(SkillDefOf.Social).TotallyDisabled && !pawn.IsPrisoner;

        public static List<Pawn> Missionaries(Caravan caravan) => caravan.PawnsListForReading.Where(CanBeMissionary).ToList();

        public static AcceptanceReport CanPreach(Caravan caravan, Settlement settlement)
        {
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            if (divinity.Tier == null)
                return "IAG_NoTier".Translate();
            if (settlement.Faction.HostileTo(Faction.OfPlayer))
                return "IAG_Reason_MissionHostile".Translate(settlement.Faction.Name);
            WorldObjectComp_Mission comp = settlement.MissionComp();
            if (comp == null)
                return false;
            if (comp.converted || settlement.Faction.ideos?.PrimaryIdeo == DivinityUtility.PlayerIdeo)
                return "IAG_Reason_AlreadyBelieves".Translate(settlement.LabelCap);
            if (divinity.missions.Any(m => m.caravan == caravan || m.settlement == settlement))
                return "IAG_Reason_MissionUnderway".Translate();
            int cooldown = comp.lastMissionTick + SettlementCooldownTicks - Find.TickManager.TicksGame;
            if (cooldown > 0)
                return "IAG_Reason_Cooldown".Translate(cooldown.ToStringTicksToPeriod());
            if (Missionaries(caravan).Count == 0)
                return "IAG_Reason_NoMissionary".Translate();
            return true;
        }

        /// <summary>The chance that preaching here wins hearts: the missionaries' eloquence, the god's might, and how the hosts feel.</summary>
        public static float SuccessChance(Caravan caravan, Settlement settlement)
        {
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            List<Pawn> missionaries = Missionaries(caravan);
            if (missionaries.Count == 0)
                return 0f;
            int social = missionaries.Max(p => p.skills.GetSkill(SkillDefOf.Social).Level);
            float chance = 0.2f + 0.03f * social + 0.03f * Mathf.Min(3, missionaries.Count - 1) + 0.06f * divinity.TierLevel;
            if (missionaries.Any(p => divinity.DemigodFor(p) != null))
                chance += 0.15f;
            if (missionaries.Contains(DivinityUtility.Prophet))
                chance += 0.1f;
            chance += settlement.Faction.PlayerGoodwill / 400f;
            RivalGod rival = divinity.RivalFor(settlement.Faction.ideos?.PrimaryIdeo);
            if (rival != null)
                chance -= rival.power / 3000f;
            if (divinity.DoubtLevel >= DoubtLevel.Crisis)
                chance -= 0.1f;
            return Mathf.Clamp(chance, 0.05f, 0.9f);
        }
    }

    /// <summary>
    /// Missions: a caravan of believers preaches in a friendly or neutral settlement for two days. Success brings
    /// the settlement closer to the god; a converted settlement offers faith every day and no longer feeds its old
    /// god; when half a faction's settlements believe, the whole faction embraces the player's ideoligion.
    /// </summary>
    public partial class GameComponent_Divinity
    {
        private const float ConvertedFaithPerDay = 6f;
        private const float FactionConversionShare = 0.5f;

        public List<Mission> missions = new List<Mission>();

        public int ConvertedSettlementCount => Find.WorldObjects.Settlements.Count(s => s.Faction != null && !s.Faction.IsPlayer && MissionUtility.IsConverted(s));

        public void StartMission(Caravan caravan, Settlement settlement)
        {
            if (!MissionUtility.CanPreach(caravan, settlement).Accepted)
                return;
            int now = Find.TickManager.TicksGame;
            missions.Add(new Mission { caravan = caravan, settlement = settlement, startTick = now, endTick = now + Mathf.RoundToInt(MissionUtility.MissionDays * GenDate.TicksPerDay) });
            settlement.MissionComp().lastMissionTick = now;
            Messages.Message("IAG_MissionStarted".Translate(caravan.Label, settlement.LabelCap, MissionUtility.MissionDays.ToString("0.#")), caravan, MessageTypeDefOf.NeutralEvent);
        }

        private void MissionsHourlyTick()
        {
            int converted = ConvertedSettlementCount;
            if (converted > 0)
                AddFaith(converted * ConvertedFaithPerDay / GenDate.HoursPerDay);

            for (int i = missions.Count - 1; i >= 0; i--)
            {
                Mission mission = missions[i];
                Caravan caravan = mission.caravan;
                Settlement settlement = mission.settlement;
                if (caravan == null || caravan.Destroyed || settlement == null || settlement.Destroyed || caravan.Tile != settlement.Tile
                    || settlement.Faction == null || settlement.Faction.HostileTo(Faction.OfPlayer) || MissionUtility.Missionaries(caravan).Count == 0)
                {
                    missions.RemoveAt(i);
                    Messages.Message("IAG_MissionAbandoned".Translate(settlement?.LabelCap ?? "?"), MessageTypeDefOf.NeutralEvent, false);
                    continue;
                }
                if (Find.TickManager.TicksGame >= mission.endTick)
                {
                    missions.RemoveAt(i);
                    ResolveMission(caravan, settlement);
                }
            }
        }

        private void ResolveMission(Caravan caravan, Settlement settlement)
        {
            WorldObjectComp_Mission comp = settlement.MissionComp();
            Faction faction = settlement.Faction;
            List<Pawn> missionaries = MissionUtility.Missionaries(caravan);
            float chance = MissionUtility.SuccessChance(caravan, settlement);
            float roll = Rand.Value;
            RivalGod rival = RivalFor(faction.ideos?.PrimaryIdeo);

            if (roll < chance)
            {
                float gain = Rand.Range(0.25f, 0.4f) + (missionaries.Any(p => DemigodFor(p) != null) ? 0.1f : 0f);
                comp.conversion = Mathf.Min(1f, comp.conversion + gain);
                float faithGained = AddFaith(30f + 10f * TierLevel);
                faction.TryAffectGoodwillWith(Faction.OfPlayer, 5, false, false);
                rival?.Humble(20f);
                foreach (Pawn pawn in missionaries)
                {
                    pawn.ideo.OffsetCertainty(0.05f);
                    DivinityUtility.GainMemory(pawn, IAG_DefOf.IAG_MissionSucceeded);
                    DemigodFor(pawn)?.GainXp(faithGained / missionaries.Count);
                }
                if (comp.conversion >= 1f)
                {
                    ConvertSettlement(settlement);
                }
                else
                {
                    Find.LetterStack.ReceiveLetter("IAG_MissionSuccessLabel".Translate(), "IAG_MissionSuccessText".Translate(settlement.LabelCap, comp.conversion.ToStringPercent(), faithGained.ToString("0")),
                        LetterDefOf.PositiveEvent, caravan);
                }
            }
            else if (roll > 1f - (1f - chance) * 0.35f)
            {
                // The hosts take offence: the missionaries are beaten and driven out.
                faction.TryAffectGoodwillWith(Faction.OfPlayer, -12, true, true);
                Pawn victim = missionaries.RandomElement();
                for (int i = 0; i < 3; i++)
                    victim.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Rand.Range(5f, 9f)));
                AddDoubt(4f, "IAG_DoubtReason_MissionDisaster".Translate(settlement.LabelCap));
                Find.LetterStack.ReceiveLetter("IAG_MissionDisasterLabel".Translate(), "IAG_MissionDisasterText".Translate(settlement.LabelCap, victim.Named("PAWN")),
                    LetterDefOf.NegativeEvent, caravan);
            }
            else
            {
                comp.conversion = Mathf.Min(0.95f, comp.conversion + 0.05f);
                faction.TryAffectGoodwillWith(Faction.OfPlayer, -2, false, false);
                Messages.Message("IAG_MissionFailed".Translate(settlement.LabelCap), caravan, MessageTypeDefOf.NeutralEvent);
            }
        }

        private void ConvertSettlement(Settlement settlement)
        {
            WorldObjectComp_Mission comp = settlement.MissionComp();
            comp.converted = true;
            comp.conversion = 1f;
            Faction faction = settlement.Faction;
            faction.TryAffectGoodwillWith(Faction.OfPlayer, 15, false, false);
            RivalFor(faction.ideos?.PrimaryIdeo)?.Humble(60f);
            AddDoubt(-6f, "IAG_DoubtReason_SettlementConverted".Translate(settlement.LabelCap));
            Find.LetterStack.ReceiveLetter("IAG_SettlementConvertedLabel".Translate(settlement.LabelCap),
                "IAG_SettlementConvertedText".Translate(settlement.LabelCap, faction.Name, ConvertedFaithPerDay.ToString("0")), LetterDefOf.PositiveEvent, settlement);

            List<Settlement> all = Find.WorldObjects.Settlements.Where(s => s.Faction == faction).ToList();
            if (all.Count > 0 && all.Count(MissionUtility.IsConverted) >= Mathf.CeilToInt(all.Count * FactionConversionShare))
                ConvertFaction(faction);
        }

        /// <summary>A whole faction takes the player's ideoligion as its own.</summary>
        private void ConvertFaction(Faction faction)
        {
            Ideo ours = DivinityUtility.PlayerIdeo;
            if (ours == null || faction.ideos == null || faction.ideos.PrimaryIdeo == ours)
                return;
            Ideo old = faction.ideos.PrimaryIdeo;
            string oldGod = RivalFor(old)?.NameCap ?? old?.name ?? "?";
            faction.ideos.SetPrimary(ours);
            faction.TryAffectGoodwillWith(Faction.OfPlayer, 25, false, false);
            RivalFor(old)?.Humble(150f);
            AddDoubt(-15f, "IAG_DoubtReason_FactionConverted".Translate(faction.Name));
            Find.LetterStack.ReceiveLetter("IAG_FactionConvertedLabel".Translate(faction.Name),
                "IAG_FactionConvertedText".Translate(faction.Name, oldGod, ours.name), LetterDefOf.PositiveEvent);
        }

        private void ExposeMissions()
        {
            Scribe_Collections.Look(ref missions, "missions", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                missions ??= new List<Mission>();
                missions.RemoveAll(m => m == null || m.caravan == null || m.settlement == null);
            }
        }
    }
}
