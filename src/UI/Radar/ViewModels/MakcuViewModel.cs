/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LoneEftDmaRadar.DMA;
using VmmSharpEx;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public sealed class MakcuViewModel : INotifyPropertyChanged
    {
        private bool _isConnected;
        private string _deviceVersion = "Not Connected";
        private List<Device.SerialDeviceInfo> _availableDevices;
        private Device.SerialDeviceInfo _selectedDevice;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand RefreshDevicesCommand { get; }
        public ICommand TestMoveCommand { get; }

        // Connection Status
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionStatus));
            }
        }

        public string ConnectionStatus => IsConnected ? "Connected" : "Disconnected";

        public string DeviceVersion
        {
            get => _deviceVersion;
            set
            {
                _deviceVersion = value;
                OnPropertyChanged();
            }
        }

        public List<Device.SerialDeviceInfo> AvailableDevices
        {
            get => _availableDevices;
            set
            {
                _availableDevices = value;
                OnPropertyChanged();
            }
        }

        public Device.SerialDeviceInfo SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                _selectedDevice = value;
                OnPropertyChanged();
            }
        }

        // Settings
        public bool AutoConnect
        {
            get => App.Config.Makcu.AutoConnect;
            set { App.Config.Makcu.AutoConnect = value; OnPropertyChanged(); }
        }

        public bool Enabled
        {
            get => App.Config.Makcu.Enabled;
            set { App.Config.Makcu.Enabled = value; OnPropertyChanged(); }
        }

        public List<Bones> AvailableBones { get; } = new List<Bones>
        {
            Bones.HumanHead,
            Bones.HumanNeck,
            Bones.HumanSpine3,
            Bones.HumanSpine2,
            Bones.HumanPelvis
        };

        public Bones TargetBone
        {
            get => App.Config.Makcu.TargetBone;
            set { App.Config.Makcu.TargetBone = value; OnPropertyChanged(); }
        }

        public bool MemWritesEnabled
        {
            get => App.Config.MemWrites.Enabled;
            set 
            { 
                // Show warning when enabling
                if (value && !App.Config.MemWrites.Enabled)
                {
                    var result = System.Windows.MessageBox.Show(
                        "?? WARNING ??\n\n" +
                        "Memory writes directly modify game memory and are HIGHLY DETECTABLE.\n\n" +
                        "This includes features like:\n" +
                        "  ? No Recoil\n" +
                        "  ? No Sway\n" +
                        "  ? Other memory modifications\n\n" +
                        "Using memory writes significantly increases your risk of detection and account ban.\n\n" +
                        "ARE YOU SURE YOU WANT TO ENABLE MEMORY WRITES?",
                        "?? CRITICAL WARNING - Memory Writes ??",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning,
                        System.Windows.MessageBoxResult.No);
        
                    if (result != System.Windows.MessageBoxResult.Yes)
                    {
                        OnPropertyChanged(); // Refresh UI to uncheck
                        return;
                    }
                }
        
                App.Config.MemWrites.Enabled = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMemWritesEnabled)); // Update dependent UI
                
                // Log the change
                System.Diagnostics.Debug.WriteLine($"[Makcu] MemWrites {(value ? "ENABLED" : "DISABLED")}");
            }
        }
        
        // Helper property for UI binding
        public bool IsMemWritesEnabled => App.Config.MemWrites.Enabled;

        public float FOV
        {
            get => App.Config.Makcu.FOV;
            set { App.Config.Makcu.FOV = value; OnPropertyChanged(); }
        }

        public float MaxDistance
        {
            get => App.Config.Makcu.MaxDistance;
            set { App.Config.Makcu.MaxDistance = value; OnPropertyChanged(); }
        }

        public int TargetingMode
        {
            get => (int)App.Config.Makcu.Targeting;
            set { App.Config.Makcu.Targeting = (MakcuConfig.TargetingMode)value; OnPropertyChanged(); }
        }

        public bool ShowDebug
        {
            get => App.Config.Makcu.ShowDebug;
            set { App.Config.Makcu.ShowDebug = value; OnPropertyChanged(); }
        }

        public bool EnablePrediction
        {
            get => App.Config.Makcu.EnablePrediction;
            set { App.Config.Makcu.EnablePrediction = value; OnPropertyChanged(); }
        }

        public bool NoRecoilEnabled
        {
            get => App.Config.MemWrites.NoRecoilEnabled;
            set { App.Config.MemWrites.NoRecoilEnabled = value; OnPropertyChanged(); }
        }

        public float NoRecoilAmount
        {
            get => App.Config.MemWrites.NoRecoilAmount;
            set { App.Config.MemWrites.NoRecoilAmount = value; OnPropertyChanged(); }
        }

        public float NoSwayAmount
        {
            get => App.Config.MemWrites.NoSwayAmount;
            set { App.Config.MemWrites.NoSwayAmount = value; OnPropertyChanged(); }
        }        

        /// <summary>
        /// True while Makcu aimbot is actively engaged (aim-key/ hotkey).
        /// </summary>
        public bool IsEngaged
        {
            get => MemDMA.MakcuAimbot?.IsEngaged ?? false;
            set
            {
                if (MemDMA.MakcuAimbot != null)
                {
                    MemDMA.MakcuAimbot.IsEngaged = value;
                    OnPropertyChanged();
                }
            }
        }

        // Target Filters
        public bool TargetPMC
        {
            get => App.Config.Makcu.TargetPMC;
            set { App.Config.Makcu.TargetPMC = value; OnPropertyChanged(); }
        }

        public bool TargetPlayerScav
        {
            get => App.Config.Makcu.TargetPlayerScav;
            set { App.Config.Makcu.TargetPlayerScav = value; OnPropertyChanged(); }
        }

        public bool TargetAIScav
        {
            get => App.Config.Makcu.TargetAIScav;
            set { App.Config.Makcu.TargetAIScav = value; OnPropertyChanged(); }
        }

        public bool TargetBoss
        {
            get => App.Config.Makcu.TargetBoss;
            set { App.Config.Makcu.TargetBoss = value; OnPropertyChanged(); }
        }

        public bool TargetRaider
        {
            get => App.Config.Makcu.TargetRaider;
            set { App.Config.Makcu.TargetRaider = value; OnPropertyChanged(); }
        }

        private bool _isTesting;
        public bool IsTesting
        {
            get => _isTesting;
            set
            {
                _isTesting = value;
                OnPropertyChanged();
            }
        }

        public MakcuViewModel()
        {
            ConnectCommand = new SimpleCommand(Connect);
            DisconnectCommand = new SimpleCommand(Disconnect);
            RefreshDevicesCommand = new SimpleCommand(RefreshDevices);
            TestMoveCommand = new SimpleCommand(TestMove);

            RefreshDevices();

            // Auto-connect if enabled (Makcu VID/PID-based)
            if (App.Config.Makcu.AutoConnect)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(1000);
                    if (Device.AutoConnectMakcu())
                    {
                        UpdateConnectionStatus();
                    }
                });
            }
        }

        private void RefreshDevices()
        {
            try
            {
                AvailableDevices = Device.EnumerateSerialDevices();
                
                // Try to select last used device
                if (!string.IsNullOrEmpty(App.Config.Makcu.LastComPort))
                {
                    SelectedDevice = AvailableDevices.FirstOrDefault(d => d.Port == App.Config.Makcu.LastComPort)
                                     ?? AvailableDevices.FirstOrDefault();
                }
                else
                {
                    SelectedDevice = AvailableDevices.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing devices: {ex}");
            }
        }

        private void Connect()
        {
            try
            {
                if (SelectedDevice == null)
                {
                    System.Windows.MessageBox.Show(
                        "Please select a device first.",
                        "Makcu",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // Auto device-type connect:
                // 1) Try Makcu (4M baud + change_cmd + km.MAKCU)
                // 2) Fall back to generic km.* device (KMBox/CH340 at 115200)
                bool ok = Device.ConnectAuto(SelectedDevice.Port);

                if (ok && Device.connected)
                {
                    App.Config.Makcu.LastComPort = SelectedDevice.Port;
                    UpdateConnectionStatus();
                }
                else
                {
                    UpdateConnectionStatus(); // make sure UI shows disconnected
                    System.Windows.MessageBox.Show(
                        "Failed to connect to device.\n\n" +
                        "If you are using a KMBox / CH340-based device, make sure it is in km.* mode.",
                        "Makcu / KM Device",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Connection error: {ex.Message}",
                    "Makcu",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void Disconnect()
        {
            try
            {
                Device.disconnect();
                UpdateConnectionStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Disconnect error: {ex}");
            }
        }

        private void UpdateConnectionStatus()
        {
            IsConnected = Device.connected;

            if (Device.connected)
            {
                var kind = Device.DeviceKind.ToString();
                if (string.IsNullOrWhiteSpace(Device.version))
                {
                    DeviceVersion = $"Connected ({kind})";
                }
                else
                {
                    DeviceVersion = $"{Device.version} ({kind})";
                }
            }
            else
            {
                DeviceVersion = "Not Connected";
            }
        }

        private async void TestMove()
        {
            if (!Device.connected)
            {
                System.Windows.MessageBox.Show(
                    "Please connect to a device first.",
                    "Test Move",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (IsTesting)
            {
                System.Windows.MessageBox.Show(
                    "Test already in progress!",
                    "Test Move",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            IsTesting = true;

            try
            {
                // Run test pattern in background
                await System.Threading.Tasks.Task.Run(() =>
                {
                    // Test pattern: small square
                    int step = 50;  // pixels
                    int delay = 200; // ms

                    System.Diagnostics.Debug.WriteLine("[MakcuTest] Starting movement test");

                    // Right
                    Device.move(step, 0);
                    System.Threading.Thread.Sleep(delay);

                    // Down
                    Device.move(0, step);
                    System.Threading.Thread.Sleep(delay);

                    // Left
                    Device.move(-step, 0);
                    System.Threading.Thread.Sleep(delay);

                    // Up
                    Device.move(0, -step);
                    System.Threading.Thread.Sleep(delay);

                    // Small circle pattern (8 points)
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * Math.PI / 4; // 45 degree increments
                        int x = (int)(Math.Cos(angle) * 30);
                        int y = (int)(Math.Sin(angle) * 30);
                        Device.Move(x, y);
                        System.Threading.Thread.Sleep(100);
                    }

                    System.Diagnostics.Debug.WriteLine("[MakcuTest] Movement test complete");
                });

                System.Windows.MessageBox.Show(
                    "Test complete! Your mouse should have moved in a square pattern, then a circle.\n\n" +
                    "If the mouse didn't move, check:\n" +
                    "? Device connection\n" +
                    "? COM port selection\n" +
                    "? Device firmware / mode",
                    "Test Move",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Test failed: {ex.Message}",
                    "Test Move",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"[MakcuTest] Error: {ex}");
            }
            finally
            {
                IsTesting = false;
            }
        }
    }
}