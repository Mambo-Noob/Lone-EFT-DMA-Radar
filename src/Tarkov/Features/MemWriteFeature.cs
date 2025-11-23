/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using System;

namespace LoneEftDmaRadar.Tarkov.Features.MemWrites
{
    /// <summary>
    /// Base class for all memory write features.
    /// </summary>
    public abstract class MemWriteFeature<T> where T : MemWriteFeature<T>, new()
    {
        private static T _instance;
        private DateTime _lastRun = DateTime.MinValue;

        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static T Instance => _instance ??= new T();

        /// <summary>
        /// Whether this feature is enabled.
        /// </summary>
        public abstract bool Enabled { get; set; }

        /// <summary>
        /// Minimum delay between applications.
        /// </summary>
        protected abstract TimeSpan Delay { get; }

        /// <summary>
        /// Try to apply the memory write feature.
        /// </summary>
        public abstract void TryApply(LocalPlayer localPlayer);

        /// <summary>
        /// Called when a raid starts.
        /// </summary>
        public abstract void OnRaidStart();

        /// <summary>
        /// Checks if enough time has passed since last run.
        /// </summary>
        protected bool ShouldRun()
        {
            var now = DateTime.UtcNow;
            if (now - _lastRun < Delay)
                return false;

            _lastRun = now;
            return true;
        }

        /// <summary>
        /// Apply with delay check.
        /// </summary>
        public void ApplyIfReady(LocalPlayer localPlayer)
        {
            if (!Enabled)
            {
                return;
            }

            if (!ShouldRun())
            {
                return;
            }

            //Debug.WriteLine($"[{typeof(T).Name}] ApplyIfReady - calling TryApply");
            TryApply(localPlayer);
        }
    }
}