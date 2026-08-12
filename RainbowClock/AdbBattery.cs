using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace RainbowClock
{
    /// <summary>电量不可用时的错误类型（显示文案按当前语言本地化）。</summary>
    public enum BatteryError
    {
        None,
        NoDevice,
        Unavailable
    }

    /// <summary>
    /// 通过 adb 查询头显（Quest/Pico 等）电量。查询在后台线程执行，结果缓存供主线程读取。
    /// 获取不到（无 adb / 无设备 / 解析失败）时 <see cref="Available"/> 为 false，界面不显示电量。
    /// </summary>
    public static class AdbBattery
    {
        private const long TicksPerSecond = 10_000_000L;

        private static readonly object Lock = new object();

        private static int _level = -1;
        private static bool _charging;
        private static bool _available;
        private static long _lastQueryTicks;
        private static bool _busy;
        private static BatteryError _lastErrorType = BatteryError.None;

        public static bool Available
        {
            get { lock (Lock) { return _available; } }
        }

        public static BatteryError LastErrorType
        {
            get { lock (Lock) { return _lastErrorType; } }
        }

        /// <summary>缓存结果格式化的显示串（带颜色），不可用时为空串。</summary>
        public static string CurrentString
        {
            get
            {
                lock (Lock)
                {
                    if (!_available || _level < 0)
                    {
                        return "";
                    }
                    return FormatBattery(_level, _charging);
                }
            }
        }

        /// <summary>主线程每帧/每 0.25s 调用：到时间自动刷新。</summary>
        public static void Tick()
        {
            int interval = Plugin.Config.BatteryRefreshSeconds;
            if (interval < 10)
            {
                interval = 10;
            }
            if (DateTime.UtcNow.Ticks - _lastQueryTicks >= interval * TicksPerSecond)
            {
                RefreshNow();
            }
        }

        /// <summary>立即异步刷新（设置页按钮 / 启动时调用）。</summary>
        public static void RefreshNow()
        {
            lock (Lock)
            {
                if (_busy)
                {
                    return;
                }
                _busy = true;
            }

            Task.Run(() =>
            {
                try
                {
                    QueryBattery();
                }
                catch (Exception e)
                {
                    lock (Lock)
                    {
                        _available = false;
                        _level = -1;
                        _lastErrorType = BatteryError.Unavailable;
                        _lastQueryTicks = DateTime.UtcNow.Ticks;
                    }
                    Plugin.Log?.Error("[RainbowClock] ADB query exception: " + e.Message);
                }
                finally
                {
                    lock (Lock)
                    {
                        _busy = false;
                    }
                }
            });
        }

        /// <summary>
        /// 执行电量查询，adb 双通道自动降级：
        /// 1. adb cmd battery get level/status（Android 11+，输出纯数字）
        /// 2. adb dumpsys battery（一次拿全，兼容旧系统）
        /// 注意：PC 版没有 OVRPlugin，且 UnityEngine.SystemInfo.batteryLevel 读的是电脑电源
        /// （无电池桌面返回 1.0，会误显示 100%），因此不走 SystemInfo 通道。
        /// status: 1=unknown 2=charging 3=discharging 4=not charging 5=full
        /// </summary>
        private static void QueryBattery()
        {
            // 通道 1：adb cmd battery
            bool noDevice;
            string levelOut = RunAdbShell("cmd battery get level", out noDevice);
            if (noDevice)
            {
                SetError(BatteryError.NoDevice);
                return;
            }
            if (levelOut != null && int.TryParse(levelOut.Trim(), out int level)
                && level >= 0 && level <= 100)
            {
                string statusOut = RunAdbShell("cmd battery get status", out noDevice);
                if (noDevice)
                {
                    SetError(BatteryError.NoDevice);
                    return;
                }
                int status = 0;
                if (statusOut != null)
                {
                    int.TryParse(statusOut.Trim(), out status);
                }
                SetOk(level, status);
                return;
            }

            // 通道 2：dumpsys battery 一次拿全
            string dump = RunAdbShell("dumpsys battery", out noDevice);
            if (noDevice)
            {
                SetError(BatteryError.NoDevice);
                return;
            }
            if (dump == null)
            {
                SetError(BatteryError.Unavailable);
                return;
            }

            int dumpLevel = -1;
            int dumpStatus = 0;
            foreach (string rawLine in dump.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.StartsWith("level:", StringComparison.Ordinal))
                {
                    int.TryParse(line.Substring(6).Trim(), out dumpLevel);
                }
                else if (line.StartsWith("status:", StringComparison.Ordinal))
                {
                    int.TryParse(line.Substring(7).Trim(), out dumpStatus);
                }
            }
            if (dumpLevel < 0 || dumpLevel > 100)
            {
                SetError(BatteryError.Unavailable);
                return;
            }
            SetOk(dumpLevel, dumpStatus);
        }

        private static void SetOk(int level, int status)
        {
            lock (Lock)
            {
                _lastQueryTicks = DateTime.UtcNow.Ticks;
                _level = level;
                _charging = status == 2 || status == 5;
                _available = true;
                _lastErrorType = BatteryError.None;
            }
        }

        private static void SetError(BatteryError error)
        {
            lock (Lock)
            {
                _lastQueryTicks = DateTime.UtcNow.Ticks;
                _available = false;
                _level = -1;
                _lastErrorType = error;
            }
        }

        private static string RunAdbShell(string shellCommand, out bool noDevice)
        {
            noDevice = false;
            string adb = Plugin.Config.AdbPath;
            if (string.IsNullOrWhiteSpace(adb))
            {
                adb = "adb";
            }
            string serial = Plugin.Config.AdbSerial?.Trim() ?? "";
            string args = string.IsNullOrEmpty(serial)
                ? "shell " + shellCommand
                : "-s " + serial + " shell " + shellCommand;

            var psi = new ProcessStartInfo
            {
                FileName = adb,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            Process proc;
            try
            {
                proc = Process.Start(psi);
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("[RainbowClock] adb start failed: " + e.Message);
                return null;
            }
            if (proc == null)
            {
                Plugin.Log?.Error("[RainbowClock] adb process failed to start");
                return null;
            }

            using (proc)
            {
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                if (!proc.WaitForExit(8000))
                {
                    try { proc.Kill(); } catch { }
                    Plugin.Log?.Error("[RainbowClock] adb timed out: " + shellCommand);
                    return null;
                }
                if (proc.ExitCode != 0)
                {
                    string err = (stderr.Trim().Length > 0 ? stderr.Trim() : stdout.Trim());
                    string lower = err.ToLowerInvariant();
                    noDevice = lower.Contains("no devices") || lower.Contains("not found") || lower.Contains("no device");
                    return null;
                }
                return stdout;
            }
        }

        /// <summary>按 Quest 版逻辑格式化电量：充电/满电青色，否则按电量渐变红→黄→绿。</summary>
        private static string FormatBattery(int level, bool charging)
        {
            string percent = level + "%";
            if (charging)
            {
                return "<color=#00FFFF>" + percent + "</color>";
            }

            float t = level / 100f;
            UnityEngine.Color color = EvaluateGradient(t);
            return "<color=#" + UnityEngine.ColorUtility.ToHtmlStringRGB(color) + ">" + percent + "</color>";
        }

        private static readonly (float r, float g, float b, float pos)[] GradientKeys =
        {
            (1f, 0f, 0f, 0.00f),       // 红
            (1f, 0.53f, 0f, 0.35f),    // 橙
            (1f, 0.84f, 0f, 0.50f),    // 黄
            (0.6f, 0.8f, 0.14f, 0.75f),// 黄绿
            (0f, 1f, 0f, 1.00f)        // 绿
        };

        private static UnityEngine.Color EvaluateGradient(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            for (int i = 1; i < GradientKeys.Length; i++)
            {
                var (r2, g2, b2, p2) = GradientKeys[i];
                if (t <= p2)
                {
                    var (r1, g1, b1, p1) = GradientKeys[i - 1];
                    float span = p2 - p1;
                    float k = span <= 0f ? 0f : (t - p1) / span;
                    return new UnityEngine.Color(
                        r1 + (r2 - r1) * k,
                        g1 + (g2 - g1) * k,
                        b1 + (b2 - b1) * k,
                        1f);
                }
            }
            return new UnityEngine.Color(0f, 1f, 0f, 1f);
        }
    }
}
