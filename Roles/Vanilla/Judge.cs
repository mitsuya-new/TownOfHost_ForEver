using AmongUs.GameOptions;
using TownOfHostForE.Roles.Crewmate;
using TownOfHostForE.Roles.Core;

namespace TownOfHostForE.Roles.Vanilla;

public sealed class Judge : RoleBase
{
    public Judge(PlayerControl player) : base(RoleInfo, player) { }
    public readonly static SimpleRoleInfo RoleInfo = SimpleRoleInfo.CreateForVanilla(typeof(Judge), player => new Judge(player), RoleTypes.Judge, "#8cffff");

    public override bool CallJudgeVote(PlayerControl voter, PlayerControl votedFor, ref byte exilePlayerId)
        => JudgeVote(voter, votedFor, ref exilePlayerId);

    public static bool JudgeVote(PlayerControl voter, PlayerControl votedFor, ref byte exilePlayerId)
    {
        exilePlayerId = byte.MaxValue;
        if (voter == null || votedFor == null || !voter.IsAlive() || !votedFor.IsAlive()) return false;

        if (Sheriff.CanBeKilledBy(votedFor) || votedFor.Is(CustomRoles.Lovers))
        {
            exilePlayerId = votedFor.PlayerId;
            votedFor.SetRealKiller(voter);
        }
        else
        {
            exilePlayerId = voter.PlayerId;
            PlayerState.GetByPlayerId(voter.PlayerId).DeathReason = CustomDeathReason.Misfire;
        }

        return true;
    }
}
