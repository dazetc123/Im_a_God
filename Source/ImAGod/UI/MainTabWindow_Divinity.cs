using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    public partial class MainTabWindow_Divinity : MainTabWindow
    {
        private enum Tab
        {
            Miracles,
            Domains,
            Demigods,
            Rivals,
            Faith
        }

        private const float StatusWidth = 320f;
        private const float RowHeight = 84f;
        private const float RivalRowHeight = 92f;
        private const float IconSize = 64f;
        private const float ButtonWidth = 110f;

        private static readonly Texture2D FaithBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.95f, 0.78f, 0.3f));
        private static readonly Texture2D StrainBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.7f, 0.3f, 0.9f));
        private static readonly Texture2D DoubtBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.75f, 0.25f, 0.2f));
        private static readonly Texture2D RivalBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.55f, 0.2f, 0.25f));

        private Tab tab = Tab.Miracles;
        private Vector2 statusScroll;
        private float statusHeight = 600f;
        private Vector2 scrollPosition;

        public override Vector2 RequestedTabSize => new Vector2(1060f, 720f);

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            Ideo ideo = DivinityUtility.PlayerIdeo;
            if (divinity == null || ideo == null)
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(inRect, "IAG_Reason_NoIdeo".Translate());
                Text.Font = GameFont.Small;
                return;
            }

            Rect status = new Rect(inRect.x, inRect.y, StatusWidth, inRect.height);
            Rect right = new Rect(status.xMax + 16f, inRect.y + TabDrawer.TabHeight, inRect.width - StatusWidth - 16f, inRect.height - TabDrawer.TabHeight);
            DrawStatus(status, divinity, ideo);

            var tabs = new List<TabRecord>
            {
                new TabRecord("IAG_Tab_Miracles".Translate(), () => tab = Tab.Miracles, tab == Tab.Miracles),
                new TabRecord("IAG_Tab_Domains".Translate(), () => tab = Tab.Domains, tab == Tab.Domains),
                new TabRecord("IAG_Tab_Demigods".Translate(), () => tab = Tab.Demigods, tab == Tab.Demigods),
                new TabRecord("IAG_Tab_Rivals".Translate(), () => tab = Tab.Rivals, tab == Tab.Rivals),
                new TabRecord("IAG_Tab_Faith".Translate(), () => tab = Tab.Faith, tab == Tab.Faith)
            };
            Widgets.DrawMenuSection(right);
            TabDrawer.DrawTabs(right, tabs);
            Rect content = right.ContractedBy(8f);
            switch (tab)
            {
                case Tab.Miracles:
                    DrawPowers(content, divinity);
                    break;
                case Tab.Domains:
                    DrawDomains(content, divinity);
                    break;
                case Tab.Demigods:
                    DrawDemigods(content, divinity);
                    break;
                case Tab.Rivals:
                    DrawRivals(content, divinity);
                    break;
                case Tab.Faith:
                    DrawFaith(content, divinity);
                    break;
            }
        }

        private void DrawStatus(Rect rect, GameComponent_Divinity divinity, Ideo ideo)
        {
            Rect view = new Rect(0f, 0f, rect.width - 16f, statusHeight);
            Widgets.BeginScrollView(rect, ref statusScroll, view);
            var list = new Listing_Standard();
            list.Begin(view);

            Text.Font = GameFont.Medium;
            list.Label("IAG_GodOf".Translate(ideo.name));
            Text.Font = GameFont.Small;
            if (divinity.HasPath)
            {
                GUI.color = divinity.path.color;
                list.Label(divinity.path.LabelCap + ": " + divinity.domains.Select(d => d.LabelCap.ToString()).ToCommaList());
                GUI.color = Color.white;
            }
            else if (list.ButtonText("IAG_ChoosePathButton".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ChooseDivinity(null));
            }

            DivinityTierDef tier = divinity.Tier;
            if (tier != null)
            {
                GUI.color = new Color(1f, 0.85f, 0.4f);
                list.Label("IAG_TierLabel".Translate(tier.level, tier.LabelCap));
                GUI.color = Color.white;
                list.Label(tier.description);
            }
            else
            {
                list.Label("IAG_NoTier".Translate());
            }
            list.Gap(6f);

            float cap = divinity.FaithCap;
            LabeledBar(list.GetRect(24f), cap > 0f ? divinity.faith / cap : 0f, FaithBarTex,
                "IAG_FaithBar".Translate(divinity.faith.ToString("0"), cap.ToString("0")), "IAG_FaithTip".Translate(), GameFont.Small);
            list.Gap(4f);

            list.Label("IAG_Followers".Translate(divinity.FollowerCount, divinity.FollowerWeight.ToString("0.#")), -1f, "IAG_FollowersTip".Translate());
            float avgCertainty = divinity.FollowerWeight > 0f ? divinity.Devotion / divinity.FollowerWeight : 0f;
            list.Label("IAG_Devotion".Translate(avgCertainty.ToStringPercent()));
            list.Label("IAG_PassiveFaith".Translate(divinity.PassiveFaithPerDay.ToString("0.#")), -1f, "IAG_PassiveFaithTip".Translate());
            list.Label("IAG_LifetimeFaith".Translate(divinity.lifetimeFaith.ToString("0")));

            LabeledBar(list.GetRect(18f), Mathf.Clamp01(divinity.strain / 10f), StrainBarTex,
                "IAG_Strain".Translate(divinity.strain.ToString("0.0")), "IAG_StrainTip".Translate(), GameFont.Tiny);
            list.Gap(4f);

            if (ImAGodMod.Settings.faithCrisisEnabled)
            {
                DoubtLevel level = divinity.DoubtLevel;
                LabeledBar(list.GetRect(18f), divinity.doubt / GameComponent_Divinity.MaxDoubt, DoubtBarTex,
                    "IAG_DoubtBar".Translate(divinity.doubt.ToString("0"), GameComponent_Divinity.DoubtLevelLabel(level)), DoubtTip(divinity), GameFont.Tiny);
                if (level != DoubtLevel.Faithful)
                {
                    GUI.color = new Color(1f, 0.6f, 0.5f);
                    list.Label(GameComponent_Divinity.DoubtLevelEffects(level));
                    GUI.color = Color.white;
                }
                if (divinity.GodIsSilent)
                    list.Label("IAG_GodSilent".Translate(), -1f, "IAG_GodSilentTip".Translate());
                if (divinity.heretics.Count > 0)
                    list.Label("IAG_Heretics".Translate(divinity.heretics.Select(p => p.LabelShort).ToCommaList()));
            }
            list.Gap(6f);

            Pawn prophet = DivinityUtility.Prophet;
            if (prophet != null)
            {
                Hediff channeling = prophet.health.hediffSet.GetFirstHediffOfDef(IAG_DefOf.IAG_DivineChanneling);
                string burden = channeling == null ? "" : " (" + channeling.LabelCap + ")";
                list.Label("IAG_Prophet".Translate(prophet.Named("PROPHET")) + burden, -1f, "IAG_ProphetTip".Translate());
            }
            else
            {
                list.Label("IAG_NoProphet".Translate(), -1f, "IAG_ProphetTip".Translate());
            }

            if (divinity.ForesightActive)
                list.Label("IAG_ForesightActive".Translate((divinity.foresightUntilTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()));

            DivinityTierDef next = divinity.NextTier;
            if (next != null)
            {
                list.GapLine();
                list.Label("IAG_NextTier".Translate(next.LabelCap));
                list.Label("IAG_NextTierFollowers".Translate(divinity.FollowerCount, next.minFollowers));
                list.Label("IAG_NextTierFaith".Translate(divinity.lifetimeFaith.ToString("0"), next.minLifetimeFaith.ToString("0")));
            }

            List<Prophecy> prophecies = divinity.PendingProphecies.ToList();
            if (prophecies.Any())
            {
                list.GapLine();
                list.Label("IAG_Prophecies".Translate());
                foreach (Prophecy prophecy in prophecies)
                    list.Label("  • " + ProphecyLine(prophecy), -1f, (TipSignal?)(prophecy.details ?? ""));
            }

            statusHeight = list.CurHeight + 12f;
            list.End();
            Widgets.EndScrollView();
        }

        private static void LabeledBar(Rect bar, float fill, Texture2D tex, string label, string tip, GameFont font)
        {
            Widgets.FillableBar(bar, Mathf.Clamp01(fill), tex);
            Text.Font = font;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(bar, label);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(bar, tip);
        }

        private static string ProphecyLine(Prophecy prophecy)
        {
            int ticks = prophecy.fireTick - Find.TickManager.TicksGame;
            return ticks > 0
                ? "IAG_ProphecyEntry".Translate(prophecy.label, ticks.ToStringTicksToPeriod()).ToString()
                : "IAG_ProphecyImminent".Translate(prophecy.label).ToString();
        }

        private static string DoubtTip(GameComponent_Divinity divinity)
        {
            string tip = "IAG_DoubtTip".Translate() + "\n\n" + "IAG_DoubtRate".Translate(divinity.DoubtChangePerDay.ToStringWithSign("0.#"));
            if (divinity.doubtLog.Any())
            {
                tip += "\n\n" + "IAG_DoubtRecent".Translate();
                foreach (DoubtEvent e in divinity.doubtLog.Take(6))
                    tip += "\n  " + e.amount.ToStringWithSign("0") + "  " + e.label;
            }
            return tip;
        }

        private void DrawPowers(Rect rect, GameComponent_Divinity divinity)
        {
            List<DivinePowerDef> powers = DefDatabase<DivinePowerDef>.AllDefsListForReading
                .Where(divinity.IsPowerKnown)
                .OrderBy(p => p.minTier).ThenBy(p => p.order).ToList();
            Rect view = new Rect(0f, 0f, rect.width - 16f, powers.Count * RowHeight);
            Widgets.BeginScrollView(rect, ref scrollPosition, view);
            float y = 0f;
            for (int i = 0; i < powers.Count; i++)
            {
                Rect row = new Rect(0f, y, view.width, RowHeight);
                if (i % 2 == 1)
                    Widgets.DrawLightHighlight(row);
                DrawPowerRow(row.ContractedBy(6f), powers[i], divinity);
                y += RowHeight;
            }
            Widgets.EndScrollView();
        }

        private void DrawPowerRow(Rect rect, DivinePowerDef power, GameComponent_Divinity divinity)
        {
            bool unlocked = divinity.TierLevel >= power.minTier;
            AcceptanceReport canInvoke = divinity.CanInvoke(power);

            Rect icon = new Rect(rect.x, rect.y + (rect.height - IconSize) / 2f, IconSize, IconSize);
            GUI.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            Widgets.DrawTextureFitted(icon, power.Icon, 1f);
            GUI.color = Color.white;

            Rect button = new Rect(rect.xMax - ButtonWidth, rect.y + (rect.height - 34f) / 2f, ButtonWidth, 34f);
            Rect text = new Rect(icon.xMax + 10f, rect.y, button.x - icon.xMax - 20f, rect.height);

            Text.Font = GameFont.Small;
            string header = power.LabelCap + "  —  " + "IAG_PowerCost".Translate(divinity.CostOf(power), power.minTier);
            if (power.requiresProphet)
                header += "  " + "IAG_PowerProphet".Translate();
            Widgets.Label(new Rect(text.x, text.y, text.width, 22f), header);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            string line = power.Worker.EffectSummary() ?? "";
            int cooldown = divinity.CooldownTicksLeft(power);
            if (line.Length > 0)
                line += "  ·  ";
            line += cooldown > 0
                ? "IAG_Reason_Cooldown".Translate(cooldown.ToStringTicksToPeriod()).ToString()
                : "IAG_PowerCooldown".Translate(power.cooldownDays.ToString("0.##")).ToString();
            Widgets.Label(new Rect(text.x, text.y + 22f, text.width, 18f), line);
            GUI.color = Color.white;
            Widgets.Label(new Rect(text.x, text.y + 40f, text.width, text.height - 40f), power.description.Truncate(text.width * 2.2f));
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(text, power.description);

            if (Widgets.ButtonText(button, "IAG_Invoke".Translate(), active: canInvoke.Accepted) && canInvoke.Accepted)
                divinity.BeginInvoke(power);
            if (!canInvoke.Accepted && !canInvoke.Reason.NullOrEmpty())
                TooltipHandler.TipRegion(button, canInvoke.Reason);
        }

        private void DrawRivals(Rect rect, GameComponent_Divinity divinity)
        {
            Rect header = new Rect(rect.x, rect.y, rect.width, 44f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(header, ImAGodMod.Settings.rivalActivity <= 0f ? "IAG_RivalsDisabled".Translate() : "IAG_RivalsIntro".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            List<RivalGod> rivals = divinity.rivals.Where(r => r.IsValid).OrderByDescending(r => r.HostileToPlayer).ThenByDescending(r => r.power).ToList();
            Rect outRect = new Rect(rect.x, header.yMax + 4f, rect.width, rect.height - header.height - 4f);
            if (rivals.Count == 0)
            {
                Widgets.Label(outRect, "IAG_Reason_NoRivals".Translate());
                return;
            }

            DivinePowerDef challenge = DefDatabase<DivinePowerDef>.AllDefsListForReading.FirstOrDefault(p => p.targetMode == DivineTargetMode.RivalGod);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, rivals.Count * RivalRowHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, view);
            float y = 0f;
            for (int i = 0; i < rivals.Count; i++)
            {
                Rect row = new Rect(0f, y, view.width, RivalRowHeight);
                if (i % 2 == 1)
                    Widgets.DrawLightHighlight(row);
                DrawRivalRow(row.ContractedBy(6f), rivals[i], divinity, challenge);
                y += RivalRowHeight;
            }
            Widgets.EndScrollView();
        }

        private void DrawRivalRow(Rect rect, RivalGod rival, GameComponent_Divinity divinity, DivinePowerDef challenge)
        {
            Rect icon = new Rect(rect.x, rect.y + 4f, 48f, 48f);
            GUI.color = rival.ideo.Color;
            Widgets.DrawTextureFitted(icon, rival.ideo.Icon, 1f);
            GUI.color = Color.white;

            Rect button = new Rect(rect.xMax - ButtonWidth, rect.y + 4f, ButtonWidth, 34f);
            Rect text = new Rect(icon.xMax + 10f, rect.y, button.x - icon.xMax - 20f, rect.height);

            Widgets.Label(new Rect(text.x, text.y, text.width, 22f), rival.NameCap + "  —  " + "IAG_RivalTier".Translate(rival.Tier));
            Text.Font = GameFont.Tiny;
            string factions = rival.Factions.Select(f => f.Name).ToCommaList();
            string stance = rival.HostileToPlayer ? "IAG_RivalHostile".Translate().ToString() : "IAG_RivalPeaceful".Translate().ToString();
            GUI.color = rival.HostileToPlayer ? new Color(1f, 0.55f, 0.5f) : new Color(0.7f, 0.9f, 0.7f);
            Widgets.Label(new Rect(text.x, text.y + 22f, text.width, 16f), stance + "  ·  " + "IAG_RivalIdeo".Translate(rival.ideo.name, factions));
            GUI.color = Color.white;

            Rect bar = new Rect(text.x, text.y + 40f, text.width, 16f);
            LabeledBar(bar, rival.power / RivalGod.MaxPower, RivalBarTex,
                "IAG_RivalPower".Translate(rival.power.ToString("0"), RivalGod.MaxPower.ToString("0")), "IAG_RivalPowerTip".Translate(), GameFont.Tiny);

            Text.Font = GameFont.Tiny;
            var notes = new List<string>();
            if (rival.Humbled)
                notes.Add("IAG_RivalHumbled".Translate((rival.humbledUntilTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()));
            float championChance = divinity.ChampionChance(rival);
            if (rival.HostileToPlayer && championChance > 0f)
                notes.Add("IAG_RivalChampionChance".Translate(championChance.ToStringPercent()));
            Widgets.Label(new Rect(text.x, text.y + 58f, text.width, 18f), string.Join("  ·  ", notes));
            Text.Font = GameFont.Small;

            if (challenge != null)
            {
                AcceptanceReport can = divinity.CanInvoke(challenge);
                if (can.Accepted)
                    can = challenge.Worker.CanApplyOnRival(rival);
                if (Widgets.ButtonText(button, challenge.LabelCap, active: can.Accepted) && can.Accepted)
                    divinity.TryApplyOnRival(challenge, rival);
                TooltipHandler.TipRegion(button, can.Accepted ? challenge.description + "\n\n" + challenge.Worker.EffectSummary() : can.Reason);
            }
        }

        private void DrawFaith(Rect rect, GameComponent_Divinity divinity)
        {
            var list = new Listing_Standard();
            list.Begin(rect);

            Text.Font = GameFont.Medium;
            list.Label("IAG_Prophecies".Translate());
            Text.Font = GameFont.Small;
            List<Prophecy> prophecies = divinity.PendingProphecies.ToList();
            if (!prophecies.Any())
                list.Label(divinity.ForesightActive ? "IAG_NoPropheciesYet".Translate() : "IAG_NoForesight".Translate());
            foreach (Prophecy prophecy in prophecies)
            {
                list.Label("• " + ProphecyLine(prophecy));
                if (!prophecy.details.NullOrEmpty())
                {
                    GUI.color = new Color(0.8f, 0.8f, 0.8f);
                    list.Label("    " + prophecy.details.Replace("\n", "\n    "));
                    GUI.color = Color.white;
                }
            }

            if (ImAGodMod.Settings.faithCrisisEnabled)
            {
                list.GapLine();
                Text.Font = GameFont.Medium;
                list.Label("IAG_FaithCrisisHeader".Translate());
                Text.Font = GameFont.Small;
                list.Label("IAG_DoubtBar".Translate(divinity.doubt.ToString("0"), GameComponent_Divinity.DoubtLevelLabel(divinity.DoubtLevel)));
                list.Label("IAG_DoubtRate".Translate(divinity.DoubtChangePerDay.ToStringWithSign("0.#")));
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                list.Label("IAG_DoubtTip".Translate());
                GUI.color = Color.white;
                list.Gap();
                list.Label("IAG_DoubtRecent".Translate());
                if (!divinity.doubtLog.Any())
                    list.Label("  -");
                foreach (DoubtEvent e in divinity.doubtLog)
                    list.Label("  " + e.amount.ToStringWithSign("0") + "   " + e.label + "  (" + (Find.TickManager.TicksGame - e.tick).ToStringTicksToPeriod() + ")");
            }
            list.End();
        }
    }
}
