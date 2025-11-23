/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.Mono.Collections;
using System;
using System.Diagnostics;
using System.Linq;

namespace LoneEftDmaRadar.Tarkov.Features.MemWrites
{
    /// <summary>
    /// Provides infinite stamina and oxygen by refilling and bypassing fatigue states.
    /// Structured to match the old InfStamina (ScatterWriteHandle) version as closely as possible.
    /// </summary>
    public sealed class InfiniteStamina : MemWriteFeature<InfiniteStamina>
    {
        private bool _lastEnabledState;
        private bool _bypassApplied;
        private ulong _cachedPhysical;
        private ulong _cachedStaminaObj;
        private ulong _cachedOxygenObj;

        private const byte INF_STAM_SOURCE_STATE_NAME = (byte)Enums.EPlayerState.Sprint;
        private const byte INF_STAM_TARGET_STATE_NAME = (byte)Enums.EPlayerState.Transition;
        private const float MAX_STAMINA = 100f;
        private const float MAX_OXYGEN = 350f;
        private const float REFILL_THRESHOLD = 0.33f; // refill when below 33%

        public override bool Enabled
        {
            get => App.Config.MemWrites.InfiniteStaminaEnabled;
            set => App.Config.MemWrites.InfiniteStaminaEnabled = value;
        }

        // Old version also ran at ~1s cadence
        protected override TimeSpan Delay => TimeSpan.FromSeconds(1);

        public override void TryApply(LocalPlayer localPlayer)
        {
            try
            {
                if (localPlayer == null)
                    return;

                var stateChanged = Enabled != _lastEnabledState;

                if (!Enabled)
                {
                    if (stateChanged)
                    {
                        _lastEnabledState = false;
                        //Debug.WriteLine("[InfiniteStamina] Disabled");
                    }
                    return;
                }

                // Apply fatigue bypass once per raid, same as old version
                if (!_bypassApplied)
                    ApplyFatigueBypass(localPlayer);

                // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
                // "Scatter-like" batching ¨C mirror old pattern:
                // build a batch of writes, then execute once.
                // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
                var batch = new StaminaWriteBatch();
                ApplyInfiniteStamina(localPlayer, batch);

                if (batch.HasWrites)
                    batch.Execute();

                if (stateChanged)
                {
                    _lastEnabledState = true;
                    //Debug.WriteLine("[InfiniteStamina] Enabled");
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"[InfiniteStamina] Error in TryApply: {ex}");
                ClearCache();
            }
        }

        // ----------------- CORE LOGIC (matches old InfStamina) -----------------

        private void ApplyInfiniteStamina(LocalPlayer localPlayer, StaminaWriteBatch writes)
        {
            var (staminaObj, oxygenObj) = GetStaminaObjects(localPlayer);

            if (!MemDMA.IsValidVirtualAddress(staminaObj) ||
                !MemDMA.IsValidVirtualAddress(oxygenObj))
            {
                return;
            }

            var currentStamina = Memory.ReadValue<float>(staminaObj + Offsets.PhysicalValue.Current, false);
            var currentOxygen  = Memory.ReadValue<float>(oxygenObj + Offsets.PhysicalValue.Current, false);

            if (!ValidateStaminaValue(currentStamina) ||
                !ValidateOxygenValue(currentOxygen))
            {
                return;
            }

            // Mirror old logic: only queue writes when under threshold
            if (currentStamina < MAX_STAMINA * REFILL_THRESHOLD)
            {
                writes.Add(staminaObj + Offsets.PhysicalValue.Current, MAX_STAMINA);

                // Optional: if you want logs like the old ScatterWriteHandle callbacks:
                // Debug.WriteLine($"[InfiniteStamina] Stamina refilled: {currentStamina:F1} -> {MAX_STAMINA:F1}");
            }

            if (currentOxygen < MAX_OXYGEN * REFILL_THRESHOLD)
            {
                writes.Add(oxygenObj + Offsets.PhysicalValue.Current, MAX_OXYGEN);

                // Optional logging:
                // Debug.WriteLine($"[InfiniteStamina] Oxygen refilled: {currentOxygen:F1} -> {MAX_OXYGEN:F1}");
            }
        }

        private (ulong staminaObj, ulong oxygenObj) GetStaminaObjects(LocalPlayer localPlayer)
        {
            var physical = GetPhysical(localPlayer);
            if (!MemDMA.IsValidVirtualAddress(physical))
                return (0x0, 0x0);

            var staminaObj = GetStaminaObject(physical);
            var oxygenObj  = GetOxygenObject(physical);

            return (staminaObj, oxygenObj);
        }

        private ulong GetPhysical(LocalPlayer localPlayer)
        {
            if (MemDMA.IsValidVirtualAddress(_cachedPhysical))
                return _cachedPhysical;

            try
            {
                var physical = Memory.ReadPtr(localPlayer + Offsets.Player.Physical, false);
                if (MemDMA.IsValidVirtualAddress(physical))
                    _cachedPhysical = physical;

                return physical;
            }
            catch
            {
                return 0;
            }
        }

