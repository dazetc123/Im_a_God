using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ImAGod
{
    /// <summary>The Domains (the god's skill trees) and Demigods pages of the divinity tab.</summary>
    public partial class MainTabWindow_Divinity
    {
        private const float TreeHeaderHeight = 54f;
        private const float NodeHeight = 82f;
        private const float RowSpacing = 118f;
        private const float DemigodListWidth = 220f;
        private const float DemigodRowHeight = 68f;

        private static readonly Color UnlockedColor = new Color(1f, 0.82f, 0.35f);
        private static readonly Color AvailableColor = new Color(0.9f, 0.9f, 0.9f);
        private static readonly Color LockedColor = new Color(0.35f, 0.35f, 0.35f);
        private static readonly Texture2D XpBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.45f, 0.75f, 0.95f));

        private Demigod selectedDemigod;

        private void DrawDomains(Rect rect, GameComponent_Divinity divinity)
        {
            if (!divinity.HasPath)
            {
                DrawNoPath(rect);
                return;
            }

            Rect header = new Rect(rect.x, rect.y, rect.width, 48f);
            string points = "IAG_DivinePoints".Translate(divinity.AvailablePoints, divinity.TotalPoints);
            Widgets.Label(new Rect(header.x, header.y, 330f, 24f), points);
            float next = divinity.FaithToNextBonusPoint;
            string pointsTip = "IAG_DivinePointsTip".Translate();
            if (next > 0f)
                pointsTip += "\n\n" + "IAG_NextBonusPoint".Translate(next.ToString("0"));
            TooltipHandler.TipRegion(new Rect(header.x, header.y, 330f, 24f), pointsTip);
            Widgets.Label(new Rect(header.x, header.y + 24f, 330f, 24f), "IAG_DomainSlots".Translate(divinity.domains.Count, divinity.DomainSlots));
            if (divinity.CanEmbraceDomain && Widgets.ButtonText(new Rect(header.xMax - 220f, header.y + 6f, 220f, 34f), "IAG_EmbraceButton".Translate()))
                Find.WindowStack.Add(new Dialog_ChooseDivinity(divinity.path));
            Rect respec = new Rect(header.xMax - (divinity.CanEmbraceDomain ? 450f : 220f), header.y + 6f, 220f, 34f);
            AcceptanceReport canRespec = divinity.CanRespec();
            if (Widgets.ButtonText(respec, "IAG_RespecButton".Translate(divinity.RespecCost), active: canRespec.Accepted) && canRespec.Accepted)
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("IAG_ConfirmRespec".Translate(divinity.RespecCost), divinity.Respec, destructive: true));
            TooltipHandler.TipRegion(respec, canRespec.Accepted ? "IAG_RespecTip".Translate().ToString() : canRespec.Reason);

            Rect tree = new Rect(rect.x, header.yMax + 6f, rect.width, rect.height - header.height - 6f);
            DrawTrees(tree, divinity.domains, d => d.GodNodes, divinity.IsUnlocked, divinity.CanUnlock, node =>
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "IAG_ConfirmUnlock".Translate(node.LabelCap, node.pointCost), () => divinity.Unlock(node)));
            }, divinity);
        }

        private void DrawDemigods(Rect rect, GameComponent_Divinity divinity)
        {
            if (!divinity.HasPath)
            {
                DrawNoPath(rect);
                return;
            }

            Rect header = new Rect(rect.x, rect.y, rect.width, 44f);
            Widgets.Label(new Rect(header.x, header.y, header.width, 22f), "IAG_DemigodCount".Translate(divinity.demigods.Count, divinity.MaxDemigods));
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(header.x, header.y + 22f, header.width, 22f), "IAG_DemigodHint".Translate(GameComponent_Divinity.AscensionMinCertainty.ToStringPercent()));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect listRect = new Rect(rect.x, header.yMax + 4f, DemigodListWidth, rect.height - header.height - 4f);
            Rect treeRect = new Rect(listRect.xMax + 10f, listRect.y, rect.width - DemigodListWidth - 10f, listRect.height);
            if (selectedDemigod != null && !divinity.demigods.Contains(selectedDemigod))
                selectedDemigod = null;
            selectedDemigod ??= divinity.demigods.FirstOrDefault();

            float y = listRect.y;
            foreach (Demigod demigod in divinity.demigods)
            {
                DrawDemigodRow(new Rect(listRect.x, y, listRect.width, DemigodRowHeight), demigod);
                y += DemigodRowHeight + 4f;
            }
            if (divinity.demigods.Count == 0)
                Widgets.Label(listRect, "IAG_NoDemigods".Translate());

            if (selectedDemigod == null)
                return;
            Demigod selected = selectedDemigod;
            Widgets.Label(new Rect(treeRect.x, treeRect.y, treeRect.width - 380f, 24f),
                "IAG_DemigodPoints".Translate(selected.pawn.Named("PAWN"), selected.Points, selected.level));
            GUI.color = UnlockedColor;
            Widgets.Label(new Rect(treeRect.x, treeRect.y + 22f, treeRect.width - 380f, 22f), selected.Title);
            GUI.color = Color.white;
            Rect relicButton = new Rect(treeRect.xMax - 370f, treeRect.y, 180f, 34f);
            AcceptanceReport canRelic = divinity.CanBestowRelic(selected);
            if (Widgets.ButtonText(relicButton, "IAG_RelicButton".Translate(GameComponent_Divinity.RelicCost), active: canRelic.Accepted) && canRelic.Accepted)
                divinity.BestowRelic(selected);
            TooltipHandler.TipRegion(relicButton, canRelic.Accepted ? "IAG_RelicTip".Translate().ToString() : canRelic.Reason);
            Rect resetButton = new Rect(treeRect.xMax - 180f, treeRect.y, 180f, 34f);
            AcceptanceReport canReset = divinity.CanRespecDemigod(selected);
            if (Widgets.ButtonText(resetButton, "IAG_DemigodResetButton".Translate(GameComponent_Divinity.DemigodRespecCost), active: canReset.Accepted) && canReset.Accepted)
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("IAG_ConfirmDemigodReset".Translate(selected.pawn.Named("PAWN"), GameComponent_Divinity.DemigodRespecCost), () => divinity.RespecDemigod(selected), destructive: true));
            if (!canReset.Accepted)
                TooltipHandler.TipRegion(resetButton, canReset.Reason);
            Rect tree = new Rect(treeRect.x, treeRect.y + 46f, treeRect.width, treeRect.height - 46f);
            DrawTrees(tree, divinity.domains, d => d.DemigodNodes, selected.Knows, n => divinity.CanLearn(selected, n), node =>
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "IAG_ConfirmLearn".Translate(selected.pawn.Named("PAWN"), node.LabelCap, node.pointCost), () => divinity.Learn(selected, node)));
            }, divinity);
        }

        private void DrawDemigodRow(Rect rect, Demigod demigod)
        {
            if (selectedDemigod == demigod)
                Widgets.DrawHighlightSelected(rect);
            else if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);
            Rect portrait = new Rect(rect.x + 2f, rect.y + 2f, rect.height - 4f, rect.height - 4f);
            GUI.DrawTexture(portrait, PortraitsCache.Get(demigod.pawn, portrait.size, Rot4.South));
            Rect text = new Rect(portrait.xMax + 6f, rect.y + 4f, rect.width - portrait.width - 12f, rect.height - 8f);
            Widgets.Label(new Rect(text.x, text.y, text.width, 22f), demigod.pawn.LabelShortCap);
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(text.x, text.y + 22f, text.width, 16f), "IAG_DemigodLevelShort".Translate(demigod.level) + " · " + demigod.Title);
            Text.Font = GameFont.Small;
            Rect bar = new Rect(text.x, text.y + 42f, text.width, 12f);
            Widgets.FillableBar(bar, demigod.level >= Demigod.MaxLevel ? 1f : demigod.xp / demigod.XpToNext, XpBarTex);
            TooltipHandler.TipRegion(rect, "IAG_DemigodXpTip".Translate(demigod.xp.ToString("0"), demigod.XpToNext.ToString("0")));
            if (Widgets.ButtonInvisible(rect))
                selectedDemigod = demigod;
        }

        private static void DrawNoPath(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 60f), "IAG_NoPathYet".Translate());
            if (Widgets.ButtonText(new Rect(rect.x, rect.y + 64f, 260f, 38f), "IAG_ChoosePathButton".Translate()))
                Find.WindowStack.Add(new Dialog_ChooseDivinity(null));
        }

        /// <summary>Draws one tree column per domain, rows top to bottom, with lines to prerequisites.</summary>
        private static void DrawTrees(Rect rect, List<DivineDomainDef> domains, Func<DivineDomainDef, IEnumerable<DivineNodeDef>> nodesOf,
            Func<DivineNodeDef, bool> isUnlocked, Func<DivineNodeDef, AcceptanceReport> canUnlock, Action<DivineNodeDef> onClick,
            GameComponent_Divinity divinity)
        {
            if (domains.Count == 0)
                return;
            float columnWidth = rect.width / Mathf.Max(domains.Count, 3);
            for (int c = 0; c < domains.Count; c++)
            {
                DivineDomainDef domain = domains[c];
                Rect column = new Rect(rect.x + c * columnWidth, rect.y, columnWidth - 8f, rect.height);
                Widgets.DrawBoxSolid(column, new Color(domain.color.r * 0.1f, domain.color.g * 0.1f, domain.color.b * 0.1f, 0.8f));

                Rect head = new Rect(column.x + 6f, column.y + 6f, column.width - 12f, TreeHeaderHeight - 8f);
                Widgets.DrawTextureFitted(new Rect(head.x, head.y, 40f, 40f), domain.Icon, 1f);
                GUI.color = domain.color;
                Widgets.Label(new Rect(head.x + 46f, head.y, head.width - 46f, 24f), domain.LabelCap);
                GUI.color = Color.white;
                if (domain.blessing != null)
                {
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(head.x + 46f, head.y + 22f, head.width - 46f, 18f), "IAG_DomainBlessing".Translate(domain.blessing.LabelCap));
                    Text.Font = GameFont.Small;
                }
                TooltipHandler.TipRegion(head, Dialog_ChooseDivinity.DomainTooltip(domain));

                List<DivineNodeDef> nodes = nodesOf(domain).ToList();
                var rects = new Dictionary<DivineNodeDef, Rect>();
                foreach (IGrouping<int, DivineNodeDef> row in nodes.GroupBy(n => n.row).OrderBy(g => g.Key))
                {
                    List<DivineNodeDef> inRow = row.OrderBy(n => n.column).ToList();
                    float nodeWidth = Mathf.Min(120f, (column.width - 12f - 8f * (inRow.Count - 1)) / inRow.Count);
                    float total = nodeWidth * inRow.Count + 8f * (inRow.Count - 1);
                    float x = column.x + (column.width - total) / 2f;
                    float y = column.y + TreeHeaderHeight + 12f + row.Key * RowSpacing;
                    foreach (DivineNodeDef node in inRow)
                    {
                        rects[node] = new Rect(x, y, nodeWidth, NodeHeight);
                        x += nodeWidth + 8f;
                    }
                }

                foreach (DivineNodeDef node in nodes)
                {
                    foreach (DivineNodeDef pre in node.prerequisites)
                    {
                        if (!rects.TryGetValue(pre, out Rect from))
                            continue;
                        Rect to = rects[node];
                        Color line = isUnlocked(pre) ? UnlockedColor : LockedColor;
                        Widgets.DrawLine(new Vector2(from.center.x, from.yMax), new Vector2(to.center.x, to.y), line, 2f);
                    }
                }

                foreach (DivineNodeDef node in nodes)
                    DrawNode(rects[node], node, isUnlocked(node), canUnlock(node), onClick, domain, divinity);
            }
        }

        private static void DrawNode(Rect rect, DivineNodeDef node, bool unlocked, AcceptanceReport can, Action<DivineNodeDef> onClick,
            DivineDomainDef domain, GameComponent_Divinity divinity)
        {
            Color border = unlocked ? UnlockedColor : can.Accepted ? AvailableColor : LockedColor;
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.1f, 0.95f));
            GUI.color = border;
            Widgets.DrawBox(rect, unlocked ? 2 : 1);
            GUI.color = Color.white;
            if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);

            Rect icon = new Rect(rect.center.x - 21f, rect.y + 4f, 42f, 42f);
            GUI.color = unlocked || can.Accepted ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            Widgets.DrawTextureFitted(icon, node.Icon, 1f);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(rect.x + 2f, icon.yMax + 1f, rect.width - 4f, 18f), node.LabelCap.Truncate(rect.width - 4f));
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            string req = unlocked ? "IAG_NodeUnlocked".Translate().ToString() : "IAG_NodeCost".Translate(node.pointCost, node.MinTier).ToString();
            if (node.forDemigod && !unlocked)
                req += " · " + "IAG_DemigodLevelShort".Translate(node.minDemigodLevel);
            Widgets.Label(new Rect(rect.x + 2f, icon.yMax + 17f, rect.width - 4f, 16f), req.Truncate(rect.width - 4f));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            DivinePowerDef power = node.power;
            string tip = node.LabelCap + "\n\n" + power.description;
            string summary = power.Worker.EffectSummary();
            if (!summary.NullOrEmpty())
                tip += "\n\n" + summary;
            tip += "\n\n" + "IAG_PowerCost".Translate(divinity.CostOf(power), power.minTier) + "  ·  " + "IAG_PowerCooldown".Translate(power.cooldownDays.ToString("0.##"));
            if (power.requiresProphet)
                tip += "  " + "IAG_PowerProphet".Translate();
            if (!unlocked && !can.Accepted && !can.Reason.NullOrEmpty())
                tip += "\n\n" + can.Reason.Colorize(ColoredText.WarningColor);
            TooltipHandler.TipRegion(rect, tip);

            if (!unlocked && Widgets.ButtonInvisible(rect))
            {
                if (can.Accepted)
                    onClick(node);
                else
                    SoundDefOf.ClickReject.PlayOneShotOnCamera();
            }
        }
    }
}
