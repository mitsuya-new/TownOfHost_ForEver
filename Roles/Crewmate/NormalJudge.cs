using AmongUs.GameOptions;
using TownOfHostForE.Roles.Core;

namespace TownOfHostForE.Roles.Crewmate;

public sealed class NormalJudge : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(NormalJudge),
            player => new NormalJudge(player),
            CustomRoles.NormalJudge,
            () => RoleTypes.Judge,
            CustomRoleTypes.Crewmate,
            2200,
            SetupOptionItem,
            "judge",
            "#8cffff"
        );

    public NormalJudge(PlayerControl player) : base(RoleInfo, player)
    {
        judgeTaskRequirementPercentage = OptionJudgeTaskRequirementPercentage.GetFloat();
    }

    private static OptionItem OptionJudgeTaskRequirementPercentage;

    enum OptionName
    {
        JudgeTaskRequirementPercentage
    }

    private static float judgeTaskRequirementPercentage;

    private static void SetupOptionItem()
    {
        OptionJudgeTaskRequirementPercentage = FloatOptionItem.Create(RoleInfo, 3, OptionName.JudgeTaskRequirementPercentage, new(0f, 100f, 25f), 50f, false)
            .SetValueFormat(OptionFormat.Percent);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.JudgeTaskRequirementPercentage = judgeTaskRequirementPercentage;
    }

    public override bool CallJudgeVote(PlayerControl voter, PlayerControl votedFor, ref byte exilePlayerId)
        => Vanilla.Judge.JudgeVote(voter, votedFor, ref exilePlayerId);
}
