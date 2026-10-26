// Copyright 2025 Takuto Nakamura
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

using RunCat365.Properties;
using System.ComponentModel;

namespace RunCat365
{
    internal class ContextMenuManager : IDisposable
    {
        private readonly CustomToolStripMenuItem systemInfoMenu = new();
        private readonly NotifyIcon notifyIcon = new();
        private readonly List<Icon> icons = [];
        private readonly Lock iconLock = new();
        private int current = 0;
        private EndlessGameForm? endlessGameForm;
        private CustomRunnerForm? customRunnerForm;
        private List<Bitmap>? customRunnerSourceFrames;

        internal ContextMenuManager(
            Func<Runner> getRunner,
            Action<Runner> setRunner,
            CustomRunnerRepository customRunnerRepository,
            Func<string?> getCustomRunnerName,
            Action<string> applyCustomRunner,
            Action<string> onCustomRunnerDeleted,
            Func<Theme> getSystemTheme,
            Func<Theme> getManualTheme,
            Action<Theme> setManualTheme,
            Func<SpeedSource> getSpeedSource,
            Action<SpeedSource> setSpeedSource,
            Func<SpeedSource, bool> isSpeedSourceAvailable,
            Func<FPSMaxLimit> getFPSMaxLimit,
            Action<FPSMaxLimit> setFPSMaxLimit,
            Func<TemperatureUnit> getTemperatureUnit,
            Action<TemperatureUnit> setTemperatureUnit,
            Func<bool> getLaunchAtStartup,
            Func<bool, bool> toggleLaunchAtStartup,
            Action showDashboard,
            Action openProjectPage,
            Action onExit
        )
        {
            systemInfoMenu.Text = "-\n-\n-\n-\n-";
            systemInfoMenu.Enabled = false;

            var runnersMenu = new CustomToolStripMenuItem(Strings.Menu_Runner);
            runnersMenu.SetupSubMenusFromEnum<Runner>(
                r => r.GetLocalizedString(),
                (parent, sender, e) =>
                {
                    HandleMenuItemSelection<Runner>(
                        parent,
                        sender,
                        (string? s, out Runner r) => Enum.TryParse(s, out r),
                        r => setRunner(r)
                    );
                    SetIcons(getSystemTheme(), getManualTheme(), getRunner());
                },
                r => getCustomRunnerName() is null && getRunner() == r,
                r => GetRunnerThumbnailBitmap(getSystemTheme(), r)
            );
            runnersMenu.DropDownOpening += (sender, e) => RefreshCustomRunnerMenu(
                runnersMenu,
                customRunnerRepository,
                ResolveTheme(getSystemTheme(), getManualTheme()),
                getRunner,
                getCustomRunnerName,
                applyCustomRunner
            );

            var themeMenu = new CustomToolStripMenuItem(Strings.Menu_Theme);
            themeMenu.SetupSubMenusFromEnum<Theme>(
                t => t.GetLocalizedString(),
                (parent, sender, e) =>
                {
                    HandleMenuItemSelection<Theme>(
                        parent,
                        sender,
                        (string? s, out Theme t) => Enum.TryParse(s, out t),
                        t => setManualTheme(t)
                    );
                    SetIcons(getSystemTheme(), getManualTheme(), getRunner());
                },
                t => getManualTheme() == t,
                _ => null
            );

            var speedSourceMenu = new CustomToolStripMenuItem(Strings.Menu_SpeedSource);
            speedSourceMenu.SetupSubMenusFromEnum<SpeedSource>(
                s => s.GetLocalizedString(),
                (parent, sender, e) =>
                {
                    HandleMenuItemSelection<SpeedSource>(
                        parent,
                        sender,
                        (string? s, out SpeedSource ss) => Enum.TryParse(s, out ss),
                        s => setSpeedSource(s)
                    );
                },
                s => getSpeedSource() == s,
                _ => null,
                isSpeedSourceAvailable
            );

            var fpsMaxLimitMenu = new CustomToolStripMenuItem(Strings.Menu_FPSMaxLimit);
            fpsMaxLimitMenu.SetupSubMenusFromEnum<FPSMaxLimit>(
                f => f.GetString(),
                (parent, sender, e) =>
                {
                    HandleMenuItemSelection<FPSMaxLimit>(
                        parent,
                        sender,
                        (string? s, out FPSMaxLimit f) => FPSMaxLimitExtension.TryParse(s, out f),
                        f => setFPSMaxLimit(f)
                    );
                },
                f => getFPSMaxLimit() == f,
                _ => null
            );

            var temperatureUnitMenu = new CustomToolStripMenuItem(Strings.Menu_TemperatureUnit);
            temperatureUnitMenu.SetupSubMenusFromEnum<TemperatureUnit>(
                u => u.GetLocalizedString(),
                (parent, sender, e) =>
                {
                    HandleMenuItemSelection<TemperatureUnit>(
                        parent,
                        sender,
                        (string? s, out TemperatureUnit u) => Enum.TryParse(s, out u),
                        u => setTemperatureUnit(u)
                    );
                },
                u => getTemperatureUnit() == u,
                _ => null
            );

            var launchAtStartupMenu = new CustomToolStripMenuItem(Strings.Menu_LaunchAtStartup)
            {
                Checked = getLaunchAtStartup()
            };
            launchAtStartupMenu.Click += (sender, e) => HandleStartupMenuClick(sender, toggleLaunchAtStartup);

            var settingsMenu = new CustomToolStripMenuItem(Strings.Menu_Settings);
            settingsMenu.DropDownItems.AddRange(
                themeMenu,
                speedSourceMenu,
                fpsMaxLimitMenu,
                temperatureUnitMenu,
                launchAtStartupMenu
            );

            var customRunnersMenu = new CustomToolStripMenuItem(Strings.Menu_CustomRunners);
            customRunnersMenu.Click += (sender, e) => ShowOrActivateCustomRunnerWindow(
                customRunnerRepository, onCustomRunnerDeleted
            );

            var endlessGameMenu = new CustomToolStripMenuItem(Strings.Menu_EndlessGame);
            endlessGameMenu.Click += (sender, e) => ShowOrActivateGameWindow(getSystemTheme);

            var appVersionMenu = new CustomToolStripMenuItem(
                $"{Application.ProductName} v{Application.ProductVersion}"
            )
            {
                Enabled = false
            };

            var projectPageMenu = new CustomToolStripMenuItem(Strings.Menu_OpenProjectPage);
            projectPageMenu.Click += (sender, e) => openProjectPage();

            var informationMenu = new CustomToolStripMenuItem(Strings.Menu_Information);
            informationMenu.DropDownItems.AddRange(
                appVersionMenu,
                projectPageMenu
            );

            var exitMenu = new CustomToolStripMenuItem(Strings.Menu_Exit);
            exitMenu.Click += (sender, e) => onExit();

            var contextMenuStrip = new ContextMenuStrip(new Container());
            var dashboardMenu = new CustomToolStripMenuItem(DashboardForm.TextFor("Open"));
            dashboardMenu.Click += (_, _) => showDashboard();
            contextMenuStrip.Items.AddRange(
                dashboardMenu,
                systemInfoMenu,
                new ToolStripSeparator(),
                runnersMenu,
                customRunnersMenu,
                new ToolStripSeparator(),
                settingsMenu,
                informationMenu,
                endlessGameMenu,
                new ToolStripSeparator(),
                exitMenu
            );
            contextMenuStrip.Renderer = new ContextMenuRenderer();

            SetIcons(getSystemTheme(), getManualTheme(), getRunner());

            notifyIcon.Visible = true;
            notifyIcon.ContextMenuStrip = contextMenuStrip;
            notifyIcon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) showDashboard(); };
        }

        private static void HandleMenuItemSelection<T>(
            ToolStripMenuItem parentMenu,
            object? sender,
            CustomTryParseDelegate<T> tryParseMethod,
            Action<T> assignValueAction
        )
        {
            if (sender is null) return;
            var item = (ToolStripMenuItem)sender;
            foreach (ToolStripItem childItem in parentMenu.DropDownItems)
            {
                if (childItem is ToolStripMenuItem menuItem)
                {
                    menuItem.Checked = false;
                }
            }
            item.Checked = true;

            if (item.Tag is T tagValue)
            {
                assignValueAction(tagValue);
            }
            else if (tryParseMethod(item.Text, out T parsedValue))
            {
                assignValueAction(parsedValue);
            }
        }

        private static Bitmap? GetRunnerThumbnailBitmap(Theme systemTheme, Runner runner)
        {
            var color = runner == Runner.Cat ? Color.Black : systemTheme.GetContrastColor();
            var iconName = $"{runner.GetString()}_0".ToLower();
            var obj = Resources.ResourceManager.GetObject(iconName);
            if (obj is not Bitmap bitmap) return null;
            return systemTheme == Theme.Light && runner != Runner.Cat ? bitmap : bitmap.Recolor(color);
        }

        private static void RefreshCustomRunnerMenu(
            CustomToolStripMenuItem runnersMenu,
            CustomRunnerRepository customRunnerRepository,
            Theme theme,
            Func<Runner> getRunner,
            Func<string?> getCustomRunnerName,
            Action<string> applyCustomRunner
        )
        {
            foreach (ToolStripItem item in runnersMenu.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem && item.Tag is Runner runner)
                {
                    menuItem.Checked = getCustomRunnerName() is null && getRunner() == runner;
                }
            }

            var customItems = runnersMenu.DropDownItems
                .Cast<ToolStripItem>()
                .Where(item => item.Tag is CustomRunnerMenuTag)
                .ToList();
            foreach (var item in customItems)
            {
                runnersMenu.DropDownItems.Remove(item);
                if (item is ToolStripMenuItem menuItem)
                {
                    menuItem.Image?.Dispose();
                }
                item.Dispose();
            }

            var profiles = customRunnerRepository.GetAll().OrderBy(p => p.Name).ToList();
            if (profiles.Count == 0) return;

            runnersMenu.DropDownItems.Add(new ToolStripSeparator { Tag = new CustomRunnerMenuTag("") });
            foreach (var profile in profiles)
            {
                var name = profile.Name;
                var item = new CustomToolStripMenuItem(name)
                {
                    Tag = new CustomRunnerMenuTag(name),
                    Checked = string.Equals(getCustomRunnerName(), name, StringComparison.OrdinalIgnoreCase),
                    Image = CreateCustomRunnerThumbnail(customRunnerRepository, name, theme)
                };
                item.Click += (sender, e) => applyCustomRunner(name);
                runnersMenu.DropDownItems.Add(item);
            }
        }

        private static Bitmap? CreateCustomRunnerThumbnail(CustomRunnerRepository repository, string name, Theme theme)
        {
            var firstFrame = repository.LoadFirstFrame(name);
            if (firstFrame is null) return null;
            if (theme == Theme.Light) return firstFrame;
            using (firstFrame)
            {
                return firstFrame.Recolor(theme.GetContrastColor());
            }
        }

        internal void SetIcons(Theme systemTheme, Theme manualTheme, Runner runner)
        {
            ClearCustomRunnerSourceFrames();

            var runnerName = runner.GetString();
            var rm = Resources.ResourceManager;
            var capacity = runner.GetFrameNumber();
            var bitmaps = new List<Bitmap>(capacity);
            for (int i = 0; i < capacity; i++)
            {
                var iconName = $"{runnerName}_{i}".ToLower();
                if (rm.GetObject(iconName) is Bitmap bitmap)
                {
                    bitmaps.Add(bitmap);
                }
            }
            ReplaceIconList(bitmaps, ResolveTheme(systemTheme, manualTheme), runner == Runner.Cat ? Color.Black : null);
        }

        internal void SetCustomIcons(List<Bitmap> frames, Theme systemTheme, Theme manualTheme)
        {
            ClearCustomRunnerSourceFrames();
            customRunnerSourceFrames = frames.Select(f => new Bitmap(f)).ToList();
            ReplaceIconList(customRunnerSourceFrames, ResolveTheme(systemTheme, manualTheme));
        }

        internal void RecolorActiveCustomIcons(Theme systemTheme, Theme manualTheme)
        {
            if (customRunnerSourceFrames is null) return;
            ReplaceIconList(customRunnerSourceFrames, ResolveTheme(systemTheme, manualTheme));
        }

        internal bool HasActiveCustomIcons => customRunnerSourceFrames is not null;

        private void ReplaceIconList(IList<Bitmap> frames, Theme theme, Color? fixedColor = null)
        {
            var color = fixedColor ?? theme.GetContrastColor();
            var list = new List<Icon>(frames.Count);
            foreach (var frame in frames)
            {
                if (theme == Theme.Light && fixedColor is null)
                {
                    list.Add(frame.ToIcon());
                }
                else
                {
                    using var recolored = frame.Recolor(color);
                    list.Add(recolored.ToIcon());
                }
            }

            List<Icon> oldIcons;
            lock (iconLock)
            {
                oldIcons = new List<Icon>(icons);
                icons.Clear();
                icons.AddRange(list);
                current = 0;
                if (icons.Count > 0)
                {
                    notifyIcon.Icon = icons[0];
                    current = 1 % icons.Count;
                }
            }

            foreach (var icon in oldIcons) icon.Dispose();
        }

        private void ClearCustomRunnerSourceFrames()
        {
            if (customRunnerSourceFrames is null) return;
            foreach (var bitmap in customRunnerSourceFrames) bitmap.Dispose();
            customRunnerSourceFrames = null;
        }

        private static Theme ResolveTheme(Theme systemTheme, Theme manualTheme)
        {
            return manualTheme == Theme.System ? systemTheme : manualTheme;
        }

        private static void HandleStartupMenuClick(object? sender, Func<bool, bool> toggleLaunchAtStartup)
        {
            if (sender is null) return;
            var item = (ToolStripMenuItem)sender;
            try
            {
                if (toggleLaunchAtStartup(item.Checked))
                {
                    item.Checked = !item.Checked;
                }
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, Strings.Message_Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

        }

        private void ShowOrActivateGameWindow(Func<Theme> getSystemTheme)
        {
            if (endlessGameForm is null)
            {
                endlessGameForm = new EndlessGameForm(getSystemTheme());
                endlessGameForm.FormClosed += (sender, e) =>
                {
                    endlessGameForm = null;
                };
                endlessGameForm.Show();
            }
            else
            {
                endlessGameForm.Activate();
            }
        }

        private void ShowOrActivateCustomRunnerWindow(
            CustomRunnerRepository repository,
            Action<string> onCustomRunnerDeleted
        )
        {
            if (customRunnerForm is null)
            {
                customRunnerForm = new CustomRunnerForm(repository, onCustomRunnerDeleted);
                customRunnerForm.FormClosed += (sender, e) =>
                {
                    customRunnerForm = null;
                };
                customRunnerForm.Show();
            }
            else
            {
                customRunnerForm.Activate();
            }
        }

        internal void ShowBalloonTip(BalloonTipType balloonTipType)
        {
            var info = balloonTipType.GetInfo();
            notifyIcon.ShowBalloonTip(5000, info.Title, info.Text, info.Icon);
        }

        internal void AdvanceFrame()
        {
            lock (iconLock)
            {
                if (icons.Count == 0) return;
                if (icons.Count <= current) current = 0;
                notifyIcon.Icon = icons[current];
                current = (current + 1) % icons.Count;
            }
        }

        internal void SetSystemInfoMenuText(string text)
        {
            systemInfoMenu.Text = text;
        }

        internal void SetNotifyIconText(string text)
        {
            notifyIcon.Text = text;
        }

        internal void ShowMenu(Point location)
        {
            notifyIcon.ContextMenuStrip?.Show(location);
        }

        internal void HideNotifyIcon()
        {
            notifyIcon.Visible = false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (iconLock)
                {
                    foreach (var icon in icons) icon.Dispose();
                    icons.Clear();
                }

                ClearCustomRunnerSourceFrames();

                if (notifyIcon is not null)
                {
                    notifyIcon.ContextMenuStrip?.Dispose();
                    notifyIcon.Dispose();
                }

                endlessGameForm?.Dispose();
                customRunnerForm?.Dispose();
            }
        }

        private delegate bool CustomTryParseDelegate<T>(string? value, out T result);

        private sealed record CustomRunnerMenuTag(string Name);
    }
}
