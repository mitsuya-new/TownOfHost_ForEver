using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AmongUs.GameOptions;
using Hazel;

using TownOfHostForE.Attributes;
using TownOfHostForE.Modules;
using TownOfHostForE.Roles.Animals;
using TownOfHostForE.Roles.Core;
using TownOfHostForE.Roles.Neutral;

namespace TownOfHostForE
{
    public static class AntiBlackout
    {
        ///<summary>
        ///追放処理を上書きするかどうか
        ///</summary>
        public static bool OverrideExiledPlayer => Main.AllPlayerControls.Count() < 4 && (Options.NoGameEnd.GetBool() || Jackal.RoleInfo.IsEnable || AnimalsIsEnable());
        public static bool IsCached { get; private set; } = false;
        public static bool IsSet { get; private set; } = false;
        public static byte DummyImpostorPlayer { get; private set; } = byte.MaxValue;
        public static MeetingVoteManager.VoteResult? VoteResult;
        private static Dictionary<byte, (bool isDead, bool Disconnected)> isDeadCache = new();
        private static List<byte> roleCache = new();
        private readonly static LogHandler logger = Logger.Handler("AntiBlackout");

        private static bool AnimalsIsEnable()
        {
            return Coyote.RoleInfo.IsEnable ||
                   Braki.RoleInfo.IsEnable  ||
                   Nyaoha.RoleInfo.IsEnable  ||
                   //Vulture.RoleInfo.IsEnable  ||
                   //Badger.RoleInfo.IsEnable  ||
                   Leopard.RoleInfo.IsEnable;
        }

        public static void SetIsDead(bool doSend = true, [CallerMemberName] string callerMethodName = "")
        {
            logger.Info($"SetIsDead is called from {callerMethodName}");
            if (IsCached)
            {
                logger.Info("再度SetIsDeadを実行する前に、RestoreIsDeadを実行してください。");
                return;
            }
            isDeadCache.Clear();
            foreach (var info in GameData.Instance.AllPlayers)
            {
                if (info == null) continue;
                isDeadCache[info.PlayerId] = (info.IsDead, info.Disconnected);
                info.IsDead = false;
                info.Disconnected = false;
            }
            IsCached = true;
            if (doSend) SendGameData();
        }
        public static void RestoreIsDead(bool doSend = true, [CallerMemberName] string callerMethodName = "")
        {
            logger.Info($"RestoreIsDead is called from {callerMethodName}");
            foreach (var info in GameData.Instance.AllPlayers)
            {
                if (info == null) continue;
                if (isDeadCache.TryGetValue(info.PlayerId, out var val))
                {
                    info.IsDead = val.isDead;
                    info.Disconnected = val.Disconnected;
                }
            }
            isDeadCache.Clear();
            IsCached = false;
            if (doSend) SendGameData();
        }

        public static void SendGameData([CallerMemberName] string callerMethodName = "")
        {
            logger.Info($"SendGameData is called from {callerMethodName}");
            foreach (var playerinfo in GameData.Instance.AllPlayers)
            {
                MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
                // 書き込み {}は読みやすさのためです。
                writer.StartMessage(5); //0x05 GameData
                {
                    writer.Write(AmongUsClient.Instance.GameId);
                    writer.StartMessage(1); //0x01 Data
                    {
                        writer.WritePacked(playerinfo.NetId);
                        playerinfo.Serialize(writer, true);

                    }
                    writer.EndMessage();
                }
                writer.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(writer);
                writer.Recycle();
            }
        }
        public static void TraceAllPlayerStates(string context)
        {
            if (GameData.Instance == null) return;

            logger.Info($"TraceAllPlayerStates: {context}");
            foreach (var info in GameData.Instance.AllPlayers)
            {
                if (info == null) continue;

                var role = info.Role?.Role.ToString() ?? "null";
                var customRole = PlayerState.GetByPlayerId(info.PlayerId)?.MainRole.ToString() ?? "null";
                logger.Info($"{info.PlayerName}({info.PlayerId}) Role={role}, CustomRole={customRole}, IsDead={info.IsDead}, Disconnected={info.Disconnected}");
            }
        }

        public static void OnDisconnect(NetworkedPlayerInfo player)
        {
            // 実行条件: クライアントがホストである, IsDeadが上書きされている, playerが切断済み
            if (!AmongUsClient.Instance.AmHost || !IsCached || !player.Disconnected) return;
            isDeadCache[player.PlayerId] = (true, true);
            player.IsDead = player.Disconnected = false;
            SendGameData();
            if (player.PlayerId == DummyImpostorPlayer)
            {
                SetRole(VoteResult);
            }
        }

