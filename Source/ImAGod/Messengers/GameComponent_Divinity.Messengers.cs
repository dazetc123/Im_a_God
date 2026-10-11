using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ImAGod
{
    /// <summary>
    /// Divine messengers on the maps: the great messenger the player's god may send (a Seraphim for Creation,
    /// Tiamat for Chaos), the spawn they summon, and the avatars rival gods send against the colony.
    /// </summary>
    public partial class GameComponent_Divinity
    {
        public const int MessengerUpkeepInterval = 60;
        private const float AvatarPowerLoss = 0.4f;
        private const int AvatarHumbledDays = 15;
        private const float AvatarSlainFaithBase = 150f;
        private const float AvatarSlainFaithPerTier = 50f;
        private const float AvatarSlainDoubt = -15f;

        private readonly List<Pawn> activeMessengers = new List<Pawn>();
        private readonly List<Pawn> corpsesToVanish = new List<Pawn>();
        private List<PendingStrike> strikes = new List<PendingStrike>();

        public IEnumerable<Pawn> ActiveMessengers => activeMessengers.Where(p => p.Spawned && !p.Dead);

        /// <summary>The great messenger of the player's god now in the mortal world, if any. There can be only one.</summary>
        public Pawn PlayerGreatMessenger =>
            ActiveMessengers.FirstOrDefault(p => p.Faction == Faction.OfPlayer && p.Messenger()?.Props.minion == false);

        public void RegisterMessenger(Pawn pawn)
        {
            if (!activeMessengers.Contains(pawn))
                activeMessengers.Add(pawn);
        }

        public void UnregisterMessenger(Pawn pawn) => activeMessengers.Remove(pawn);

        public void ScheduleStrike(PendingStrike strike) => strikes.Add(strike);

        private void MessengersTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = strikes.Count - 1; i >= 0; i--)
            {
                PendingStrike strike = strikes[i];
                if (strike.map == null || strike.skill == null || !Find.Maps.Contains(strike.map))
                {
                    strikes.RemoveAt(i);
                }
                else if (now >= strike.tick)
                {
                    strikes.RemoveAt(i);
                    (strike.skill.Worker as MessengerSkill_Pillar)?.Strike(strike.caster != null && !strike.caster.Dead ? strike.caster : null, strike.map, strike.cell);
                }
            }

            if (corpsesToVanish.Count > 0)
            {
                foreach (Pawn pawn in corpsesToVanish)
                {
                    Corpse corpse = pawn.Corpse;
                    if (corpse != null && corpse.Spawned)
                    {
                        MessengerUtility.Glow(corpse.Map, corpse.DrawPos, Color.white, 3f);
                        corpse.Destroy();
                    }
                }
                corpsesToVanish.Clear();
            }

            if (now % MessengerUpkeepInterval == 0)
            {
                foreach (Pawn pawn in activeMessengers.ToList())
                    pawn.Messenger()?.Upkeep();
            }
        }

        /// <summary>Brings a messenger into the world.</summary>
        /// <param name="lifetimeDays">How long it stays, or 0 to stay until slain.</param>
        public Pawn SpawnMessenger(PawnKindDef kind, Faction faction, IntVec3 cell, Map map, float lifetimeDays, Pawn master, bool avatar, Ideo rivalIdeo)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(kind, faction);
            CompMessenger comp = pawn.Messenger();
            if (comp != null)
            {
                comp.expiresTick = lifetimeDays > 0f ? Find.TickManager.TicksGame + Mathf.RoundToInt(lifetimeDays * GenDate.TicksPerDay) : -1;
                comp.avatar = avatar;
                comp.rivalIdeo = rivalIdeo;
                comp.master = master;
            }
            pawn.health.AddHediff(IAG_DefOf.IAG_DivineBody);
            if (avatar)
                pawn.health.AddHediff(IAG_DefOf.IAG_RivalAvatar);
            GenSpawn.Spawn(pawn, cell, map, WipeMode.VanishOrMoveAside);
            return pawn;
        }

        /// <summary>The god sends its great messenger to fight for the colony.</summary>
        public Pawn SummonGreatMessenger(PawnKindDef kind, IntVec3 cell, Map map, float lifetimeDays)
        {
            Pawn pawn = SpawnMessenger(kind, Faction.OfPlayer, cell, map, lifetimeDays, null, false, null);
            Color color = DivineVfx.PathColor;
            MessengerUtility.Pillar(map, cell.ToVector3Shifted(), color, 5f, 18f);
            MessengerUtility.Ring(map, cell.ToVector3Shifted(), color, 12f);
            foreach (Pawn follower in map.mapPawns.FreeColonistsSpawned)
            {
                if (DivinityUtility.IsFollower(follower))
                    DivinityUtility.GainMemory(follower, IAG_DefOf.IAG_MessengerDescended);
            }
            Find.LetterStack.ReceiveLetter("IAG_MessengerArrivedLabel".Translate(kind.LabelCap),
                "IAG_MessengerArrivedText".Translate(kind.label, (Mathf.RoundToInt(lifetimeDays * GenDate.TicksPerDay)).ToStringTicksToPeriod()), LetterDefOf.PositiveEvent, pawn);
            return pawn;
        }

        /// <summary>A messenger of the player's god leaves the mortal world in a pillar of light.</summary>
        public void Ascend(Pawn pawn, string messageKey)
        {
            if (pawn == null || pawn.Destroyed)
                return;
            if (pawn.Spawned)
            {
                Map map = pawn.Map;
                MessengerUtility.Pillar(map, pawn.DrawPos, DivineVfx.PathColor, 3f, 14f);
                MessengerUtility.Ring(map, pawn.DrawPos, DivineVfx.PathColor, 5f);
                if (!pawn.Messenger().Props.minion)
                    Messages.Message(messageKey.Translate(pawn.LabelShortCap), new TargetInfo(pawn.Position, map), MessageTypeDefOf.NeutralEvent);
                pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.Vanished);
                pawn.DeSpawn();
            }
            if (!pawn.Destroyed)
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
        }

        public void Notify_MessengerKilled(Pawn pawn, CompMessenger comp, Map map, DamageInfo? dinfo)
        {
            if (comp.avatar)
            {
                Notify_AvatarSlain(pawn, comp, map, dinfo?.Instigator as Pawn);
                return;
            }
            // Messengers and their spawn leave no flesh behind.
            corpsesToVanish.Add(pawn);
            if (comp.PlayerControlled && !comp.Props.minion)
                Messages.Message("IAG_MessengerFell".Translate(pawn.LabelShortCap), MessageTypeDefOf.NegativeEvent);
        }

        private void Notify_AvatarSlain(Pawn avatar, CompMessenger comp, Map map, Pawn killer)
        {
            RivalGod rival = RivalFor(comp.rivalIdeo);
            string name = rival?.NameCap ?? comp.rivalIdeo?.name ?? "?";
            if (rival != null)
            {
                rival.Humble(rival.power * AvatarPowerLoss);
                rival.humbledUntilTick = Find.TickManager.TicksGame + AvatarHumbledDays * GenDate.TicksPerDay;
            }
            float gained = AddFaith(AvatarSlainFaithBase + AvatarSlainFaithPerTier * TierLevel);
            AddDoubt(AvatarSlainDoubt, "IAG_DoubtReason_AvatarSlain".Translate(name));
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
            {
                if (DivinityUtility.IsFollower(pawn))
                    DivinityUtility.GainMemory(pawn, IAG_DefOf.IAG_AvatarSlain);
            }
            DemigodFor(killer)?.GainXp(gained);
            if (map != null && avatar.PositionHeld.IsValid)
                DivineVfx.Miracle(map, avatar.PositionHeld, 4f, true);
            Find.LetterStack.ReceiveLetter("IAG_AvatarSlainLabel".Translate(name), "IAG_AvatarSlainText".Translate(name, gained.ToString("0"), AvatarHumbledDays),
                LetterDefOf.PositiveEvent, new TargetInfo(avatar.PositionHeld, map));
        }

        /// <summary>A rival god sends its avatar, with an escort of its faithful, to destroy the colony.</summary>
        public Pawn SendAvatar(RivalGod rival, Faction faction, Map map)
        {
            if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReachColony(c), map, CellFinder.EdgeRoadChance_Hostile, out IntVec3 cell))
                return null;
            PawnKindDef kind = MessengerUtility.AvatarKindFor(rival);
            Pawn avatar = SpawnMessenger(kind, faction, cell, map, 0f, null, true, rival.ideo);
            avatar.Name = new NameSingle("IAG_AvatarName".Translate(rival.NameCap, kind.label));
            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, false, false, false, false, false), map, new List<Pawn> { avatar });
            MessengerUtility.Pillar(map, cell.ToVector3Shifted(), new Color(0.6f, 0.2f, 0.9f), 5f, 18f);
            return avatar;
        }

        private void ExposeMessengers()
        {
            Scribe_Collections.Look(ref strikes, "messengerStrikes", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                strikes ??= new List<PendingStrike>();
                strikes.RemoveAll(s => s == null || s.skill == null);
            }
        }
    }
}
