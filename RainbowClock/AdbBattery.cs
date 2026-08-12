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
            // 解析目标设备：配置序列号优先，否则自动选择（有线 USB 优先，其次无线 WiFi）
            ResolveDevice();

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
            // 记忆查询成功的设备（多设备选择时优先）
            if (!string.IsNullOrEmpty(_targetSerial) && Plugin.Config.LastAdbSerial != _targetSerial)
            {
                Plugin.Config.LastAdbSerial = _targetSerial;
            }
            Plugin.Log?.Info($"[RainbowClock] battery: serial={_targetSerial} level={level} status={status} charging={status == 2 || status == 5}");
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

        private static string _targetSerial = "";

        /// <summary>当前查询目标设备序列号（空表示无可用设备）。</summary>
        public static string TargetSerial
        {
            get { lock (Lock) { return _targetSerial; } }
        }

        /// <summary>
        /// 解析目标设备（优先级）：
        /// 1. 配置的 AdbSerial（手动指定）
        /// 2. 有线（USB）在线设备
        /// 3. 上次查询成功的设备（自动记忆，在线则优先）
        /// 4. 无线 VR 头显（model 含 Quest/Pico/Vive/Index）
        /// 5. 无线其他设备（列表顺序）
        /// </summary>
        private static void ResolveDevice()
        {
            string configured = Plugin.Config.AdbSerial?.Trim() ?? "";
            if (!string.IsNullOrEmpty(configured))
            {
                _targetSerial = configured;
                return;
            }

            _targetSerial = "";
            string remembered = Plugin.Config.LastAdbSerial?.Trim() ?? "";
            string wired = "";
            string wirelessVr = "";
            string wirelessAny = "";
            bool rememberedOnline = false;

            try
            {
                string output = RunAdbProcess("devices -l", out bool noDevice);
                if (noDevice || output == null)
                {
                    Plugin.Log?.Warn($"[RainbowClock] ResolveDevice: noDevice={noDevice} output=null");
                    return;
                }
                Plugin.Log?.Info($"[RainbowClock] ResolveDevice output: [{output.Trim()}]");
                foreach (string rawLine in output.Split('\n'))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    // adb devices -l 在 Windows 上用空格对齐列（非 tab），兼容两者
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2 || parts[1] != "device")
                    {
                        continue;
                    }
                    string address = parts[0].Trim();
                    bool isVr = IsVrModel(line);

                    if (address == remembered)
                    {
                        rememberedOnline = true;
                    }

                    if (!address.Contains(":"))
                    {
                        if (wired.Length == 0)
                        {
                            wired = address;
                        }
                    }
                    else
                    {
                        if (isVr && wirelessVr.Length == 0)
                        {
                            wirelessVr = address;
                        }
                        if (wirelessAny.Length == 0)
                        {
                            wirelessAny = address;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("[RainbowClock] ResolveDevice: " + e.Message);
            }

            if (wired.Length > 0)
            {
                _targetSerial = wired;
            }
            else if (rememberedOnline)
            {
                _targetSerial = remembered;
            }
            else if (wirelessVr.Length > 0)
            {
                _targetSerial = wirelessVr;
            }
            else if (wirelessAny.Length > 0)
            {
                _targetSerial = wirelessAny;
            }
        }

        /// <summary>从 adb devices -l 输出行解析 model 字段并判断是否为 VR 头显。</summary>
        private static bool IsVrModel(string deviceLine)
        {
            int idx = deviceLine.IndexOf("model:", StringComparison.Ordinal);
            if (idx < 0)
            {
                return false;
            }
            int start = idx + 6;
            int end = deviceLine.IndexOf(' ', start);
            string model = (end < 0 ? deviceLine.Substring(start) : deviceLine.Substring(start, end - start)).ToLowerInvariant();
            return model.Contains("quest") || model.Contains("pico") || model.Contains("vive") || model.Contains("index");
        }

        private static string RunAdbShell(string shellCommand, out bool noDevice)
        {
            return RunAdbProcess("shell " + shellCommand, out noDevice);
        }

        private static string RunAdbProcess(string args, out bool noDevice)
        {
            noDevice = false;
            string adb = Plugin.Config.AdbPath;
            if (string.IsNullOrWhiteSpace(adb))
            {
                adb = "adb";
            }
            string serial = Plugin.Config.AdbSerial?.Trim() ?? "";
            if (string.IsNullOrEmpty(serial))
            {
                serial = _targetSerial;
            }
            if (!string.IsNullOrEmpty(serial))
            {
                args = "-s " + serial + " " + args;
            }

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
                    Plugin.Log?.Error("[RainbowClock] adb timed out: " + args);
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

        /// <summary>按 Quest 版逻辑格式化电量：一律按电量渐变红→黄→绿（每 20% 均匀分布），不区分充电状态。</summary>
        private static string FormatBattery(int level, bool charging)
        {
            string percent = level + "%";
            float t = level / 100f;
            UnityEngine.Color color = EvaluateGradient(t);
            return "<color=#" + UnityEngine.ColorUtility.ToHtmlStringRGB(color) + ">" + percent + "</color>";
        }

        private static readonly (float r, float g, float b, float pos)[] GradientKeys =
        {
            (1f, 0f, 0f, 0.00f),        // 红
            (1f, 0.30f, 0f, 0.20f),     // 橙红
            (1f, 0.53f, 0f, 0.40f),     // 橙
            (1f, 0.84f, 0f, 0.60f),     // 黄
            (0.6f, 0.8f, 0.14f, 0.80f), // 黄绿
            (0f, 1f, 0f, 1.00f)         // 绿
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
