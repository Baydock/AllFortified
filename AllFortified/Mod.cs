using AllFortified.Utils;
using HarmonyLib;
using Il2Cpp;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Bloons;
using Il2CppAssets.Scripts.Models.Rounds;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.Display;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.BloonMenu;
using Il2CppAssets.Scripts.Utils;
using Il2CppNinjaKiwi.Common;
using MelonLoader;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(AllFortified.Mod), "All Fortified", "1.0.0", "Baydock")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]
[assembly: MelonColor(255, 177, 72, 27)]
[assembly: MelonAuthorColor(255, 255, 104, 0)]

namespace AllFortified {
    [HarmonyPatch]
    public sealed class Mod : MelonMod {

        private static System.Random Random { get; } = new();

        public static MelonLogger.Instance Logger { get; private set; }

        public override void OnInitializeMelon() {
            Logger = LoggerInstance;
        }

        [HarmonyPatch(typeof(GameModelLoader), nameof(GameModelLoader.Load))]
        [HarmonyPostfix]
        public static void AddFortified(GameModel __result) {
            for (int i = 0; i < __result.bloons.Length; i++) {
                BloonModel bloon = __result.bloons[i];
                if (AllFortified.BloonsToFortify.Contains(bloon.baseId)) {
                    BloonModel fortifiedBloon = AllFortified.Fortify(bloon.CloneCast());
                    __result.bloons = __result.bloons.Insert(i + 1, fortifiedBloon);
                    __result.childDependants.Add(fortifiedBloon);
                    i++;
                } else if (AllFortified.BloonsToFortifyChildren.Contains(bloon.baseId) && bloon.isFortified)
                    AllFortified.FortifyChildren(bloon);
            }

#if DEBUG
            __result.roundSets = __result.roundSets.Add(new RoundSetModel("AllFortifiedRoundSet", new Il2CppReferenceArray<RoundModel>(1) {
                [0] = new RoundModel("", __result.bloons.Where(bloon => bloon.isFortified && AllFortified.BloonsToFortify.Contains(bloon.baseId)).Select((bloon, i) => new BloonGroupModel("", bloon.id, i * 60, (i + 1) * 60, 1)).ToArray())
            }));
#endif

            AllFortified.AddLocalization(LocalizationManager.Instance.defaultTable);
        }

        #region Sandbox Bloon menu

        [HarmonyPatch(typeof(BloonMenu), nameof(BloonMenu.CreateBloonButtons))]
        [HarmonyPrefix]
        public static void FortifyInBloonsMenu(BloonMenu __instance, Il2CppSystem.Collections.Generic.List<BloonModel> sortedBloons) {
            if (__instance.fortified) {
                for (int i = 0; i < sortedBloons.Count; i++) {
                    if (AllFortified.BloonsToFortify.Contains(sortedBloons[i].baseId) && !sortedBloons[i].isFortified)
                        sortedBloons[i] = InGame.Bridge.Model.GetBloon(AllFortified.RenameFortified(sortedBloons[i].id));
                }
                // fix zebra and lead swapping
                int zebraIndex = sortedBloons.FindIndex(new System.Func<BloonModel, bool>(b => b.baseId.Equals("Zebra")));
                int leadIndex = sortedBloons.FindIndex(new System.Func<BloonModel, bool>(b => b.baseId.Equals("Lead")));
                (sortedBloons[zebraIndex], sortedBloons[leadIndex]) = (sortedBloons[leadIndex], sortedBloons[zebraIndex]);
            }
        }

        #endregion

        #region Challenge Editor

