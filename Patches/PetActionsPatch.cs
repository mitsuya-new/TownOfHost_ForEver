using System;
using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using TownOfHostForE.Attributes;
using TownOfHostForE.OneTimeAbillitys;
using TownOfHostForE.Roles;
using TownOfHostForE.Roles.Core;
using TownOfHostForE.Roles.Crewmate;

namespace TownOfHostForE.Patches;
/*
 * HUGE THANKS TO
 * ImaMapleTree / 단풍잎
 * FOR THE CODE
 *
 * THIS IS JUST SMALL FOR NOW BUT MAY EVENTUALLY BE BIG
*/

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.TryPet))]
class LocalPetPatch
{
    public static void Prefix(PlayerControl __instance)
    {
        if (!GameStates.IsLobby)
        {
            if (!(AmongUsClient.Instance.AmHost && AmongUsClient.Instance.AmClient)) return;
            //__instance.petting = true;
            ExternalRpcPetPatch.Prefix(__instance.MyPhysics, 51, new MessageReader());
        }
    }

    //public static void Postfix(PlayerControl __instance)
    //{
    //    if (!GameStates.IsLobby)
    //    {
    //        if (!(AmongUsClient.Instance.AmHost && AmongUsClient.Instance.AmClient)) return;
    //        __instance.petting = false;

    //    }
    //}

}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
class ExternalRpcPetPatch
{
    public static void Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callId,
        [HarmonyArgument(1)] MessageReader reader)
    {
        if (!AmongUsClient.Instance.AmHost || GameStates.IsLobby) return;
        var rpcType = callId == 51 ? RpcCalls.Pet : (RpcCalls)callId;
        if (rpcType != RpcCalls.Pet) return;

        PlayerControl playerControl = __instance.myPlayer;
        //if (callId == 51 && playerControl.GetCustomRole().PetActivatedAbility() && GameStates.IsInGame)
        if (callId == 51 && GameStates.IsInGame &&
            (playerControl.GetCustomRole().PetActivatedAbility()
             ||
             OneTimeAbilittyController.CheckSettingAbilitys(playerControl.PlayerId,OneTimeAbilittyController.OneTimeAbility.petKill))
            )
                __instance.CancelPet();

        if (callId != 51)
        {
            CustomRpcSender sender = CustomRpcSender.Create("SelectRoles Sender", SendOption.Reliable);

            if (AmongUsClient.Instance.AmHost && playerControl.GetCustomRole().PetActivatedAbility() && GameStates.IsInGame)
                __instance.CancelPet();
            foreach (PlayerControl player in PlayerControl.AllPlayerControls)
                AmongUsClient.Instance.FinishRpcImmediately(AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, 50, SendOption.None, player.GetClientId()));
        }

        GreatDetective.TargetOnPetsButton(playerControl);

        //ワンタイムがあればそちらを優先
        if (OneTimeAbilittyController.CheckSettingAbilitys(playerControl.PlayerId, OneTimeAbilittyController.OneTimeAbility.petKill))
        {
            OneTimeAbilittyController.ActivationPetAbillitys(playerControl);
            return;
        }

        var cRole = playerControl.GetCustomRole();
        if (!cRole.IsNotAssignRoles())
        {
            playerControl.GetRoleClass().OnTouchPet(playerControl);
        }
    }
}
public static class PetSettings
{
    public static HashSet<byte> petNotSetPlayerIds = new();
    public static bool AllPetAssign = false;
    private static readonly HashSet<byte> autoGrantedPlayerIds = new();
    private static int assignmentGeneration;

    public const string FREEPET_STRING = "pet_HamPet";
    public static bool AutoGrantPetEnabled => Options.AutoGrantPet == null || Options.AutoGrantPet.GetBool();

    [GameModuleInitializer]
    public static void GameInit()
    {
        petNotSetPlayerIds.Clear();
        autoGrantedPlayerIds.Clear();
        assignmentGeneration++;
        AllPetAssign = false;
    }


    public static void CheckNotHasPetPlayers()
    {
        petNotSetPlayerIds.Clear();
        foreach (var pc in Main.AllPlayerControls)
        {
            if (!HasPet(pc))
            {
                petNotSetPlayerIds.Add(pc.PlayerId);
            }
        }
    }

