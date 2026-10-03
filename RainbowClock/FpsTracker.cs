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
        /// <summary>低于该值视为采样不可信（暂停/加载/单帧卡顿），用显示器刷新率兜底，避免显示 FPS 0。</summary>
        private const int MinPlausibleFps = 5;

        private static float _accumulatedTime;
        private static float _timeLeft = UpdateInterval;
        private static int _frameCount;
        private static int _currentFps;
        /// <summary>刷新率上限缓存（0 = 还没读到）。头显刷新率一次会话内不会变，无需反复枚举 XR 子系统。</summary>
        private static int _targetFps;
        private static bool _targetFpsResolved;

        /// <summary>原始采样帧率（可能因暂停为 0）。</summary>
        public static int CurrentFps => _currentFps;

        /// <summary>
        /// 用于显示的帧率：暂停（timeScale=0）或采样不可信时回退到刷新率上限，
        /// 避免菜单/暂停界面长期显示「FPS 0」红色。
        /// </summary>
        public static int DisplayFps
        {
            get
            {
                if (_currentFps >= MinPlausibleFps)
                {
                    return _currentFps;
                }
                int target = GetTargetFps();
                return target > 0 ? target : _currentFps;
            }
        }

        /// <summary>
        /// 获取帧率上限（头显刷新率）：通过 XRDisplaySubsystem.TryGetDisplayRefreshRate 读取。
        /// 结果缓存（首次成功读到后不再枚举子系统）；读不到时每次都会重试，
        /// 因为 XR 子系统可能比本模组初始化得更晚。
        /// </summary>
        public static int GetTargetFps()
        {
            if (_targetFpsResolved)
            {
                return _targetFps;
            }
            try
            {
                var subsystems = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
                UnityEngine.SubsystemManager.GetSubsystems(subsystems);
                foreach (UnityEngine.XR.XRDisplaySubsystem subsystem in subsystems)
                {
                    if (subsystem != null && subsystem.TryGetDisplayRefreshRate(out float rate) && rate > 1f)
                    {
                        _targetFps = Mathf.RoundToInt(rate);
                        _targetFpsResolved = true;
                        return _targetFps;
                    }
                }
            }
            catch
            {
                // 忽略：XR 子系统不可用时按"读不到上限"处理
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

            // 暂停时 timeScale=0，累计值为 0 属于正常现象，交给 DisplayFps 兜底
            _currentFps = Mathf.RoundToInt(_accumulatedTime / Mathf.Max(1, _frameCount));
            _timeLeft = UpdateInterval;
            _accumulatedTime = 0f;
            _frameCount = 0;
        }
    }
}
