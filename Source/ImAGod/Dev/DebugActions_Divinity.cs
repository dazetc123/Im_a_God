using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>Dev-mode overrides kept by the divinity component. They do nothing unless set from the debug menu.</summary>
    public partial class GameComponent_Divinity
    {
        /// <summary>Miracles cost no faith and have no cooldown. Not saved: it lasts until the game is restarted.</summary>
        public static bool DevFreeMiracles;

        /// <summary>Forced tier level, or -1 to compute the tier from followers and faith as usual.</summary>
        public int devTierOverride = -1;
        public int devBonusPoints;
        private DivineDomainDef devHolyDay;
        private int devHolyDayEndTick;

        private void ExposeDev()
        {
            Scribe_Values.Look(ref devTierOverride, "devTierOverride", -1);
            Scribe_Values.Look(ref devBonusPoints, "devBonusPoints");
            Scribe_Defs.Look(ref devHolyDay, "devHolyDay");
            Scribe_Values.Look(ref devHolyDayEndTick, "devHolyDayEndTick");
        }

        internal void DevSetTier(int level)
        {
            devTierOverride = level;
            Recount();
            if (level < 0)
                highestTier = TierLevel;
            else
                highestTier = Mathf.Max(highestTier, TierLevel);
            UpdateBlessings();
        }

        internal void DevResetCooldowns()
        {
            lastUsedTick.Clear();
            strain = 0f;
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
            {
                Hediff channeling = pawn.health?.hediffSet?.GetFirstHediffOfDef(IAG_DefOf.IAG_DivineChanneling);
                if (channeling != null)
                    pawn.health.RemoveHediff(channeling);
            }
        }

        internal void DevResetPath()
        {
            foreach (Demigod demigod in demigods)
            {
                foreach (DivineNodeDef node in demigod.learned)
                {
                    if (node.ability != null)
                        demigod.pawn?.abilities?.RemoveAbility(node.ability);
                }
                demigod.learned.Clear();
            }
            if (commandment != null && commandment.def.path != null)
                commandment = null;
            path = null;
            domains.Clear();
            unlockedNodes.Clear();
            devHolyDay = null;
            pathDialogDeferred = false;
            DirtyOfferings();
            UpdateBlessings();
            PromptForPathIfNeeded();
        }

        internal void DevEmbrace(DivineDomainDef domain)
        {
            if (domains.Contains(domain))
                return;
            if (path == null)
                path = domain.path;
            domains.Add(domain);
            DirtyOfferings();
            UpdateBlessings();
        }

        internal void DevUnlockAllNodes()
        {
            foreach (DivineNodeDef node in DefDatabase<DivineNodeDef>.AllDefsListForReading)
            {
                if (node.forDemigod || !domains.Contains(node.domain) || unlockedNodes.Contains(node))
                    continue;
                unlockedNodes.Add(node);
                devBonusPoints += node.pointCost;
            }
        }

        internal void DevForceHolyDay(DivineDomainDef domain)
        {
            devHolyDay = domain;
            devHolyDayEndTick = Find.TickManager.TicksGame + GenDate.TicksPerDay;
            lastHolyDayAnnounced = -1;
            HolyDaysHourlyTick();
        }

        internal void DevClearHolyDay() => devHolyDay = null;

        internal bool DevIssuePetition(PetitionKind kind, Map map)
        {
            petitions.RemoveAll(p => p.kind == kind);
            return TryIssue(kind, map, kind == PetitionKind.Healing ? 24 : 48, true);
        }

        internal void DevExpirePetitions()
        {
            foreach (Petition petition in petitions.ToList())
                Ignore(petition);
        }

        internal void DevIssueCommandment(CommandmentDef def)
        {
            commandment = null;
            lastCommandmentEndTick = -CommandmentCooldownTicks;
            IssueCommandment(def);
        }

        internal void DevEndCommandment(bool kept)
        {
            if (commandment != null)
                EndCommandment(kept);
        }

        internal void DevClearCommandmentCooldown() => lastCommandmentEndTick = -CommandmentCooldownTicks;

        internal bool DevStartHeresy() => TryStartHeresy(null, 2f);

        internal void DevIntervene(RivalGod rival, RivalInterventionDef intervention, Map map)
        {
            int now = Find.TickManager.TicksGame;
            rival.lastInterventionTick = now;
            lastAnyInterventionTick = now;
            if (ForesightActive)
                ProphesyIntervention(rival, intervention, map);
            else
                ExecuteIntervention(rival, intervention, map);
        }

        internal void DevAscend(Pawn pawn)
        {
            if (DemigodFor(pawn) == null)
                Ascend(pawn);
        }

        internal void DevUnmake(Pawn pawn)
        {
            Demigod demigod = DemigodFor(pawn);
            if (demigod != null)
                Unmake(demigod);
        }

        internal string DevReport()
        {
            var lines = new List<string>
            {
                $"Tier {TierLevel} (override {devTierOverride}, highest {highestTier}) — followers {followerCount}, weight {followerWeight:0.0}, devotion {devotion:0.00}",
                $"Faith {faith:0}/{FaithCap:0}, lifetime {lifetimeFaith:0}, passive {PassiveFaithPerDay:0.0}/day, strain {strain:0.00}",
                $"Path {path?.defName ?? "none"}, domains [{domains.Select(d => d.defName).ToCommaList()}], points {SpentPoints}/{TotalPoints} (dev +{devBonusPoints})",
                $"Doubt {doubt:0.0} ({DoubtLevel}), heretics {heretics.Count}",
                $"Foresight {(ForesightActive ? (foresightUntilTick - Find.TickManager.TicksGame).ToStringTicksToPeriod() : "off")}, pending prophecies {PendingProphecies.Count()}",
                $"Demigods {demigods.Count}/{MaxDemigods}: {demigods.Select(d => $"{d.pawn?.LabelShort} Lv{d.level}").ToCommaList()}",
                $"Petitions: {petitions.Select(p => p.kind.ToString()).ToCommaList()}",
                $"Commandment: {commandment?.def.defName ?? "none"} {(commandment != null ? $"{commandment.progress:0}/{commandment.goal:0}" : "")}, cooldown {CommandmentCooldownLeft.ToStringTicksToPeriod()}",
                $"Holy day: {CurrentHolyDay?.defName ?? "none"}",
                $"Free miracles: {DevFreeMiracles}"
            };
            lines.AddRange(rivals.Select(r => $"Rival {r.NameCap}: power {r.power:0} (tier {r.Tier}), hostile {r.HostileToPlayer}, humbled {r.Humbled}"));
            return string.Join("\n", lines);
        }
    }

    /// <summary>Dev-mode tools for testing the mod, in the debug actions menu under "I'm a God".</summary>
    public static class DebugActions_Divinity
    {
        private const string Category = "I'm a God";

        private static GameComponent_Divinity Divinity => GameComponent_Divinity.Instance;

        private static void ShowOptions(IEnumerable<DebugMenuOption> options)
        {
            List<DebugMenuOption> list = options.ToList();
            if (list.Count == 0)
            {
                Messages.Message("Nothing to choose from.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        private static DebugMenuOption Option(string label, Action action) => new DebugMenuOption(label, DebugMenuOptionMode.Action, action);

        private static Map HomeMap => Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome ? Find.CurrentMap : Find.AnyPlayerHomeMap;

        // ---------------------------------------------------------------- Faith and tier

        [DebugAction(Category, "Faith +100", allowedGameStates = AllowedGameStates.Playing, displayPriority = 100)]
        private static void FaithSmall() => AddRawFaith(100f);

        [DebugAction(Category, "Faith +1000", allowedGameStates = AllowedGameStates.Playing, displayPriority = 99)]
        private static void FaithLarge() => AddRawFaith(1000f);

        [DebugAction(Category, "Fill faith to cap", allowedGameStates = AllowedGameStates.Playing, displayPriority = 98)]
        private static void FaithFill()
        {
            Divinity.Recount();
            Divinity.faith = Divinity.FaithCap;
        }

        [DebugAction(Category, "Faith to 0", allowedGameStates = AllowedGameStates.Playing, displayPriority = 97)]
        private static void FaithEmpty() => Divinity.faith = 0f;

        /// <summary>Adds faith and lifetime faith directly, ignoring the gain multiplier, the cap and the need for a tier.</summary>
        private static void AddRawFaith(float amount)
        {
            GameComponent_Divinity divinity = Divinity;
            divinity.lifetimeFaith += amount;
            divinity.Recount();
            divinity.faith = divinity.Tier == null ? divinity.faith + amount : Mathf.Min(divinity.faith + amount, divinity.FaithCap);
        }

        [DebugAction(Category, "Set divine tier...", allowedGameStates = AllowedGameStates.Playing, displayPriority = 96)]
        private static void SetTier()
        {
            var options = new List<DebugMenuOption> { Option("Automatic (from followers and faith)", () => Divinity.DevSetTier(-1)) };
            options.AddRange(DefDatabase<DivinityTierDef>.AllDefsListForReading.OrderBy(t => t.level)
                .Select(t => Option($"{t.level}: {t.LabelCap}", () => Divinity.DevSetTier(t.level))));
            options.Insert(1, Option("0: no tier", () => Divinity.DevSetTier(0)));
            ShowOptions(options);
        }

        [DebugAction(Category, "Divine points +5", allowedGameStates = AllowedGameStates.Playing, displayPriority = 95)]
        private static void BonusPoints() => Divinity.devBonusPoints += 5;

        [DebugAction(Category, "Toggle free miracles", allowedGameStates = AllowedGameStates.Playing, displayPriority = 94)]
        private static void ToggleFree()
        {
            GameComponent_Divinity.DevFreeMiracles = !GameComponent_Divinity.DevFreeMiracles;
            Messages.Message("Free miracles: " + (GameComponent_Divinity.DevFreeMiracles ? "ON (no cost, no cooldown)" : "OFF"), MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction(Category, "Reset cooldowns, strain, channeling", allowedGameStates = AllowedGameStates.Playing, displayPriority = 93)]
        private static void ResetCooldowns() => Divinity.DevResetCooldowns();

        [DebugAction(Category, "Log divinity state", allowedGameStates = AllowedGameStates.Playing, displayPriority = 92)]
        private static void LogState() => Log.Message("[I'm a God]\n" + Divinity.DevReport());

        // ---------------------------------------------------------------- Path, domains, tree

        [DebugAction(Category, "Reset path and domains", allowedGameStates = AllowedGameStates.Playing, displayPriority = 80)]
        private static void ResetPath() => Divinity.DevResetPath();

        [DebugAction(Category, "Embrace domain (ignore slots)...", allowedGameStates = AllowedGameStates.Playing, displayPriority = 79)]
        private static void Embrace()
        {
            GameComponent_Divinity divinity = Divinity;
            ShowOptions(DefDatabase<DivineDomainDef>.AllDefsListForReading
                .Where(d => !divinity.domains.Contains(d) && (divinity.path == null || d.path == divinity.path))
                .Select(d => Option($"{d.path.LabelCap}: {d.LabelCap}", () => divinity.DevEmbrace(d))));
        }

        [DebugAction(Category, "Unlock all miracles of embraced domains", allowedGameStates = AllowedGameStates.Playing, displayPriority = 78)]
        private static void UnlockAll() => Divinity.DevUnlockAllNodes();

        [DebugAction(Category, "Forget all miracles (free respec)", allowedGameStates = AllowedGameStates.Playing, displayPriority = 77)]
        private static void FreeRespec() => Divinity.unlockedNodes.Clear();

        // ---------------------------------------------------------------- Crisis and rivals

        [DebugAction(Category, "Doubt +25", allowedGameStates = AllowedGameStates.Playing, displayPriority = 70)]
        private static void DoubtUp() => Divinity.AddDoubt(25f, "Dev");

        [DebugAction(Category, "Doubt -25", allowedGameStates = AllowedGameStates.Playing, displayPriority = 69)]
        private static void DoubtDown() => Divinity.AddDoubt(-25f, "Dev");

        [DebugAction(Category, "Doubt to 0", allowedGameStates = AllowedGameStates.Playing, displayPriority = 68)]
        private static void DoubtClear() => Divinity.doubt = 0f;

        [DebugAction(Category, "Start heresy now", allowedGameStates = AllowedGameStates.Playing, displayPriority = 67)]
        private static void Heresy()
        {
            if (!Divinity.DevStartHeresy())
                Messages.Message("No believer could turn heretic (needs a follower who is not the prophet, and another ideoligion).", MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction(Category, "Rival gods: power +300", allowedGameStates = AllowedGameStates.Playing, displayPriority = 66)]
        private static void RivalPower()
        {
            Divinity.SyncRivals();
            foreach (RivalGod rival in Divinity.rivals)
                rival.power = Mathf.Min(RivalGod.MaxPower, rival.power + 300f);
        }

        [DebugAction(Category, "Rival intervention now...", allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 65)]
        private static void RivalIntervention()
        {
            GameComponent_Divinity divinity = Divinity;
            divinity.SyncRivals();
            Map map = HomeMap;
            if (map == null)
                return;
            ShowOptions(divinity.rivals.Where(r => r.IsValid).Select(rival => Option($"{rival.NameCap} (power {rival.power:0})", () =>
                ShowOptions(DefDatabase<RivalInterventionDef>.AllDefsListForReading.Select(def =>
                    Option(def.LabelCap + (def.Worker.CanDoNow(rival, map) ? "" : " (cannot now)"), () => divinity.DevIntervene(rival, def, map)))))));
        }

        // ---------------------------------------------------------------- Prophecy, petitions, commandments, holy days

        [DebugAction(Category, "Start foresight (3 days, 12h lead)", allowedGameStates = AllowedGameStates.Playing, displayPriority = 60)]
        private static void Foresight() => Divinity.StartForesight(3 * GenDate.TicksPerDay, 12 * GenDate.TicksPerHour);

        [DebugAction(Category, "Issue petition...", allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 59)]
        private static void Petition()
        {
            Map map = HomeMap;
            ShowOptions(Enum.GetValues(typeof(PetitionKind)).Cast<PetitionKind>().Select(kind => Option(kind.ToString(), () =>
            {
                if (map == null || !Divinity.DevIssuePetition(kind, map))
                    Messages.Message("Could not issue: needs a divine tier and a follower on the map.", MessageTypeDefOf.RejectInput, false);
            })));
        }

        [DebugAction(Category, "Expire all petitions (ignored)", allowedGameStates = AllowedGameStates.Playing, displayPriority = 58)]
        private static void ExpirePetitions() => Divinity.DevExpirePetitions();

        [DebugAction(Category, "Issue commandment (ignore cooldown)...", allowedGameStates = AllowedGameStates.Playing, displayPriority = 57)]
        private static void Commandment()
        {
            ShowOptions(Divinity.AvailableCommandments.Select(def => Option(def.LabelCap, () => Divinity.DevIssueCommandment(def))));
        }

        [DebugAction(Category, "End commandment: kept", allowedGameStates = AllowedGameStates.Playing, displayPriority = 56)]
        private static void CommandmentKept() => Divinity.DevEndCommandment(true);

        [DebugAction(Category, "End commandment: broken", allowedGameStates = AllowedGameStates.Playing, displayPriority = 55)]
        private static void CommandmentBroken() => Divinity.DevEndCommandment(false);

        [DebugAction(Category, "Clear commandment cooldown", allowedGameStates = AllowedGameStates.Playing, displayPriority = 54)]
        private static void CommandmentCooldown() => Divinity.DevClearCommandmentCooldown();

        [DebugAction(Category, "Make today a holy day...", allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 53)]
        private static void HolyDay()
        {
            GameComponent_Divinity divinity = Divinity;
            var options = new List<DebugMenuOption> { Option("(clear)", divinity.DevClearHolyDay) };
            options.AddRange(DefDatabase<DivineDomainDef>.AllDefsListForReading.Where(d => d.holyDay >= 0)
                .Select(d => Option($"{d.HolyDayName} ({d.LabelCap})", () => divinity.DevForceHolyDay(d))));
            ShowOptions(options);
        }

        // ---------------------------------------------------------------- Pawn tools

        [DebugAction(Category, "Make follower", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 40)]
        private static void MakeFollower(Pawn pawn)
        {
            Ideo ideo = DivinityUtility.PlayerIdeo;
            if (ideo == null || pawn.ideo == null)
                return;
            pawn.ideo.SetIdeo(ideo);
            pawn.ideo.OffsetCertainty(1f);
            Divinity.Recount();
        }

        [DebugAction(Category, "Make demigod (ignore limits)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 39)]
        private static void MakeDemigod(Pawn pawn)
        {
            if (!pawn.RaceProps.Humanlike)
                return;
            Divinity.DevAscend(pawn);
        }

        [DebugAction(Category, "Demigod: +1 level", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 38)]
        private static void DemigodLevel(Pawn pawn)
        {
            Demigod demigod = Divinity.DemigodFor(pawn);
            demigod?.GainXp(demigod.XpToNext - demigod.xp);
        }

        [DebugAction(Category, "Demigod: max level", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 37)]
        private static void DemigodMax(Pawn pawn)
        {
            Demigod demigod = Divinity.DemigodFor(pawn);
            while (demigod != null && demigod.level < Demigod.MaxLevel)
                demigod.GainXp(demigod.XpToNext - demigod.xp);
        }

        [DebugAction(Category, "Demigod: allow relic again", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 36)]
        private static void DemigodRelic(Pawn pawn)
        {
            Demigod demigod = Divinity.DemigodFor(pawn);
            if (demigod != null)
                demigod.relicBestowed = false;
        }

        [DebugAction(Category, "Demigod: unmake", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 35)]
        private static void DemigodUnmake(Pawn pawn) => Divinity.DevUnmake(pawn);

        [DebugAction(Category, "Make rival champion", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 34)]
        private static void MakeChampion(Pawn pawn)
        {
            if (pawn.health.hediffSet.HasHediff(IAG_DefOf.IAG_Champion))
                return;
            Hediff hediff = pawn.health.AddHediff(IAG_DefOf.IAG_Champion);
            HediffComp_Champion comp = hediff.TryGetComp<HediffComp_Champion>();
            if (comp != null)
                comp.ideo = pawn.Ideo;
        }
    }
}
