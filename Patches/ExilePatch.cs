using AmongUs.Data;
using AmongUs.GameOptions;
using HarmonyLib;
using System;
using TownOfHostForE.Roles.AddOns.Common;
using TownOfHostForE.Roles.Animals;
using TownOfHostForE.Roles.Core;
using TownOfHostForE.Roles.Crewmate;
using TownOfHostForE.Roles.Neutral;

namespace TownOfHostForE
{
    class ExileControllerWrapUpPatch
    {
        public static NetworkedPlayerInfo AntiBlackout_LastExiled;
        [HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
        class BaseExileControllerPatch
        {
            public static void Postfix(ExileController __instance)
            {
                try
                {
                    WrapUpPostfix(__instance.initData.networkedPlayer);
                }
                catch (Exception ex)
                {
                    Logger.Info("追放処理例外：" + ex.Message + "/" + ex.StackTrace, "WrapUpAndSpawn");
                }
                finally
                {
                    WrapUpFinalizer(__instance.initData.networkedPlayer);
                }
            }
        }

        [HarmonyPatch(typeof(AirshipExileController), nameof(AirshipExileController.Animate))]
        class AirshipStatusPatch
        {
            public static void Postfix(AirshipExileController __instance, ref Il2CppSystem.Collections.IEnumerator __result)
            {
                //WrapUpAndSpawnに直接パッチが当たらないのでAnimateメソッド中にパッチを当てる
                var pathcer = new CoroutinPatcher(__result);
                //WrapUpAndSpawnはステートマシンとしてクラス化されているためそのクラス実行前にパッチを当てる
                //元々Postfixだが、タイミング的にはPrefixの方が適切なのでPrefixに当てる
                pathcer.AddPrefix(typeof(AirshipExileController._WrapUpAndSpawn_d__11), () =>
                    AirshipExileControllerPatch.Postfix(__instance)
                );
                __result = pathcer.EnumerateWithPatch();
            }
        }
        // Patchが当たらないが念のためコメントアウト
        //[HarmonyPatch(typeof(AirshipExileController), nameof(AirshipExileController.WrapUpAndSpawn))]
        class AirshipExileControllerPatch
        {
            public static void Postfix(AirshipExileController __instance)
            {
                try
                {
                    WrapUpPostfix(__instance.initData.networkedPlayer);
                }
                catch (Exception ex)
                {
                    Logger.Info("追放処理例外：" + ex.Message + "/" + ex.StackTrace, "WrapUpAndSpawn");
                }
                finally
                {
                    WrapUpFinalizer(__instance.initData.networkedPlayer);
                }
            }
        }
        static void WrapUpPostfix(NetworkedPlayerInfo exiled)
        {
            if (AntiBlackout.OverrideExiledPlayer)
            {
                exiled = AntiBlackout_LastExiled;
            }

            var mapId = Main.NormalOptions.MapId;
            // エアシップではまだ湧かない
            if ((MapNames)mapId != MapNames.Airship)
            {
                foreach (var state in PlayerState.AllPlayerStates.Values)
                {
                    state.HasSpawned = true;
                }
            }

            bool DecidedWinner = false;
            if (!AmongUsClient.Instance.AmHost) return; //ホスト以外はこれ以降の処理を実行しません
            AntiBlackout.RestoreIsDead(doSend: false);
            if (exiled != null)
            {
                var role = exiled.GetCustomRole();
                var info = role.GetRoleInfo();
                //霊界用暗転バグ対処
                if (Utils.AllPlayersCount < 4 && !AntiBlackout.OverrideExiledPlayer && info?.IsDesyncImpostor == true)
                    exiled.Object?.ResetPlayerCam(1f);

                exiled.IsDead = true;
                if (role != CustomRoles.AntiComplete)
                    PlayerState.GetByPlayerId(exiled.PlayerId).DeathReason = CustomDeathReason.Vote;

                foreach (var roleClass in CustomRoleManager.AllActiveRoles.Values)
                {
                    roleClass.OnExileWrapUp(exiled, ref DecidedWinner);
                }
                Sending.OnExileWrapUp(exiled.Object);

                if (CustomWinnerHolder.WinnerTeam != CustomWinner.Terrorist) PlayerState.GetByPlayerId(exiled.PlayerId).SetDead();
            }
            foreach (var pc in Main.AllPlayerControls)
            {
                pc.ResetKillCooldown();
            }
            if (RandomSpawn.IsRandomSpawn())
            {
                RandomSpawn.SpawnMap map;
                switch (mapId)
                {
                    case 0:
                        map = new RandomSpawn.SkeldSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                    case 1:
                        map = new RandomSpawn.MiraHQSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                    case 2:
                        map = new RandomSpawn.PolusSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                    case 5:
                        map = new RandomSpawn.FungleSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                }
            }
            FallFromLadder.Reset();
            Utils.CountAlivePlayers(true);
            Utils.AfterMeetingTasks();
            if (mapId != 4)
            {
                foreach (var pc in Main.AllPlayerControls)
                {
                    pc.GetRoleClass()?.OnSpawn();
                }

            }
            Utils.NotifyRoles();
        }
        static void WrapUpFinalizer(NetworkedPlayerInfo exiled)
        {
            //WrapUpPostfixで例外が発生しても、この部分だけは確実に実行されます。
            if (AmongUsClient.Instance.AmHost)
            {
                _ = new LateTask(() =>
                {
                    exiled = AntiBlackout_LastExiled;
                    if (AntiBlackout.OverrideExiledPlayer && // 追放対象が上書きされる状態 (上書きされない状態なら実行不要)
                        exiled != null && //exiledがnullでない
                        exiled.Object != null) //exiled.Objectがnullでない
                    {
                        exiled.Object.RpcExileV3();
                    }
                }, 0.5f, "Restore IsDead Task");
                _ = new LateTask(() =>
                {
                    if (exiled != null && exiled.Object != null)
                    {
                        exiled.Object.RpcExileV3();
                    }

                    if (Main.NormalOptions.MapId is not 4 || AntiBlackout.OverrideExiledPlayer)
                    {
                        Main.AllPlayerControls.Do(AntiBlackout.ResetSetRole);
                    }

                    if (!Options.ExAftermeetingflash.GetBool()) return;

                    if (Main.NormalOptions.MapId is 4)
                        _ = new LateTask(() => Utils.AllPlayerKillFlash(), 3f, "AftermeetingFlash");
                    else
                        Utils.AllPlayerKillFlash();
                }, 0.52f, "AfterMeetingFlash Task");
                _ = new LateTask(() =>
                {
                    Main.AfterMeetingDeathPlayers.Do(x =>
                    {
                        var player = Utils.GetPlayerById(x.Key);
                        var roleClass = CustomRoleManager.GetByPlayerId(x.Key);
                        var requireResetCam = Utils.AllPlayersCount < 4 && player?.GetCustomRole().GetRoleInfo()?.IsDesyncImpostor == true;
                        var state = PlayerState.GetByPlayerId(x.Key);
                        Logger.Info($"{player.GetNameWithRole()}を{x.Value}で死亡させました", "AfterMeetingDeath");
                        state.DeathReason = x.Value;
                        player?.RpcExileV3();
                        state.SetDead();
                        if (x.Value == CustomDeathReason.Suicide)
                            player?.SetRealKiller(player, true);
                        if (requireResetCam)
                            player?.ResetPlayerCam(1f);
                        if (roleClass is Executioner executioner && executioner.TargetId == x.Key)
                            Executioner.ChangeRoleByTarget(x.Key);
                    });
                    Main.AfterMeetingDeathPlayers.Clear();
                }, 0.6f, "AfterMeetingDeathPlayers Task");
                _ = new LateTask(() =>
                {
                    if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Default) return;
                    foreach (var pc in Main.AllPlayerControls)
                    {
                        var state = PlayerState.GetByPlayerId(pc.PlayerId);
                        state.IsBlackOut = false;
                        pc.ResetKillCooldown();
                        pc.MarkDirtySettings();
                    }
                    Utils.SyncAllSettings();
                }, 1.0f, "AfterMeeting_ResetBlackOut");
            }

            GameStates.AlreadyDied |= !Utils.IsAllAlive;
            RemoveDisableDevicesPatch.UpdateDisableDevices();
            SoundManager.Instance.ChangeAmbienceVolume(DataManager.Settings.Audio.AmbienceVolume);
            GameStates.InTask = true;
            Logger.Info("タスクフェイズ開始", "Phase");
            Badger.MeetingEndCheck();
            Tiikawa.MeetingEndCheck();
            RestoreLocalRoleAfterMeeting();
            _ = new LateTask(() => MeetingStates.MeetingCalled = false, 2.0f, "MeetingCalled Reset");
            _ = new LateTask(() => GameStates.ExiledAnimate = false, 3f, "ExiledAnimate Reset");
            if (AmongUsClient.Instance.AmHost && Main.NormalOptions.MapId is 4 && !AntiBlackout.OverrideExiledPlayer)
            {
                _ = new LateTask(() => Main.AllPlayerControls.Do(AntiBlackout.ResetSetRole), 11.5f, "AirshipSetRole");
            }
        }

