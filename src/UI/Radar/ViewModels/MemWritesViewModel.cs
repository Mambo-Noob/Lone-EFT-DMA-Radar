/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace LoneEftDmaRadar.UI.Radar.ViewModels
{
    public sealed class MemWritesViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public bool Enabled
        {
            get => App.Config.MemWrites.Enabled;
            set
            {
                // Show warning when enabling
                if (value && !App.Config.MemWrites.Enabled)
                {
                    var result = MessageBox.Show(
                        "???? FINAL WARNING ????\n\n" +
                        "Memory writes DIRECTLY MODIFY GAME MEMORY and are HIGHLY DETECTABLE.\n\n" +
                        "This includes features like:\n" +
                        "  ? No Recoil\n" +
                        "  ? Infinite Stamina\n" +
                        "  ? Other memory modifications\n\n" +
                        "Using memory writes significantly INCREASES your risk of detection and permanent account ban.\n\n" +
                        "?? USE ONLY ON ACCOUNTS YOU ARE WILLING TO LOSE! ??\n\n" +
                        "ARE YOU ABSOLUTELY SURE YOU WANT TO ENABLE MEMORY WRITES?",
                        "?? CRITICAL WARNING - Memory Writes ??",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Stop,
                        MessageBoxResult.No);

                    if (result != MessageBoxResult.Yes)
                    {
                        OnPropertyChanged(); // Refresh UI to uncheck
                        return;
                    }
                }

                App.Config.MemWrites.Enabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));

                System.Diagnostics.Debug.WriteLine($"[MemWrites] Master switch {(value ? "ENABLED" : "DISABLED")}");
            }
        }

        public string StatusText => Enabled ? "?? ENABLED - HIGH RISK" : "Disabled - Safe";
        public string StatusColor => Enabled ? "Red" : "Green";

        // No Recoil
        public bool NoRecoilEnabled
        {
            get => App.Config.MemWrites.NoRecoilEnabled;
            set
            {
                App.Config.MemWrites.NoRecoilEnabled = value;
                OnPropertyChanged();
            }
        }

        public float NoRecoilAmount
        {
            get => App.Config.MemWrites.NoRecoilAmount;
            set
            {
                App.Config.MemWrites.NoRecoilAmount = value;
                OnPropertyChanged();
            }
        }

        public float NoSwayAmount
        {
            get => App.Config.MemWrites.NoSwayAmount;
            set
            {
                App.Config.MemWrites.NoSwayAmount = value;
                OnPropertyChanged();
            }
        }
        public bool MemoryAimEnabled
        {
            get => App.Config.MemWrites.MemoryAimEnabled;
            set
            {
                App.Config.MemWrites.MemoryAimEnabled = value;
                OnPropertyChanged();
            }
        }
        // Infinite Stamina
        public bool InfiniteStaminaEnabled
        {
            get => App.Config.MemWrites.InfiniteStaminaEnabled;
            set
            {
                App.Config.MemWrites.InfiniteStaminaEnabled = value;
                OnPropertyChanged();
            }
        }
    }
}