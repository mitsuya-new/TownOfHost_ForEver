using System;

namespace TownOfHostForE.Modules;

public static class SystemEnvironment
{
    public static void SetEnvironmentVariables()
    {
        // ユーザ環境変数に最近開かれたTOHアモアスフォルダのパスを設定
        SetUserEnvironmentVariableIfChanged("TOWN_OF_HOST_DIR_ROOT", Environment.CurrentDirectory);
        // ユーザ環境変数にログフォルダのパスを設定
        SetUserEnvironmentVariableIfChanged("TOWN_OF_HOST_DIR_LOGS", Utils.GetLogFolder().FullName);
    }

    private static void SetUserEnvironmentVariableIfChanged(string name, string value)
    {
        var currentValue = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
        if (string.Equals(currentValue, value, StringComparison.Ordinal)) return;

        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
    }
}
