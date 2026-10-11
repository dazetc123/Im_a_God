using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ImAGod
{
    /// <summary>
    /// Chooses the god's path and first domain, or (when the path is already set) one more domain to embrace.
    /// </summary>
    public class Dialog_ChooseDivinity : Window
    {
        private const float PathCardHeight = 210f;
        private const float DomainCardWidth = 280f;
        private const float DomainCardHeight = 150f;

        private readonly bool embraceOnly;
        private DivinePathDef selectedPath;
        private DivineDomainDef selectedDomain;
        private Vector2 scroll;

        public Dialog_ChooseDivinity(DivinePathDef fixedPath)
        {
            embraceOnly = fixedPath != null;
            selectedPath = fixedPath;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = embraceOnly;
            closeOnClickedOutside = false;
            closeOnCancel = embraceOnly;
        }

        public override Vector2 InitialSize => new Vector2(940f, embraceOnly ? 560f : 780f);

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_Divinity divinity = GameComponent_Divinity.Instance;
            if (divinity == null)
            {
                Close();
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f), embraceOnly ? "IAG_EmbraceTitle".Translate() : "IAG_ChoosePathTitle".Translate());
            Text.Font = GameFont.Small;
            float y = inRect.y + 38f;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Rect intro = new Rect(inRect.x, y, inRect.width, 40f);
            Widgets.Label(intro, embraceOnly ? "IAG_EmbraceIntro".Translate() : "IAG_ChoosePathIntro".Translate());
            GUI.color = Color.white;
            y += 44f;

            if (!embraceOnly)
            {
                List<DivinePathDef> paths = DefDatabase<DivinePathDef>.AllDefsListForReading.OrderBy(p => p.order).ToList();
                float w = (inRect.width - 12f * (paths.Count - 1)) / paths.Count;
                for (int i = 0; i < paths.Count; i++)
                    DrawPathCard(new Rect(inRect.x + i * (w + 12f), y, w, PathCardHeight), paths[i]);
                y += PathCardHeight + 10f;
            }

            Rect bottom = new Rect(inRect.x, inRect.yMax - 40f, inRect.width, 40f);
            Rect domainsRect = new Rect(inRect.x, y, inRect.width, bottom.y - y - 8f);
            if (selectedPath != null)
                DrawDomains(domainsRect, divinity);

            if (!embraceOnly && Widgets.ButtonText(new Rect(bottom.x, bottom.y, 180f, 38f), "IAG_DecideLater".Translate()))
            {
                divinity.DeferPathChoice();
                Close();
            }

            bool ready = selectedPath != null && selectedDomain != null;
            string confirm = selectedDomain == null ? "IAG_PickDomain".Translate().ToString() : "IAG_ConfirmDomain".Translate(selectedDomain.label).ToString();
            if (Widgets.ButtonText(new Rect(bottom.xMax - 300f, bottom.y, 300f, 38f), confirm, active: ready) && ready)
            {
                if (embraceOnly)
                    divinity.EmbraceDomain(selectedDomain);
                else
                    divinity.ChoosePath(selectedPath, selectedDomain);
                Close();
            }
        }

        private void DrawPathCard(Rect rect, DivinePathDef path)
        {
            bool selected = selectedPath == path;
            Widgets.DrawBoxSolid(rect, new Color(path.color.r * 0.18f, path.color.g * 0.18f, path.color.b * 0.18f, 1f));
            GUI.color = selected ? path.color : new Color(0.4f, 0.4f, 0.4f);
            Widgets.DrawBox(rect, selected ? 3 : 1);
            GUI.color = Color.white;
            if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);

            Rect icon = new Rect(rect.x + 12f, rect.y + 12f, 120f, 120f);
            Widgets.DrawTextureFitted(icon, path.Icon, 1f);
            Rect text = new Rect(icon.xMax + 12f, rect.y + 10f, rect.width - icon.width - 36f, rect.height - 20f);
            Text.Font = GameFont.Medium;
            GUI.color = path.color;
            Widgets.Label(new Rect(text.x, text.y, text.width, 32f), path.LabelCap);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(text.x, text.y + 34f, text.width, text.height - 34f), path.description);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(new Rect(rect.x + 12f, icon.yMax + 8f, rect.width - 24f, rect.yMax - icon.yMax - 12f),
                "IAG_PathDomains".Translate(path.Domains.Select(d => d.LabelCap.ToString()).ToCommaList()));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (Widgets.ButtonInvisible(rect))
            {
                if (selectedPath != path)
                    selectedDomain = null;
                selectedPath = path;
            }
        }

        private void DrawDomains(Rect rect, GameComponent_Divinity divinity)
        {
            List<DivineDomainDef> list = selectedPath.Domains.Where(d => !divinity.domains.Contains(d)).ToList();
            int perRow = Mathf.Max(1, Mathf.FloorToInt((rect.width - 16f) / (DomainCardWidth + 10f)));
            int rows = Mathf.CeilToInt(list.Count / (float)perRow);
            Rect view = new Rect(0f, 0f, rect.width - 16f, rows * (DomainCardHeight + 10f));
            Widgets.BeginScrollView(rect, ref scroll, view);
            for (int i = 0; i < list.Count; i++)
            {
                Rect card = new Rect((i % perRow) * (DomainCardWidth + 10f), (i / perRow) * (DomainCardHeight + 10f), DomainCardWidth, DomainCardHeight);
                DrawDomainCard(card, list[i]);
            }
            Widgets.EndScrollView();
        }

        private void DrawDomainCard(Rect rect, DivineDomainDef domain)
        {
            bool selected = selectedDomain == domain;
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.14f));
            GUI.color = selected ? domain.color : new Color(0.35f, 0.35f, 0.35f);
            Widgets.DrawBox(rect, selected ? 3 : 1);
            GUI.color = Color.white;
            if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);

            Rect icon = new Rect(rect.x + 8f, rect.y + 8f, 56f, 56f);
            Widgets.DrawTextureFitted(icon, domain.Icon, 1f);
            GUI.color = domain.color;
            Widgets.Label(new Rect(icon.xMax + 8f, rect.y + 8f, rect.width - 80f, 24f), domain.LabelCap);
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(icon.xMax + 8f, rect.y + 32f, rect.width - 80f, 40f), domain.description.Truncate((rect.width - 80f) * 2.6f));
            GUI.color = new Color(0.75f, 0.85f, 0.75f);
            Widgets.Label(new Rect(rect.x + 8f, icon.yMax + 6f, rect.width - 16f, rect.yMax - icon.yMax - 10f),
                "IAG_DomainOfferings".Translate(domain.OfferingsSummary) + "\n" +
                "IAG_DomainMiracles".Translate(domain.GodNodes.OrderBy(n => n.row).Select(n => n.LabelCap.ToString()).ToCommaList()));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(rect, DomainTooltip(domain));

            if (Widgets.ButtonInvisible(rect))
                selectedDomain = domain;
        }

        public static string DomainTooltip(DivineDomainDef domain)
        {
            string tip = domain.LabelCap + "\n\n" + domain.description;
            if (domain.blessing != null)
                tip += "\n\n" + "IAG_DomainBlessing".Translate(domain.blessing.LabelCap) + "\n" + domain.blessing.description;
            tip += "\n\n" + "IAG_DomainOfferings".Translate(domain.OfferingsSummary);
            tip += "\n\n" + "IAG_DomainMiracles".Translate(domain.GodNodes.OrderBy(n => n.row).Select(n => n.LabelCap.ToString()).ToCommaList());
            tip += "\n" + "IAG_DomainDemigodAbilities".Translate(domain.DemigodNodes.OrderBy(n => n.row).Select(n => n.LabelCap.ToString()).ToCommaList());
            return tip;
        }
    }
}
