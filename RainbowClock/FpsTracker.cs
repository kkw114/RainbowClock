using UnityEngine;

namespace RainbowClock
{
    /// <summary>
    /// FPS 统计（算法参考 FPS Counter 模组：每帧累计 timeScale/Δt，周期取平均）。
    /// 由 CoroutineRunner 每帧驱动。
    /// </summary>
    public static class FpsTracker
    {
        private const float UpdateInterval = 0.5f;

        private static float _accumulatedTime;
        private static float _timeLeft = UpdateInterval;
        private static int _frameCount;
        private static int _currentFps;

        public static int CurrentFps => _currentFps;

        /// <summary>
        /// 获取帧率上限（头显刷新率）：通过 XRDisplaySubsystem.TryGetDisplayRefreshRate 读取。
        /// </summary>
        public static int GetTargetFps()
        {
            try
            {
                var subsystems = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
                UnityEngine.SubsystemManager.GetSubsystems(subsystems);
                foreach (UnityEngine.XR.XRDisplaySubsystem subsystem in subsystems)
                {
                    if (subsystem != null && subsystem.TryGetDisplayRefreshRate(out float rate) && rate > 1f)
                    {
                        return Mathf.RoundToInt(rate);
                    }
                }
            }
            catch
            {
                // 忽略
            }
            return 0;
        }

        public static void Tick()
        {
            float localDeltaTime = Time.deltaTime;
            if (localDeltaTime <= 0.0001f)
            {
                return;
            }
            _accumulatedTime += Time.timeScale / localDeltaTime;
            _timeLeft -= localDeltaTime;
            _frameCount++;

            if (_timeLeft > 0f)
            {
                return;
            }

            _currentFps = Mathf.RoundToInt(_accumulatedTime / Mathf.Max(1, _frameCount));
            _timeLeft = UpdateInterval;
            _accumulatedTime = 0f;
            _frameCount = 0;
        }
    }
}
