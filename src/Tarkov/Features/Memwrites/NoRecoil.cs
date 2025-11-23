using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using System;
using System.Diagnostics;
using System.Numerics;

namespace LoneEftDmaRadar.Tarkov.Features.MemWrites
{
    /// <summary>
    /// Removes weapon recoil and sway by modifying effector intensities.
    /// </summary>
    public sealed class NoRecoil : MemWriteFeature<NoRecoil>
    {
        private bool _lastEnabledState;
        private ulong _cachedBreathEffector;
        private ulong _cachedShotEffector;
        private ulong _cachedNewShotRecoil;

        // Used to avoid unnecessary writes every tick
        private float _lastRecoilAmount = 1.0f;
        private float _lastSwayAmount   = 1.0f;

        // Optional: track "session" so we can invalidate cache when weapon changes
        private ulong _lastPwaPtr;

        [Flags]
        public enum EProceduralAnimationMask
        {
            Breathing      = 1,
            Walking        = 2,
            MotionReaction = 4,
            ForceReaction  = 8,
            Shooting       = 16,
            DrawDown       = 32,
            Aiming         = 64,
            HandShake      = 128,
        }

        private const int ORIGINAL_PWA_MASK =
            (int)(EProceduralAnimationMask.MotionReaction |
                  EProceduralAnimationMask.ForceReaction |
                  EProceduralAnimationMask.Shooting |
                  EProceduralAnimationMask.DrawDown |
                  EProceduralAnimationMask.Aiming |
                  EProceduralAnimationMask.Breathing);

        public override bool Enabled
        {
            get => App.Config.MemWrites.NoRecoilEnabled;
            set => App.Config.MemWrites.NoRecoilEnabled = value;
        }

        protected override TimeSpan Delay => TimeSpan.FromMilliseconds(50);