    //ペット付けてない人がいたらペットつけるための処理
    public static void SetPetRoleInPet()
    {
        if (!ShouldAutoGrant()) return;

        foreach (var pc in Main.AllPlayerControls)
        {
            if (pc != null) EnsureAutoPet(pc.PlayerId);
        }
    }

    public static void SchedulePetAssignment()
    {
        if (!AmongUsClient.Instance.AmHost || !AutoGrantPetEnabled) return;

        var generation = assignmentGeneration;
        // イントロ後の衣装・スポーン処理に備え、有限回だけ再確認する。
        foreach (var delay in new[] { 0.8f, 2.5f, 5f })
        {
            _ = new LateTask(() =>
            {
                if (generation == assignmentGeneration) SetPetRoleInPet();
            }, delay, "VerifyAutoPetAfterIntro");
        }
    }

    public static void RemovePetSet()
    {
        //登録が0じゃないなら
        if(autoGrantedPlayerIds.Count != 0)
        {
            foreach (byte playerId in autoGrantedPlayerIds)
            {
                var pc = Utils.GetPlayerById(playerId);
                if (pc?.Data?.DefaultOutfit?.PetId != FREEPET_STRING) continue;
                pc.RpcSetPet("");
                UpdateCachedPet(playerId, "");
            }
        }
        petNotSetPlayerIds.Clear();
        autoGrantedPlayerIds.Clear();
        assignmentGeneration++;
        AllPetAssign = false;
    }

    //private static bool PetRoleCheck()
    //{
    //    //ちょっと地道だけどいい方法見つけたらそれにする
    //    return CustomRoles.Badger.IsPresent() ||
    //           CustomRoles.Gizoku.IsPresent() ||
    //           CustomRoles.SpiderMad.IsPresent() ||
    //           CustomRoles.Nyaoha.IsPresent() ||
    //           CustomRoles.DogSheriff.IsPresent();
    //}
    public static void PetRoleCheck(CustomRoles nowRole)
    {
        switch (nowRole)
        {
            case CustomRoles.Badger:
            case CustomRoles.Gizoku:
            case CustomRoles.SpiderMad:
            case CustomRoles.Nyaoha:
            case CustomRoles.Dolphin:
            case CustomRoles.DogSheriff:
                AllPetAssign = true;
                break;
        }
    }

    public static void ReapplyAutoGrantedPets()
    {
        SetPetRoleInPet();
    }

    private static bool ShouldAutoGrant()
    {
        return AmongUsClient.Instance.AmHost
            && AutoGrantPetEnabled
            && AmongUsClient.Instance.IsGameStarted
            && !GameStates.IsLobby;
    }

    private static void EnsureAutoPet(byte playerId)
    {
        var pc = Utils.GetPlayerById(playerId);
        if (pc?.Data == null || pc.Data.Disconnected || !pc.IsAlive() || HasPet(pc)) return;

        pc.RpcSetPet(FREEPET_STRING);
        petNotSetPlayerIds.Add(playerId);
        autoGrantedPlayerIds.Add(playerId);
        UpdateCachedPet(playerId, FREEPET_STRING);
        Logger.Info($"{pc.Data?.PlayerName} にペットを自動付与: {FREEPET_STRING}", "PetSettings");
    }

    private static void UpdateCachedPet(byte playerId, string petId)
    {
        if (!Camouflage.PlayerSkins.TryGetValue(playerId, out var outfit)) return;
        outfit.PetId = petId;
        Camouflage.PlayerSkins[playerId] = outfit;
    }

    private static bool HasPet(PlayerControl pc)
    {
        if (pc == null) return false;

        return IsValidPet(pc.Data?.DefaultOutfit?.PetId)
            || IsValidPet(pc.CurrentOutfit?.PetId);
    }

    private static bool IsValidPet(string petId)
    {
        if (string.IsNullOrEmpty(petId)) return false;

        return !petId.Equals("none", StringComparison.OrdinalIgnoreCase)
            && !petId.Equals("pet_none", StringComparison.OrdinalIgnoreCase)
            && !petId.Equals("pet_emptypet", StringComparison.OrdinalIgnoreCase)
            && !petId.Equals("pet_enmptypet", StringComparison.OrdinalIgnoreCase);
    }
}

[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
internal static class AfterMeetingPetAssignPatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        _ = new LateTask(PetSettings.ReapplyAutoGrantedPets, 1.5f, "AfterMeetingPetAssign");
    }
}
