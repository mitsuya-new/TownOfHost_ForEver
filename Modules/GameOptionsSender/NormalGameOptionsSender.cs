using System;
using AmongUs.GameOptions;

namespace TownOfHostForE.Modules
{
    public class NormalGameOptionsSender : GameOptionsSender
    {
        public override IGameOptions BasedGameOptions =>
            GameOptionsManager.Instance.CurrentGameOptions;
        public override bool IsDirty
        {
            get
            {
                try
                {
                    if (!GameManager.Instance) return false;
                    if (GameManager.Instance.LogicComponents == null) return false;
                    if (_logicOptions == null || !GameManager.Instance?.LogicComponents?.Contains(_logicOptions) == true)
                    {
                        foreach (var glc in GameManager.Instance.LogicComponents)
                            if (glc.TryCast<LogicOptions>(out var lo))
                                _logicOptions = lo;
                    }
                    return _logicOptions != null && (_logicOptions?.IsDirty ?? false);
                }
                catch (Exception ex)
                {
                    Logger.Error($"{ex}", "NormalGameOptionsSender");
                    return false;
                }
            }
            protected set
            {
                if (_logicOptions != null)
                    _logicOptions.ClearDirtyFlag();
            }
        }
        private LogicOptions _logicOptions;

        public override IGameOptions BuildGameOptions()
            => BasedGameOptions;

        public override void SendGameOptions()
            => GameOptionsSender.RpcSendOptions();
    }
}