        public override void TryApply(LocalPlayer localPlayer)
        {
            //Debug.WriteLine($"[NoRecoil] TryApply called - Enabled: {Enabled}");

            try
            {
                if (localPlayer == null)
                {
                    //Debug.WriteLine("[NoRecoil] LocalPlayer is null");
                    return;
                }

                var stateChanged = Enabled != _lastEnabledState;
                //Debug.WriteLine($"[NoRecoil] State changed: {stateChanged}, LastState: {_lastEnabledState}, CurrentState: {Enabled}");

                if (!Enabled)
                {
                    if (stateChanged)
                    {
                        //Debug.WriteLine("[NoRecoil] Disabling - calling ResetNoRecoil");
                        ResetNoRecoil(localPlayer);
                        _lastEnabledState = false;
                        //Debug.WriteLine("[NoRecoil] Disabled");
                    }
                    return;
                }

                //Debug.WriteLine($"[NoRecoil] LocalPlayer.PWA: 0x{localPlayer.PWA:X}");

                // Early-out if PWA is not valid (not in raid / no weapon)
                if (!MemDMA.IsValidVirtualAddress(localPlayer.PWA))
                {
                    //Debug.WriteLine("[NoRecoil] PWA is invalid - no weapon equipped?");
                    if (stateChanged)
                        //Debug.WriteLine("[NoRecoil] Enabled but PWA invalid ¨C waiting for raid/weapon");
                    ClearCache();
                    _lastEnabledState = Enabled;
                    return;
                }

                //Debug.WriteLine("[NoRecoil] PWA is valid, calling ApplyNoRecoil");
                ApplyNoRecoil(localPlayer);

                if (stateChanged)
                {
                    _lastEnabledState = true;
                    //Debug.WriteLine("[NoRecoil] Enabled (state changed)");
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"[NoRecoil] Error in TryApply: {ex}");
                //Debug.WriteLine($"[NoRecoil] Stack trace: {ex.StackTrace}");
                ClearCache();
            }
        }

private void ApplyNoRecoil(LocalPlayer localPlayer)
{
    //Debug.WriteLine("[NoRecoil] ApplyNoRecoil called");

    try
    {
        // For testing - hardcoded values
        float recoilAmount = 0.1f;
        float swayAmount   = 0.1f;

        //Debug.WriteLine($"[NoRecoil] Target - RecoilAmount: {recoilAmount:F3}, SwayAmount: {swayAmount:F3}");

        var (breathEffector, shotEffector, newShotRecoil) = GetEffectorPointers(localPlayer);

        //Debug.WriteLine($"[NoRecoil] Pointers - Breath: 0x{breathEffector:X}, Shot: 0x{shotEffector:X}, NewShotRecoil: 0x{newShotRecoil:X}");

        if (!ValidatePointers(breathEffector, shotEffector, newShotRecoil))
        {
            //Debug.WriteLine("[NoRecoil] Invalid effector pointers, clearing cache");
            ClearCache();
            return;
        }

        //Debug.WriteLine("[NoRecoil] Pointers valid, reading current values...");

        // ? Read CURRENT values from game memory
        float currentBreath = Memory.ReadValue<float>(
            breathEffector + Offsets.BreathEffector.Intensity, false);

        //Debug.WriteLine($"[NoRecoil] Current breath intensity: {currentBreath:F3}");

        if (currentBreath < 0f || currentBreath > 5f)
        {
            //Debug.WriteLine($"[NoRecoil] Invalid breath value: {currentBreath}, clearing cache");
            ClearCache();
            return;
        }

        Vector3 currentRecoil = Memory.ReadValue<Vector3>(
            newShotRecoil + Offsets.NewShotRecoil.IntensitySeparateFactors, false);

        //Debug.WriteLine($"[NoRecoil] Current recoil vector: ({currentRecoil.X:F3}, {currentRecoil.Y:F3}, {currentRecoil.Z:F3})");

        int currentMask = Memory.ReadValue<int>(
            localPlayer.PWA + Offsets.ProceduralWeaponAnimation.Mask, false);

        //Debug.WriteLine($"[NoRecoil] Current PWA mask: {currentMask}");

        // ? Compare CURRENT vs TARGET (not target vs last target!)
        if (Math.Abs(currentBreath - swayAmount) > 0.001f)
        {
            //Debug.WriteLine($"[NoRecoil] Writing sway: {currentBreath:F3} -> {swayAmount:F3}");
            Memory.WriteValue(
                breathEffector + Offsets.BreathEffector.Intensity,
                swayAmount);
        }
        else
        {
            //Debug.WriteLine($"[NoRecoil] Sway already correct: {currentBreath:F3}");
        }

        // ? Compare CURRENT recoil vs TARGET
        var recoilVec = new Vector3(recoilAmount, recoilAmount, recoilAmount);
        if (Vector3.Distance(currentRecoil, recoilVec) > 0.001f)
        {
            //Debug.WriteLine($"[NoRecoil] Writing recoil: {currentRecoil} -> {recoilVec}");
            Memory.WriteValue(
                newShotRecoil + Offsets.NewShotRecoil.IntensitySeparateFactors,
                recoilVec);
        }
        else
        {
            //Debug.WriteLine($"[NoRecoil] Recoil already correct: {currentRecoil}");
        }

        // Mask management
        int targetMask;
        if (recoilAmount <= 0.15f && swayAmount <= 0.15f)
        {
            targetMask = (int)EProceduralAnimationMask.Shooting;
        }
        else
        {
            targetMask = ORIGINAL_PWA_MASK;
        }

        //Debug.WriteLine($"[NoRecoil] Target mask: {targetMask} (current: {currentMask})");

        if (currentMask != targetMask)
        {
            //Debug.WriteLine($"[NoRecoil] Writing mask: {currentMask} -> {targetMask}");
            Memory.WriteValue(
                localPlayer.PWA + Offsets.ProceduralWeaponAnimation.Mask,
                targetMask);
        }
        else
        {
            //Debug.WriteLine($"[NoRecoil] Mask already correct: {currentMask}");
        }

        _lastRecoilAmount = recoilAmount;
        _lastSwayAmount   = swayAmount;

        //Debug.WriteLine("[NoRecoil] ApplyNoRecoil completed successfully");
    }
    catch (Exception ex)
    {
        //Debug.WriteLine($"[NoRecoil] Error in ApplyNoRecoil: {ex}");
        //Debug.WriteLine($"[NoRecoil] Stack trace: {ex.StackTrace}");
        throw;
    }
}

        private void ResetNoRecoil(LocalPlayer localPlayer)
        {
            //Debug.WriteLine("[NoRecoil] ResetNoRecoil called");

            try
            {
                var (breathEffector, shotEffector, newShotRecoil) = GetEffectorPointers(localPlayer);

                //Debug.WriteLine($"[NoRecoil] Reset pointers - Breath: 0x{breathEffector:X}, Shot: 0x{shotEffector:X}, NewShotRecoil: 0x{newShotRecoil:X}");

                if (ValidatePointers(breathEffector, shotEffector, newShotRecoil))
                {
                    //Debug.WriteLine("[NoRecoil] Pointers valid, resetting to defaults...");

                    Memory.WriteValue(
                        breathEffector + Offsets.BreathEffector.Intensity,
                        1.0f);

                    Memory.WriteValue(
                        newShotRecoil + Offsets.NewShotRecoil.IntensitySeparateFactors,
                        new Vector3(1f, 1f, 1f));

                    Memory.WriteValue(
                        localPlayer.PWA + Offsets.ProceduralWeaponAnimation.Mask,
                        ORIGINAL_PWA_MASK);

                    //Debug.WriteLine("[NoRecoil] Reset to defaults");
                }
                else
                {
                    //Debug.WriteLine("[NoRecoil] Reset failed - invalid pointers");
                }

                _lastRecoilAmount = 1.0f;
                _lastSwayAmount   = 1.0f;
                ClearCache();
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"[NoRecoil] Reset error: {ex}");
                //Debug.WriteLine($"[NoRecoil] Stack trace: {ex.StackTrace}");
            }
        }

