using System.Collections.Generic;
using System.Reflection;
using AmongUs.GameOptions;
using HarmonyLib;
using InnerNet;

using TownOfHostForE.Roles.Core;
using static TownOfHostForE.Translator;

namespace TownOfHostForE
{
    [HarmonyPatch(typeof(RoleOptionSetting), nameof(RoleOptionSetting.UpdateValuesAndText))]
    class ChanceChangePatch
    {
        public static void Postfix(RoleOptionSetting __instance)
        {
            // The Phantom does not work together with desynchronized impostor roles e.g. Sheriff so we need to disable it.
            // This may be removed in the future when we have implemented changing vanilla role or some other stuff.
            if (__instance.Role.Role is RoleTypes.GuardianAngel || (__instance.Role.Role is RoleTypes.Phantom && !DebugModeManager.IsDebugMode))
            {
                string disableText = $" ({GetString("Disabled")})";
                //+-ボタンを非表示
                foreach (var button in __instance.GetComponentsInChildren<PassiveButton>())
                {
                    button.gameObject.SetActive(false);
                }
                if (!__instance.titleText.text.Contains(disableText))
                    __instance.titleText.text += disableText;
                if (__instance.roleChance != 0 || __instance.roleMaxCount != 0)
                {
                    __instance.roleChance = 0;
                    __instance.roleMaxCount = 0;
                    __instance.OnValueChanged.Invoke(__instance);
                }
            }
        }
    }

    [HarmonyPatch]
    class ClassicOnlyCreateGamePatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CreateGameOptions), nameof(CreateGameOptions.Start));
            yield return AccessTools.Method(typeof(CreateGameOptions), nameof(CreateGameOptions.Show));
        }

        public static void Postfix(CreateGameOptions __instance)
        {
            __instance.SetGameMode(GameModes.Normal);

            var buttons = __instance.modeButtons;
            if (buttons == null) return;
            // 作成画面の先頭はクラシック。HnSはマウス・コントローラーとも選択不可にする。
            for (var i = 1; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (!button) continue;
                button.gameObject.SetActive(false);
                __instance.ControllerSelectable?.Remove(button);
            }
        }
    }

    [HarmonyPatch]
    class ClassicOnlyCreateGameModePatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CreateGameOptions), nameof(CreateGameOptions.SetGameMode));
            yield return AccessTools.Method(typeof(CreateGameOptions), nameof(CreateGameOptions.SwitchOptions));
        }

        public static void Prefix([HarmonyArgument(0)] ref GameModes mode)
        {
            mode = GameModes.Normal;
        }
    }

    [HarmonyPatch]
    class ClassicOnlyCreateGameSelectionPatch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CreateGameOptions), nameof(CreateGameOptions.SelectMode));
            yield return AccessTools.Method(typeof(CreateGameOptions), nameof(CreateGameOptions.SetModeButton));
        }

        public static void Prefix([HarmonyArgument(0)] ref int index)
        {
            index = 0;
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HostGame))]
    class HostClassicGamePatch
    {
        public static void Prefix([HarmonyArgument(0)] ref IGameOptions settings)
        {
            // 強制的にクラッシクモードに
            var manager = GameOptionsManager.Instance;
            var classicOptions = settings?.TryCast<NormalGameOptionsV12>() ?? manager.normalGameHostOptions;
            manager.SwitchGameMode(GameModes.Normal);
            settings = classicOptions.Cast<IGameOptions>();
            manager.GameHostOptions = settings;
            manager.CurrentGameOptions = settings;
        }
    }
}
