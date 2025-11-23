/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.UI.Windows;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public sealed class EspViewModel : INotifyPropertyChanged
    {
        private EspWindow _espWindow;
        private List<MonitorInfo> _availableMonitors;
        private MonitorInfo _selectedMonitor;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ICommand OpenEspCommand { get; }
        public ICommand CloseEspCommand { get; }
        public ICommand RefreshMonitorsCommand { get; }
        public ICommand DetectResolutionCommand { get; }

        // Window Settings
        public bool AutoOpen
        {
            get => App.Config.ESP.AutoOpen;
            set { App.Config.ESP.AutoOpen = value; OnPropertyChanged(); }
        }

        public List<MonitorInfo> AvailableMonitors
        {
            get => _availableMonitors;
            set { _availableMonitors = value; OnPropertyChanged(); }
        }

        public MonitorInfo SelectedMonitor
        {
            get => _selectedMonitor;
            set
            {
                _selectedMonitor = value;
                if (value != null)
                {
                    App.Config.ESP.SelectedMonitor = value.Index;
                    App.Config.ESP.Resolution = new Size(value.Width, value.Height);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ResolutionWidth));
                    OnPropertyChanged(nameof(ResolutionHeight));
                }
            }
        }

        public int ResolutionWidth
        {
            get => (int)App.Config.ESP.Resolution.Width;
            set
            {
                App.Config.ESP.Resolution = new Size(value, App.Config.ESP.Resolution.Height);
                OnPropertyChanged();
            }
        }

        public int ResolutionHeight
        {
            get => (int)App.Config.ESP.Resolution.Height;
            set
            {
                App.Config.ESP.Resolution = new Size(App.Config.ESP.Resolution.Width, value);
                OnPropertyChanged();
            }
        }

        // General
        public bool ShowDebug
        {
            get => App.Config.ESP.ShowDebug;
            set { App.Config.ESP.ShowDebug = value; OnPropertyChanged(); }
        }

        // Players - Display Options
        public bool ShowPlayers
        {
            get => App.Config.ESP.ShowPlayers;
            set { App.Config.ESP.ShowPlayers = value; OnPropertyChanged(); }
        }

        public bool ShowBox
        {
            get => App.Config.ESP.ShowBox;
            set { App.Config.ESP.ShowBox = value; OnPropertyChanged(); }
        }

        public bool ShowSkeleton
        {
            get => App.Config.ESP.ShowSkeleton;
            set { App.Config.ESP.ShowSkeleton = value; OnPropertyChanged(); }
        }

        public bool ShowHeadDot
        {
            get => App.Config.ESP.ShowHeadDot;
            set { App.Config.ESP.ShowHeadDot = value; OnPropertyChanged(); }
        }

        public bool ShowNames
        {
            get => App.Config.ESP.ShowNames;
            set { App.Config.ESP.ShowNames = value; OnPropertyChanged(); }
        }

        public bool ShowDistance
        {
            get => App.Config.ESP.ShowDistance;
            set { App.Config.ESP.ShowDistance = value; OnPropertyChanged(); }
        }

        public bool ShowHealth
        {
            get => App.Config.ESP.ShowHealth;
            set { App.Config.ESP.ShowHealth = value; OnPropertyChanged(); }
        }

        // Player Types
        public bool ShowPMC
        {
            get => App.Config.ESP.ShowPMC;
            set { App.Config.ESP.ShowPMC = value; OnPropertyChanged(); }
        }

        public bool ShowTeammate
        {
            get => App.Config.ESP.ShowTeammate;
            set { App.Config.ESP.ShowTeammate = value; OnPropertyChanged(); }
        }

        public bool ShowPScav
        {
            get => App.Config.ESP.ShowPScav;
            set { App.Config.ESP.ShowPScav = value; OnPropertyChanged(); }
        }

        public bool ShowAIScav
        {
            get => App.Config.ESP.ShowAIScav;
            set { App.Config.ESP.ShowAIScav = value; OnPropertyChanged(); }
        }

        public bool ShowAIRaider
        {
            get => App.Config.ESP.ShowAIRaider;
            set { App.Config.ESP.ShowAIRaider = value; OnPropertyChanged(); }
        }

        public bool ShowAIBoss
        {
            get => App.Config.ESP.ShowAIBoss;
            set { App.Config.ESP.ShowAIBoss = value; OnPropertyChanged(); }
        }

        public bool ShowSpecialPlayer
        {
            get => App.Config.ESP.ShowSpecialPlayer;
            set { App.Config.ESP.ShowSpecialPlayer = value; OnPropertyChanged(); }
        }

        // Distance Filters
        public float MaxDistancePMC
        {
            get => App.Config.ESP.MaxDistancePMC;
            set { App.Config.ESP.MaxDistancePMC = value; OnPropertyChanged(); }
        }

        public float MaxDistanceTeammate
        {
            get => App.Config.ESP.MaxDistanceTeammate;
            set { App.Config.ESP.MaxDistanceTeammate = value; OnPropertyChanged(); }
        }

        public float MaxDistancePScav
        {
            get => App.Config.ESP.MaxDistancePScav;
            set { App.Config.ESP.MaxDistancePScav = value; OnPropertyChanged(); }
        }

        public float MaxDistanceAIScav
        {
            get => App.Config.ESP.MaxDistanceAIScav;
            set { App.Config.ESP.MaxDistanceAIScav = value; OnPropertyChanged(); }
        }

        public float MaxDistanceAIRaider
        {
            get => App.Config.ESP.MaxDistanceAIRaider;
            set { App.Config.ESP.MaxDistanceAIRaider = value; OnPropertyChanged(); }
        }

        public float MaxDistanceAIBoss
        {
            get => App.Config.ESP.MaxDistanceAIBoss;
            set { App.Config.ESP.MaxDistanceAIBoss = value; OnPropertyChanged(); }
        }

        public float MaxDistanceSpecialPlayer
        {
            get => App.Config.ESP.MaxDistanceSpecialPlayer;
            set { App.Config.ESP.MaxDistanceSpecialPlayer = value; OnPropertyChanged(); }
        }

        // Loot
        public bool ShowLoot
        {
            get => App.Config.ESP.ShowLoot;
            set { App.Config.ESP.ShowLoot = value; OnPropertyChanged(); }
        }
        public bool ShowMakcuDebug
        {
            get => App.Config.Makcu.ShowDebug;
            set { App.Config.Makcu.ShowDebug = value; OnPropertyChanged(); }
        }

        public float MaxDistanceLoot
        {
            get => App.Config.ESP.MaxDistanceLoot;
            set { App.Config.ESP.MaxDistanceLoot = value; OnPropertyChanged(); }
        }

        // Containers
        public bool ShowContainers
        {
            get => App.Config.ESP.ShowContainers;
            set { App.Config.ESP.ShowContainers = value; OnPropertyChanged(); }
        }

        public float MaxDistanceContainers
        {
            get => App.Config.ESP.MaxDistanceContainers;
            set { App.Config.ESP.MaxDistanceContainers = value; OnPropertyChanged(); }
        }

        public EspViewModel()
        {
            OpenEspCommand = new SimpleCommand(OpenEsp);
            CloseEspCommand = new SimpleCommand(CloseEsp);
            RefreshMonitorsCommand = new SimpleCommand(RefreshMonitors);
            DetectResolutionCommand = new SimpleCommand(DetectResolution);

            // Initialize monitors
            RefreshMonitors();

            // Auto-open if enabled
            if (App.Config.ESP.AutoOpen)
            {
                OpenEsp();
            }
        }

        private void RefreshMonitors()
        {
            try
            {
                var newMonitors = MonitorInfo.GetAllMonitors();
                AvailableMonitors = newMonitors;

                System.Diagnostics.Debug.WriteLine($"Found {newMonitors.Count} monitors");

                // Try to maintain selection
                var currentIndex = App.Config.ESP.SelectedMonitor;
                var monitor = newMonitors.FirstOrDefault(m => m.Index == currentIndex) 
                              ?? newMonitors.FirstOrDefault(m => m.IsPrimary) 
                              ?? newMonitors.FirstOrDefault();

                if (monitor != null)
                {
                    SelectedMonitor = monitor;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing monitors: {ex}");

                // Fallback
                AvailableMonitors = new List<MonitorInfo>
                {
                    new MonitorInfo
                    {
                        Index = 0,
                        Name = "Primary Display",
                        Width = 1920,
                        Height = 1080,
                        Left = 0,
                        Top = 0,
                        IsPrimary = true
                    }
                };
                SelectedMonitor = AvailableMonitors.FirstOrDefault();
            }
        }

        private void DetectResolution()
        {
            try
            {
                if (SelectedMonitor != null)
                {
                    App.Config.ESP.Resolution = new Size(SelectedMonitor.Width, SelectedMonitor.Height);
                    OnPropertyChanged(nameof(ResolutionWidth));
                    OnPropertyChanged(nameof(ResolutionHeight));
                    System.Diagnostics.Debug.WriteLine($"Set resolution to {SelectedMonitor.Width}x{SelectedMonitor.Height}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error detecting resolution: {ex}");
            }
        }

        private void OpenEsp()
        {
            try
            {
                if (_espWindow != null)
                {
                    System.Diagnostics.Debug.WriteLine("ESP window already open");
                    return;
                }

                var monitor = MonitorInfo.GetMonitor(App.Config.ESP.SelectedMonitor);
                if (monitor == null)
                {
                    System.Diagnostics.Debug.WriteLine("No monitor found, using primary");
                    monitor = MonitorInfo.GetPrimaryMonitor();
                }

                System.Diagnostics.Debug.WriteLine($"Opening ESP on monitor {monitor.Index}: {monitor.Width}x{monitor.Height} at ({monitor.Left}, {monitor.Top})");

                _espWindow = new EspWindow(monitor);
                _espWindow.Closed += (s, e) => 
                {
                    System.Diagnostics.Debug.WriteLine("ESP window closed");
                    _espWindow = null;
                };
                _espWindow.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening ESP: {ex}");
                System.Windows.MessageBox.Show($"Failed to open ESP window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseEsp()
        {
            _espWindow?.Close();
            _espWindow = null;
        }

        // Helper to parse color from config
        public static SKColor GetColor(string hexColor)
        {
            try
            {
                return SKColor.Parse(hexColor);
            }
            catch
            {
                return SKColors.White;
            }
        }
    }
}