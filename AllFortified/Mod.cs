using AllFortified.Utils;
using HarmonyLib;
using Il2Cpp;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Bloons;
using Il2CppAssets.Scripts.Models.Rounds;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.BloonMenu;
using Il2CppInterop.Runtime;
using Il2CppNinjaKiwi.Common;
using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(AllFortified.Mod), "All Fortified", "1.0.1", "Baydock")]
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
                AllFortifiedToggle.isOn = AllFortified.IsAllFortified(__instance.dcm);
        }

        [HarmonyPatch(typeof(ChallengeEditor), nameof(ChallengeEditor.Randomize))]
        [HarmonyPostfix]
        public static void RandomizeAllFortifiedToggle() {
            ForceOn = Random.NextDouble() >= .5;
            FromRandom = true;
        }

        [HarmonyPatch(typeof(ChallengeEditor), nameof(ChallengeEditor.ApplyValues))]
        [HarmonyPostfix]
        public static void ApplyAllFortifiedToDCM(ChallengeEditor __instance) {
            bool hasIdentifier = AllFortified.IsAllFortified(__instance.dcm);
            if (AllFortifiedToggle.isOn && !hasIdentifier)
                AllFortified.AddAllFortified(__instance.dcm);
            else if (!AllFortifiedToggle.isOn && hasIdentifier)
                AllFortified.RemoveAllFortified(__instance.dcm);
        }

        [HarmonyPatch(typeof(PlayerChallengeManager), nameof(PlayerChallengeManager.ValidateTitle))]
        [HarmonyPrefix]
        public static void AllowHiddenInfoChars() {
            TitleSettings titleSettings = PlayerChallengeManager.BrowserSettings.title;
            List<char> newAllowedSymbols = titleSettings.allowedSymbols.ToList();

            bool newAdded = false;
            if (!newAllowedSymbols.Contains(MessageHider.ZeroBit)) {
                newAllowedSymbols.Add(MessageHider.ZeroBit);
                newAdded = true;
            }
            if (!newAllowedSymbols.Contains(MessageHider.OneBit)) {
                newAllowedSymbols.Add(MessageHider.OneBit);
                newAdded = true;
            }
            titleSettings.maxLength = short.MaxValue;

            if (newAdded)
                titleSettings.allowedSymbols = newAllowedSymbols.ToArray();
        }

        [HarmonyPatch(typeof(ChallengeEditorPlay), nameof(ChallengeEditorPlay.ShowModIcons))]
        [HarmonyPostfix]
        public static void ShowAllFortifiedModifier(ChallengeEditorPlay __instance) {
            if (AllFortified.IsAllFortified(__instance.dcm)) {
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
            if (AllFortified.IsAllFortified(__instance.dcm)) {
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
            if (AllFortified.IsAllFortified(dcm)) {
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

        [HarmonyPatch(typeof(ResourceManager), nameof(ResourceManager.ProvideResource), typeof(IResourceLocation), typeof(Il2CppSystem.Type), typeof(bool))]
        [HarmonyPrefix]
        public static bool LoadSprites(IResourceLocation location, Il2CppSystem.Type desiredType, ref AsyncOperationHandle __result) {
            if (location is null || desiredType is null)
                return true;

            string asset = Path.GetFileName(location.InternalId);
            if (string.IsNullOrEmpty(asset))
                return true;

            if (desiredType.Equals(Il2CppType.Of<Sprite>())) {
                Sprite sprite = AllFortified.LoadSprite(asset);
                if (sprite is not null) {
                    __result = Addressables.ResourceManager.CreateCompletedOperation(sprite, null);
                    return false;
                }
            }

            return true;
        }

        [HarmonyPatch(typeof(AddressablesImpl), nameof(AddressablesImpl.InstantiateAsync), typeof(Il2CppSystem.Object), typeof(InstantiationParameters), typeof(bool))]
        [HarmonyPrefix]
        public static bool LoadModels(Il2CppSystem.Object key, InstantiationParameters instantiateParameters, ref AsyncOperationHandle<GameObject> __result) {
            if (key is null)
                return true;

            string asset = Path.GetFileName(key.ToString());
            if (string.IsNullOrEmpty(asset))
                return true;

            GameObject display = AllFortified.LoadDisplay(asset);
            if (display is not null) {
                display.transform.parent = instantiateParameters.Parent;
                __result = Addressables.ResourceManager.CreateCompletedOperation(display, null);
                return false;
            }

            return true;
        }

        #endregion
    }
}
