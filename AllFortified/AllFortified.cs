using AllFortified.Properties;
using AllFortified.Utils;
using Il2CppAssets.Scripts.Models.Bloons;
using Il2CppAssets.Scripts.Models.Bloons.Behaviors;
using Il2CppAssets.Scripts.Models.GenericBehaviors;
using Il2CppAssets.Scripts.Unity.Display;
using UnityEngine;
using Resources = AllFortified.Properties.Resources;

namespace AllFortified {
    internal static class AllFortified {
        private static readonly AssetBundle bundle = Resources.GetAssetBundle("allfortified");

        public const string Identifier = "AllF";
        public const string LocalizeKey = "EditorAllFortified";

        public static string[] BloonsToFortify { get; } = { "Black", "Blue", "Green", "Pink", "Purple", "Rainbow", "Red", "White", "Yellow", "Zebra", "TestBloon" };
        public static string[] BloonsToFortifyChildren { get; } = { "Ceramic", "Lead" };

        public static BloonModel Fortify(BloonModel bloon) {
            bloon.name = bloon.id = RenameFortified(bloon.name);
            bloon.isFortified = true;
            bloon.maxHealth *= 2;
            bloon.leakDamage *= 2;
            bloon.danger += .5f;
            bloon.tags = bloon.tags.Add("Fortified");

            string fortifiedDisplayName = GetFortifiedDisplayName(bloon);

            bloon.icon = new() { guidRef = $"Ui[{fortifiedDisplayName}]" };

            FortifyChildren(bloon);
            FortifyRegrow(bloon);

            DisplayModel display = bloon.FirstBehavior<DisplayModel>();
            bloon.display = display.display = new() { guidRef = fortifiedDisplayName };

            return bloon;
        }

        public static bool LoadDisplay(string name, Factory.__c__DisplayClass21_0 assetFactory, System.Action<UnityDisplayNode> onComplete) {
            Sprite sprite = bundle.GetResource<Sprite>(name);
            if (sprite is not null) {
                assetFactory.__4__this.CreateAsync(new() { guidRef = "33adb905c9b871b44962fa3b9c3eb7de" }, new System.Action<UnityDisplayNode>(udn => {
                    udn.gameObject.name = name;
                    udn.IsSprite = true;
                    udn.gameObject.GetComponentInChildren<SpriteRenderer>().sprite = sprite;
                    onComplete?.Invoke(udn);
                }));
                return true;
            }
            return false;
        }

        public static Sprite LoadSprite(string name) => bundle.GetResource<Sprite>(name);

        public static Sprite LoadIcon() => bundle.GetResource<Sprite>("AllFortified");

        public static void FortifyChildren(BloonModel bloon) {
            SpawnChildrenModel spawns = bloon.FirstBehavior<SpawnChildrenModel>();
            if (spawns is null)
                return;

            for (int i = 0; i < spawns.children.Length; i++)
                spawns.children[i] = RenameFortified(spawns.children[i]);
        }

        public static void FortifyRegrow(BloonModel bloon) {
            GrowModel grow = bloon.FirstBehavior<GrowModel>();
            if (grow is null)
                return;

            grow.growToId = RenameFortified(grow.growToId);
        }

        public static string RenameFortified(string name) {
            if (!name.Contains("Fortified")) {
                int index = name.IndexOf("Camo");
                if (index == -1)
                    index = name.Length;
                return name.Insert(index, "Fortified");
            }
            return name;
        }

        public static void AddLocalization(Il2CppSystem.Collections.Generic.Dictionary<string, string> defaultTable) {
            defaultTable.Add(LocalizeKey, "All Fortified");
        }

        private static string GetFortifiedDisplayName(BloonModel bloon) => $"Fortified{(bloon.isCamo ? "Camo" : "")}{(bloon.isGrow ? "Regrow" : "")}{bloon.baseId}";
    }
}