        private ulong GetStaminaObject(ulong physical)
        {
            if (MemDMA.IsValidVirtualAddress(_cachedStaminaObj))
                return _cachedStaminaObj;

            try
            {
                var staminaObj = Memory.ReadPtr(physical + Offsets.Physical.Stamina, false);
                if (MemDMA.IsValidVirtualAddress(staminaObj))
                    _cachedStaminaObj = staminaObj;

                return staminaObj;
            }
            catch
            {
                return 0;
            }
        }

        private ulong GetOxygenObject(ulong physical)
        {
            if (MemDMA.IsValidVirtualAddress(_cachedOxygenObj))
                return _cachedOxygenObj;

            try
            {
                var oxygenObj = Memory.ReadPtr(physical + Offsets.Physical.Oxygen, false);
                if (MemDMA.IsValidVirtualAddress(oxygenObj))
                    _cachedOxygenObj = oxygenObj;

                return oxygenObj;
            }
            catch
            {
                return 0;
            }
        }

        private void ApplyFatigueBypass(LocalPlayer localPlayer)
        {
            try
            {
                var originalStateContainer = GetOriginalStateContainer(localPlayer);
                GetStates(localPlayer, out var originalState, out var patchState);

                if (originalState == 0x0)
                {
                    // Already patched in a previous run / previous raid
                    _bypassApplied = true;
                    return;
                }

                if (!MemDMA.IsValidVirtualAddress(patchState))
                    return;

                var targetHash = Memory.ReadValue<int>(patchState + Offsets.MovementState.AnimatorStateHash, false);

                Memory.WriteValue(originalStateContainer + Offsets.PlayerStateContainer.StateFullNameHash, targetHash);
                Memory.WriteValue(originalState + Offsets.MovementState.AnimatorStateHash, targetHash);
                Memory.WriteValue(originalState + Offsets.MovementState.Name, INF_STAM_TARGET_STATE_NAME);

                _bypassApplied = true;
            }
            catch
            {
                // Swallow and retry next tick; matches old behaviour
            }
        }

        private static void GetStates(LocalPlayer localPlayer, out ulong originalState, out ulong patchState)
        {
            try
            {
                using var states = GetStatesDict(localPlayer);

                originalState = states
                    .FirstOrDefault(x =>
                        Memory.ReadValue<byte>(x.Value + Offsets.MovementState.Name, false) ==
                        INF_STAM_SOURCE_STATE_NAME)
                    .Value;

                if (originalState == 0x0)
                {
                    patchState = 0x0;
                    return;
                }

                patchState = states
                    .First(x =>
                        Memory.ReadValue<byte>(x.Value + Offsets.MovementState.Name, false) ==
                        INF_STAM_TARGET_STATE_NAME)
                    .Value;
            }
            catch
            {
                originalState = 0;
                patchState = 0;
            }
        }

        private static MonoDictionary<byte, ulong> GetStatesDict(LocalPlayer localPlayer)
        {
            var statesPtr = Memory.ReadPtr(localPlayer.MovementContext + Offsets.MovementContext._states, false);
            return MonoDictionary<byte, ulong>.Create(statesPtr, false);
        }

        private static ulong GetOriginalStateContainer(LocalPlayer localPlayer)
        {
            var movementStatesPtr = Memory.ReadPtr(localPlayer.MovementContext + Offsets.MovementContext._movementStates, false);
            using var movementStates = MonoArray<ulong>.Create(movementStatesPtr, false);

            return movementStates.First(x =>
                Memory.ReadValue<byte>(x + Offsets.PlayerStateContainer.Name, false) ==
                INF_STAM_SOURCE_STATE_NAME);
        }

        private static bool ValidateStaminaValue(float stamina)
        {
            return stamina >= 0f && stamina <= 500f;
        }

        private static bool ValidateOxygenValue(float oxygen)
        {
            return oxygen >= 0f && oxygen <= 1000f;
        }

        private void ClearCache()
        {
            _cachedPhysical   = default;
            _cachedStaminaObj = default;
            _cachedOxygenObj  = default;
        }

        public override void OnRaidStart()
        {
            _lastEnabledState = default;
            _bypassApplied    = default;
            ClearCache();
        }

        // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        // Minimal "scatter-like" batch for stamina writes only.
        // This mirrors the old ScatterWriteHandle usage:
        //   - queue entries via Add(...)
        //   - apply them in one Execute() call.
        // If you want real DMA scatter, swap this class to use
        // Memory.CreateScatter(...) + VmmScatter writes instead.
        // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        private sealed class StaminaWriteBatch
        {
            private struct Entry
            {
                public ulong Address;
                public float Value;
            }

            private Entry[] _entries;
            private int _count;

            public StaminaWriteBatch()
            {
                _entries = new Entry[4]; // tiny fixed capacity is enough
                _count   = 0;
            }

            public bool HasWrites => _count > 0;

            public void Add(ulong addr, float value)
            {
                if (_count == _entries.Length)
                {
                    Array.Resize(ref _entries, _entries.Length * 2);
                }

                _entries[_count++] = new Entry
                {
                    Address = addr,
                    Value   = value
                };
            }

            public void Execute()
            {
                for (int i = 0; i < _count; i++)
                {
                    var e = _entries[i];
                    Memory.WriteValue(e.Address, e.Value);
                }
            }
        }
    }
}
