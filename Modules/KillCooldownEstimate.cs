using System;

namespace TownOfHostForE.Modules;

/// <summary>ホスト側で、クライアントのキルクール残り時間を推定する。</summary>
internal sealed class KillCooldownEstimate
{
    public float Remaining { get; private set; }

    public KillCooldownEstimate(float seconds) => Reset(seconds);

    public void Reset(float seconds) => Remaining = Math.Max(0f, seconds);

    public void Tick(float deltaTime, bool canCountDown)
    {
        if (canCountDown && deltaTime > 0f)
            Remaining = Math.Max(0f, Remaining - deltaTime);
    }

    // FailedProtected は設定値の半分を残り時間にする。
    // 設定値が0だとバニラ側がタイマー更新を省くため、最小の正数を送る。
    public float ProtectedMurderSetting => Math.Max(0.0001f, Remaining * 2f);
}
