// Copyright 2020 Takuto Nakamura
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using Microsoft.Win32;
using RunCat365.Properties;
using System.Diagnostics;
using System.Globalization;
using FormsTimer = System.Windows.Forms.Timer;

namespace RunCat365
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            var defaultCultureInfo = SupportedLanguage.English.GetDefaultCultureInfo();
            CultureInfo.CurrentUICulture = defaultCultureInfo;
            CultureInfo.CurrentCulture = defaultCultureInfo;

            using var procMutex = new Mutex(true, "_RUNCAT_MUTEX", out var result);
            if (!result) return;

            try
            {
                ApplicationConfiguration.Initialize();
                Application.SetColorMode(SystemColorMode.System);
                Application.Run(new RunCat365ApplicationContext());
            }
            finally
            {
                procMutex?.ReleaseMutex();
            }
        }
    }

    internal class RunCat365ApplicationContext : ApplicationContext
    {
        private const int FETCH_TIMER_DEFAULT_INTERVAL = 1000;
        private const int FETCH_COUNTER_SIZE = 5;
        private const int ANIMATE_TIMER_DEFAULT_INTERVAL = 200;
        private readonly CPURepository cpuRepository;
        private readonly GPURepository gpuRepository;
        private readonly MemoryRepository memoryRepository;
        private readonly TemperatureRepository temperatureRepository;
        private readonly StorageRepository storageRepository;
        private readonly NetworkRepository networkRepository;
        private readonly CustomRunnerRepository customRunnerRepository;
        private readonly LaunchAtStartupManager launchAtStartupManager;
        private readonly ContextMenuManager contextMenuManager;
        private readonly FormsTimer fetchTimer;
        private readonly FormsTimer animateTimer;
        private readonly CodexUsageRepository codexRepository = new();
        private readonly FormsTimer codexTimer = new() { Interval = 15000 };
        private readonly DashboardForm dashboard = new();
        private CodexUsageState codexState = new(null, CodexReadStatus.NoUsage, null);
        private bool isReadingCodex;
        private bool isDisposed;
        private Runner runner = Runner.Cat;
        private Theme manualTheme = Theme.System;
        private TemperatureUnit temperatureUnit = TemperatureUnit.System;
        private FPSMaxLimit fpsMaxLimit = FPSMaxLimit.FPS40;
        private SpeedSource speedSource = SpeedSource.CPU;
        private string? customRunnerName;
        private int fetchCounter = 5;
        private bool isFetching;

        public RunCat365ApplicationContext()
        {
            UserSettings.Default.Reload();
            _ = Enum.TryParse(UserSettings.Default.Runner, out runner);
            _ = Enum.TryParse(UserSettings.Default.Theme, out manualTheme);
            _ = Enum.TryParse(UserSettings.Default.TemperatureUnit, out temperatureUnit);
            _ = Enum.TryParse(UserSettings.Default.FPSMaxLimit, out fpsMaxLimit);
            _ = Enum.TryParse(UserSettings.Default.SpeedSource, out speedSource);
            customRunnerName = string.IsNullOrEmpty(UserSettings.Default.CustomRunnerName)
                ? null
                : UserSettings.Default.CustomRunnerName;

            SystemEvents.UserPreferenceChanged += new UserPreferenceChangedEventHandler(UserPreferenceChanged);

            cpuRepository = new CPURepository();
            gpuRepository = new GPURepository();
            memoryRepository = new MemoryRepository();
            temperatureRepository = new TemperatureRepository();
            storageRepository = new StorageRepository();
            networkRepository = new NetworkRepository();
            customRunnerRepository = new CustomRunnerRepository();
            launchAtStartupManager = new LaunchAtStartupManager();

            ResolveSpeedSource();

            contextMenuManager = new ContextMenuManager(
                () => runner,
                r => ChangeRunner(r),
                customRunnerRepository,
                () => customRunnerName,
                name => ApplyCustomRunner(name),
                deletedName => HandleCustomRunnerDeleted(deletedName),
                () => GetSystemTheme(),
                () => manualTheme,
                t => ChangeManualTheme(t),
                () => speedSource,
                s => ChangeSpeedSource(s),
                s => IsSpeedSourceAvailable(s),
                () => fpsMaxLimit,
                f => ChangeFPSMaxLimit(f),
                () => temperatureUnit,
                u => ChangeTemperatureUnit(u),
                () => launchAtStartupManager.GetStartup(),
                s => launchAtStartupManager.ToggleStartup(s),
                () => ShowDashboard(),
                () => OpenProjectPage(),
                () => Application.Exit()
            );

            dashboard.MenuRequested += point => contextMenuManager.ShowMenu(point);

            if (customRunnerName is not null)
            {
                ApplyCustomRunner(customRunnerName);
            }

            animateTimer = new FormsTimer
            {
                Interval = ANIMATE_TIMER_DEFAULT_INTERVAL
            };
            animateTimer.Tick += new EventHandler(AnimationTick);
            animateTimer.Start();

            fetchTimer = new FormsTimer
            {
                Interval = FETCH_TIMER_DEFAULT_INTERVAL
            };
            fetchTimer.Tick += new EventHandler(FetchTick);
            fetchTimer.Start();

            codexTimer.Tick += RefreshCodex;
            codexTimer.Start();
            RefreshCodex(this, EventArgs.Empty);

            ShowBalloonTipIfNeeded();
        }

        private static Theme GetSystemTheme()
        {
            var keyName = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            using var rKey = Registry.CurrentUser.OpenSubKey(keyName);
            if (rKey is null) return Theme.Light;
            var value = rKey.GetValue("SystemUsesLightTheme");
            if (value is null) return Theme.Light;
            return (int)value == 0 ? Theme.Dark : Theme.Light;
        }

        private void ShowDashboard()
        {
            if (dashboard.Visible) { dashboard.Hide(); return; }
            UpdateDashboard();
            dashboard.ShowNearCursor();
        }

        private void UpdateDashboard()
        {
            dashboard.UpdateMetrics(cpuRepository.IsAvailable ? cpuRepository.Get() : null,
                memoryRepository.Get(), codexState, manualTheme == Theme.System ? GetSystemTheme() : manualTheme);
        }

        private async void RefreshCodex(object? sender, EventArgs e)
        {
            if (isReadingCodex || isDisposed) return;
            isReadingCodex = true;
            try
            {
                await Task.Run(codexRepository.Update);
                if (isDisposed) return;
                codexState = codexRepository.State;
                if (dashboard.Visible) UpdateDashboard();
            }
            finally { isReadingCodex = false; }
        }

        private bool IsSpeedSourceAvailable(SpeedSource speedSource)
        {
            return speedSource switch
            {
                SpeedSource.CPU => true,
                SpeedSource.GPU => gpuRepository.IsAvailable,
                SpeedSource.Memory => true,
                _ => false,
            };
        }

        private void ResolveSpeedSource()
        {
            if (!IsSpeedSourceAvailable(speedSource))
            {
                ChangeSpeedSource(SpeedSource.CPU);
            }
        }

        private void ShowBalloonTipIfNeeded()
        {
            if (!cpuRepository.IsAvailable)
            {
                contextMenuManager.ShowBalloonTip(BalloonTipType.CPUInfoUnavailable);
            }
            else if (UserSettings.Default.FirstLaunch)
            {
                contextMenuManager.ShowBalloonTip(BalloonTipType.AppLaunched);
                UserSettings.Default.FirstLaunch = false;
                UserSettings.Default.Save();
            }
        }

        private void UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General) return;
            var systemTheme = GetSystemTheme();
            if (contextMenuManager.HasActiveCustomIcons)
            {
                contextMenuManager.RecolorActiveCustomIcons(systemTheme, manualTheme);
            }
            else
            {
                contextMenuManager.SetIcons(systemTheme, manualTheme, runner);
            }
        }

        private static void OpenProjectPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = "https://github.com/runcat-dev/RunCat365",
                    UseShellExecute = true
                });
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }
        }

        private void ChangeRunner(Runner r)
        {
            runner = r;
            customRunnerName = null;
            UserSettings.Default.Runner = runner.ToString();
            UserSettings.Default.CustomRunnerName = string.Empty;
            UserSettings.Default.Save();
        }

        private void ApplyCustomRunner(string name)
        {
            var frames = customRunnerRepository.LoadFrames(name);
            if (frames.Count == 0) return;
            customRunnerName = name;
            UserSettings.Default.CustomRunnerName = name;
            UserSettings.Default.Save();
            contextMenuManager.SetCustomIcons(frames, GetSystemTheme(), manualTheme);
            foreach (var frame in frames) frame.Dispose();
        }

        private void RevertToBuiltInRunner()
        {
            customRunnerName = null;
            UserSettings.Default.CustomRunnerName = string.Empty;
            UserSettings.Default.Save();
            contextMenuManager.SetIcons(GetSystemTheme(), manualTheme, runner);
        }

        private void HandleCustomRunnerDeleted(string deletedName)
        {
            if (!string.Equals(customRunnerName, deletedName, StringComparison.OrdinalIgnoreCase)) return;
            RevertToBuiltInRunner();
        }

        private void ChangeManualTheme(Theme t)
        {
            manualTheme = t;
            UserSettings.Default.Theme = manualTheme.ToString();
            UserSettings.Default.Save();
        }

        private void ChangeTemperatureUnit(TemperatureUnit u)
        {
            temperatureUnit = u;
            UserSettings.Default.TemperatureUnit = temperatureUnit.ToString();
            UserSettings.Default.Save();
        }

        private void ChangeSpeedSource(SpeedSource s)
        {
            speedSource = s;
            UserSettings.Default.SpeedSource = speedSource.ToString();
            UserSettings.Default.Save();
        }

        private void ChangeFPSMaxLimit(FPSMaxLimit f)
        {
            fpsMaxLimit = f;
            UserSettings.Default.FPSMaxLimit = fpsMaxLimit.ToString();
            UserSettings.Default.Save();
        }

        private void AnimationTick(object? sender, EventArgs e)
        {
            contextMenuManager.AdvanceFrame();
        }

        private string GetInfoDescription(CPUInfo cpuInfo, GPUInfo? gpuInfo, MemoryInfo memoryInfo, TemperatureInfo? temperatureInfo)
        {
            var baseDescription = speedSource switch
            {
                SpeedSource.CPU => cpuInfo.GetDescription(),
                SpeedSource.GPU => gpuInfo?.GetDescription() ?? "",
                SpeedSource.Memory => memoryInfo.GetDescription(),
                _ => "",
            };

            var temperatureDescription = temperatureInfo?.GetDescription(temperatureUnit) ?? "";
            return string.IsNullOrEmpty(temperatureDescription) ? baseDescription : $"{baseDescription}\n{temperatureDescription}";
        }

        private int CalculateInterval(CPUInfo cpuInfo, GPUInfo? gpuInfo, MemoryInfo memoryInfo)
        {
            var load = speedSource switch
            {
                SpeedSource.CPU => cpuInfo.Total,
                SpeedSource.GPU => gpuInfo?.Maximum ?? 0f,
                SpeedSource.Memory => memoryInfo.MemoryLoad,
                _ => 0f,
            };
            var speed = (float)Math.Max(1.0f, (load / 5.0f) * fpsMaxLimit.GetRate());
            return (int)(500.0f / speed);
        }

        private int FetchSystemInfo()
        {
            var cpuInfo = cpuRepository.Get();
            var gpuInfo = gpuRepository.Get();
            var memoryInfo = memoryRepository.Get();
            var temperatureInfo = temperatureRepository.Get();
            var storageInfo = storageRepository.Get();
            var networkInfo = networkRepository.Get();

            contextMenuManager.SetNotifyIconText(GetInfoDescription(cpuInfo, gpuInfo, memoryInfo, temperatureInfo));

            var systemInfoValues = new List<string>();
            systemInfoValues.AddRange(cpuInfo.GenerateIndicator());
            if (gpuInfo.HasValue)
            {
                systemInfoValues.AddRange(gpuInfo.Value.GenerateIndicator());
            }
            systemInfoValues.AddRange(memoryInfo.GenerateIndicator());
            if (temperatureInfo.HasValue)
            {
                systemInfoValues.AddRange(temperatureInfo.Value.GenerateIndicator(temperatureUnit));
            }
            systemInfoValues.AddRange(storageInfo.GenerateIndicator());
            if (networkInfo.HasValue)
            {
                systemInfoValues.AddRange(networkInfo.Value.GenerateIndicator());
            }
            contextMenuManager.SetSystemInfoMenuText(string.Join("\n", [.. systemInfoValues]));

            return CalculateInterval(cpuInfo, gpuInfo, memoryInfo);
        }

        private async void FetchTick(object? sender, EventArgs e)
        {
            if (isFetching || isDisposed) return;
            isFetching = true;
            try
            {
                var doHeavySlice = await Task.Run(() =>
                {
                    cpuRepository.Update();
                    memoryRepository.Update();
                    gpuRepository.Update();
                    fetchCounter += 1;
                    if (fetchCounter < FETCH_COUNTER_SIZE) return false;
                    fetchCounter = 0;
                    temperatureRepository.Update();
                    storageRepository.Update();
                    networkRepository.Update();
                    return true;
                });

                if (isDisposed) return;
                dashboard.RecordSample(DateTimeOffset.UtcNow, cpuRepository.IsAvailable ? cpuRepository.Get().Total : null,
                    memoryRepository.Get().TotalMemory > 0 ? memoryRepository.Get().MemoryLoad : null);
                if (dashboard.Visible) UpdateDashboard();
                if (!doHeavySlice) return;

                var interval = FetchSystemInfo();
                animateTimer.Stop();
                animateTimer.Interval = interval;
                animateTimer.Start();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"FetchTick failed: {exception.Message}");
            }
            finally
            {
                isFetching = false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                isDisposed = true;
                codexTimer.Stop();
                codexTimer.Dispose();
                dashboard.Dispose();
                SystemEvents.UserPreferenceChanged -= UserPreferenceChanged;

                animateTimer?.Stop();
                animateTimer?.Dispose();
                fetchTimer?.Stop();
                fetchTimer?.Dispose();

                cpuRepository?.Close();
                gpuRepository?.Close();
                temperatureRepository?.Close();

                contextMenuManager?.HideNotifyIcon();
                contextMenuManager?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
