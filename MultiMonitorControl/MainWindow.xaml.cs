using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using MultiMonitorControl.Models;

namespace MultiMonitorControl
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Windows API Declarations
        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("dxva2.dll")]
        static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

        [DllImport("dxva2.dll")]
        static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

        [DllImport("dxva2.dll")]
        static extern bool SetMonitorContrast(IntPtr hMonitor, uint dwNewContrast);

        [DllImport("dxva2.dll")]
        static extern bool GetMonitorContrast(IntPtr hMonitor, out uint pdwMinimumContrast, out uint pdwCurrentContrast, out uint pdwMaximumContrast);

        [DllImport("dxva2.dll")]
        static extern bool SetMonitorRedGreenOrBlueGain(IntPtr hMonitor, uint dwGainType, uint dwNewGain);

        [DllImport("dxva2.dll")]
        static extern bool GetMonitorRedGreenOrBlueGain(IntPtr hMonitor, uint dwGainType, out uint pdwMinimumGain, out uint pdwCurrentGain, out uint pdwMaximumGain);

        [DllImport("dxva2.dll")]
        static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll")]
        static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll")]
        static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("user32.dll")]
        static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        // Constants
        const uint MC_RED_GAIN = 0;
        const uint MC_GREEN_GAIN = 1;
        const uint MC_BLUE_GAIN = 2;
        const uint MONITOR_DEFAULTTOPRIMARY = 1;

        // Structures
        [StructLayout(LayoutKind.Sequential)]
        public struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
        #endregion

        #region Properties and Fields
        private List<MonitorInfo> _monitors = new();
        private MonitorInfo? _selectedMonitor;
        private readonly DispatcherTimer _autoSaveTimer;
        private bool _isUpdatingSliders = false;
        private readonly Dictionary<string, MonitorProfile> _quickProfiles;
        private string _statusMessage = "Ready";

        public event PropertyChangedEventHandler? PropertyChanged;

        public List<MonitorInfo> Monitors
        {
            get => _monitors;
            set
            {
                _monitors = value;
                OnPropertyChanged(nameof(Monitors));
            }
        }

        public MonitorInfo? SelectedMonitor
        {
            get => _selectedMonitor;
            set
            {
                _selectedMonitor = value;
                OnPropertyChanged(nameof(SelectedMonitor));
                LoadMonitorSettings();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize quick profiles
            _quickProfiles = new Dictionary<string, MonitorProfile>
            {
                ["Gaming"] = new MonitorProfile { Brightness = 80, Contrast = 85, RedGain = 55, GreenGain = 50, BlueGain = 45, Description = "High brightness and contrast for gaming" },
                ["Office Work"] = new MonitorProfile { Brightness = 40, Contrast = 60, RedGain = 50, GreenGain = 50, BlueGain = 50, Description = "Eye-friendly settings for office work" },
                ["Movie Watching"] = new MonitorProfile { Brightness = 30, Contrast = 70, RedGain = 52, GreenGain = 50, BlueGain = 48, Description = "Optimized for movie viewing" },
                ["Photo Editing"] = new MonitorProfile { Brightness = 60, Contrast = 75, RedGain = 50, GreenGain = 50, BlueGain = 50, Description = "Color-accurate settings for photo editing" }
            };

            // Setup auto-save timer
            _autoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;

            InitializeApplicationAsync();
        }

        private async void InitializeApplicationAsync()
        {
            UpdateStatus("Initializing application...");
            await LoadMonitorsAsync();
            await LoadLastUsedSettingsAsync();
        }

        #region Monitor Detection and Management
        private async Task LoadMonitorsAsync()
        {
            UpdateStatus("Detecting monitors...");

            await Task.Run(() =>
            {
                var monitors = new List<MonitorInfo>();
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                    (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                    {
                        var monitor = CreateMonitorInfo(hMonitor, lprcMonitor);
                        if (monitor != null)
                            monitors.Add(monitor);
                        return true;
                    }, IntPtr.Zero);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Monitors = monitors;
                    if (Monitors.Any() && SelectedMonitor == null)
                    {
                        SelectedMonitor = Monitors.FirstOrDefault(m => m.IsPrimary) ?? Monitors[0];
                    }
                    UpdateStatus($"Found {Monitors.Count} monitor(s)");
                });
            });
        }

        private MonitorInfo? CreateMonitorInfo(IntPtr hMonitor, RECT rect)
        {
            var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO)) };

            if (GetMonitorInfo(hMonitor, ref mi))
            {
                if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint numMonitors) && numMonitors > 0)
                {
                    var physicalMonitors = new PHYSICAL_MONITOR[numMonitors];
                    if (GetPhysicalMonitorsFromHMONITOR(hMonitor, numMonitors, physicalMonitors))
                    {
                        var pm = physicalMonitors[0];
                        return new MonitorInfo
                        {
                            Handle = pm.hPhysicalMonitor,
                            Name = pm.szPhysicalMonitorDescription,
                            LogicalHandle = hMonitor,
                            Bounds = new System.Drawing.Rectangle(
                                rect.left, rect.top,
                                rect.right - rect.left,
                                rect.bottom - rect.top),
                            IsPrimary = (mi.dwFlags & 1) != 0,
                            SupportsControlAPI = TestMonitorSupport(pm.hPhysicalMonitor)
                        };
                    }
                }
            }
            return null;
        }

        private static bool TestMonitorSupport(IntPtr handle)
        {
            try
            {
                return GetMonitorBrightness(handle, out _, out _, out _);
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Settings Management
        private void LoadMonitorSettings()
        {
            if (SelectedMonitor?.Handle == IntPtr.Zero) return;

            _isUpdatingSliders = true;
            try
            {
                if (!SelectedMonitor.SupportsControlAPI)
                {
                    UpdateStatus("Selected monitor doesn't support hardware control");
                    DisableControls();
                    return;
                }

                EnableControls();

                // Load brightness
                if (GetMonitorBrightness(SelectedMonitor.Handle, out uint minBrightness, out uint currentBrightness, out uint maxBrightness))
                {
                    BrightnessSlider.Minimum = minBrightness;
                    BrightnessSlider.Maximum = maxBrightness;
                    BrightnessSlider.Value = currentBrightness;
                }

                // Load contrast
                if (GetMonitorContrast(SelectedMonitor.Handle, out uint minContrast, out uint currentContrast, out uint maxContrast))
                {
                    ContrastSlider.Minimum = minContrast;
                    ContrastSlider.Maximum = maxContrast;
                    ContrastSlider.Value = currentContrast;
                }

                // Load RGB gains
                LoadColorGain(MC_RED_GAIN, RedSlider);
                LoadColorGain(MC_GREEN_GAIN, GreenSlider);
                LoadColorGain(MC_BLUE_GAIN, BlueSlider);

                UpdateStatus($"Loaded settings for {SelectedMonitor.Name}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading settings: {ex.Message}");
            }
            finally
            {
                _isUpdatingSliders = false;
            }
        }

        private void LoadColorGain(uint gainType, Slider slider)
        {
            if (SelectedMonitor != null && GetMonitorRedGreenOrBlueGain(SelectedMonitor.Handle, gainType, out uint minGain, out uint currentGain, out uint maxGain))
            {
                slider.Minimum = minGain;
                slider.Maximum = maxGain;
                slider.Value = currentGain;
            }
        }

        private void EnableControls()
        {
            BrightnessSlider.IsEnabled = true;
            ContrastSlider.IsEnabled = true;
            RedSlider.IsEnabled = true;
            GreenSlider.IsEnabled = true;
            BlueSlider.IsEnabled = true;
        }

        private void DisableControls()
        {
            BrightnessSlider.IsEnabled = false;
            ContrastSlider.IsEnabled = false;
            RedSlider.IsEnabled = false;
            GreenSlider.IsEnabled = false;
            BlueSlider.IsEnabled = false;
        }
        #endregion

        #region Event Handlers
        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Add null check for SelectedMonitor
            if (_isUpdatingSliders || SelectedMonitor == null || SelectedMonitor.Handle == IntPtr.Zero)
                return;

            try
            {
                SetMonitorBrightness(SelectedMonitor.Handle, (uint)e.NewValue);
                RestartAutoSaveTimer();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error setting brightness: {ex.Message}");
            }
        }

        private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Add null check for SelectedMonitor
            if (_isUpdatingSliders || SelectedMonitor == null || SelectedMonitor.Handle == IntPtr.Zero)
                return;

            try
            {
                SetMonitorContrast(SelectedMonitor.Handle, (uint)e.NewValue);
                RestartAutoSaveTimer();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error setting contrast: {ex.Message}");
            }
        }

        private void RedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Add null check for SelectedMonitor
            if (_isUpdatingSliders || SelectedMonitor == null || SelectedMonitor.Handle == IntPtr.Zero)
                return;

            try
            {
                SetMonitorRedGreenOrBlueGain(SelectedMonitor.Handle, MC_RED_GAIN, (uint)e.NewValue);
                RestartAutoSaveTimer();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error setting red gain: {ex.Message}");
            }
        }

        private void GreenSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Add null check for SelectedMonitor
            if (_isUpdatingSliders || SelectedMonitor == null || SelectedMonitor.Handle == IntPtr.Zero)
                return;

            try
            {
                SetMonitorRedGreenOrBlueGain(SelectedMonitor.Handle, MC_GREEN_GAIN, (uint)e.NewValue);
                RestartAutoSaveTimer();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error setting green gain: {ex.Message}");
            }
        }

        private void BlueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Add null check for SelectedMonitor
            if (_isUpdatingSliders || SelectedMonitor == null || SelectedMonitor.Handle == IntPtr.Zero)
                return;

            try
            {
                SetMonitorRedGreenOrBlueGain(SelectedMonitor.Handle, MC_BLUE_GAIN, (uint)e.NewValue);
                RestartAutoSaveTimer();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error setting blue gain: {ex.Message}");
            }
        }

        private void QuickProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag?.ToString() is string profileName)
            {
                if (_quickProfiles.ContainsKey(profileName))
                {
                    ApplyProfile(_quickProfiles[profileName]);
                    UpdateStatus($"Applied {profileName} profile");
                }
            }
        }

        private async void RefreshMonitors_Click(object sender, RoutedEventArgs e)
        {
            var currentSelection = SelectedMonitor?.Name;

            await LoadMonitorsAsync();

            // Try to reselect the previously selected monitor
            if (!string.IsNullOrEmpty(currentSelection))
            {
                var previousMonitor = Monitors.FirstOrDefault(m => m.Name == currentSelection);
                if (previousMonitor != null)
                {
                    SelectedMonitor = previousMonitor;
                }
            }
        }

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedMonitor == null)
            {
                MessageBox.Show("No monitor selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Monitor Profile (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json",
                FileName = $"MonitorProfile_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                SaveMonitorProfileToFile(dialog.FileName);
            }
        }

        private void LoadProfile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Monitor Profile (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadMonitorProfileFromFile(dialog.FileName);
            }
        }

        private void ResetToDefaults_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedMonitor == null)
            {
                MessageBox.Show("No monitor selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to defaults?",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ResetMonitorToDefaults();
            }
        }

        private void AutoSaveTimer_Tick(object? sender, EventArgs e)
        {
            _autoSaveTimer.Stop();
            _ = SaveLastUsedSettingsAsync();
        }

        protected override void OnKeyDown( KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                RefreshMonitors_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key >= Key.F1 && e.Key <= Key.F4)
            {
                var profiles = _quickProfiles.Keys.ToArray();
                var index = e.Key - Key.F1;
                if (index < profiles.Length)
                {
                    ApplyProfile(_quickProfiles[profiles[index]]);
                    UpdateStatus($"Applied {profiles[index]} profile");
                }
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }
        #endregion

        #region Profile Management
        private void ApplyProfile(MonitorProfile profile)
        {
            if (SelectedMonitor?.Handle == IntPtr.Zero) return;

            try
            {
                SetMonitorBrightness(SelectedMonitor.Handle, (uint)profile.Brightness);
                SetMonitorContrast(SelectedMonitor.Handle, (uint)profile.Contrast);
                SetMonitorRedGreenOrBlueGain(SelectedMonitor.Handle, MC_RED_GAIN, (uint)profile.RedGain);
                SetMonitorRedGreenOrBlueGain(SelectedMonitor.Handle, MC_GREEN_GAIN, (uint)profile.GreenGain);
                SetMonitorRedGreenOrBlueGain(SelectedMonitor.Handle, MC_BLUE_GAIN, (uint)profile.BlueGain);

                LoadMonitorSettings();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error applying profile: {ex.Message}");
                MessageBox.Show($"Error applying profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private MonitorProfile GetCurrentProfile()
        {
            return new MonitorProfile
            {
                MonitorName = SelectedMonitor?.Name ?? "",
                Brightness = (int)BrightnessSlider.Value,
                Contrast = (int)ContrastSlider.Value,
                RedGain = (int)RedSlider.Value,
                GreenGain = (int)GreenSlider.Value,
                BlueGain = (int)BlueSlider.Value,
                Timestamp = DateTime.Now,
                Version = "1.0"
            };
        }

        private void SaveMonitorProfileToFile(string filename)
        {
            try
            {
                var profile = GetCurrentProfile();
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(profile, options);
                File.WriteAllText(filename, json);
                UpdateStatus($"Profile saved to {Path.GetFileName(filename)}");
                MessageBox.Show($"Profile saved successfully to {Path.GetFileName(filename)}", "Profile Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadMonitorProfileFromFile(string filename)
        {
            try
            {
                var json = File.ReadAllText(filename);
                var profile = JsonSerializer.Deserialize<MonitorProfile>(json);
                if (profile != null)
                {
                    ApplyProfile(profile);
                    UpdateStatus($"Profile loaded from {Path.GetFileName(filename)}");
                    MessageBox.Show($"Profile loaded successfully from {Path.GetFileName(filename)}", "Profile Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveLastUsedSettingsAsync()
        {
            try
            {
                var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiMonitorControl");
                Directory.CreateDirectory(settingsPath);

                 var profile = GetCurrentProfile();
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(profile, options);
                await File.WriteAllTextAsync(Path.Combine(settingsPath, "LastUsed.json"), json);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to save settings: {ex.Message}");
            }
        }

        private async Task LoadLastUsedSettingsAsync()
        {
            try
            {
                var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiMonitorControl", "LastUsed.json");
                if (File.Exists(settingsPath))
                {
                    var json = await File.ReadAllTextAsync(settingsPath);
                    var profile = JsonSerializer.Deserialize<MonitorProfile>(json);

                    if (profile != null && SelectedMonitor != null)
                    {
                        // Apply after a short delay to ensure monitor is loaded
                        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                        timer.Tick += (s, e) =>
                        {
                            timer.Stop();
                            if (SelectedMonitor != null)
                            {
                                ApplyProfile(profile);
                                UpdateStatus("Restored previous settings");
                            }
                        };
                        timer.Start();
                    }
                }
            }
            catch
            {
                // Silently fail - settings restoration is not critical
            }
        }

        private void ResetMonitorToDefaults()
        {
            if (SelectedMonitor?.Handle == IntPtr.Zero) return;

            try
            {
                // Get the monitor's capability ranges first
                GetMonitorBrightness(SelectedMonitor.Handle, out uint minBright, out _, out uint maxBright);
                GetMonitorContrast(SelectedMonitor.Handle, out uint minContrast, out _, out uint maxContrast);
                GetMonitorRedGreenOrBlueGain(SelectedMonitor.Handle, MC_RED_GAIN, out uint minRed, out _, out uint maxRed);
                GetMonitorRedGreenOrBlueGain(SelectedMonitor.Handle, MC_GREEN_GAIN, out uint minGreen, out _, out uint maxGreen);
                GetMonitorRedGreenOrBlueGain(SelectedMonitor.Handle, MC_BLUE_GAIN, out uint minBlue, out _, out uint maxBlue);

                // Set to middle values
                uint midBright = (minBright + maxBright) / 2;
                uint midContrast = (minContrast + maxContrast) / 2;
                uint midRed = (minRed + maxRed) / 2;
                uint midGreen = (minGreen + maxGreen) / 2;
                uint midBlue = (minBlue + maxBlue) / 2;

                SetMonitorBrightness(SelectedMonitor.Handle, midBright);
                SetMonitorContrast(SelectedMonitor.Handle, midContrast);
                SetMonitorRedGreenOrBlueGain(SelectedMonitor.Handle, MC_RED_GAIN, midRed);
                SetMonitorRedGreenOrBlueGain(SelectedMonitor.Handle, MC_GREEN_GAIN, midGreen);
                SetMonitorRedGreenOrBlueGain(SelectedMonitor.Handle, MC_BLUE_GAIN, midBlue);

                LoadMonitorSettings();
                UpdateStatus("Settings reset to defaults");
                MessageBox.Show("Monitor settings have been reset to defaults.", "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error resetting settings: {ex.Message}");
                MessageBox.Show($"Error resetting settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Utility Methods
        private void RestartAutoSaveTimer()
        {
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        private void UpdateStatus(string message)
        {
            StatusMessage = message;

            // Clear status after 5 seconds for non-error messages
            if (!message.ToLower().Contains("error"))
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    if (StatusMessage == message) // Only clear if message hasn't changed
                    {
                        StatusMessage = "Ready";
                    }
                };
                timer.Start();
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected override void OnClosed(EventArgs e)
        {
            // Save settings on close
            _ = SaveLastUsedSettingsAsync();

            // Clean up physical monitors
            if (Monitors != null)
            {
                foreach (var monitor in Monitors.Where(m => m.Handle != IntPtr.Zero))
                {
                    try
                    {
                        var physicalMonitors = new PHYSICAL_MONITOR[] { new PHYSICAL_MONITOR { hPhysicalMonitor = monitor.Handle } };
                        DestroyPhysicalMonitors(1, physicalMonitors);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            base.OnClosed(e);
        }
        #endregion
    }
}