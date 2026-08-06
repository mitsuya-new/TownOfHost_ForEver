using System;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using TownOfHostForE.Modules.OptionItems.Interfaces;
using TownOfHostForE.Modules.OptionItems;
using TownOfHostForE;
using UnityEngine;
using static TownOfHostForE.Translator;
using Object = UnityEngine.Object;

namespace TownOfHostForE
{
    [HarmonyPatch(typeof(GameSettingMenu))]
    public static class GameSettingMenuPatch
    {
        private static GameOptionsMenu tohSettingsTab;
        private static PassiveButton tohSettingsButton;
        private static GameSettingMenu gameSettingMenu;
        private static StringOption optionTemplate;
        private static Il2CppSystem.Collections.Generic.List<OptionBehaviour> tohOptionBehaviours;
        private static bool tohSettingsBuilt;
        public static CategoryHeaderMasked MainCategoryHeader { get; private set; }
        public static CategoryHeaderMasked ImpostorRoleCategoryHeader { get; private set; }
        public static CategoryHeaderMasked MadmateRoleCategoryHeader { get; private set; }
        public static CategoryHeaderMasked CrewmateRoleCategoryHeader { get; private set; }
        public static CategoryHeaderMasked NeutralRoleCategoryHeader { get; private set; }
        public static CategoryHeaderMasked AnimalsRoleCategoryHeader { get; private set; }
        public static CategoryHeaderMasked AddOnCategoryHeader { get; private set; }

        [HarmonyPatch(nameof(GameSettingMenu.Start)), HarmonyPostfix]
        public static void StartPostfix(GameSettingMenu __instance)
        {
            gameSettingMenu = __instance;
            tohSettingsTab = Object.Instantiate(__instance.GameSettingsTab, __instance.GameSettingsTab.transform.parent);
            tohSettingsTab.name = TOHMenuName;
            var vanillaOptions = tohSettingsTab.settingsContainer.GetComponentsInChildren<OptionBehaviour>();
            foreach (var vanillaOption in vanillaOptions)
            {
                if (vanillaOption == null) continue;
                Object.Destroy(vanillaOption.gameObject);
            }
            if (tohSettingsTab.MapPicker != null)
            {
                tohSettingsTab.MapPicker.gameObject.SetActive(false);
            }
            optionTemplate = __instance.GameSettingsTab.stringOptionOrigin;
            tohOptionBehaviours = new Il2CppSystem.Collections.Generic.List<OptionBehaviour>();
            tohSettingsTab.Children = tohOptionBehaviours;
            tohSettingsBuilt = false;
            foreach (var option in OptionItem.AllOptions)
            {
                option.OptionBehaviour = null;
            }

            // TOH設定ボタンのスペースを作るため，左側の要素を上に詰める
            var gameSettingsLabel = __instance.transform.Find("GameSettingsLabel");
            if (gameSettingsLabel)
            {
                gameSettingsLabel.localPosition += Vector3.up * 0.2f;
            }
            __instance.MenuDescriptionText.transform.parent.localPosition += Vector3.up * 0.4f;
            __instance.GamePresetsButton.transform.parent.localPosition += Vector3.up * 0.5f;

            // TOH設定ボタン
            tohSettingsButton = Object.Instantiate(__instance.GameSettingsButton, __instance.GameSettingsButton.transform.parent);
            tohSettingsButton.name = "TOHSettingsButton";
            tohSettingsButton.transform.localPosition = __instance.RoleSettingsButton.transform.localPosition + (__instance.RoleSettingsButton.transform.localPosition - __instance.GameSettingsButton.transform.localPosition);
            tohSettingsButton.buttonText.DestroyTranslator();
            tohSettingsButton.buttonText.text = GetString("TOHSettingsButtonLabel");
            var activeSprite = tohSettingsButton.activeSprites.GetComponent<SpriteRenderer>();
            var selectedSprite = tohSettingsButton.selectedSprites.GetComponent<SpriteRenderer>();
            activeSprite.color = selectedSprite.color = Main.UnityModColor;
            tohSettingsButton.OnClick.AddListener((Action)(() =>
            {
                __instance.ChangeTab(-1, false);  // バニラタブを閉じる
                EnsureTohSettingsBuilt(__instance);
                tohSettingsTab.gameObject.SetActive(true);
                __instance.MenuDescriptionText.text = GetString("TOHSettingsDescription");
                tohSettingsButton.SelectButton(true);
                GameOptionsMenuUpdatePatch.RefreshNow(tohSettingsTab);
            }));

            tohSettingsTab.gameObject.SetActive(false);
        }

