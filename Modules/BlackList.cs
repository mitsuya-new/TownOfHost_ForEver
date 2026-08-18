using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using InnerNet;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace TownOfHostForE.Modules;

public static class Blacklist
{
    public static class BlacklistHash
    {
        public static string ToHash(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";

            byte[] beforeByteArray = Encoding.UTF8.GetBytes(str);
            SHA256 sha256 = SHA256.Create();

            byte[] afterByteArray = sha256.ComputeHash(beforeByteArray);
            sha256.Clear();

            StringBuilder sb = new();
            foreach (byte b in afterByteArray)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }

    public class BlackPlayer
    {
        public static List<BlackPlayer> Players = new();
        public string Code;
        public string AddedMod = "None";
        public string ReasonCode = "NoneCode";
        public string ReasonTitle = "";
        public string ReasonDescription = "None";
        public DateTime? EndBanTime = null;
        public bool IsPUID;

        public BlackPlayer(string code, string addedMod, string reasonCode,
            string reasonTitle, string reasonDescription, bool isPuid, DateTime? endBanTime = null)
        {
            Code = code;
            AddedMod = addedMod;
            ReasonCode = reasonCode;
            ReasonTitle = reasonTitle;
            ReasonDescription = reasonDescription;
            EndBanTime = endBanTime;
            IsPUID = isPuid;
            Players.Add(this);
        }
    }

    public const string BlacklistServerURL = "https://blacklist.supernewroles.com/api/get_list?hash=true";
    static bool downloaded = false;
    static bool downloading = false;

    private static bool UseJapanese =>
        Main.ForceJapanese?.Value == true ||
        (TranslationController.InstanceExists &&
         TranslationController.Instance.currentLanguage.languageID == SupportedLangs.Japanese);

    private static DateTime? ParseEndBanTime(string endBanTime)
    {
        if (string.IsNullOrEmpty(endBanTime) || endBanTime == "never") return null;
        return DateTime.TryParse(endBanTime, out DateTime resultTime)
            ? resultTime - new TimeSpan(9, 0, 0)
            : null;
    }

    private static void AddPlayersFromJson(JToken users, string codeKey, bool isPuid)
    {
        if (users == null) return;

        for (var user = users.First; user != null; user = user.Next)
        {
            string endBanTime = user["EndBanTime"]?.ToString();
            _ = new BlackPlayer(
                user[codeKey]?.ToString(),
                user["AddedMod"]?.ToString(),
                user["Reason"]?["Code"]?.ToString(),
                user["Reason"]?["Title"]?.ToString(),
                user["Reason"]?["Description"]?.ToString(),
                isPuid,
                ParseEndBanTime(endBanTime));
        }
    }

    private static bool IsLocalClient(ClientData clientData)
    {
        return PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.GetClientId() == clientData.Id;
    }

    private static string GetClientHash(ClientData clientData, bool isPuid)
    {
        return BlacklistHash.ToHash(isPuid ? clientData?.ProductUserId : clientData?.FriendCode);
    }

    public static IEnumerator FetchBlacklist()
    {
        if (downloaded || downloading)
        {
            yield break;
        }

        downloading = true;
        var request = UnityWebRequest.Get(BlacklistServerURL);
        yield return request.SendWebRequest();

        if (request.isNetworkError || request.isHttpError)
        {
            downloading = false;
            downloaded = false;
            Logger.Info("Blacklist Error Fetch:" + request.responseCode, "BlackList");
            yield break;
        }

        try
        {
            var json = JObject.Parse(request.downloadHandler.text);
            BlackPlayer.Players.Clear();
            AddPlayersFromJson(json["blockedPlayers"], "FriendCode", false);
            AddPlayersFromJson(json["blockedPlayersPUID"], "PUID", true);
            downloaded = true;
            Logger.Info($"Blacklist loaded: {BlackPlayer.Players.Count}", "BlackList");
        }
        catch (Exception e)
        {
            downloaded = false;
            Logger.Info(e.ToString(), "BlackList");
        }

        downloading = false;
    }