        private static void RestoreLocalRoleAfterMeeting()
        {
            var localPlayer = PlayerControl.LocalPlayer;
            if (localPlayer == null || RoleManager.Instance == null) return;

            var roleInfo = localPlayer.GetCustomRole().GetRoleInfo();
            var role = (roleInfo?.IsDesyncImpostor == true && roleInfo.BaseRoleType.Invoke() is RoleTypes.Impostor)
                ? RoleTypes.Crewmate
                : (roleInfo?.BaseRoleType?.Invoke() ?? RoleTypes.Crewmate);

            if (!localPlayer.IsAlive())
            {
                role = IsCrewmateRole(role) ? RoleTypes.CrewmateGhost : RoleTypes.ImpostorGhost;
            }

            RoleManager.Instance.SetRole(localPlayer, role);
            Logger.Info($"LocalRoleRestored: {localPlayer.GetNameWithRole()} => {role}", "AfterMeeting_RoleSync");
        }

        private static bool IsCrewmateRole(RoleTypes role)
            => role is RoleTypes.Crewmate
                or RoleTypes.Scientist
                or RoleTypes.Engineer
                or RoleTypes.Tracker
                or RoleTypes.Detective
                or RoleTypes.Noisemaker
                or RoleTypes.GuardianAngel;
    }
    //static void WrapUpFinalizer(NetworkedPlayerInfo exiled)
    //{
    //    //WrapUpPostfixで例外が発生しても、この部分だけは確実に実行されます。
    //    if (AmongUsClient.Instance.AmHost)
    //    {
    //        _ = new LateTask(() =>
    //        {
    //            exiled = AntiBlackout_LastExiled;
    //            AntiBlackout_LastExiled = null;
    //            AntiBlackout.SendGameData();
    //            if (AntiBlackout.OverrideExiledPlayer && // 追放対象が上書きされる状態 (上書きされない状態なら実行不要)
    //                exiled != null && //exiledがnullでない
    //                exiled.Object != null) //exiled.Objectがnullでない
    //            {
    //                exiled.Object.RpcExileV2();
    //            }
    //        }, 0.5f, "Restore IsDead Task");
    //        _ = new LateTask(() =>
    //        {
    //            Main.AfterMeetingDeathPlayers.Do(x =>
    //            {
    //                REIKAITENSOU(x.Key, x.Value);
    //            });
    //            Main.AfterMeetingDeathPlayers.Clear();
    //        }, 0.5f, "AfterMeetingDeathPlayers Task");
    //    }