        public static bool IsTohSettingsBuilt => tohSettingsBuilt;

        private static void EnsureTohSettingsBuilt(GameSettingMenu __instance)
        {
            if (tohSettingsBuilt) return;

            MainCategoryHeader = CreateCategoryHeader(__instance, tohSettingsTab, "TabGroup.MainSettings");
            ImpostorRoleCategoryHeader = CreateCategoryHeader(__instance, tohSettingsTab, "TabGroup.ImpostorRoles");
            MadmateRoleCategoryHeader = CreateCategoryHeader(__instance, tohSettingsTab, "TabGroup.MadmateRoles");
            CrewmateRoleCategoryHeader = CreateCategoryHeader(__instance, tohSettingsTab, "TabGroup.CrewmateRoles");
            NeutralRoleCategoryHeader = CreateCategoryHeader(__instance, tohSettingsTab, "TabGroup.NeutralRoles");
            AnimalsRoleCategoryHeader = CreateCategoryHeader(__instance, tohSettingsTab, "TabGroup.AnimalsRoles");
            AddOnCategoryHeader = CreateCategoryHeader(__instance, tohSettingsTab, "TabGroup.Addons");

            var jumpButtonY = -0.4f;
            CreateJumpToCategoryButton(__instance, tohSettingsTab, "TownOfHost_ForEver.Resources.TabIcon_MainSettings.png", ref jumpButtonY, MainCategoryHeader);
            CreateJumpToCategoryButton(__instance, tohSettingsTab, "TownOfHost_ForEver.Resources.TabIcon_ImpostorRoles.png", ref jumpButtonY, ImpostorRoleCategoryHeader);
            CreateJumpToCategoryButton(__instance, tohSettingsTab, "TownOfHost_ForEver.Resources.TabIcon_MadmateRoles.png", ref jumpButtonY, MadmateRoleCategoryHeader);
            CreateJumpToCategoryButton(__instance, tohSettingsTab, "TownOfHost_ForEver.Resources.TabIcon_CrewmateRoles.png", ref jumpButtonY, CrewmateRoleCategoryHeader);
            CreateJumpToCategoryButton(__instance, tohSettingsTab, "TownOfHost_ForEver.Resources.TabIcon_NeutralRoles.png", ref jumpButtonY, NeutralRoleCategoryHeader);
            CreateJumpToCategoryButton(__instance, tohSettingsTab, "TownOfHost_ForEver.Resources.TabIcon_AnimalsRoles.png", ref jumpButtonY, AnimalsRoleCategoryHeader);
            CreateJumpToCategoryButton(__instance, tohSettingsTab, "TownOfHost_ForEver.Resources.TabIcon_Addons.png", ref jumpButtonY, AddOnCategoryHeader);

            tohSettingsBuilt = true;
            GameOptionsMenuUpdatePatch.MarkDirty();
        }

        public static StringOption GetOrCreateOptionBehaviour(OptionItem option)
        {
            if (option.OptionBehaviour != null && option.OptionBehaviour.gameObject != null)
            {
                return option.OptionBehaviour;
            }
            if (optionTemplate == null || tohSettingsTab == null || tohOptionBehaviours == null || gameSettingMenu == null)
            {
                return null;
            }

            var stringOption = Object.Instantiate(optionTemplate, tohSettingsTab.settingsContainer);
            tohOptionBehaviours.Add(stringOption);
            stringOption.SetClickMask(gameSettingMenu.GameSettingsButton.ClickMask);
            stringOption.SetUpFromData(stringOption.data, GameOptionsMenu.MASK_LAYER);
            stringOption.OnValueChanged = new Action<OptionBehaviour>((o) => { });
            stringOption.TitleText.text = option.GetName(option is RoleSpawnChanceOptionItem);
            stringOption.Value = stringOption.oldValue = option.CurrentValue;
            stringOption.ValueText.text = option.GetString();
            stringOption.name = option.Name;

            var indent = 0f;
            var parent = option.Parent;
            while (parent != null)
            {
                indent += 0.15f;
                parent = parent.Parent;
            }
            stringOption.LabelBackground.size += new Vector2(2f - indent * 2, 0f);
            stringOption.LabelBackground.transform.localPosition += new Vector3(-1f + indent, 0f, 0f);
            stringOption.TitleText.rectTransform.sizeDelta += new Vector2(2f - indent * 2, 0f);
            stringOption.TitleText.transform.localPosition += new Vector3(-1f + indent, 0f, 0f);

            option.OptionBehaviour = stringOption;
            option.Refresh();
            return stringOption;
        }

