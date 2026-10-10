using HarmonyLib;
using UnityEngine;
using Verse;

namespace ImAGod
{
    public class ImAGodSettings : ModSettings
    {
        public float faithGainMultiplier = 1f;
        public float powerCostMultiplier = 1f;
        public bool foresightEnabled = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref faithGainMultiplier, "faithGainMultiplier", 1f);
            Scribe_Values.Look(ref powerCostMultiplier, "powerCostMultiplier", 1f);
            Scribe_Values.Look(ref foresightEnabled, "foresightEnabled", true);
        }
    }

    public class ImAGodMod : Mod
    {
        public static ImAGodSettings Settings;

        public ImAGodMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ImAGodSettings>();
            new Harmony("dazetc123.ImAGod").PatchAll();
        }

        public override string SettingsCategory() => "IAG_ModName".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            list.Label("IAG_Setting_FaithGain".Translate(Settings.faithGainMultiplier.ToStringPercent()));
            Settings.faithGainMultiplier = Mathf.Round(list.Slider(Settings.faithGainMultiplier, 0.25f, 3f) * 20f) / 20f;
            list.Label("IAG_Setting_PowerCost".Translate(Settings.powerCostMultiplier.ToStringPercent()));
            Settings.powerCostMultiplier = Mathf.Round(list.Slider(Settings.powerCostMultiplier, 0.25f, 3f) * 20f) / 20f;
            list.CheckboxLabeled("IAG_Setting_Foresight".Translate(), ref Settings.foresightEnabled, "IAG_Setting_ForesightDesc".Translate());
            list.End();
        }
    }
}
