using System.Globalization;
using AmongUs.InnerNet.GameDataMessages;
using HarmonyLib;
using InnerNet;
using UnityEngine;
using static TownOfHostForE.Translator;
using Hazel;
using System.Collections.Generic;
using TownOfHostForE.Modules;

namespace TownOfHostForE
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]
    class MakePublicPatch
    {
        public static bool Prefix(GameStartManager __instance)
        {
            // 定数設定による公開ルームブロック
            if (!Main.AllowPublicRoom)
            {
                var message = GetString("DisabledByProgram");
                Logger.Info(message, "MakePublicPatch");
                Logger.SendInGame(message);
                return false;
            }
            if (ModUpdater.isBroken || ModUpdater.hasUpdate || !VersionChecker.IsSupported || !Main.IsPublicAvailableOnThisVersion)
            {
                var message = "";
                if (!Main.IsPublicAvailableOnThisVersion) message = GetString("PublicNotAvailableOnThisVersion");
                if (!VersionChecker.IsSupported) message = GetString("UnsupportedVersion");
                if (ModUpdater.isBroken) message = GetString("ModBrokenMessage");
                if (ModUpdater.hasUpdate) message = GetString("CanNotJoinPublicRoomNoLatest");
                Logger.Info(message, "MakePublicPatch");
                Logger.SendInGame(message);
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(MMOnlineManager), nameof(MMOnlineManager.Start))]
    class MMOnlineManagerStartPatch
    {
        public static void Postfix(MMOnlineManager __instance)
        {
            if (!(ModUpdater.hasUpdate || ModUpdater.isBroken || !VersionChecker.IsSupported || !Main.IsPublicAvailableOnThisVersion)) return;
            var obj = GameObject.Find("FindGameButton");
            if (obj)
            {
                obj?.SetActive(false);
                var parentObj = obj.transform.parent.gameObject;
                var textObj = Object.Instantiate<TMPro.TextMeshPro>(obj.transform.FindChild("Text_TMP").GetComponent<TMPro.TextMeshPro>());
                textObj.transform.position = new Vector3(1f, -0.3f, 0);
                textObj.name = "CanNotJoinPublic";
                textObj.DestroyTranslator();
                string message = "";
                if (ModUpdater.hasUpdate)
                {
                    message = GetString("CanNotJoinPublicRoomNoLatest");
                }
                else if (ModUpdater.isBroken)
                {
                    message = GetString("ModBrokenMessage");
                }
                else if (!VersionChecker.IsSupported)
                {
                    message = GetString("UnsupportedVersion");
                }
                else if (!Main.IsPublicAvailableOnThisVersion)
                {
                    message = GetString("PublicNotAvailableOnThisVersion");
                }
                textObj.text = $"<size=2>{Utils.ColorString(Color.red, message)}</size>";
            }
        }
    }
    [HarmonyPatch(typeof(SplashManager), nameof(SplashManager.Update))]
    class SplashLogoAnimatorPatch
    {
        public static void Prefix(SplashManager __instance)
        {
            if (DebugModeManager.AmDebugger)
            {
                __instance.sceneChanger.AllowFinishLoadingScene();
                __instance.startedSceneLoad = true;
            }
        }
    }
    [HarmonyPatch(typeof(EOSManager), nameof(EOSManager.IsAllowedOnline))]
    class RunLoginPatch
    {
        public static void Prefix(ref bool canOnline)
        {
#if DEBUG
            if (CultureInfo.CurrentCulture.Name != "ja-JP") canOnline = false;
#endif
        }
    }
    [HarmonyPatch(typeof(BanMenu), nameof(BanMenu.SetVisible))]
    class BanMenuSetVisiblePatch
    {
        public static bool Prefix(BanMenu __instance, bool show)
        {
            if (!AmongUsClient.Instance.AmHost) return true;
            show &= PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.Data != null;
            __instance.BanButton.gameObject.SetActive(AmongUsClient.Instance.CanBan());
            __instance.KickButton.gameObject.SetActive(AmongUsClient.Instance.CanKick());
            __instance.MenuButton.gameObject.SetActive(show);
            return false;
        }
    }
    [HarmonyPatch(typeof(InnerNet.InnerNetClient), nameof(InnerNet.InnerNetClient.CanBan))]
    class InnerNetClientCanBanPatch
    {
        public static bool Prefix(InnerNet.InnerNetClient __instance, ref bool __result)
        {
            __result = __instance.AmHost;
            return false;
        }
    }
    [HarmonyPatch(typeof(InnerNet.InnerNetClient), nameof(InnerNet.InnerNetClient.KickPlayer))]
    class KickPlayerPatch
    {
        public static void Prefix(InnerNet.InnerNetClient __instance, int clientId, bool ban)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (ban) BanManager.AddBanPlayer(AmongUsClient.Instance.GetRecentClient(clientId));
        }
    }
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendAllStreamedObjects))]
    class InnerNetObjectSerializePatch
    {
        public static void Prefix(InnerNetClient __instance)
        {
            if (AmongUsClient.Instance.AmHost)
                GameOptionsSender.SendAllGameOptions();
        }
    }
    [HarmonyPatch]
    class InnerNetClientPatch
    {
        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HandleMessage)), HarmonyPrefix]
        public static bool HandleMessagePatch(InnerNetClient __instance, MessageReader reader, SendOption sendOption)
        {
            if (DebugModeManager.IsDebugMode)
            {
                Logger.Info($"HandleMessagePatch:Packet({reader.Length}) ,SendOption:{sendOption}", "InnerNetClient");
            }
            else if (reader.Length > 1000)
            {
                Logger.Info($"HandleMessagePatch:Large Packet({reader.Length})", "InnerNetClient");
            }
            return true;
        }
        public static bool DontTouch = false;
        public const int StreamSplitSize = 500;
        static Dictionary<int, int> messageCount = new(10);
        const int warningThreshold = 100;
        static int peak = warningThreshold;
        static float timer = 0f;
        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate)), HarmonyPrefix]
        public static void FixedUpdatePatch(InnerNetClient __instance)
        {
            int last = (int)timer % 10;
            timer += Time.fixedDeltaTime;
            int current = (int)timer % 10;
            if (last != current)
            {
                messageCount[current] = 0;
            }
        }

        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendInitialData)), HarmonyPrefix]
        public static bool SendInitialDataPatch(InnerNetClient __instance, int clientId)
        {
            if (!ShouldFixSpawnPacketSize()) return true;

            try
            {
                Logger.Info("SendInitialDataPatch: Start", "InnerNetClient");
                var sentGameObjects = new HashSet<GameObject>();
                var gameManager = GameManager.Instance;

                if (gameManager)
                {
                    __instance.SendGameManager(clientId, gameManager);
                    sentGameObjects.Add(gameManager.gameObject);
                }

                var allObjects = __instance.allObjects?.allObjects;
                if (allObjects == null) return false;

                lock (__instance.allObjects)
                {
                    for (int i = 0; i < allObjects.Count; i++)
                    {
                        var netObject = allObjects[i];
                        if (!netObject) continue;
                        if (netObject.OwnerId == -4 && !__instance.AmModdedHost) continue;
                        if (!sentGameObjects.Add(netObject.gameObject)) continue;

                        var spawnMessage = __instance.CreateSpawnMessage(netObject, netObject.OwnerId, netObject.SpawnFlags);
                        if (spawnMessage == null) continue;
                        SendGameDataMessageTo(__instance, clientId, spawnMessage);
                        spawnMessage.ClearOrDecrementChildObjectDirt();
                    }
                }

                Logger.Info("SendInitialDataPatch: End", "InnerNetClient");
                return false;
            }
            catch (System.Exception ex)
            {
                Logger.Exception(ex, "SendInitialDataPatch");
                return true;
            }
        }

        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendOrDisconnect)), HarmonyPrefix]
        public static bool SendOrDisconnectPatch(InnerNetClient __instance, MessageWriter msg)
        {
            //分割するサイズ。大きすぎるとリトライ時不利、小さすぎると受信パケット取りこぼしが発生しうる。
            //Vanila側で500byteで分割しているため競合を避け1000byteに設定
            var limitSize = 1000;

            if (DebugModeManager.IsDebugMode)
            {
                Logger.Info($"SendOrDisconnectPatch:Packet({msg.Length}) ,SendOption:{msg.SendOption}", "InnerNetClient");
            }
            else if (msg.Length > limitSize)
            {
                Logger.Info($"SendOrDisconnectPatch:Large Packet({msg.Length}) ,SendOption:{msg.SendOption}", "InnerNetClient");
                DescribeLargePacket(msg);
            }
            //メッセージピークのログ出力
            if (msg.SendOption == SendOption.Reliable)
            {
                int last = (int)timer % 10;
                messageCount[last]++;
                int totalMessages = 0;
                foreach (var count in messageCount.Values)
                {
                    totalMessages += count;
                }
                if (totalMessages > warningThreshold)
                {
                    if (peak > totalMessages)
                    {
                        Logger.Warn($"SendOrDisconnectPatch:Packet Spam Detected ({peak})", "InnerNetClient");
                        peak = warningThreshold;
                    }
                    else
                    {
                        peak = totalMessages;
                    }
                }
            }
            if (!ShouldSplitLargePacket(msg)) return true;
            if (DontTouch || AntiBlackout.IsCached) return true;

            //ラージパケットを分割(9人以上部屋で落ちる現象の対策コード)

            //メッセージが大きすぎる場合は分割して送信を試みる
            if (msg.Length > limitSize)
            {
                var writer = MessageWriter.Get(msg.SendOption);
                var reader = MessageReader.Get(msg.ToByteArray(false));

                //Tagレベルの処理
                while (reader.Position < reader.Length)
                {
                    //Logger.Info($"SendOrDisconnectPatch:reader {reader.Position} / {reader.Length}", "InnerNetClient");

                    var partMsg = reader.ReadMessage();
                    var tag = partMsg.Tag;

                    //Logger.Info($"SendOrDisconnectPatch:partMsg Tag={tag} Length={partMsg.Length}", "InnerNetClient");

                    //TagがGameData,GameDataToの場合のみ分割処理
                    //それ以外では多分分割しなくても問題ない
                    if (tag is 5 or 6 && partMsg.Length > limitSize)
                    {
                        //分割を試みる
                        DivideLargeMessage(__instance, writer, partMsg);
                    }
                    else
                    {
                        //そのまま追加
                        WriteMessage(writer, partMsg);
                    }

                    //送信サイズが制限を超えた場合は送信
                    if (writer.Length > limitSize)
                    {
                        Send(__instance, writer);
                        writer.Clear(writer.SendOption);
                    }
                }

                //残りの送信
                if (writer.HasBytes(7))
                {
                    Send(__instance, writer);
                }

                writer.Recycle();
                reader.Recycle();
                return false;
            }
            return true;
        }
        private static void DivideLargeMessage(InnerNetClient __instance, MessageWriter writer, MessageReader partMsg)
        {
            var tag = partMsg.Tag;
            var GameId = partMsg.ReadInt32();
            var ClientId = -1;
            var hasSubMessage = false;

            //元と同じTagを開く
            writer.StartMessage(tag);
            writer.Write(GameId);
            if (tag == 6)
            {
                ClientId = partMsg.ReadPackedInt32();
                writer.WritePacked(ClientId);
            }

            //Flag単位の処理
            while (partMsg.Position < partMsg.Length)
            {
                var subMsg = partMsg.ReadMessage();
                var subLength = subMsg.Length;

                //加算すると制限を超える場合は先に送信
                if (writer.Length + subLength > 800 && hasSubMessage)
                {
                    writer.EndMessage();
                    Send(__instance, writer);
                    //再度Tagを開く
                    writer.Clear(writer.SendOption);
                    writer.StartMessage(tag);
                    writer.Write(GameId);
                    if (tag == 6)
                    {
                        writer.WritePacked(ClientId);
                    }
                    hasSubMessage = false;
                }
                //メッセージの出力
                WriteMessage(writer, subMsg);
                hasSubMessage = true;
            }
            writer.EndMessage();
        }

        private static void SendGameDataMessageTo(InnerNetClient __instance, int clientId, BaseGameDataMessage gameDataMessage)
        {
            var writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(6);
            writer.Write(__instance.GameId);
            writer.WritePacked(clientId);
            gameDataMessage.Serialize(writer);
            writer.EndMessage();

            if (writer.Length > 1000)
            {
                Logger.Warn($"SendInitialDataPatch: Large {gameDataMessage.GameDataType} Message Length={writer.Length}", "InnerNetClient");
            }

            Send(__instance, writer);
            writer.Recycle();
        }

        private static bool ShouldSplitLargePacket(MessageWriter msg)
        {
            return ShouldFixSpawnPacketSize();
        }

        public static bool ShouldFixSpawnPacketSize()
        {
            try
            {
                if (Options.FixSpawnPacketSize != null && Options.FixSpawnPacketSize.GetBool()) return true;

                return AmongUsClient.Instance != null &&
                       AmongUsClient.Instance.AmHost &&
                       GameStates.IsOnlineGame &&
                       GameStates.IsLobby;
            }
            catch
            {
                return false;
            }
        }

        private static void WriteMessage(MessageWriter writer, MessageReader reader)
        {
            writer.Write((ushort)reader.Length);
            writer.Write(reader.Tag);
            writer.Write(reader.ReadBytes(reader.Length));
        }

        private static void DescribeLargePacket(MessageWriter msg)
        {
            try
            {
                var reader = MessageReader.Get(msg.ToByteArray(false));
                var details = new List<string>();

                while (reader.Position < reader.Length && details.Count < 8)
                {
                    var partMsg = reader.ReadMessage();
                    details.Add($"top={DescribeRootTag(partMsg.Tag)} len={partMsg.Length}");

                    if (partMsg.Tag is 5 or 6)
                    {
                        partMsg.ReadInt32();
                        if (partMsg.Tag == 6) partMsg.ReadPackedInt32();

                        var subDetails = new List<string>();
                        while (partMsg.Position < partMsg.Length && subDetails.Count < 8)
                        {
                            var subMsg = partMsg.ReadMessage();
                            subDetails.Add($"{DescribeGameDataTag(subMsg.Tag)}:{subMsg.Length}{DescribeRpcSubMessage(subMsg)}");
                        }

                        if (subDetails.Count > 0)
                        {
                            details.Add($"sub=[{string.Join(", ", subDetails)}]");
                        }
                    }
                }

                Logger.Info($"SendOrDisconnectPatch: Large Packet Detail {string.Join(" / ", details)}", "InnerNetClient");
                reader.Recycle();
            }
            catch (System.Exception ex)
            {
                Logger.Warn($"SendOrDisconnectPatch: Large Packet Detail failed: {ex.Message}", "InnerNetClient");
            }
        }

        private static string DescribeRootTag(byte tag) => tag switch
        {
            5 => "GameData",
            6 => "GameDataTo",
            _ => tag.ToString(),
        };

        private static string DescribeGameDataTag(byte tag) => tag switch
        {
            1 => "Data",
            2 => "Rpc",
            4 => "Spawn",
            5 => "Despawn",
            6 => "Scene",
            7 => "Ready",
            _ => tag.ToString(),
        };

        private static string DescribeRpcSubMessage(MessageReader subMsg)
        {
            if (subMsg.Tag != 2) return "";
            try
            {
                var targetNetId = subMsg.ReadPackedUInt32();
                var callId = subMsg.ReadByte();
                return $"({targetNetId}/{RPC.GetRpcName(callId)})";
            }
            catch
            {
                return "";
            }
        }

        private static void Send(InnerNetClient __instance, MessageWriter writer)
        {
            Logger.Info($"SendOrDisconnectPatch: SendMessage Length={writer.Length}", "InnerNetClient");
            var err = __instance.connection.Send(writer);
            if (err != SendErrors.None)
            {
                Logger.Info($"SendOrDisconnectPatch: SendMessage Error={err}", "InnerNetClient");
            }
        }

        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.GetMaxMessagePackingLimit)), HarmonyPrefix]
        public static bool GetMaxMessagePackingLimitPatch(ref int __result)
        {
            if (!ShouldFixSpawnPacketSize()) return true;
            __result = StreamSplitSize;
            return false;
        }
    }
}
