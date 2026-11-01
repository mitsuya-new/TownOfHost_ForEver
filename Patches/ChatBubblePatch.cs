using HarmonyLib;
using UnityEngine;

namespace TownOfHostForE.Patches
{
    [HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetRight))]
    class ChatBubbleSetRightPatch
    {
        public static void Postfix(ChatBubble __instance)
        {
            if (Main.isChatCommand) __instance.SetLeft();
        }
    }
    [HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetName))]
    class ChatBubbleSetNamePatch
    {
        public static void Postfix(ChatBubble __instance)
        {
            if (GameStates.IsInGame && __instance.playerInfo.PlayerId == PlayerControl.LocalPlayer.PlayerId)
                __instance.NameText.color = PlayerControl.LocalPlayer.GetRoleColor();
        }
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.SetChatBubbleName))]
    class ChatBubbleMeetingNameColorPatch
    {
        public static void Postfix([HarmonyArgument(0)] ChatBubble bubble, [HarmonyArgument(1)] NetworkedPlayerInfo playerInfo)
        {
            if (!GameStates.IsModHost || !GameStates.IsMeeting || !bubble || !bubble.NameText || playerInfo == null) return;

            var seer = PlayerControl.LocalPlayer;
            if (!NameColorManager.ShouldRemoveVanillaImpostorColor(seer) || playerInfo.PlayerId == seer.PlayerId) return;
            var displayName = bubble.NameText.text;
            // 名前欄に本文を載せるシステムメッセージは、見出しなどの色をそのまま残す。
            if (string.IsNullOrEmpty(displayName) || displayName.Contains("<align=\"left\">")) return;

            var target = Utils.GetPlayerById(playerInfo.PlayerId);
            if (target == null) return;

            // TMPの基底色と埋め込み色タグの両方を補正し、能力による色付けを再適用する。
            bubble.NameText.color = Color.white;
            bubble.NameText.text = displayName.ApplyNameColorData(seer, target, true);
        }
    }
}