        private static Toggle AllFortifiedToggle { get; set; }
        [HarmonyPatch(typeof(ChallengeEditor), nameof(ChallengeEditor.SetupUI))]
        [HarmonyPostfix]
        public static void AddAllFortifiedToggle(ChallengeEditor __instance) {
            RectTransform allRegenToggle = __instance.allRegenToggle.transform.parent.Cast<RectTransform>();
            RectTransform everything = allRegenToggle.parent.Cast<RectTransform>();
            int regenIndex = allRegenToggle.GetSiblingIndex();

            RectTransform oneBefore = everything.GetChild(regenIndex - 1).Cast<RectTransform>();
            RectTransform twoBefore = everything.GetChild(regenIndex - 2).Cast<RectTransform>();

            RectTransform firstCol, secondCol;
            bool startOnNewRow;
            if (Mathf.Approximately(oneBefore.position.y, allRegenToggle.position.y)) {
                firstCol = oneBefore;
                secondCol = twoBefore;
                startOnNewRow = true;
            } else {
                firstCol = twoBefore;
                secondCol = oneBefore;
                startOnNewRow = false;
            }

            float firstColX = firstCol.localPosition.x, secondColX = secondCol.localPosition.x;
            float rowH = secondCol.localPosition.y - allRegenToggle.localPosition.y;

            float[] firstColChildXs = new float[firstCol.childCount], secondColChildXs = new float[secondCol.childCount];
            for (int i = 0; i < firstCol.childCount; i++)
                firstColChildXs[i] = firstCol.GetChild(i).localPosition.x;
            for (int i = 0; i < secondCol.childCount; i++)
                secondColChildXs[i] = secondCol.GetChild(i).localPosition.x;

            RectTransform allFortifiedToggle = Object.Instantiate(allRegenToggle.gameObject).GetComponent<RectTransform>();
            allFortifiedToggle.parent = everything;
            allFortifiedToggle.localScale = Vector3.one;
            allFortifiedToggle.SetSiblingIndex(regenIndex + 1);
            allFortifiedToggle.gameObject.name = "AllFortifiedToggle";
            AllFortifiedToggle = allFortifiedToggle.GetComponentInChildren<Toggle>();

            NK_TextMeshProUGUI allFortifiedLabel = allFortifiedToggle.GetComponentInChildren<NK_TextMeshProUGUI>();
            allFortifiedLabel.localizeKey = AllFortified.LocalizeKey;

            int numTogglesMoved = 0;
            for (int i = regenIndex + 1; i < everything.childCount && everything.GetChild(i).name.EndsWith("Toggle"); i++, numTogglesMoved++) {
                RectTransform toggle = everything.GetChild(i).GetComponent<RectTransform>();
                int indexOffset = i - regenIndex - 1;

                bool inFirstCol = indexOffset % 2 == (startOnNewRow ? 0 : 1);
                float x = inFirstCol ? firstColX : secondColX;

                int row = (indexOffset + (startOnNewRow ? 0 : 1)) / 2 + (startOnNewRow ? 1 : 0);
                float y = allRegenToggle.localPosition.y - row * rowH;

                toggle.localPosition = new Vector3(x, y);

                for (int j = 0; j < toggle.childCount; j++) {
                    Transform child = toggle.GetChild(j);
                    Vector3 childPos = child.localPosition;
                    childPos.x = (inFirstCol ? firstColChildXs : secondColChildXs)[j];
                    child.localPosition = childPos;
                }
            }

            bool addedNewRow = numTogglesMoved % 2 == (startOnNewRow ? 1 : 0);

            if (addedNewRow)
                everything.sizeDelta += new Vector2(0, rowH);
        }

        private static bool FromRandom { get; set; }
        private static bool ForceOn { get; set; }
        [HarmonyPatch(typeof(ChallengeEditor), nameof(ChallengeEditor.PopulateVisuals))]
        [HarmonyPostfix]
        public static void PopulateAllFortifiedToggleValue(ChallengeEditor __instance) {
            if (FromRandom) {
                AllFortifiedToggle.isOn = ForceOn;
                FromRandom = false;
                ApplyAllFortifiedToDCM(__instance);
            } else
                AllFortifiedToggle.isOn = MessageHider.HasMessage(__instance.dcm.name, AllFortified.Identifier);
        }

        [HarmonyPatch(typeof(ChallengeEditor), nameof(ChallengeEditor.Randomize))]
        [HarmonyPostfix]
        public static void RandomizeAllFortifiedToggle() {
            ForceOn = Random.NextDouble() >= .5;
            FromRandom = true;
        }

        // As long as this is a postfix, nk already sanitizes the name in the input field for me to my favor
        [HarmonyPatch(typeof(ChallengeEditor), nameof(ChallengeEditor.ApplyValues))]
        [HarmonyPostfix]
        public static void ApplyAllFortifiedToDCM(ChallengeEditor __instance) {
            bool hasIdentifier = MessageHider.HasMessage(__instance.dcm.name, AllFortified.Identifier);
            if (AllFortifiedToggle.isOn && !hasIdentifier)
                __instance.dcm.name = MessageHider.HideMessage(__instance.dcm.name, AllFortified.Identifier);
            else if (!AllFortifiedToggle.isOn && hasIdentifier)
                __instance.dcm.name = MessageHider.RemoveMessage(__instance.dcm.name, AllFortified.Identifier);
        }

