/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using System;
using System.Diagnostics;
using System.Numerics;

namespace LoneEftDmaRadar.Tarkov.Features.MemWrites
{
    /// <summary>
    /// Silent aim by modifying shot direction in memory.
    /// Only writes when target is set AND aim key is held.
    /// </summary>
    public sealed class MemoryAim : MemWriteFeature<MemoryAim>
    {
        private bool _lastEnabledState;
        private Vector3? _targetPosition;
        private bool _isEngaged; // ? Track if aim key is held

        public override bool Enabled
        {
            get => App.Config.MemWrites.MemoryAimEnabled;
            set => App.Config.MemWrites.MemoryAimEnabled = value;
        }

        protected override TimeSpan Delay => TimeSpan.FromMilliseconds(1);

        /// <summary>
        /// Set whether aim key is currently held. Called by MakcuAimbot.
        /// </summary>
        public void SetEngaged(bool engaged)
        {
            _isEngaged = engaged;
            if (!engaged)
            {
                _targetPosition = null; // Clear target when key is released
            }
        }

        /// <summary>
        /// Set the target position to aim at. Called by MakcuAimbot.
        /// </summary>
        public void SetTargetPosition(Vector3? targetPos)
        {
            _targetPosition = targetPos;
        }

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
                        _targetPosition = null;
                        _isEngaged = false;
                        Debug.WriteLine("[MemoryAim] Disabled");
                    }
                    return;
                }

                if (stateChanged)
                {
                    _lastEnabledState = true;
                    Debug.WriteLine("[MemoryAim] Enabled");
                }
                //Debug.WriteLine("[MemoryAim] TryApply called");
                // ? Only apply if aim key is held AND we have a target
                if (!_isEngaged || _targetPosition == null)
                    return;
                Debug.WriteLine("[MemoryAim] Applying aim");
                ApplyMemoryAim(localPlayer, _targetPosition.Value);
                
                // Clear target after writing (will be set again next frame by MakcuAimbot if still aiming)
                _targetPosition = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoryAim] Error: {ex}");
            }
        }

        public void ApplyMemoryAim(LocalPlayer localPlayer, Vector3 targetPosition)
        {
            try
            {
                Debug.WriteLine($"[MemoryAim] Applying aim to target at {targetPosition}");
                var firearmManager = localPlayer.FirearmManager;
                if (firearmManager == null)
                {
                    Debug.WriteLine("[MemoryAim] FirearmManager is null");
                    return;
                }

                var fireportPos = firearmManager.FireportPosition;
                var fireportRot = firearmManager.FireportRotation;

                // Validate fireport
                if (!fireportPos.HasValue || fireportPos.Value == Vector3.Zero)
                {
                    Debug.WriteLine("[MemoryAim] Fireport position is null or zero");
                    return;
                }

                if (!fireportRot.HasValue)
                {
                    Debug.WriteLine("[MemoryAim] Fireport rotation is null");
                    return;
                }

                // Calculate direction
                var worldDirection = fireportPos.Value.CalculateDirection(targetPosition);
                var newDirection = fireportRot.Value.InverseTransformDirection(worldDirection);

                // Write directly to shot direction offset
                var shotDirectionAddr = localPlayer.PWA + Offsets.ProceduralWeaponAnimation._shotDirection;
                
                if (!MemDMA.IsValidVirtualAddress(shotDirectionAddr))
                {
                    Debug.WriteLine($"[MemoryAim] Invalid shot direction address: 0x{shotDirectionAddr:X}");
                    return;
                }

                Memory.WriteValue(shotDirectionAddr, newDirection);

                Debug.WriteLine($"[MemoryAim] ? Aim applied - Direction: {newDirection}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemoryAim] ApplyMemoryAim error: {ex}");
            }
        }

        public override void OnRaidStart()
        {
            Debug.WriteLine("[MemoryAim] OnRaidStart called");
            _lastEnabledState = default;
            _targetPosition = null;
            _isEngaged = false;
        }
    }

    public static class Vector3Extensions
    {
        public static Vector3 CalculateDirection(this Vector3 source, Vector3 destination)
        {
            Vector3 dir = destination - source;
            return Vector3.Normalize(dir);
        }
    }

    public static class QuaternionExtensions
    {
        public static Vector3 InverseTransformDirection(this Quaternion rotation, Vector3 direction)
        {
            return Quaternion.Conjugate(rotation).Multiply(direction);
        }

        public static Vector3 Multiply(this Quaternion rotation, Vector3 point)
        {
            float num = rotation.X * 2f;
            float num2 = rotation.Y * 2f;
            float num3 = rotation.Z * 2f;
            float num4 = rotation.X * num;
            float num5 = rotation.Y * num2;
            float num6 = rotation.Z * num3;
            float num7 = rotation.X * num2;
            float num8 = rotation.X * num3;
            float num9 = rotation.Y * num3;
            float num10 = rotation.W * num;
            float num11 = rotation.W * num2;
            float num12 = rotation.W * num3;

            Vector3 result;
            result.X = (1f - (num5 + num6)) * point.X + (num7 - num12) * point.Y + (num8 + num11) * point.Z;
            result.Y = (num7 + num12) * point.X + (1f - (num4 + num6)) * point.Y + (num9 - num10) * point.Z;
            result.Z = (num8 - num11) * point.X + (num9 + num10) * point.Y + (1f - (num4 + num5)) * point.Z;
            return result;
        }
    }
}