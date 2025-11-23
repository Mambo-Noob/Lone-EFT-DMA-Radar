/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
 * MIT License
 */

using System;
using System.Diagnostics;
using System.Linq;
using LoneEftDmaRadar.Misc.Workers;
using LoneEftDmaRadar.UI.Hotkeys;
using LoneEftDmaRadar.UI.Misc;
using VmmSharpEx;
using VmmSharpEx.Extensions.Input;

namespace LoneEftDmaRadar.DMA
{
    /// <summary>
    /// Central input poller for hotkeys.
    /// - Primary source: VmmInputManager (Win32 keyboard/mouse via MemProcFS)
    /// - Failsafe / secondary source: Makcu device (mouse buttons)
    /// </summary>
    public sealed class InputManager
    {
        private readonly VmmInputManager _input;   // may be null if Win32 backend failed
        private readonly WorkerThread _thread;

        /// <summary>
        /// True if VmmInputManager (Win32) backend is available.
        /// </summary>
        public bool IsWin32BackendAvailable => _input is not null;

        public InputManager(Vmm vmm)
        {
            try
            {
                _input = new VmmInputManager(vmm);
                Debug.WriteLine("[InputManager] VmmInputManager initialized.");
            }
            catch (Exception ex)
            {
                // Do NOT throw; this is our failsafe.
                _input = null;
                Debug.WriteLine($"[InputManager] Failed to initialize VmmInputManager (Win32 backend). " +
                                $"Hotkeys will use Makcu-only fallback if available. {ex}");
            }

            _thread = new WorkerThread
            {
                Name = nameof(InputManager),
                SleepDuration = TimeSpan.FromMilliseconds(12),
                SleepMode = WorkerThreadSleepMode.DynamicSleep
            };
            _thread.PerformWork += InputManager_PerformWork;
            _thread.Start();
        }

        private void InputManager_PerformWork(object sender, WorkerThreadArgs e)
        {
            var hotkeys = HotkeyManagerViewModel.Hotkeys.AsEnumerable();
            if (!hotkeys.Any())
                return;

            bool haveWin32 = _input is not null;

            // Update Win32 state if backend is present.
            if (haveWin32)
            {
                try
                {
                    _input.UpdateKeys();
                }
                catch (Exception ex)
                {
                    // If Win32 backend dies mid-run, we just fall back to Makcu.
                    Debug.WriteLine($"[InputManager] VmmInputManager.UpdateKeys failed: {ex}");
                    // We keep _input non-null but effectively ignore it after this tick.
                    haveWin32 = false;
                }
            }

            foreach (var kvp in hotkeys)
            {
                var vk    = kvp.Key;
                var action = kvp.Value;

                bool isDownWin32 = false;
                if (haveWin32)
                {
                    try
                    {
                        isDownWin32 = _input.IsKeyDown(vk);
                    }
                    catch
                    {
                        // treat as not pressed if backend misbehaves
                        isDownWin32 = false;
                    }
                }

                bool isDownMakcu = IsMakcuKeyDown(vk);

                // FINAL state: key is considered down if EITHER backend reports it.
                bool isKeyDown = isDownWin32 || isDownMakcu;

                action.Execute(isKeyDown);
            }
        }

        /// <summary>
        /// Maps some Win32 virtual keys (mouse buttons) to Makcu buttons
        /// and returns whether that logical key is down according to Makcu.
        /// 
        /// This gives us:
        /// - LBUTTON   → MakcuMouseButton.Left
        /// - RBUTTON   → MakcuMouseButton.Right
        /// - MBUTTON   → MakcuMouseButton.Middle
        /// - XBUTTON1  → MakcuMouseButton.mouse4
        /// - XBUTTON2  → MakcuMouseButton.mouse5
        /// 
        /// So users can bind hotkeys to those keys in the hotkey UI ಮತ್ತು
        /// they will work even when VmmInputManager is unavailable, as long
        /// as the Makcu device is connected.
        /// </summary>
        private static bool IsMakcuKeyDown(Win32VirtualKey vk)
        {
            if (!Device.connected || Device.bState == null)
                return false;

            MakcuMouseButton button;

            switch (vk)
            {
                case Win32VirtualKey.LBUTTON:
                    button = MakcuMouseButton.Left;
                    break;

                case Win32VirtualKey.RBUTTON:
                    button = MakcuMouseButton.Right;
                    break;

                case Win32VirtualKey.MBUTTON:
                    button = MakcuMouseButton.Middle;
                    break;

                case Win32VirtualKey.XBUTTON1:
                    button = MakcuMouseButton.mouse4;
                    break;

                case Win32VirtualKey.XBUTTON2:
                    button = MakcuMouseButton.mouse5;
                    break;

                default:
                    // any non-mouse key is not handled by Makcu
                    return false;
            }

            return Device.button_pressed(button);
        }
    }
}
