/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LoneEftDmaRadar.Tarkov.Features.MemWrites
{
    /// <summary>
    /// Manages all memory write features.
    /// </summary>
    public sealed class MemWritesManager
    {
        private readonly List<Action<LocalPlayer>> _features = new();

        public MemWritesManager()
        {
                // Register all features
                if(App.Config.MemWrites.NoRecoilEnabled)
                _features.Add(lp => NoRecoil.Instance.ApplyIfReady(lp));
                if(App.Config.MemWrites.InfiniteStaminaEnabled)
                _features.Add(lp => InfiniteStamina.Instance.ApplyIfReady(lp));
                if(App.Config.MemWrites.MemoryAimEnabled)
                _features.Add(lp => MemoryAim.Instance.ApplyIfReady(lp));
        }

        /// <summary>
        /// Apply all enabled memory write features.
        /// </summary>
        public void Apply(LocalPlayer localPlayer)
        {
            if (!App.Config.MemWrites.Enabled)
            {

                return;
            }

            if (localPlayer == null)
            {
                Debug.WriteLine("[MemWritesManager] LocalPlayer is null");
                return;
            }

            try
            {
                if(!Memory.InRaid)
                    return;

                //Debug.WriteLine($"[MemWritesManager] Applying {_features.Count} features");

                foreach (var feature in _features)
                {
                    try
                    {
                        feature(localPlayer);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MemWritesManager] Feature error: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MemWritesManager] Apply error: {ex}");
            }
        }

        /// <summary>
        /// Called when raid starts.
        /// </summary>
        public void OnRaidStart()
        {
            NoRecoil.Instance.OnRaidStart();
            InfiniteStamina.Instance.OnRaidStart();
            MemoryAim.Instance.OnRaidStart(); 
        }
    }
}