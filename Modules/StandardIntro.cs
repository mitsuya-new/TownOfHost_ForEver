using System;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHostForE.Roles.Core;

namespace TownOfHostForE
{
    class StandardIntro
    {
        private const int MaxPacketSize = 800;

        private static bool IsEnabled()
            => AmongUsClient.Instance.AmHost
                && Options.CurrentGameMode == CustomGameMode.Standard
                && Main.SetRoleOverride;

        public static void CoGameIntroWeight()
        {
            if (!IsEnabled()) return;

            _ = new LateTask(() =>
            {
                try
                {
                    InnerNetClientPatch.DontTouch = true;
                    GameDataSerializePatch.SerializeMessageCount++;
                    SendDisconnectedStateForIntro();
                    Logger.Info("SetDisconnected", "StandardIntro");
                    GameDataSerializePatch.DontTouch = true;

                    foreach (var data in GameData.Instance.AllPlayers)
                    {
                        data.Disconnected = false;
                    }
                }
                finally
                {
                    InnerNetClientPatch.DontTouch = false;
                    GameDataSerializePatch.SerializeMessageCount = Math.Max(0, GameDataSerializePatch.SerializeMessageCount - 1);
                }
            }, 0.5f, "setdisconnected");
        }

        public static void CoResetRoleY()
        {
            if (!IsEnabled()) return;

            Logger.Info("ShowIntro", "StandardIntro");
            GameDataSerializePatch.DontTouch = false;
            InnerNetClientPatch.DontTouch = true;
            GameDataSerializePatch.SerializeMessageCount++;

            try
            {
                SendHostIntroReset();
                SendIntroRoles(firstPhase: true);

                _ = new LateTask(() =>
                {
                    try
                    {
                        RestoreDisconnectedStateForIntro();
                        SendIntroRoles(firstPhase: false);
                        ScheduleTaskRefresh();
                        ScheduleBaseRoleRestore();
                        SelectRolesPatch.roleAssigned = true;
                    }
                    finally
                    {
                        InnerNetClientPatch.DontTouch = false;
                        GameDataSerializePatch.SerializeMessageCount = Math.Max(0, GameDataSerializePatch.SerializeMessageCount - 1);
                    }
                }, 0.75f, "SetRoleDelay");

                _ = new LateTask(() =>
                {
                    HudManagerCoShowIntroPatch.Cancel = false;
                    var hud = DestroyableSingleton<HudManager>.Instance;
                    hud.StartCoroutine(hud.CoShowIntro());
                    hud.HideGameLoader();
                    Utils.NotifyRoles(ForceLoop: true);
                }, 0.2f, "ShowIntro");
            }
            catch
            {
                InnerNetClientPatch.DontTouch = false;
                GameDataSerializePatch.SerializeMessageCount = Math.Max(0, GameDataSerializePatch.SerializeMessageCount - 1);
                throw;
            }
        }

        private static void SendDisconnectedStateForIntro()
        {
            var stream = StartGameDataMessage();
            var shouldStartNewMessage = false;
            var hostId = PlayerControl.LocalPlayer.PlayerId;

            foreach (var data in GameData.Instance.AllPlayers)
            {
                if (data.PlayerId == hostId) continue;

                if (shouldStartNewMessage)
                {
                    stream = StartGameDataMessage();
                    shouldStartNewMessage = false;
                }

                data.Disconnected = true;
                WritePlayerInfo(stream, data);
                Logger.Info($"{data.PlayerName}", "StandardIntro");

                if (stream.Length > MaxPacketSize)
                {
                    shouldStartNewMessage = true;
                    SendAndRecycle(stream);
                }
            }

            if (!shouldStartNewMessage)
            {
                SendAndRecycle(stream);
            }
        }

        private static void SendHostIntroReset()
        {
            var host = PlayerControl.LocalPlayer;
            var stream = StartGameDataMessage();

            host.Data.Disconnected = true;
            WritePlayerInfo(stream, host.Data);

            stream.StartMessage(2);
            stream.WritePacked(host.NetId);
            stream.Write((byte)RpcCalls.SetRole);
            stream.Write((ushort)RoleTypes.Crewmate);
            stream.Write(true);
            stream.EndMessage();

            var count = 0;
            foreach (var data in GameData.Instance.AllPlayers)
            {
                count++;
                data.Disconnected = false;
                if (count > 4) continue;
                WritePlayerInfo(stream, data);
            }

            SendAndRecycle(stream);
        }

