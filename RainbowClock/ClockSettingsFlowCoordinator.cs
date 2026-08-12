using System;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Util;
using HMUI;
using UnityEngine;

namespace RainbowClock
{
    /// <summary>
    /// 主菜单左侧 MODS 按钮点击后弹出的设置页 FlowCoordinator。
    /// </summary>
    public class ClockSettingsFlowCoordinator : FlowCoordinator
    {
        private ViewController _settingsViewController;

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            try
            {
                if (firstActivation)
                {
                    SetTitle("彩虹时钟");
                    showBackButton = true;

                    _settingsViewController = BeatSaberUI.CreateViewController<ViewController>();
                    _settingsViewController.rectTransform.sizeDelta = new Vector2(110f, 0f);
                    _settingsViewController.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                    _settingsViewController.rectTransform.anchorMax = new Vector2(0.5f, 1f);

                    BSMLParser.Instance.Parse(
                        Utilities.GetResourceContent(GetType().Assembly, "RainbowClock.Views.ClockSettings.bsml"),
                        _settingsViewController.gameObject,
                        Plugin.SettingsHost);
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("[RainbowClock] FlowCoordinator DidActivate: " + e);
            }
            ProvideInitialViewControllers(_settingsViewController);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            if (BeatSaberUI.MainFlowCoordinator != null)
            {
                BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this, null);
            }
            else
            {
                base.BackButtonWasPressed(topViewController);
            }
        }
    }
}