    public static IEnumerator Check(ClientData clientData = null, int ClientId = -1)
    {
        if (clientData == null)
        {
            do
            {
                yield return null;
                clientData = AmongUsClient.Instance
                    .allClients
                    .ToArray()
                    .FirstOrDefault(client => client.Id == ClientId);
            } while (clientData == null);
        }

        while (downloading && !downloaded)
        {
            yield return null;
        }

        if (!downloaded)
        {
            Logger.Error("Blacklist data has not been downloaded.", "BCheck");
            if (IsLocalClient(clientData))
            {
                AmongUsClient.Instance.ExitGame(DisconnectReasons.Custom);
                AmongUsClient.Instance.LastCustomDisconnect = UseJapanese
                    ? "<size=0%>MOD</size><size=0%>NoFriend</size><size=180%>ブラックリストデータを取得できませんでした。</size>\n\nネットワーク環境を確認して、ゲームを再起動してください。"
                    : "<size=0%>MOD</size><size=0%>NoFriend</size><size=180%>No blacklist data has been downloaded.</size>\n\nPlease review your network environment and restart the game.";
            }
            yield break;
        }

        if ((string.IsNullOrEmpty(clientData.FriendCode) || !clientData.FriendCode.Contains('#')) &&
            AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame)
        {
            if (IsLocalClient(clientData))
            {
                AmongUsClient.Instance.ExitGame(DisconnectReasons.Custom);
                AmongUsClient.Instance.LastCustomDisconnect = UseJapanese
                    ? "<size=0%>MOD</size><size=0%>NoFriend</size><size=225%>フレンドコードがありません</size>\n\nおうちのひとにみせてください。\n\n【保護者の方へ】\nフレンドコードが設定されていないため、このMODをプレイできません。\nフレンド機能を有効にしてください。\nフレンド機能を有効にする：<link=\"https://parents.innersloth.com/ja/login\">https://parents.innersloth.com/ja/login</link>"
                    : "<size=0%>MOD</size><size=0%>NoFriend</size><size=225%>No friend code</size>\n\nPlease show this to your family.\n\nFor parents:\nYou cannot play this mod because you do not have a friend code set up.\nPlease enable the friend function.\nEnable the friend function:<link=\"https://parents.innersloth.com/ja/login\">https://parents.innersloth.com/ja/login</link>";
            }
            else if (Options.KickPlayerFriendCodeNotExist.GetBool())
            {
                AmongUsClient.Instance.KickPlayer(clientData.Id, ban: true);
            }
        }

        foreach (var player in BlackPlayer.Players)
        {
            if (player.EndBanTime.HasValue && player.EndBanTime.Value < DateTime.UtcNow)
                continue;

            if (player.Code != GetClientHash(clientData, player.IsPUID))
                continue;

            if (IsLocalClient(clientData))
            {
                AmongUsClient.Instance.ExitGame(DisconnectReasons.Custom);
                AmongUsClient.Instance.LastCustomDisconnect = UseJapanese
                    ? "<size=0%>MOD</size>" + player.ReasonTitle + "\n\nこのアカウントはMODによりゲームプレイが制限されています。\nBANコード：" + player.ReasonCode + "\n理由：" + player.ReasonDescription + "\n期間：" + (!player.EndBanTime.HasValue ? "永久" : player.EndBanTime.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") + "まで")
                    : "<size=0%>MOD</size>" + player.ReasonTitle + "\n\nThis account's gameplay is restricted by a MOD.\nBAN code: " + player.ReasonCode + "\nReason: " + player.ReasonDescription + "\nPeriod: " + (!player.EndBanTime.HasValue ? "Permanent" : player.EndBanTime.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss"));
            }
            else
            {
                AmongUsClient.Instance.KickPlayer(clientData.Id, ban: true);
                Logger.SendInGame(string.Format(Translator.GetString("Message.BlackList"), clientData.PlayerName, player.ReasonCode));
            }
        }
    }
}

[HarmonyPatch(typeof(DisconnectPopup), nameof(DisconnectPopup.Close))]
internal class DisconnectPopupClosePatch
{
    public static void Prefix(DisconnectPopup __instance)
    {
        try
        {
            if (AmongUsClient.Instance.LastDisconnectReason == DisconnectReasons.Custom &&
                AmongUsClient.Instance.LastCustomDisconnect.StartsWith("<size=0%>MOD</size>"))
            {
                __instance.transform.FindChild("CloseButton").localPosition = new(-2.75f, 0.5f, 0);
                __instance.GetComponent<SpriteRenderer>().size = new(5, 1.5f);
                __instance._textArea.fontSizeMin = 1.9f;
                __instance._textArea.enableWordWrapping = true;
            }
        }
        catch (Exception e)
        {
            Logger.Info(e.ToString(), "BlackList");
        }
    }
}

[HarmonyPatch(typeof(DisconnectPopup), nameof(DisconnectPopup.DoShow))]
internal class DisconnectPopupDoShowPatch
{
    public static void Postfix(DisconnectPopup __instance)
    {
        if (AmongUsClient.Instance.LastDisconnectReason == DisconnectReasons.Custom &&
            AmongUsClient.Instance.LastCustomDisconnect.StartsWith("<size=0%>MOD</size>"))
        {
            __instance.transform.FindChild("CloseButton").localPosition = new(-3.2f, 2.15f, -1);
            __instance.GetComponent<SpriteRenderer>().size = new(6, 4);
            __instance._textArea.fontSizeMin = 1.9f;
            __instance._textArea.enableWordWrapping = false;
            if (AmongUsClient.Instance.LastCustomDisconnect.StartsWith("<size=0%>MOD</size><size=0%>NoFriend</size>"))
            {
                __instance.GetComponentInChildren<SelectableHyperLink>().transform.localPosition = new(1.25f, -1.25f, -2);
            }
        }
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
internal class OnGameJoinedPatch
{
    public static void Postfix(AmongUsClient __instance)
    {
        __instance.StartCoroutine(Blacklist.Check(ClientId: __instance.ClientId).WrapToIl2Cpp());
        _ = new LateTask(() =>
        {
            foreach (var pc in Main.AllPlayerControls)
            {
                if (pc?.GetClient() != null)
                    __instance.StartCoroutine(Blacklist.Check(pc.GetClient(), pc.GetClientId()).WrapToIl2Cpp());
            }
            __instance.StartCoroutine(Blacklist.Check(ClientId: __instance.ClientId).WrapToIl2Cpp());
        }, 1f, "BlacklistCheck");
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
internal class OnPlayerJoinedPatch
{
    public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ClientData data)
    {
        if (!__instance.AmHost) return;

        __instance.StartCoroutine(Blacklist.Check(data).WrapToIl2Cpp());

        foreach (var pc in Main.AllPlayerControls)
        {
            if (pc?.GetClient() != null)
                __instance.StartCoroutine(Blacklist.Check(pc.GetClient(), pc.GetClientId()).WrapToIl2Cpp());
        }
    }
}
