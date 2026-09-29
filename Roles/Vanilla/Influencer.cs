using AmongUs.GameOptions;
using TownOfHostForE.Roles.Core;

namespace TownOfHostForE.Roles.Vanilla;

// SpiritGuide is the game's internal name for the Influencer ghost role.
public sealed class Influencer : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.CreateForVanilla(
            typeof(Influencer),
            player => new Influencer(player),
            RoleTypes.SpiritGuide,
            "#8cffff",
            assignInfo: new RoleAssignInfo(CustomRoles.Influencer, CustomRoleTypes.Crewmate)
            {
                IsInitiallyAssignableCallBack = () => false
            }
        );

    public Influencer(PlayerControl player) : base(RoleInfo, player) { }
}
