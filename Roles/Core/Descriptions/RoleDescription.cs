using System.Linq;
using System.Text;
using UnityEngine;
using AmongUs.GameOptions;

namespace TownOfHostForE.Roles.Core.Descriptions;

public abstract class RoleDescription
{
    public RoleDescription(SimpleRoleInfo roleInfo)
    {
        RoleInfo = roleInfo;
    }

    public SimpleRoleInfo RoleInfo { get; }
    /// <summary>イントロなどで表示される短い文</summary>
    public abstract string Blurb { get; }
    /// <summary>
    /// ヘルプコマンドで使用される長い説明文<br/>
    /// AmongUs2023.7.12時点で，Impostor, Crewmateに関してはバニラ側でロング説明文が未実装のため「タスクを行う」と表示される
    /// </summary>
    public abstract string Description { get; }
    public string FullFormatHelp
    {
        get
        {
            //var builder = new StringBuilder(256);
            var builder = new StringBuilder();
            // 役職名と説明文
            builder.AppendFormat("<size={0}>{1}</size>\n", FirstHeaderSize, Translator.GetRoleString(RoleInfo.RoleName.ToString()).Color(RoleInfo.RoleColor.ToReadableColor()));
            // 陣営
            //   マッドメイトはインポスター陣営
            var roleTeam = RoleInfo.CustomRoleType == CustomRoleTypes.Madmate ? CustomRoleTypes.Impostor : RoleInfo.CustomRoleType;
            Color roleColor = RoleInfo.CustomRoleType == CustomRoleTypes.Crewmate ? Utils.GetRoleColor(CustomRoles.Crewmate) : Utils.GetRoleColor(RoleInfo.RoleName);
            builder.AppendFormat("<size={0}>{1}</size>\n", ThirdHeaderSize, $"陣営：{Utils.ColorString(roleColor, Translator.GetString($"CustomRoleTypes.{roleTeam}"))}");
            builder.AppendFormat("<size={0}>{1}</size>\n", BodySize, Description);
            //設定
            var settingsBuilder = new StringBuilder();
            if (Options.CustomRoleSpawnChances?.TryGetValue(RoleInfo.RoleName, out var roleOption) == true)
            {
                Utils.ShowChildrenSettings(roleOption, ref settingsBuilder);
            }
            var settingsText = settingsBuilder.ToString().TrimEnd();
            if (!string.IsNullOrWhiteSpace(settingsText))
            {
                builder.AppendFormat("<size={0}>{1}</size>\n", ThirdHeaderSize, "【設定】");
                builder.AppendFormat("<size={0}>{1}</size>\n", BodySize, settingsText);
            }

            // バニラ役職判定
            builder.AppendFormat("<size={0}>{1}</size>\n", SecondHeaderSize, Translator.GetString("Basis"));
            builder.AppendFormat("<size={0}>{1}</size>\n", BodySize, Translator.GetString(RoleInfo.BaseRoleType.Invoke().ToString()));
            return builder.ToString();
        }
    }

    public const string FirstHeaderSize = "130%";
    public const string SecondHeaderSize = "100%";
    public const string ThirdHeaderSize = "80%";
    public const string BodySize = "70%";
    public const string BlankLineSize = "30%";
}
