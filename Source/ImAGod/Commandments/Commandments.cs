using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>
    /// A commandment the god can lay upon its followers for a few days: either a prohibition (eat no meat, shed no
    /// blood) or a task (raise buildings, pray often). Keeping it rewards faith; breaking or failing it breeds doubt.
    /// </summary>
    public class CommandmentDef : Def
    {
        public Type workerClass;
        public DivinePathDef path;
        public float durationDays = 3f;
        public float rewardFaith = 60f;
        public float rewardDoubt = -10f;
        public float failDoubt = 8f;

        public RecordDef record;
        public ThoughtDef thought;
        public float goalFlat;
        public float goalPerFollower;
        public string forbid;

        [Unsaved] private CommandmentWorker workerInt;

        public CommandmentWorker Worker
        {
            get
            {
                if (workerInt == null)
                {
                    workerInt = (CommandmentWorker)Activator.CreateInstance(workerClass);
                    workerInt.def = this;
                }
                return workerInt;
            }
        }
    }

    public class Commandment : IExposable
    {
        public CommandmentDef def;
        public int startTick;
        public int endTick;
        public float progress;
        public float goal;

        public bool IsGoal => def.Worker.IsGoal;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref endTick, "endTick");
            Scribe_Values.Look(ref progress, "progress");
            Scribe_Values.Look(ref goal, "goal");
        }
    }

    public abstract class CommandmentWorker
    {
        public CommandmentDef def;

        public virtual bool IsGoal => false;

        /// <summary>Returns true when the deed breaks a prohibition or advances a task.</summary>
        public virtual bool OnRecord(Pawn pawn, RecordDef record, float amount, Commandment state) => false;

        public virtual bool OnThought(Pawn pawn, ThoughtDef thought, Commandment state) => false;

        public virtual bool OnIngested(Pawn pawn, Thing food, Commandment state) => false;

        public virtual bool OnPrayed(Pawn pawn, Commandment state) => false;
    }

    /// <summary>Forbids a deed recorded in records, such as killing.</summary>
    public class CommandmentWorker_ForbidRecord : CommandmentWorker
    {
        public override bool OnRecord(Pawn pawn, RecordDef record, float amount, Commandment state) => record == def.record && amount > 0f;
    }

    /// <summary>Forbids eating meat or taking drugs.</summary>
    public class CommandmentWorker_ForbidIngest : CommandmentWorker
    {
        public override bool OnIngested(Pawn pawn, Thing food, Commandment state)
        {
            if (def.forbid == "Drug")
                return food.def.ingestible != null && food.def.ingestible.drugCategory != DrugCategory.None;
            if (food.def.IsMeat || food is Corpse)
                return true;
            CompIngredients ingredients = food.TryGetComp<CompIngredients>();
            return ingredients != null && ingredients.ingredients.Any(i => i.IsMeat);
        }
    }

    public class CommandmentWorker_GoalRecord : CommandmentWorker
    {
        public override bool IsGoal => true;

        public override bool OnRecord(Pawn pawn, RecordDef record, float amount, Commandment state)
        {
            if (record != def.record)
                return false;
            state.progress += amount;
            return true;
        }
    }

    public class CommandmentWorker_GoalThought : CommandmentWorker
    {
        public override bool IsGoal => true;

        public override bool OnThought(Pawn pawn, ThoughtDef thought, Commandment state)
        {
            if (thought != def.thought)
                return false;
            state.progress += 1f;
            return true;
        }
    }

    public class CommandmentWorker_GoalPrayers : CommandmentWorker
    {
        public override bool IsGoal => true;

        public override bool OnPrayed(Pawn pawn, Commandment state)
        {
            state.progress += 1f;
            return true;
        }
    }

    public partial class GameComponent_Divinity
    {
        private const int CommandmentCooldownTicks = 5 * GenDate.TicksPerDay;

        public Commandment commandment;
        private int lastCommandmentEndTick = -CommandmentCooldownTicks;

        public int CommandmentCooldownLeft => Mathf.Max(0, lastCommandmentEndTick + CommandmentCooldownTicks - Find.TickManager.TicksGame);

        public IEnumerable<CommandmentDef> AvailableCommandments =>
            DefDatabase<CommandmentDef>.AllDefsListForReading.Where(c => c.path == null || c.path == path);

        public AcceptanceReport CanIssueCommandment()
        {
            if (tier == null)
                return "IAG_NoTier".Translate();
            if (commandment != null)
                return "IAG_Reason_CommandmentActive".Translate();
            int cooldown = CommandmentCooldownLeft;
            if (cooldown > 0)
                return "IAG_Reason_Cooldown".Translate(cooldown.ToStringTicksToPeriod());
            return true;
        }

        public void IssueCommandment(CommandmentDef def)
        {
            if (!CanIssueCommandment().Accepted)
                return;
            int now = Find.TickManager.TicksGame;
            commandment = new Commandment
            {
                def = def,
                startTick = now,
                endTick = now + Mathf.RoundToInt(def.durationDays * GenDate.TicksPerDay),
                goal = Mathf.Max(1f, Mathf.Round(def.goalFlat + def.goalPerFollower * followerCount))
            };
            string what = commandment.IsGoal ? "IAG_CommandmentGoal".Translate(commandment.goal.ToString("0")).ToString() : "";
            Find.LetterStack.ReceiveLetter("IAG_CommandmentLabel".Translate(def.LabelCap),
                "IAG_CommandmentText".Translate(def.description, what, (commandment.endTick - now).ToStringTicksToPeriod(), def.rewardFaith.ToString("0")),
                LetterDefOf.NeutralEvent);
            DivineVfx.Global(Find.CurrentMap, false);
        }

        private void CommandmentDeed(Func<CommandmentWorker, bool> deed)
        {
            if (commandment == null)
                return;
            if (!deed(commandment.def.Worker))
                return;
            if (!commandment.IsGoal)
                EndCommandment(false);
            else if (commandment.progress >= commandment.goal)
                EndCommandment(true);
        }

        private void CommandmentsHourlyTick()
        {
            if (commandment != null && Find.TickManager.TicksGame >= commandment.endTick)
                EndCommandment(!commandment.IsGoal);
        }

        private void EndCommandment(bool kept)
        {
            CommandmentDef def = commandment.def;
            commandment = null;
            lastCommandmentEndTick = Find.TickManager.TicksGame;
            if (kept)
            {
                float gained = AddFaith(def.rewardFaith);
                AddDoubt(def.rewardDoubt, "IAG_DoubtReason_CommandmentKept".Translate(def.label));
                foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
                {
                    if (DivinityUtility.IsFollower(pawn))
                        DivinityUtility.GainMemory(pawn, IAG_DefOf.IAG_CommandmentKept);
                }
                Find.LetterStack.ReceiveLetter("IAG_CommandmentKeptLabel".Translate(), "IAG_CommandmentKeptText".Translate(def.label, gained.ToString("0")), LetterDefOf.PositiveEvent);
            }
            else
            {
                AddDoubt(def.failDoubt, "IAG_DoubtReason_CommandmentBroken".Translate(def.label));
                Find.LetterStack.ReceiveLetter("IAG_CommandmentBrokenLabel".Translate(), "IAG_CommandmentBrokenText".Translate(def.label), LetterDefOf.NegativeEvent);
            }
        }

        private static bool CountsForCommandment(Pawn pawn) => pawn != null && pawn.Faction == Faction.OfPlayer && DivinityUtility.IsFollower(pawn);

        public void Notify_Ingested(Pawn pawn, Thing food)
        {
            if (commandment != null && CountsForCommandment(pawn))
                CommandmentDeed(w => w.OnIngested(pawn, food, commandment));
        }

        public void Notify_Prayed(Pawn pawn)
        {
            if (commandment != null && CountsForCommandment(pawn))
                CommandmentDeed(w => w.OnPrayed(pawn, commandment));
        }

        private void CommandmentRecord(Pawn pawn, RecordDef record, float amount)
        {
            if (commandment != null && CountsForCommandment(pawn))
                CommandmentDeed(w => w.OnRecord(pawn, record, amount, commandment));
        }

        private void CommandmentThought(Pawn pawn, ThoughtDef thought)
        {
            if (commandment != null && CountsForCommandment(pawn))
                CommandmentDeed(w => w.OnThought(pawn, thought, commandment));
        }

        private void ExposeCommandments()
        {
            Scribe_Deep.Look(ref commandment, "commandment");
            Scribe_Values.Look(ref lastCommandmentEndTick, "lastCommandmentEndTick", -CommandmentCooldownTicks);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && commandment?.def == null)
                commandment = null;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    public static class Patch_Thing_Ingested
    {
        public static void Prefix(Thing __instance, Pawn ingester)
        {
            if (ingester != null && __instance != null)
                GameComponent_Divinity.Instance?.Notify_Ingested(ingester, __instance);
        }
    }
}
