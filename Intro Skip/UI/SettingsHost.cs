using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Settings;
using System;
using Zenject;

namespace IntroSkip.UI
{
    internal class SettingsHost : IInitializable, IDisposable
    {
        private readonly Config _config;

        [UIValue("intro-skip-toggle")]
        public bool IntroSkipToggle
        {
            get => _config.AllowIntroSkip;
            set => _config.AllowIntroSkip = value;
        }

        [UIValue("outro-skip-toggle")]
        public bool OutroSkipToggle
        {
            get => _config.AllowOutroSkip;
            set => _config.AllowOutroSkip = value;
        }

        [UIValue("break-skip-toggle")]
        public bool BreakSkipToggle
        {
            get => _config.AllowBreakSkip;
            set => _config.AllowBreakSkip = value;
        }

        public SettingsHost(Config config)
        {
            _config = config;
        }

        public void Initialize()
        {
            BSMLSettings.instance.AddSettingsMenu("Intro Skip", "IntroSkip.UI.settings.bsml", this);
        }

        public void Dispose()
        {
            if (BSMLParser.IsSingletonAvailable && BSMLSettings.instance != null)
                BSMLSettings.instance.RemoveSettingsMenu(this);
        }

        [UIAction("set-intro-skip-toggle")]
        protected void SetIntro(bool value) => IntroSkipToggle = value;

        [UIAction("set-outro-skip-toggle")]
        protected void SetOutro(bool value) => OutroSkipToggle = value;

        [UIAction("set-break-skip-toggle")]
        protected void SetBreak(bool value) => BreakSkipToggle = value;
    }
}