        [HarmonyPatch(typeof(ChallengeEditorPlay), nameof(ChallengeEditorPlay.ShowModIcons))]
        [HarmonyPostfix]
        public static void ShowAllFortifiedModifier(ChallengeEditorPlay __instance) {
            if (MessageHider.HasMessage(__instance.dcm.name, AllFortified.Identifier)) {
                GameObject allFortified = Object.Instantiate(__instance.modifierPrefab, __instance.modifierContent);

                allFortified.GetComponentInChildren<Image>().sprite = AllFortified.LoadIcon();

                ButtonExtended allFortifiedBtn = allFortified.GetComponentInChildren<ButtonExtended>();
                allFortifiedBtn.OnPointerDownEvent = new System.Action<PointerEventData>(ped => {
                    NK_TextMeshProUGUI desc = __instance.modifierInfo.GetComponentInChildren<NK_TextMeshProUGUI>();
                    desc.text = LocalizationManager.Instance.GetText(AllFortified.LocalizeKey);
                    __instance.modifierInfo.transform.position = allFortified.transform.position;
                    __instance.modDownStartTime = Time.realtimeSinceStartup;
                });
                allFortifiedBtn.OnPointerUpEvent = new System.Action<PointerEventData>(ped => {
                    __instance.modifierInfo.SetActive(false);
                    __instance.modDownStartTime = float.MaxValue;
                });

                allFortified.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(ChallengeRulesScreen), nameof(ChallengeRulesScreen.SetUi))]
        [HarmonyPostfix]
        public static void ShowAllFortifiedModifierInRules(ChallengeRulesScreen __instance) {
            if (MessageHider.HasMessage(__instance.dcm.name, AllFortified.Identifier)) {
                GameObject allFortified = Object.Instantiate(__instance.modifierPrefab, __instance.gameRuleContent);
                allFortified.GetComponentInChildren<NK_TextMeshProUGUI>().localizeKey = AllFortified.LocalizeKey;
                allFortified.GetComponentInChildren<Image>().sprite = AllFortified.LoadIcon();
                allFortified.GetComponentsInChildren<NK_TextMeshProUGUI>()[1].gameObject.SetActive(false);
                allFortified.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(DailyChallengeModel), nameof(DailyChallengeModel.ApplyDCToGameModel))]
        [HarmonyPostfix]
        public static void ApplyAllFortified(DailyChallengeModel dcm, GameModel gameModel) {
            if (MessageHider.HasMessage(dcm.name, AllFortified.Identifier)) {
                foreach (RoundSetModel roundSet in gameModel.roundSets)
                    foreach (RoundModel round in roundSet.rounds)
                        foreach (BloonGroupModel group in round.groups)
                            group.bloon = AllFortified.RenameFortified(group.bloon);
                foreach (FreeplayBloonGroupModel freeplayGroup in gameModel.freeplayGroups)
                    freeplayGroup.group.bloon = AllFortified.RenameFortified(freeplayGroup.group.bloon);
            }
        }

        #endregion

        #region Asset Loading

        [HarmonyPatch(typeof(Factory.__c__DisplayClass21_0), nameof(Factory.__c__DisplayClass21_0._CreateAsync_b__0))]
        [HarmonyPrefix]
        public static bool LoadModels(Factory.__c__DisplayClass21_0 __instance, UnityDisplayNode prototype) {
            string objectId = __instance.objectId.guidRef;
            if (!string.IsNullOrEmpty(objectId) && prototype is null) {
                return !AllFortified.LoadDisplay(objectId, __instance, new System.Action<UnityDisplayNode>(proto => {
                    SetUpPrototype(proto, __instance.objectId, __instance);
                    SetUpDisplay(proto, __instance);
                }));
            }
            return true;
        }
        private static void SetUpPrototype(UnityDisplayNode proto, PrefabReference protoRef, Factory.__c__DisplayClass21_0 assetFactory) {
            proto.transform.parent = assetFactory.__4__this.PrototypeRoot;
            proto.Active = false;
            proto.gameObject.transform.position = new Vector3(-3000, 0, 0);
            proto.gameObject.transform.eulerAngles = Vector3.zero;
            proto.cloneOf = protoRef;
            assetFactory.__4__this.prototypeHandles[protoRef] = Addressables.Instance.ResourceManager.CreateCompletedOperation(proto.gameObject, "");
        }
        private static void SetUpDisplay(UnityDisplayNode proto, Factory.__c__DisplayClass21_0 assetFactory) {
            UnityDisplayNode display = Object.Instantiate(proto.gameObject, assetFactory.__4__this.DisplayRoot).GetComponent<UnityDisplayNode>();

            display.transform.parent = assetFactory.__4__this.DisplayRoot;
            display.Active = true;
            display.cloneOf = proto.cloneOf;
            assetFactory.__4__this.active.Add(display);
            assetFactory.onComplete?.Invoke(display);
        }

        [HarmonyPatch(typeof(SpriteAtlas), nameof(SpriteAtlas.GetSprite))]
        [HarmonyPrefix]
        public static bool LoadSprites(ref Sprite __result, string name) {
            if (!string.IsNullOrEmpty(name)) {
                Sprite sprite = AllFortified.LoadSprite(name);
                if (sprite is not null) {
                    __result = sprite;
                    return false;
                }
            }
            return true;
        }

        #endregion
    }
}