        private (ulong breathEffector, ulong shotEffector, ulong newShotRecoil) GetEffectorPointers(LocalPlayer localPlayer)
        {
            var pwa = localPlayer.PWA;

            //Debug.WriteLine($"[NoRecoil] GetEffectorPointers - PWA: 0x{pwa:X}, LastPWA: 0x{_lastPwaPtr:X}");

            // If PWA changed (weapon swap, death, etc.), drop cache
            if (pwa != _lastPwaPtr)
            {
                //Debug.WriteLine("[NoRecoil] PWA changed, clearing cache");
                ClearCache();
                _lastPwaPtr = pwa;
            }

            // Return cached if valid
            if (MemDMA.IsValidVirtualAddress(_cachedBreathEffector) &&
                MemDMA.IsValidVirtualAddress(_cachedShotEffector) &&
                MemDMA.IsValidVirtualAddress(_cachedNewShotRecoil))
            {
                //Debug.WriteLine("[NoRecoil] Using cached pointers");
                return (_cachedBreathEffector, _cachedShotEffector, _cachedNewShotRecoil);
            }

            //Debug.WriteLine("[NoRecoil] Cache invalid, reading from memory...");

            if (!MemDMA.IsValidVirtualAddress(pwa))
            {
                //Debug.WriteLine("[NoRecoil] PWA invalid, returning zeros");
                return (0, 0, 0);
            }

            var breathEffector = Memory.ReadPtr(pwa + Offsets.ProceduralWeaponAnimation.Breath, false);
            var shotEffector   = Memory.ReadPtr(pwa + Offsets.ProceduralWeaponAnimation.Shootingg, false);

            //Debug.WriteLine($"[NoRecoil] Read - BreathEffector: 0x{breathEffector:X}, ShotEffector: 0x{shotEffector:X}");

            if (!MemDMA.IsValidVirtualAddress(breathEffector) ||
                !MemDMA.IsValidVirtualAddress(shotEffector))
            {
                //Debug.WriteLine("[NoRecoil] Invalid breathEffector or shotEffector");
                return (0, 0, 0);
            }

            var newShotRecoil = Memory.ReadPtr(
                shotEffector + Offsets.ShotEffector.NewShotRecoil, false);

            //Debug.WriteLine($"[NoRecoil] Read - NewShotRecoil: 0x{newShotRecoil:X}");

            if (!MemDMA.IsValidVirtualAddress(newShotRecoil))
            {
                //Debug.WriteLine("[NoRecoil] Invalid newShotRecoil");
                return (0, 0, 0);
            }

            // Cache the pointers
            _cachedBreathEffector = breathEffector;
            _cachedShotEffector   = shotEffector;
            _cachedNewShotRecoil  = newShotRecoil;

            //Debug.WriteLine("[NoRecoil] Cached new pointers");

            return (breathEffector, shotEffector, newShotRecoil);
        }

        private static bool ValidatePointers(ulong breathEffector, ulong shotEffector, ulong newShotRecoil)
        {
            return MemDMA.IsValidVirtualAddress(breathEffector) &&
                   MemDMA.IsValidVirtualAddress(shotEffector)   &&
                   MemDMA.IsValidVirtualAddress(newShotRecoil);
        }

        private void ClearCache()
        {
            //Debug.WriteLine("[NoRecoil] Clearing cache");
            _cachedBreathEffector = 0;
            _cachedShotEffector   = 0;
            _cachedNewShotRecoil  = 0;
        }

        public override void OnRaidStart()
        {
            //Debug.WriteLine("[NoRecoil] OnRaidStart called");
            _lastEnabledState  = default;
            _lastRecoilAmount  = 1.0f;
            _lastSwayAmount    = 1.0f;
            _lastPwaPtr        = 0;
            ClearCache();
        }
    }
}