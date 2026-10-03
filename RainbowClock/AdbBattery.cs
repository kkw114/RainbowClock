using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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

        /// <summary>电量**连续**失败次数上限：达到后停止自动轮询并结束 adb。一次成功即清零。</summary>
        private const int MaxBatteryRetries = 3;
        /// <summary>失败后的重试间隔（秒），随连续失败次数递增。</summary>
        private const int BatteryRetryIntervalSeconds = 30;
        /// <summary>失败重试间隔的上限（秒）。</summary>
        private const int BatteryMaxRetryIntervalSeconds = 180;
        /// <summary>进程启动后首次查询电量的延迟（秒）。</summary>
        private const int BatteryStartDelaySeconds = 10;

        private static readonly object Lock = new object();

        private static int _level = -1;
        private static bool _available;
        private static bool _busy;
        private static bool _shuttingDown;
        private static long _sessionStartTicks = GetSessionStartTicks();
        private static long _nextQueryTicks;
        private static int _retryCount;
        private static bool _automaticPollingEnabled = true;
        private static string _resolvedAdb;
        private static BatteryError _lastErrorType = BatteryError.None;
        /// <summary>已格式化的显示串缓存：只在查询结果变化时重建（主线程 4Hz 读取，避免反复拼字符串/开色）。</summary>
        private static string _currentString = "";

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
            get { lock (Lock) { return _currentString; } }
        }

        /// <summary>
        /// 主线程每 0.25s 调用：到时间自动刷新。
        /// 进程启动 10 秒后首次查询；失败后按递增间隔重试，
        /// 连续失败 3 次后停止自动轮询并结束 adb 进程（一次成功即清零计数）。
        /// </summary>
        public static void Tick()
        {
            if (_shuttingDown)
            {
                return;
            }
            if (!_automaticPollingEnabled)
            {
                return; // 已因连续失败而放弃，不再自动查询（可手动刷新恢复）
            }
            long now = DateTime.UtcNow.Ticks;
            if (_nextQueryTicks == 0)
            {
                // 首次查询安排在进程启动 10 秒后
                _nextQueryTicks = _sessionStartTicks + BatteryStartDelaySeconds * TicksPerSecond;
            }
            if (now >= _nextQueryTicks)
            {
                RefreshNow();
            }
        }

        /// <summary>
        /// 失败后的重试间隔（秒）：随连续失败次数递增，避免设备长时间离线时每 30 秒白跑一次 adb。
        /// </summary>
        private static long RetryIntervalTicks(int retryCount)
        {
            int seconds = BatteryRetryIntervalSeconds;
            for (int i = 0; i < retryCount && seconds < BatteryMaxRetryIntervalSeconds; i++)
            {
                seconds *= 2;
            }
            if (seconds > BatteryMaxRetryIntervalSeconds)
            {
                seconds = BatteryMaxRetryIntervalSeconds;
            }
            return seconds * TicksPerSecond;
        }

        /// <summary>立即异步刷新（设置页按钮 / 自动轮询）。手动调用时重新启用自动轮询并重置重试计数。</summary>
        public static void RefreshNow(bool manual = false)
        {
            if (_shuttingDown)
            {
                return;
            }

            bool available;
            int retryCount;
            lock (Lock)
            {
                if (manual)
                {
                    _automaticPollingEnabled = true;
                    _retryCount = 0;
                }
                available = _available;
                retryCount = _retryCount;
            }

            // 下次自动查询间隔：成功时按配置间隔，失败时按递增的重试间隔
            long interval = available
                ? Plugin.Config.BatteryRefreshSeconds * TicksPerSecond
                : RetryIntervalTicks(retryCount);
            if (interval < 10 * TicksPerSecond)
            {
                interval = 10 * TicksPerSecond;
            }
            lock (Lock)
            {
                if (_busy)
                {
                    // 已有查询在跑：把下次自动查询顺延一个周期，避免自动轮询每 0.25s 空转
                    _nextQueryTicks = DateTime.UtcNow.Ticks + interval;
                    return;
                }
                _busy = true;
                _nextQueryTicks = DateTime.UtcNow.Ticks + interval;
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
                        _currentString = "";
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

                // 查询结果统计：连续失败达到上限则停止自动轮询并结束 adb
                bool ok;
                bool exhaust = false;
                lock (Lock)
                {
                    ok = _available;
                    if (ok)
                    {
                        _retryCount = 0;
                    }
                    else
                    {
                        _retryCount++;
                        if (_retryCount >= MaxBatteryRetries)
                        {
                            _automaticPollingEnabled = false;
                            exhaust = true;
                        }
                    }
                }
                if (exhaust)
                {
                    Plugin.Log?.Warn($"[RainbowClock] battery unavailable after {MaxBatteryRetries} attempts; stopping ADB polling and killing adb processes.");
                    RunAdbKillServer();
                    KillSessionAdb();
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
            if (!ResolveDevice())
            {
                // 一个在线设备都没有：直接判定「未检测到设备」，
                // 不再盲目执行 adb（没有 -s 参数时 adb 会等待/超时，白等 8 秒）
                SetError(BatteryError.NoDevice);
                return;
            }

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
            _ = status; // status 目前只用于日志；电量一律按电量值渐变，不区分充放电
            string formatted = FormatBattery(level);
            lock (Lock)
            {
                _level = level;
                _available = true;
                _lastErrorType = BatteryError.None;
                _currentString = formatted;
            }
            // 记忆查询成功的设备（多设备选择时优先）
            string serial = TargetSerial;
            if (!string.IsNullOrEmpty(serial) && Plugin.Config.LastAdbSerial != serial)
            {
                Plugin.Config.LastAdbSerial = serial;
            }
            Plugin.Log?.Info($"[RainbowClock] battery: serial={serial} level={level} status={status} charging={status == 2 || status == 5}");
        }

        private static void SetError(BatteryError error)
        {
            lock (Lock)
            {
                _available = false;
                _level = -1;
                _lastErrorType = error;
                _currentString = "";
            }
        }

        private static string _targetSerial = "";

        /// <summary>当前查询目标设备序列号（空表示无可用设备）。</summary>
        public static string TargetSerial
        {
            get { lock (Lock) { return _targetSerial; } }
        }

        /// <summary>统一在锁内更新目标设备（查询在后台线程执行）。</summary>
        private static void SetTargetSerial(string value)
        {
            lock (Lock)
            {
                _targetSerial = value ?? "";
            }
        }

        /// <summary>
        /// 解析目标设备（优先级）：
        /// 1. 配置的 AdbSerial（手动指定）
        /// 2. 有线（USB）在线设备
        /// 3. 上次查询成功的设备（自动记忆，在线则优先）
        /// 4. 无线 VR 头显（model 含 Quest/Pico/Vive/Index）
        /// 5. 无线其他设备（列表顺序）
        /// 返回 false 表示没有任何可用目标（配置为空且无在线设备）。
        /// </summary>
        private static bool ResolveDevice()
        {
            string configured = Plugin.Config.AdbSerial?.Trim() ?? "";
            if (!string.IsNullOrEmpty(configured))
            {
                SetTargetSerial(configured);
                return true;
            }

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
                    Plugin.Log?.Warn($"[RainbowClock] ResolveDevice: noDevice={noDevice}");
                    SetTargetSerial("");
                    return false;
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

            string target;
            if (wired.Length > 0)
            {
                target = wired;
            }
            else if (rememberedOnline)
            {
                target = remembered;
            }
            else if (wirelessVr.Length > 0)
            {
                target = wirelessVr;
            }
            else
            {
                target = wirelessAny;
            }

            SetTargetSerial(target);
            return target.Length > 0;
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
            string adb = ResolveAdbExecutable();
            string serial = Plugin.Config.AdbSerial?.Trim() ?? "";
            if (string.IsNullOrEmpty(serial))
            {
                serial = TargetSerial;
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

        /// <summary>
        /// 游戏退出时调用：结束由本模组拉起的 adb 进程（含常驻 adb server），
        /// 防止 adb 子进程在游戏结束后继续存活，导致 Steam 依“进程树/作业对象”判定游戏仍在运行。
        /// 仅在 <see cref="Plugin.Config.KillAdbOnExit"/> 开启时执行。
        /// </summary>
        public static void Shutdown()
        {
            if (!Plugin.Config.KillAdbOnExit)
            {
                return;
            }
            lock (Lock)
            {
                _shuttingDown = true;
            }
            Plugin.Log?.Info("[RainbowClock] shutdown: cleaning up adb processes...");

            // 1) 优雅关闭 adb server
            RunAdbKillServer();

            // 2) 兜底清理：本次游戏会话期间启动的 adb 进程（含常驻 server 与残留客户端）
            KillSessionAdb();
        }

        /// <summary>向 adb server 发送优雅关闭请求（kill-server 仅作用于本机 adb server）。</summary>
        private static void RunAdbKillServer()
        {
            string adb = ResolveAdbExecutable();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = adb,
                    Arguments = "kill-server",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        return;
                    }
                    if (!proc.WaitForExit(5000))
                    {
                        try { proc.Kill(); } catch { }
                    }
                }
                Plugin.Log?.Info("[RainbowClock] shutdown: adb kill-server requested.");
            }
            catch (Exception e)
            {
                Plugin.Log?.Warn("[RainbowClock] shutdown: adb kill-server failed: " + e.Message);
            }
        }

        /// <summary>
        /// 兜底清理：结束本次游戏会话期间启动的 adb 进程（含常驻 server 与残留客户端）。
        /// 按进程启动时间过滤，避免误杀其他工具在游戏启动前就常驻的 adb server。
        /// </summary>
        private static void KillSessionAdb()
        {
            DateTime gameStart;
            try
            {
                gameStart = Process.GetCurrentProcess().StartTime;
            }
            catch
            {
                gameStart = DateTime.MinValue; // 拿不到启动时间则全量清理
            }

            Process[] adbProcs;
            try
            {
                adbProcs = Process.GetProcessesByName("adb");
            }
            catch (Exception e)
            {
                Plugin.Log?.Error("[RainbowClock] failed to enumerate adb processes: " + e.Message);
                return;
            }

            int killed = 0;
            foreach (Process p in adbProcs)
            {
                try
                {
                    DateTime startTime = p.StartTime; // 可能抛（进程已退出/权限不足），必须放在 try 内
                    if (gameStart != DateTime.MinValue && startTime < gameStart)
                    {
                        continue; // 游戏启动前就存在的 adb，不属于本模组
                    }
                    p.Kill();
                    p.WaitForExit(2000);
                    killed++;
                    Plugin.Log?.Info($"[RainbowClock] killed adb pid={p.Id} start={startTime:HH:mm:ss}");
                }
                catch (Exception e)
                {
                    Plugin.Log?.Warn("[RainbowClock] failed to kill adb pid=" + p.Id + ": " + e.Message);
                }
                finally
                {
                    p.Dispose();
                }
            }
            Plugin.Log?.Info(killed > 0
                ? $"[RainbowClock] killed {killed} leftover adb process(es)."
                : "[RainbowClock] no leftover adb processes.");
        }

        /// <summary>
        /// 解析 adb 可执行文件：显式配置的路径 &gt; 游戏目录内置（部署时安装到游戏根目录） &gt; PATH 中的 adb。
        /// 只在解析出"确定存在的文件"时才缓存：退化为裸 "adb"（PATH 查找）时不缓存，
        /// 否则用户后放进游戏根目录的 adb.exe（或后改的 AdbPath）在本次游戏会话里永远不会被发现。
        /// </summary>
        private static string ResolveAdbExecutable()
        {
            // 用户显式配置的路径：直接用，配置改了要立刻生效
            string configured = Plugin.Config.AdbPath?.Trim() ?? "";
            if (!string.IsNullOrEmpty(configured)
                && !string.Equals(configured, "adb", StringComparison.OrdinalIgnoreCase))
            {
                return configured;
            }

            if (_resolvedAdb != null)
            {
                return _resolvedAdb;
            }

            string gameLocal = FindGameLocalAdb();
            if (!string.IsNullOrEmpty(gameLocal))
            {
                _resolvedAdb = gameLocal;
                Plugin.Log?.Info("[RainbowClock] using bundled adb: " + gameLocal);
                return _resolvedAdb;
            }

            // 退化为 PATH 查找（不缓存，下次查询再探一次游戏目录）
            return "adb";
        }

        /// <summary>查找游戏根目录下内置的 adb.exe（Plugins 的上一级目录），部署时安装到那里。</summary>
        private static string FindGameLocalAdb()
        {
            try
            {
                string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(pluginDir))
                {
                    return null;
                }
                string gameRoot = Directory.GetParent(pluginDir)?.FullName;
                if (string.IsNullOrEmpty(gameRoot))
                {
                    return null;
                }
                string candidate = Path.Combine(gameRoot, "adb.exe");
                return File.Exists(candidate) ? candidate : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>游戏进程启动时间（UTC ticks），用于安排首次查询与判断 adb 是否为本会话所启动。</summary>
        private static long GetSessionStartTicks()
        {
            try
            {
                return Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;
            }
            catch
            {
                return DateTime.UtcNow.Ticks;
            }
        }

        /// <summary>按 Quest 版逻辑格式化电量：一律按电量渐变红→黄→绿（每 20% 均匀分布），不区分充电状态。</summary>
        private static string FormatBattery(int level)
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