        private static MapSelectButton CreateJumpToCategoryButton(GameSettingMenu __instance, GameOptionsMenu tohTab, string resourcePath, ref float localY, CategoryHeaderMasked jumpTo)
        {
            var image = Utils.LoadSprite(resourcePath, 100f);
            var button = Object.Instantiate(__instance.GameSettingsTab.MapPicker.MapButtonOrigin, Vector3.zero, Quaternion.identity, tohTab.transform);
            button.SetImage(image, GameOptionsMenu.MASK_LAYER);
            button.transform.localPosition = new(7.1f, localY, -10f);
            button.Button.ClickMask = tohTab.ButtonClickMask;
            button.Button.OnClick.AddListener((Action)(() =>
            {
                tohTab.scrollBar.velocity = Vector2.zero;  // ドラッグの慣性によるスクロールを止める
                var relativePosition = tohTab.scrollBar.transform.InverseTransformPoint(jumpTo.transform.position);  // Scrollerのローカル空間における座標に変換
                var scrollAmount = CategoryJumpY - relativePosition.y;
                tohTab.scrollBar.Inner.localPosition = tohTab.scrollBar.Inner.localPosition + Vector3.up * scrollAmount;  // 強制スクロール
                tohTab.scrollBar.ScrollRelative(Vector2.zero);  // スクロール範囲内に収め，スクロールバーを更新する
            }));
            button.Button.activeSprites.transform.GetChild(0).gameObject.SetActive(false);  // チェックボックスを消す
            localY -= JumpButtonSpacing;
            return button;
        }
        private const float JumpButtonSpacing = 0.6f;
        // ジャンプしたカテゴリヘッダのScrollerとの相対Y座標がこの値になる
        private const float CategoryJumpY = 2f;
        private static CategoryHeaderMasked CreateCategoryHeader(GameSettingMenu __instance, GameOptionsMenu tohTab, string translationKey)
        {
            var categoryHeader = Object.Instantiate(__instance.GameSettingsTab.categoryHeaderOrigin, Vector3.zero, Quaternion.identity, tohTab.settingsContainer);
            categoryHeader.name = translationKey;
            categoryHeader.Title.text = GetString(translationKey);
            var maskLayer = GameOptionsMenu.MASK_LAYER;
            categoryHeader.Background.material.SetInt(PlayerMaterial.MaskLayer, maskLayer);
            if (categoryHeader.Divider != null)
            {
                categoryHeader.Divider.material.SetInt(PlayerMaterial.MaskLayer, maskLayer);
            }
            categoryHeader.Title.fontMaterial.SetFloat("_StencilComp", 3f);
            categoryHeader.Title.fontMaterial.SetFloat("_Stencil", (float)maskLayer);
            categoryHeader.transform.localScale = Vector3.one * GameOptionsMenu.HEADER_SCALE;
            return categoryHeader;
        }

        // 初めてロール設定を表示したときに発生する例外(バニラバグ)の影響を回避するためPrefix
        [HarmonyPatch(nameof(GameSettingMenu.ChangeTab)), HarmonyPrefix]
        public static void ChangeTabPrefix(bool previewOnly)
        {
            if (!previewOnly)
            {
                if (tohSettingsTab)
                {
                    tohSettingsTab.gameObject.SetActive(false);
                }
                if (tohSettingsButton)
                {
                    tohSettingsButton.SelectButton(false);
                }
            }
        }