    //    GameStates.AlreadyDied |= !Utils.IsAllAlive;
    //    RemoveDisableDevicesPatch.UpdateDisableDevices();
    //    SoundManager.Instance.ChangeAmbienceVolume(DataManager.Settings.Audio.AmbienceVolume);
    //    Logger.Info("タスクフェイズ開始", "Phase");
    //    Badger.MeetingEndCheck();
    //    Tiikawa.MeetingEndCheck();
    //}



    [HarmonyPatch(typeof(PbExileController), nameof(PbExileController.PlayerSpin))]
    class PolusExileHatFixPatch
    {
        public static void Prefix(PbExileController __instance)
        {
            __instance.Player.cosmetics.hat.transform.localPosition = new(-0.2f, 0.6f, 1.1f);
        }
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
    class ExileControllerBeginPatch
    {
        public static bool SecondBegin = false;

        public static bool Prefix(ExileController __instance, ExileController.InitProperties init)
        {
            if (Utils.AllPlayersCount < 4) return true;

            var result = AntiBlackout.VoteResult;
            if (!result.HasValue || result.Value.Exiled is null) return true;

            if (SecondBegin)
            {
                __instance.completeString = string.Format(
                    Translator.GetString(StringNames.ExileTextNonConfirm),
                    result.Value.Exiled.PlayerName);
                SecondBegin = false;
                return true;
            }

            var modinit = init;
            modinit.networkedPlayer = result.Value.Exiled;
            modinit.outfit = Camouflage.PlayerSkins.TryGetValue(result.Value.Exiled.PlayerId, out var skin)
                ? skin
                : result.Value.Exiled.DefaultOutfit;
            modinit.voteTie = false;
            SecondBegin = true;
            __instance.Begin(modinit);
            return false;
        }

        public static void Postfix(ExileController __instance)
        {
            var result = AntiBlackout.VoteResult;
            if (result.HasValue && result.Value.Exiled is null)
            {
                __instance.completeString = result.Value.IsTie
                    ? Translator.GetString(StringNames.NoExileTie)
                    : Translator.GetString(StringNames.NoExileSkip);
            }

            SecondBegin = false;
        }
    }
}
