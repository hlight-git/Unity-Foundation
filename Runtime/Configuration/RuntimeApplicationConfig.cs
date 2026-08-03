using System;
using UnityEngine;

namespace Hlight.Foundation
{
    [Serializable]
    public class RuntimeApplicationConfig
    {
        private enum TargetFrameRate { RefreshRateInCurrentResolutionOfDevice, MaximumRefreshRateOfDevice, In24Hz, In60Hz }
        private enum SleepTimeout { Never = UnityEngine.SleepTimeout.NeverSleep, System = UnityEngine.SleepTimeout.SystemSetting }

        [SerializeField] private TargetFrameRate targetFrameRate = TargetFrameRate.MaximumRefreshRateOfDevice;
        [SerializeField] private SleepTimeout sleepTimeout = SleepTimeout.System;
        [SerializeField] private bool multiTouchEnabled = true;

        public void Apply()
        {
            Application.targetFrameRate = targetFrameRate switch
            {
                TargetFrameRate.RefreshRateInCurrentResolutionOfDevice => GetCurrentRefreshRate(),
                TargetFrameRate.MaximumRefreshRateOfDevice => GetMaximumRefreshRate(),
                TargetFrameRate.In24Hz => 24,
                TargetFrameRate.In60Hz => 60,
                _ => throw new ArgumentOutOfRangeException()
            };
            Input.multiTouchEnabled = multiTouchEnabled;
            Screen.sleepTimeout = (int)sleepTimeout;
        }

        private static int GetCurrentRefreshRate() =>
            NormalizeRefreshRate(Screen.currentResolution.refreshRateRatio.value);

        private static int GetMaximumRefreshRate()
        {
            var resolutions = Screen.resolutions;
            if (resolutions == null || resolutions.Length == 0)
                return GetCurrentRefreshRate();

            var maximum = 0d;
            for (var i = 0; i < resolutions.Length; i++)
                maximum = Math.Max(maximum, resolutions[i].refreshRateRatio.value);

            return NormalizeRefreshRate(maximum);
        }

        private static int NormalizeRefreshRate(double refreshRate)
        {
            if (double.IsNaN(refreshRate) || double.IsInfinity(refreshRate) || refreshRate <= 0)
                return 60;

            return Math.Max(1, (int)Math.Round(refreshRate));
        }
    }
}