        private static void RestoreDisconnectedStateForIntro()
        {
            var stream = StartGameDataMessage();
            var shouldStartNewMessage = false;
            var count = 0;

            foreach (var data in GameData.Instance.AllPlayers)
            {
                count++;
                if (count <= 4) continue;

                if (shouldStartNewMessage)
                {
                    stream = StartGameDataMessage();
                    shouldStartNewMessage = false;
                }

                data.Disconnected = false;
                WritePlayerInfo(stream, data);

                if (stream.Length > MaxPacketSize)
                {
                    shouldStartNewMessage = true;
                    SendAndRecycle(stream);
                }
            }

            if (!shouldStartNewMessage)
            {
                SendAndRecycle(stream);
            }
        }

        private static void SendIntroRoles(bool firstPhase)
        {
            foreach (var pc in Main.AllPlayerControls)
            {
                if (pc.PlayerId == PlayerControl.LocalPlayer.PlayerId) continue;
                if (pc.GetClientId() == -1) continue;

                var roleType = GetIntroRoleType(pc, firstPhase);
                pc.RpcSetRoleDesync(roleType, pc.GetClientId(), SendOption.None);
            }
        }

        private static RoleTypes GetIntroRoleType(PlayerControl pc, bool firstPhase)
        {
            var role = pc.GetCustomRole();
            var roleType = role.GetRoleTypes();
            var roleInfo = role.GetRoleInfo();

            if (roleInfo?.IsDesyncImpostor == true || role.IsMadmate() || (role.IsNeutral() && role is not CustomRoles.Egoist))
            {
                if (role.IsCrewmate()) return RoleTypes.Crewmate;
                if (role.IsMadmate()) return firstPhase ? RoleTypes.Crewmate : RoleTypes.Phantom;
                if (role.IsNeutral() && role is not CustomRoles.Egoist) return firstPhase ? RoleTypes.Impostor : RoleTypes.Crewmate;
            }

            return roleType;
        }

        private static void ScheduleTaskRefresh()
        {
            _ = new LateTask(() =>
            {
                foreach (var pc in Main.AllPlayerControls)
                {
                    if (RpcSetTasksPatch.taskIds.TryGetValue(pc.PlayerId, out var taskids))
                    {
                        pc.Data.RpcSetTasks(taskids);
                    }
                    else
                    {
                        Logger.Error($"{pc.Data.PlayerName} => taskIds is null", "AssignTask");
                        pc.Data.RpcSetTasks(Array.Empty<byte>());
                    }
                }

                foreach (var pc in Main.AllPlayerControls)
                {
                    PlayerState.GetByPlayerId(pc.PlayerId).InitTask(pc);
                }
                GameData.Instance.RecomputeTaskCounts();
                TaskState.InitialTotalTasks = GameData.Instance.TotalTasks;
            }, 3f, "SetTask");
        }

        private static void ScheduleBaseRoleRestore()
        {
            var delay = 3.5f + (GameStates.IsOnlineGame ? 0.4f : 0f);
            _ = new LateTask(() =>
            {
                foreach (var pc in Main.AllPlayerControls)
                {
                    RestoreBaseRole(pc);
                }

                Utils.NotifyRoles(ForceLoop: true);
            }, delay, "RestoreBaseRole");
        }

        private static void RestoreBaseRole(PlayerControl pc)
        {
            if (pc == null || pc.Data == null || pc.GetCustomRole() == CustomRoles.GM) return;

            var role = pc.GetCustomRole();
            var roleInfo = role.GetRoleInfo();
            if (roleInfo == null) return;

            var roleType = roleInfo.BaseRoleType.Invoke();
            if (pc.PlayerId == PlayerControl.LocalPlayer.PlayerId)
            {
                RoleManager.Instance.SetRole(PlayerControl.LocalPlayer, roleType);
            }
            else if (pc.GetClientId() != -1)
            {
                pc.RpcSetRoleDesync(roleType, pc.GetClientId());
            }

            Logger.Info($"{pc.Data.PlayerName} => {roleType}", "StandardIntro.RestoreBaseRole");
        }

        private static MessageWriter StartGameDataMessage()
        {
            var stream = MessageWriter.Get(SendOption.Reliable);
            stream.StartMessage(5);
            stream.Write(AmongUsClient.Instance.GameId);
            return stream;
        }

        private static void WritePlayerInfo(MessageWriter stream, NetworkedPlayerInfo data)
        {
            stream.StartMessage(1);
            stream.WritePacked(data.NetId);
            data.Serialize(stream, false);
            stream.EndMessage();
        }

        private static void SendAndRecycle(MessageWriter stream)
        {
            stream.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(stream);
            stream.Recycle();
        }
    }
}
