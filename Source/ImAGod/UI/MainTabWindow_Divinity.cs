using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    public class MainTabWindow_Divinity : MainTabWindow
    {
        private const float StatusWidth = 320f;
        private const float RowHeight = 84f;
        private const float IconSize = 64f;
        private const float ButtonWidth = 110f;

        private static readonly Texture2D FaithBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.95f, 0.78f, 0.3f));
        private static readonly Texture2D StrainBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.7f, 0.3f, 0.9f));

        private Vector2 scrollPosition;

        public override Vector2 RequestedTabSize => new Vector2(960f, 660f);

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
            Rect powers = new Rect(status.xMax + 16f, inRect.y, inRect.width - StatusWidth - 16f, inRect.height);
            DrawStatus(status, divinity, ideo);
            DrawPowers(powers, divinity);
        }

        private void DrawStatus(Rect rect, GameComponent_Divinity divinity, Ideo ideo)
        {
            var list = new Listing_Standard();
            list.Begin(rect);

            Text.Font = GameFont.Medium;
            list.Label("IAG_GodOf".Translate(ideo.name));
            Text.Font = GameFont.Small;

            DivinityTierDef tier = divinity.Tier;
            if (tier != null)
            {
                GUI.color = new Color(1f, 0.85f, 0.4f);
                list.Label("IAG_TierLabel".Translate(tier.level, tier.LabelCap));
                GUI.color = Color.white;
                list.Label(tier.description, -1f);
            }
            else
            {
                list.Label("IAG_NoTier".Translate());
            }
            list.Gap(6f);

            float cap = divinity.FaithCap;
            Rect bar = list.GetRect(24f);
            Widgets.FillableBar(bar, cap > 0f ? divinity.faith / cap : 0f, FaithBarTex);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(bar, "IAG_FaithBar".Translate(divinity.faith.ToString("0"), cap.ToString("0")));
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(bar, "IAG_FaithTip".Translate());
            list.Gap(4f);

            list.Label("IAG_Followers".Translate(divinity.FollowerCount, divinity.FollowerWeight.ToString("0.#")), -1f, "IAG_FollowersTip".Translate());
            float avgCertainty = divinity.FollowerWeight > 0f ? divinity.Devotion / divinity.FollowerWeight : 0f;
            list.Label("IAG_Devotion".Translate(avgCertainty.ToStringPercent()));
            list.Label("IAG_PassiveFaith".Translate(divinity.PassiveFaithPerDay.ToString("0.#")), -1f, "IAG_PassiveFaithTip".Translate());
            list.Label("IAG_LifetimeFaith".Translate(divinity.lifetimeFaith.ToString("0")));

            Rect strainBar = list.GetRect(18f);
            Widgets.FillableBar(strainBar, Mathf.Clamp01(divinity.strain / 10f), StrainBarTex);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(strainBar, "IAG_Strain".Translate(divinity.strain.ToString("0.0")));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(strainBar, "IAG_StrainTip".Translate());
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

            List<Prophecy> prophecies = divinity.prophecies.Where(p => p.fireTick > Find.TickManager.TicksGame).OrderBy(p => p.fireTick).ToList();
            if (prophecies.Any())
            {
                list.GapLine();
                list.Label("IAG_Prophecies".Translate());
                foreach (Prophecy prophecy in prophecies)
                    list.Label("  • " + "IAG_ProphecyEntry".Translate(prophecy.label, (prophecy.fireTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()));
            }

            list.End();
        }

        private void DrawPowers(Rect rect, GameComponent_Divinity divinity)
        {
            List<DivinePowerDef> powers = DefDatabase<DivinePowerDef>.AllDefsListForReading.OrderBy(p => p.minTier).ThenBy(p => p.order).ToList();
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
            string detail = power.Worker.EffectSummary();
            int cooldown = divinity.CooldownTicksLeft(power);
            string line = detail ?? "";
            if (cooldown > 0)
                line += (line.Length > 0 ? "  ·  " : "") + "IAG_Reason_Cooldown".Translate(cooldown.ToStringTicksToPeriod());
            else
                line += (line.Length > 0 ? "  ·  " : "") + "IAG_PowerCooldown".Translate(power.cooldownDays.ToString("0.##"));
            Widgets.Label(new Rect(text.x, text.y + 22f, text.width, 18f), line);
            GUI.color = Color.white;
            Widgets.Label(new Rect(text.x, text.y + 40f, text.width, text.height - 40f), power.description.Truncate(text.width * 2.2f));
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(text, power.description);

            if (Widgets.ButtonText(button, "IAG_Invoke".Translate(), active: canInvoke.Accepted))
            {
                if (canInvoke.Accepted)
                    divinity.BeginInvoke(power);
            }
            if (!canInvoke.Accepted && !canInvoke.Reason.NullOrEmpty())
                TooltipHandler.TipRegion(button, canInvoke.Reason);
        }
    }
}
