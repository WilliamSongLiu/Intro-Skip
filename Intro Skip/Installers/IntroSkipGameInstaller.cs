using IntroSkip.Displays;
using Zenject;

namespace IntroSkip.Installers
{
    internal class IntroSkipGameInstaller : Installer
    {
        private readonly Config _config;
        private readonly GameplayCoreSceneSetupData _gameplayCoreSceneSetupData;

        public IntroSkipGameInstaller(Config config, GameplayCoreSceneSetupData gameplayCoreSceneSetupData)
        {
            _config = config;
            _gameplayCoreSceneSetupData = gameplayCoreSceneSetupData;
        }

        public override void InstallBindings()
        {
            if (_config.AllowIntroSkip || _config.AllowOutroSkip || _config.AllowBreakSkip)
            {
                Container.BindInterfacesTo<SkipDaemon>().AsSingle();
            }
            Container.BindInterfacesTo(_gameplayCoreSceneSetupData.playerSpecificSettings.noTextsAndHuds ? typeof(NoSkipDisplayService) : typeof(AnimatedSkipDisplayService)).AsSingle();
        }
    }
}