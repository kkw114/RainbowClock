using System;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.MenuButtons;
using IPALogger = IPA.Logging.Logger;
using SiraUtil.Zenject;

namespace RainbowClock
{
    [Plugin(RuntimeOptions.DynamicInit), NoEnableDisable]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }
        internal static ClockConfig Config { get; private set; }
        internal static ClockSettingsHost SettingsHost { get; } = new ClockSettingsHost();

        private bool _settingsRegistered;
        private bool _menuButtonRegistered;
        private MenuButton _menuButton;
        private BeatSaberMarkupLanguage.Settings.BSMLSettings _registeredSettingsInstance;
        private MenuButtons _registeredMenuButtonsInstance;

        [Init]
        public Plugin(IPALogger logger, IPA.Config.Config conf, Zenjector zenject)
        {
            Instance = this;
            Log = logger;
            Config = conf.Generated<ClockConfig>();
            Log.Info("RainbowClock (彩虹时钟) initialized.");

            zenject.UseLogger(logger);
            zenject.UseMetadataBinder<Plugin>();

            zenject.Install<ClockInstaller>(Location.Menu);
            zenject.Install<ClockInstaller>(Location.Player);
            zenject.Install<ClockInstaller>(Location.Tutorial);
        }

        [OnStart]
        public void OnStart()
        {
            Loc.SetMode((LangMode)Config.Language);
            TryRegisterSettings();
            AprilFoolsPatch.Apply();
            Log.Info("RainbowClock (彩虹时钟) started.");
        }

        [OnExit]
        public void OnExit()
        {
            AprilFoolsPatch.Remove();
            Log.Info("RainbowClock (彩虹时钟) stopped.");
        }

        /// <summary>
        /// 注册设置页与主菜单按钮；若 BSML 尚未就绪则稍后重试（由主协程驱动）。
        /// 分辨率等设置变化会导致菜单 Zenject 容器重建（BSMLSettings/MenuButtons 变成新实例），
        /// 检测到实例变化时自动重新注册，避免入口"消失"。
        /// </summary>
        public void TryRegisterSettings()
        {
            try
            {
                var bsmlSettings = BeatSaberMarkupLanguage.Settings.BSMLSettings.Instance;
                if (bsmlSettings == null)
                {
                    return;
                }
                if (_settingsRegistered && !ReferenceEquals(_registeredSettingsInstance, bsmlSettings))
                {
                    _settingsRegistered = false;
                    Log.Info("BSMLSettings instance changed, re-registering settings menu.");
                }
                if (!_settingsRegistered)
                {
                    bsmlSettings.AddSettingsMenu("彩虹时钟", "RainbowClock.Views.ClockSettings.bsml", SettingsHost);
                    _registeredSettingsInstance = bsmlSettings;
                    _settingsRegistered = true;
                    Log.Info("RainbowClock settings menu registered.");
                }
            }
            catch (Exception e)
            {
                Log.Error("Failed to register settings menu: " + e);
            }

            try
            {
                var menuButtons = MenuButtons.Instance;
                if (menuButtons == null)
                {
                    return;
                }
                if (_menuButtonRegistered && !ReferenceEquals(_registeredMenuButtonsInstance, menuButtons))
                {
                    _menuButtonRegistered = false;
                    Log.Info("MenuButtons instance changed, re-registering menu button.");
                }
                if (!_menuButtonRegistered)
                {
                    _menuButton = new MenuButton("彩虹时钟", GetMenuButtonHint(), OpenClockSettings);
                    menuButtons.RegisterButton(_menuButton);
                    _registeredMenuButtonsInstance = menuButtons;
                    _menuButtonRegistered = true;
                    Log.Info("RainbowClock main menu button registered.");
                }
            }
            catch (Exception e)
            {
                Log.Error("Failed to register main menu button: " + e);
            }
        }

        private static string GetMenuButtonHint()
        {
            return Loc.IsChinese ? "打开彩虹时钟设置" : "Open Rainbow Clock settings";
        }

        /// <summary>语言切换后刷新主菜单按钮的悬停提示。</summary>
        public static void UpdateMenuButtonHint()
        {
            if (Instance?._menuButton != null)
            {
                Instance._menuButton.HoverHint = GetMenuButtonHint();
            }
        }

        private static void OpenClockSettings()
        {
            try
            {
                var flow = BeatSaberUI.CreateFlowCoordinator<ClockSettingsFlowCoordinator>();
                BeatSaberUI.MainFlowCoordinator.PresentFlowCoordinator(flow, null, HMUI.ViewController.AnimationDirection.Horizontal, true);
            }
            catch (Exception e)
            {
                Log.Error("Failed to open settings flow: " + e);
            }
        }

        /// <summary>由主协程每 0.25s 调用：驱动设置页刷新与重试注册。</summary>
        public static void TickSettings()
        {
            Instance.TryRegisterSettings();
            SettingsHost.Tick();
        }
    }
}