        public const string TOHMenuName = "TownOfHostTab";
    }

    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Initialize))]
    public static class GameOptionsMenuInitializePatch
    {
        public static bool Prefix(GameOptionsMenu __instance)
        {
            if (__instance == null || __instance.name != GameSettingMenuPatch.TOHMenuName) return true;

            if (GameOptionsManager.Instance != null)
            {
                __instance.cachedData = GameOptionsManager.Instance.CurrentGameOptions;
            }
            return false;
        }

        public static void Postfix(GameOptionsMenu __instance)
        {
            if (__instance.name == GameSettingMenuPatch.TOHMenuName) return;

            foreach (var ob in __instance.Children)
            {
                switch (ob.Title)
                {
                    case StringNames.GameShortTasks:
                    case StringNames.GameLongTasks:
                    case StringNames.GameCommonTasks:
                        ob.Cast<NumberOption>().ValidRange = new FloatRange(0, 99);
                        break;
                    case StringNames.GameKillCooldown:
                        ob.Cast<NumberOption>().ValidRange = new FloatRange(0, 180);
                        break;
                    case StringNames.GameNumImpostors:
                        if (DebugModeManager.IsDebugMode)
                        {
                            ob.Cast<NumberOption>().ValidRange.min = 0;
                        }
                        break;
                    default:
                        break;
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Update))]
    public class GameOptionsMenuUpdatePatch
    {
        private static bool _dirty = true;
        private static CustomGameMode _lastGameMode;
        private static bool _lastAmHost;
        private static int _remainingOptionCreates;
        private static bool _needsMoreOptionCreates;

        public static void MarkDirty()
        {
            _dirty = true;
        }

        public static void Postfix(GameOptionsMenu __instance)
        {
            if (__instance.name != GameSettingMenuPatch.TOHMenuName) return;
            if (!GameSettingMenuPatch.IsTohSettingsBuilt || !__instance.gameObject.activeSelf) return;

            var currentGameMode = Options.CurrentGameMode;
            var amHost = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
            if (_lastGameMode != currentGameMode || _lastAmHost != amHost)
            {
                _lastGameMode = currentGameMode;
                _lastAmHost = amHost;
                _dirty = true;
            }

            if (!_dirty) return;
            RefreshNow(__instance);
        }

        public static void RefreshNow(GameOptionsMenu __instance)
        {
            if (__instance == null || !GameSettingMenuPatch.IsTohSettingsBuilt) return;
            _dirty = false;
            _needsMoreOptionCreates = false;
            _remainingOptionCreates = MaxOptionCreatesPerRefresh;
            var offset = 2.7f;
            var isOdd = true;

            UpdateCategoryHeader(GameSettingMenuPatch.MainCategoryHeader, ref offset);
            foreach (var option in OptionItem.MainOptions)
            {
                UpdateOption(ref isOdd, option, ref offset);
            }
            UpdateCategoryHeader(GameSettingMenuPatch.ImpostorRoleCategoryHeader, ref offset);
            foreach (var option in OptionItem.ImpostorRoleOptions)
            {
                UpdateOption(ref isOdd, option, ref offset);
            }
            UpdateCategoryHeader(GameSettingMenuPatch.MadmateRoleCategoryHeader, ref offset);
            foreach (var option in OptionItem.MadmateRoleOptions)
            {
                UpdateOption(ref isOdd, option, ref offset);
            }
            UpdateCategoryHeader(GameSettingMenuPatch.CrewmateRoleCategoryHeader, ref offset);
            foreach (var option in OptionItem.CrewmateRoleOptions)
            {
                UpdateOption(ref isOdd, option, ref offset);
            }
            UpdateCategoryHeader(GameSettingMenuPatch.NeutralRoleCategoryHeader, ref offset);
            foreach (var option in OptionItem.NeutralRoleOptions)
            {
                UpdateOption(ref isOdd, option, ref offset);
            }
            UpdateCategoryHeader(GameSettingMenuPatch.AnimalsRoleCategoryHeader, ref offset);
            foreach (var option in OptionItem.AnimalsRoleOptions)
            {
                UpdateOption(ref isOdd, option, ref offset);
            }
            UpdateCategoryHeader(GameSettingMenuPatch.AddOnCategoryHeader, ref offset);
            foreach (var option in OptionItem.AddOnOptions)
            {
                UpdateOption(ref isOdd, option, ref offset);
            }

            __instance.scrollBar.ContentYBounds.max = (-offset) - 1.5f;
            if (_needsMoreOptionCreates)
            {
                _dirty = true;
            }
        }
        private static void UpdateCategoryHeader(CategoryHeaderMasked categoryHeader, ref float offset)
        {
            offset -= GameOptionsMenu.HEADER_HEIGHT;
            categoryHeader.transform.localPosition = new(GameOptionsMenu.HEADER_X, offset, -2f);
        }
        private static void UpdateOption(ref bool isOdd, OptionItem item, ref float offset)
        {
            if (item == null) return;

            var enabled = true;
            var parent = item.Parent;

            // 親オプションの値を見て表示するか決める
            enabled = AmongUsClient.Instance.AmHost && !item.IsHiddenOn(Options.CurrentGameMode);
            while (parent != null && enabled)
            {
                enabled = parent.GetBool();
                parent = parent.Parent;
            }

            if (!enabled)
            {
                var hiddenOption = item.OptionBehaviour;
                if (hiddenOption != null && hiddenOption.gameObject != null)
                {
                    hiddenOption.gameObject.SetActive(false);
                }
                return;
            }

            var stringOption = item.OptionBehaviour;
            if (stringOption == null || stringOption.gameObject == null)
            {
                if (_remainingOptionCreates <= 0)
                {
                    _needsMoreOptionCreates = true;
                }
                else
                {
                    _remainingOptionCreates--;
                    stringOption = GameSettingMenuPatch.GetOrCreateOptionBehaviour(item);
                }
            }

            offset -= GameOptionsMenu.SPACING_Y;
            if (item.IsHeader)
            {
                // IsHeaderなら隙間を広くする
                offset -= HeaderSpacingY;
            }

            if (stringOption != null && stringOption.gameObject != null)
            {
                stringOption.gameObject.SetActive(true);
                stringOption.LabelBackground.color = item is IRoleOptionItem roleOption ? roleOption.RoleColor : (isOdd ? Color.cyan : Color.white);
                stringOption.transform.localPosition = new Vector3(
                    GameOptionsMenu.START_POS_X,
                    offset,
                    -2f);
            }

            isOdd = !isOdd;
        }

        private const int MaxOptionCreatesPerRefresh = 40;
        private const float HeaderSpacingY = 0.2f;
    }

    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Initialize))]
    public class StringOptionInitializePatch
    {
        public static bool Prefix(StringOption __instance)
        {
            var option = OptionItem.AllOptions.FirstOrDefault(opt => opt.OptionBehaviour == __instance);
            if (option == null) return true;

            __instance.OnValueChanged = new Action<OptionBehaviour>((o) => { });
            __instance.TitleText.text = option.GetName(option is RoleSpawnChanceOptionItem);
            __instance.Value = __instance.oldValue = option.CurrentValue;
            __instance.ValueText.text = option.GetString();

            return false;
        }
    }

    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Increase))]
    public class StringOptionIncreasePatch
    {
        public static bool Prefix(StringOption __instance)
        {
            var option = OptionItem.AllOptions.FirstOrDefault(opt => opt.OptionBehaviour == __instance);
            if (option == null) return true;

            option.SetValue(option.CurrentValue + (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 5 : 1));
            return false;
        }
    }

    [HarmonyPatch(typeof(StringOption), nameof(StringOption.Decrease))]
    public class StringOptionDecreasePatch
    {
        public static bool Prefix(StringOption __instance)
        {
            var option = OptionItem.AllOptions.FirstOrDefault(opt => opt.OptionBehaviour == __instance);
            if (option == null) return true;

            option.SetValue(option.CurrentValue - (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 5 : 1));
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSyncSettings))]
    public class RpcSyncSettingsPatch
    {
        public static void Postfix()
        {
            OptionItem.SyncAllOptions();
        }
    }
    [HarmonyPatch(typeof(RolesSettingsMenu), nameof(RolesSettingsMenu.InitialSetup))]
    public static class RolesSettingsMenuPatch
    {
        public static void Postfix(RolesSettingsMenu __instance)
        {
            foreach (var ob in __instance.advancedSettingChildren)
            {
                switch (ob.Title)
                {
                    case StringNames.EngineerCooldown:
                        ob.Cast<NumberOption>().ValidRange = new FloatRange(0, 180);
                        break;
                    case StringNames.ShapeshifterCooldown:
                        ob.Cast<NumberOption>().ValidRange = new FloatRange(0, 180);
                        break;
                    default:
                        break;
                }
            }
        }
    }
    [HarmonyPatch(typeof(NormalGameOptionsV10), nameof(NormalGameOptionsV10.SetRecommendations), typeof(int), typeof(bool), typeof(RulesPresets))]
    public static class SetRecommendationsPatch
    {
        public static bool Prefix(NormalGameOptionsV10 __instance, int numPlayers, bool isOnline, RulesPresets rulesPresets)
        {
            switch (rulesPresets)
            {
                case RulesPresets.Standard: SetStandardRecommendations(__instance, numPlayers, isOnline); return false;
                // スタンダード以外のプリセットは一旦そのままにしておく
                default: return true;
            }
        }
        private static void SetStandardRecommendations(NormalGameOptionsV10 __instance, int numPlayers, bool isOnline)
        {
            numPlayers = Mathf.Clamp(numPlayers, 4, 15);
            __instance.PlayerSpeedMod = __instance.MapId == 4 ? 1.25f : 1f; //AirShipなら1.25、それ以外は1
            __instance.CrewLightMod = 0.5f;
            __instance.ImpostorLightMod = 1.75f;
            __instance.KillCooldown = 25f;
            __instance.NumCommonTasks = 2;
            __instance.NumLongTasks = 4;
            __instance.NumShortTasks = 6;
            __instance.NumEmergencyMeetings = 1;
            if (!isOnline)
                __instance.NumImpostors = NormalGameOptionsV10.RecommendedImpostors[numPlayers];
            __instance.KillDistance = 0;
            __instance.DiscussionTime = 0;
            __instance.VotingTime = 150;
            __instance.IsDefaults = true;
            __instance.ConfirmImpostor = false;
            __instance.VisualTasks = false;

            __instance.roleOptions.SetRoleRate(RoleTypes.Shapeshifter, 0, 0);
            __instance.roleOptions.SetRoleRate(RoleTypes.Phantom, 0, 0);
            __instance.roleOptions.SetRoleRate(RoleTypes.Viper, 0, 0);
            __instance.roleOptions.SetRoleRate(RoleTypes.Scientist, 0, 0);
            __instance.roleOptions.SetRoleRate(RoleTypes.GuardianAngel, 0, 0);
            __instance.roleOptions.SetRoleRate(RoleTypes.Engineer, 0, 0);
            __instance.roleOptions.SetRoleRate(RoleTypes.Noisemaker, 0, 0);
            __instance.roleOptions.SetRoleRate(RoleTypes.Detective, 0, 0);
            __instance.roleOptions.SetRoleRate(RoleTypes.Tracker, 0, 0);
            __instance.roleOptions.SetRoleRecommended(RoleTypes.Shapeshifter);
            __instance.roleOptions.SetRoleRecommended(RoleTypes.Phantom);
            __instance.roleOptions.SetRoleRecommended(RoleTypes.Viper);
            __instance.roleOptions.SetRoleRecommended(RoleTypes.Scientist);
            __instance.roleOptions.SetRoleRecommended(RoleTypes.GuardianAngel);
            __instance.roleOptions.SetRoleRecommended(RoleTypes.Engineer);
            __instance.roleOptions.SetRoleRecommended(RoleTypes.Noisemaker);
            __instance.roleOptions.SetRoleRecommended(RoleTypes.Detective);
            __instance.roleOptions.SetRoleRecommended(RoleTypes.Tracker);

            if (Options.CurrentGameMode == CustomGameMode.HideAndSeek) //HideAndSeek
            {
                __instance.PlayerSpeedMod = 1.75f;
                __instance.CrewLightMod = 5f;
                __instance.ImpostorLightMod = 0.25f;
                __instance.NumImpostors = 1;
                __instance.NumCommonTasks = 0;
                __instance.NumLongTasks = 0;
                __instance.NumShortTasks = 10;
                __instance.KillCooldown = 10f;
            }
            if (Options.IsStandardHAS) //StandardHAS
            {
                __instance.PlayerSpeedMod = 1.75f;
                __instance.CrewLightMod = 5f;
                __instance.ImpostorLightMod = 0.25f;
                __instance.NumImpostors = 1;
                __instance.NumCommonTasks = 0;
                __instance.NumLongTasks = 0;
                __instance.NumShortTasks = 10;
                __instance.KillCooldown = 10f;
            }
        }
    }
}
