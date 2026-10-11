using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ImAGod
{
    /// <summary>
    /// A divine messenger: its skill cooldowns, how long it may stay in the mortal world, and whether it is the
    /// avatar of a rival god. A messenger of the player's god is not drafted; it fights on its own, and the player
    /// guides it with its gizmos.
    /// </summary>
    public class CompMessenger : ThingComp
    {
        private Dictionary<MessengerSkillDef, int> lastCast = new Dictionary<MessengerSkillDef, int>();
        /// <summary>When a messenger of the player's god returns to the heavens, or -1 to stay.</summary>
        public int expiresTick = -1;
        /// <summary>The avatar of a rival god, come to destroy the colony.</summary>
        public bool avatar;
        public Ideo rivalIdeo;
        /// <summary>The greater messenger that summoned this one, if any.</summary>
        public Pawn master;
        public bool autoCast = true;
        /// <summary>Where the player told the messenger to stand guard, or invalid to fight freely.</summary>
        public IntVec3 guardCell = IntVec3.Invalid;

        public CompProperties_Messenger Props => (CompProperties_Messenger)props;

        public Pawn Pawn => (Pawn)parent;

        public bool PlayerControlled => Pawn.Faction == Faction.OfPlayer;

        public int CooldownLeft(MessengerSkillDef skill) =>
            lastCast.TryGetValue(skill, out int last) ? Mathf.Max(0, last + skill.cooldownTicks - Find.TickManager.TicksGame) : 0;

        public bool Ready(MessengerSkillDef skill) => GameComponent_Divinity.DevFreeMiracles && PlayerControlled || CooldownLeft(skill) <= 0;

        public void Notify_Cast(MessengerSkillDef skill) => lastCast[skill] = Find.TickManager.TicksGame;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            GameComponent_Divinity.Instance?.RegisterMessenger(Pawn);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            GameComponent_Divinity.Instance?.UnregisterMessenger(Pawn);
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            GameComponent_Divinity.Instance?.Notify_MessengerKilled(Pawn, this, prevMap, dinfo);
        }

        /// <summary>Called every second by the divinity component: healing, the end of its stay, and falling.</summary>
        public void Upkeep()
        {
            Pawn pawn = Pawn;
            if (pawn.Dead || !pawn.Spawned)
                return;
            if (pawn.Downed)
            {
                // A messenger is never taken captive: the player's returns to the heavens, an enemy's perishes.
                if (PlayerControlled)
                    GameComponent_Divinity.Instance.Ascend(pawn, "IAG_MessengerFell");
                else
                    pawn.Kill(null);
                return;
            }
            if (expiresTick >= 0 && Find.TickManager.TicksGame >= expiresTick)
            {
                GameComponent_Divinity.Instance.Ascend(pawn, "IAG_MessengerDeparted");
                return;
            }
            if (master != null && (master.Dead || !master.Spawned) && Props.minion && !PlayerControlled)
            {
                pawn.Kill(null);
                return;
            }
            float heal = Props.regenPerDay * GameComponent_Divinity.MessengerUpkeepInterval / GenDate.TicksPerDay;
            foreach (Hediff_Injury injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
            {
                if (heal <= 0f)
                    break;
                float amount = Mathf.Min(heal, injury.Severity);
                injury.Heal(amount);
                heal -= amount;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
                yield return g;
            if (!PlayerControlled || Pawn.Dead)
                yield break;

            for (int i = 0; i < Props.skills.Count; i++)
                yield return SkillGizmo(Props.skills[i], i);

            yield return new Command_Toggle
            {
                defaultLabel = "IAG_MessengerAutoCast".Translate(),
                defaultDesc = "IAG_MessengerAutoCastDesc".Translate(),
                icon = TexCommand.FireAtWill,
                isActive = () => autoCast,
                toggleAction = () => autoCast = !autoCast
            };
            yield return new Command_Target
            {
                defaultLabel = "IAG_MessengerGuard".Translate(),
                defaultDesc = "IAG_MessengerGuardDesc".Translate(),
                icon = TexCommand.SquadAttack,
                targetingParams = new TargetingParameters { canTargetLocations = true, canTargetPawns = false, canTargetBuildings = false },
                action = target =>
                {
                    guardCell = target.Cell;
                    Pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, target.Cell), JobTag.Misc);
                }
            };
            if (guardCell.IsValid)
            {
                yield return new Command_Action
                {
                    defaultLabel = "IAG_MessengerRoam".Translate(),
                    defaultDesc = "IAG_MessengerRoamDesc".Translate(),
                    icon = TexCommand.ReleaseAnimals,
                    action = () => guardCell = IntVec3.Invalid
                };
            }
            yield return new Command_Target
            {
                defaultLabel = "IAG_MessengerAttack".Translate(),
                defaultDesc = "IAG_MessengerAttackDesc".Translate(),
                icon = TexCommand.AttackMelee,
                targetingParams = TargetingParameters.ForAttackAny(),
                action = target =>
                {
                    if (target.Thing == null)
                        return;
                    Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                    job.playerForced = true;
                    Pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
            };
            yield return new Command_Action
            {
                defaultLabel = "IAG_MessengerDismiss".Translate(),
                defaultDesc = "IAG_MessengerDismissDesc".Translate(),
                icon = TexCommand.ClearPrioritizedWork,
                action = () => GameComponent_Divinity.Instance.Ascend(Pawn, "IAG_MessengerDeparted")
            };
        }

        private Command SkillGizmo(MessengerSkillDef skill, int index)
        {
            Command command;
            if (skill.target == SkillTarget.Self)
            {
                command = new Command_Action { action = () => OrderCast(index, Pawn) };
            }
            else
            {
                command = new Command_Target
                {
                    targetingParams = skill.target == SkillTarget.Enemy
                        ? new TargetingParameters { canTargetPawns = true, canTargetBuildings = false, canTargetLocations = false, validator = t => t.Thing is Pawn p && p.HostileTo(Pawn) }
                        : new TargetingParameters { canTargetLocations = true, canTargetPawns = true, canTargetBuildings = true },
                    action = target => OrderCast(index, target)
                };
            }
            command.defaultLabel = skill.LabelCap;
            command.defaultDesc = skill.description;
            command.icon = skill.Icon;
            int cooldown = CooldownLeft(skill);
            if (!Ready(skill))
                command.Disable("IAG_Reason_Cooldown".Translate(cooldown.ToStringTicksToPeriod()));
            return command;
        }

        private void OrderCast(int index, LocalTargetInfo target)
        {
            MessengerSkillDef skill = Props.skills[index];
            if (!skill.Worker.ValidTarget(Pawn, target))
            {
                Messages.Message("IAG_Reason_InvalidTarget".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Job job = JobMaker.MakeJob(IAG_DefOf.IAG_MessengerCast, target);
            job.count = index;
            job.playerForced = true;
            Pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public override string CompInspectStringExtra()
        {
            var lines = new List<string>();
            if (avatar)
                lines.Add("IAG_AvatarInspect".Translate(GameComponent_Divinity.Instance?.RivalFor(rivalIdeo)?.NameCap ?? rivalIdeo?.name ?? "?"));
            if (expiresTick >= 0)
                lines.Add("IAG_MessengerTimeLeft".Translate(Mathf.Max(0, expiresTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()));
            if (PlayerControlled && guardCell.IsValid)
                lines.Add("IAG_MessengerGuarding".Translate());
            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref lastCast, "lastCast", LookMode.Def, LookMode.Value);
            Scribe_Values.Look(ref expiresTick, "expiresTick", -1);
            Scribe_Values.Look(ref avatar, "avatar");
            Scribe_References.Look(ref rivalIdeo, "rivalIdeo");
            Scribe_References.Look(ref master, "master");
            Scribe_Values.Look(ref autoCast, "autoCast", true);
            Scribe_Values.Look(ref guardCell, "guardCell", IntVec3.Invalid);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lastCast ??= new Dictionary<MessengerSkillDef, int>();
                lastCast.RemoveAll(kv => kv.Key == null);
            }
        }
    }

    /// <summary>Casts a ready skill when there is a target worth it. Used in both the main and the constant think tree.</summary>
    public class JobGiver_MessengerSkill : ThinkNode_JobGiver
    {
        private const float SearchRadius = 45f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            CompMessenger comp = pawn.Messenger();
            if (comp == null || !pawn.Spawned || pawn.Downed || pawn.InMentalState)
                return null;
            if (pawn.CurJobDef == IAG_DefOf.IAG_MessengerCast || pawn.CurJob?.playerForced == true)
                return null;
            if (comp.PlayerControlled && !comp.autoCast)
                return null;
            IntVec3 center = comp.guardCell.IsValid && comp.PlayerControlled ? comp.guardCell : pawn.Position;
            List<Pawn> enemies = MessengerUtility.EnemiesNear(pawn, center, SearchRadius);
            if (enemies.Count == 0)
                return null;
            List<MessengerSkillDef> skills = comp.Props.skills;
            foreach (int i in Enumerable.Range(0, skills.Count).OrderByDescending(i => skills[i].aiPriority))
            {
                MessengerSkillDef skill = skills[i];
                if (!comp.Ready(skill))
                    continue;
                LocalTargetInfo target = skill.Worker.AITarget(pawn, enemies);
                if (!target.IsValid)
                    continue;
                Job job = JobMaker.MakeJob(IAG_DefOf.IAG_MessengerCast, target);
                job.count = i;
                return job;
            }
            return null;
        }
    }

    /// <summary>Attacks the nearest enemy in melee. A guarding messenger stays near its post.</summary>
    public class JobGiver_MessengerFight : ThinkNode_JobGiver
    {
        private const float FreeRadius = 70f;
        private const float GuardRadius = 14f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            CompMessenger comp = pawn.Messenger();
            if (comp == null || pawn.Downed)
                return null;
            bool guarding = comp.PlayerControlled && comp.guardCell.IsValid;
            IntVec3 center = guarding ? comp.guardCell : pawn.Position;
            float radius = comp.PlayerControlled ? (guarding ? GuardRadius : FreeRadius) : 999f;
            Pawn enemy = MessengerUtility.EnemiesNear(pawn, center, radius)
                .FirstOrDefault(e => pawn.CanReach(e, PathEndMode.Touch, Danger.Deadly));
            if (enemy == null)
                return null;
            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, enemy);
            job.maxNumMeleeAttacks = 4;
            job.expiryInterval = 300;
            job.checkOverrideOnExpire = true;
            job.attackDoorIfTargetLost = true;
            return job;
        }
    }

    /// <summary>With no enemy about: the player's messenger keeps to its post or near the colony; an enemy's marches on the colony.</summary>
    public class JobGiver_MessengerIdle : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            CompMessenger comp = pawn.Messenger();
            if (comp == null)
                return null;
            Map map = pawn.Map;
            IntVec3 root;
            if (comp.PlayerControlled)
            {
                root = comp.guardCell.IsValid ? comp.guardCell : (comp.master != null && comp.master.Spawned ? comp.master.Position : pawn.Position);
            }
            else
            {
                Pawn colonist = map.mapPawns.FreeColonistsSpawned.Where(p => pawn.CanReach(p, PathEndMode.Touch, Danger.Deadly)).RandomElementWithFallback();
                root = colonist?.Position ?? pawn.Position;
            }
            if ((pawn.Position - root).LengthHorizontal > 5f)
            {
                IntVec3 dest = CellFinder.RandomClosewalkCellNear(root, map, 3);
                if (pawn.CanReach(dest, PathEndMode.OnCell, Danger.Deadly))
                {
                    Job go = JobMaker.MakeJob(JobDefOf.Goto, dest);
                    go.expiryInterval = 600;
                    go.checkOverrideOnExpire = true;
                    return go;
                }
            }
            Job wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            wait.expiryInterval = 180;
            wait.checkOverrideOnExpire = true;
            return wait;
        }
    }

    /// <summary>Gets in range of the target, gathers power, then casts a skill.</summary>
    public class JobDriver_MessengerCast : JobDriver
    {
        private MessengerSkillDef Skill
        {
            get
            {
                List<MessengerSkillDef> skills = pawn.Messenger()?.Props.skills;
                return skills != null && job.count >= 0 && job.count < skills.Count ? skills[job.count] : null;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Skill == null || !Skill.Worker.ValidTarget(pawn, job.targetA));
            this.FailOn(() => pawn.Downed);

            Toil approach = ToilMaker.MakeToil("MessengerApproach");
            approach.initAction = () =>
            {
                MessengerSkillDef skill = Skill;
                CompMessenger comp = pawn.Messenger();
                if (skill == null || comp == null || !comp.Ready(skill))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                if (skill.Worker.InRange(pawn, job.targetA))
                {
                    ReadyForNextToil();
                    return;
                }
                pawn.pather.StartPath(job.targetA, job.targetA.HasThing ? PathEndMode.Touch : PathEndMode.OnCell);
            };
            approach.tickAction = () =>
            {
                if (Skill.Worker.InRange(pawn, job.targetA))
                {
                    pawn.pather.StopDead();
                    ReadyForNextToil();
                }
            };
            approach.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return approach;

            Toil warmup = ToilMaker.MakeToil("MessengerWarmup");
            warmup.initAction = () => pawn.pather.StopDead();
            warmup.tickAction = () =>
            {
                if (job.targetA.IsValid && Skill.target != SkillTarget.Self)
                    pawn.rotationTracker.FaceTarget(job.targetA);
                Skill.Worker.WarmupEffect(pawn, job.targetA, ticksLeftThisToil);
            };
            warmup.defaultCompleteMode = ToilCompleteMode.Delay;
            warmup.defaultDuration = Skill?.warmupTicks ?? 30;
            yield return warmup;

            Toil cast = ToilMaker.MakeToil("MessengerCast");
            cast.initAction = () =>
            {
                MessengerSkillDef skill = Skill;
                skill.Worker.Cast(pawn, skill.target == SkillTarget.Self ? (LocalTargetInfo)pawn : job.targetA);
                pawn.Messenger()?.Notify_Cast(skill);
            };
            cast.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return cast;
        }
    }

    /// <summary>A constant-think-tree node that never gives a job: a placeholder so the tree is never empty.</summary>
    public class JobGiver_Nothing : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn) => null;
    }
}