        public static void SetRole(MeetingVoteManager.VoteResult? result = null)
        {
            if (!AmongUsClient.Instance.AmHost || GameData.Instance == null) return;

            roleCache.Clear();
            IsSet = true;

            var exiledId = result?.Exiled?.PlayerId ?? byte.MaxValue;
            var dummy = SelectDummyImpostor(exiledId);
            DummyImpostorPlayer = dummy?.PlayerId ?? byte.MaxValue;

            if (DummyImpostorPlayer == byte.MaxValue)
            {
                logger.Warn("一時インポスター対象が見つかりませんでした");
                return;
            }

            foreach (var player in Main.AllPlayerControls)
            {
                roleCache.Add(player.PlayerId);
            }
            GameDataSerializePatch.SerializeMessageCount++;
            try
            {
                var sender = CustomRpcSender.Create("AntiBlackoutSetRole", SendOption.Reliable);
                sender.StartMessage();

                foreach (var target in GameData.Instance.AllPlayers)
                {
                    if (target == null || target.Disconnected || target.Object == null) continue;

                    var setRole = target.PlayerId == DummyImpostorPlayer ? RoleTypes.Impostor : RoleTypes.Crewmate;
                    if (setRole == RoleTypes.Impostor)
                    {
                        target.IsDead = false;
                    }

                    sender.StartRpc(target.Object.NetId, RpcCalls.SetRole)
                        .Write((ushort)setRole)
                        .Write(true)
                        .EndRpc();

                    logger.Info($"{target.PlayerName} => {setRole}");
                }

                sender.EndMessage();
                sender.SendMessage();
            }
            finally
            {
                GameDataSerializePatch.SerializeMessageCount = Math.Max(0, GameDataSerializePatch.SerializeMessageCount - 1);
            }
        }

        public static void ResetSetRole(PlayerControl player)
        {
            if (!AmongUsClient.Instance.AmHost || player == null) return;
            DummyImpostorPlayer = byte.MaxValue;
            roleCache.Remove(player.PlayerId);

            var clientId = player.GetClientId();
            if (clientId == -1) return;

            foreach (var target in Main.AllPlayerControls)
            {
                var role = GetRoleForAfterMeeting(player, target);
                target.RpcSetRoleDesync(role, clientId);

                if (target.PlayerId == player.PlayerId)
                {
                    logger.Info($"{player.GetNameWithRole()} <= {target.GetNameWithRole()} => {role}");
                }
            }

            var state = PlayerState.GetByPlayerId(player.PlayerId);
            state.IsBlackOut = false;
            player.ResetKillCooldown();
            player.MarkDirtySettings();
            player.RpcResetAbilityCooldown();

            if (roleCache.Count == 0)
            {
                IsSet = false;
            }
        }

        private static PlayerControl SelectDummyImpostor(byte exiledId)
        {
            var localPlayer = PlayerControl.LocalPlayer;
            if (localPlayer != null && localPlayer.IsAlive() && localPlayer.PlayerId != exiledId)
            {
                return localPlayer;
            }

            return Main.AllAlivePlayerControls
                .Where(player => player.PlayerId != exiledId)
                .OrderBy(player => player.PlayerId)
                .FirstOrDefault()
                ?? Main.AllPlayerControls.OrderBy(player => player.PlayerId).FirstOrDefault();
        }

        private static RoleTypes GetRoleForAfterMeeting(PlayerControl seer, PlayerControl target)
        {
            var customRole = target.GetCustomRole();
            var roleInfo = customRole.GetRoleInfo();
            var role = roleInfo?.BaseRoleType?.Invoke() ?? RoleTypes.Crewmate;
            var isAlive = target.IsAlive();

            if (!isAlive)
            {
                role = customRole.IsImpostor() || target.CanUseSabotageButton()
                    ? RoleTypes.ImpostorGhost
                    : RoleTypes.CrewmateGhost;
            }

            if (seer.PlayerId != target.PlayerId && roleInfo?.IsDesyncImpostor == true)
            {
                role = isAlive ? RoleTypes.Crewmate : RoleTypes.CrewmateGhost;
            }

            var seerIsDesyncImpostor = seer.GetCustomRole().GetRoleInfo()?.IsDesyncImpostor == true;
            if (seerIsDesyncImpostor && seer.PlayerId != target.PlayerId)
            {
                role = isAlive ? RoleTypes.Crewmate : RoleTypes.CrewmateGhost;
            }

            return role;
        }

        ///<summary>
        ///一時的にIsDeadを本来のものに戻した状態でコードを実行します
        ///<param name="action">実行内容</param>
        ///</summary>
        public static void TempRestore(Action action)
        {
            logger.Info("==Temp Restore==");
            //IsDeadが上書きされた状態でTempRestoreが実行されたかどうか
            bool before_IsCached = IsCached;
            try
            {
                if (before_IsCached) RestoreIsDead(doSend: false);
                action();
            }
            catch (Exception ex)
            {
                logger.Warn("AntiBlackout.TempRestore内で例外が発生しました");
                logger.Exception(ex);
            }
            finally
            {
                if (before_IsCached) SetIsDead(doSend: false);
                logger.Info("==/Temp Restore==");
            }
        }

        [GameModuleInitializer]
        public static void Reset()
        {
            logger.Info("==Reset==");
            if (isDeadCache == null) isDeadCache = new();
            if (roleCache == null) roleCache = new();
            isDeadCache.Clear();
            roleCache.Clear();
            IsCached = false;
            IsSet = false;
            DummyImpostorPlayer = byte.MaxValue;
            VoteResult = null;
        }
    }
}
