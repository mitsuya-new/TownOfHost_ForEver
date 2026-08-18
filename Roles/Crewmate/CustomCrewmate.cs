using AmongUs.GameOptions;
using TownOfHostForE.Roles.Core;

namespace TownOfHostForE.Roles.Crewmate;
public sealed class CustomCrewmate : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(CustomCrewmate),
            player => new CustomCrewmate(player),
            CustomRoles.CustomCrewmate,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            9210,
            SetupOptionItem,
            "カスタムクルーメイト",
            "#8cffff"
        );
    public CustomCrewmate(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    public static void SetupOptionItem()
    {
        Options.SetUpAddOnOptions(RoleInfo.ConfigId + 10, RoleInfo.RoleName, RoleInfo.Tab);
        Options.OverrideTasksData.Create(RoleInfo, 50);
    }
}