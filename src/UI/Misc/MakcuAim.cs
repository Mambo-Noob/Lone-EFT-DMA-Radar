/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld;
using LoneEftDmaRadar.Tarkov.GameWorld.Camera;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.Mono.Collections;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc.Ballistics;
using SkiaSharp;

namespace LoneEftDmaRadar.UI.Misc
{
    /// <summary>
    /// Makcu-based hardware aimbot with ballistics prediction.
    /// </summary>
    public sealed class MakcuAimbot : IDisposable
    {
        #region Fields

        private static MakcuConfig Config => App.Config.Makcu;
        private readonly MemDMA _memory;
        private readonly Thread _worker;
        private bool _disposed;
        private BallisticsInfo _lastBallistics;
        private string _debugStatus = "Initializing...";
        private AbstractPlayer _lockedTarget;
        private bool _isEngaged;

        // FOV / target debug fields
        private int _lastCandidateCount;
        private float _lastLockedTargetFov = float.NaN;
        private bool _lastIsTargetValid;
        private Vector3 _lastFireportPos;
        private bool _hasLastFireport;

        // Per-stage debug counters
        private int _dbgTotalPlayers;
        private int _dbgEligibleType;
        private int _dbgWithinDistance;
        private int _dbgHaveSkeleton;
        private int _dbgW2SPassed;
        private ulong _cachedBreathEffector;
        private ulong _cachedShotEffector;
        private ulong _cachedNewShotRecoil;
        private float _lastRecoilAmount = 1.0f;
        private float _lastSwayAmount = 1.0f;
        #endregion
		[Flags]
		public enum EProceduralAnimationMask
		{
			Breathing = 1,
			Walking = 2,
			MotionReaction = 4,
			ForceReaction = 8,
			Shooting = 16,
			DrawDown = 32,
			Aiming = 64,
			HandShake = 128,
		}        
        private const int ORIGINAL_PWA_MASK = 
            (int)(EProceduralAnimationMask.MotionReaction |
                  EProceduralAnimationMask.ForceReaction |
                  EProceduralAnimationMask.Shooting |
                  EProceduralAnimationMask.DrawDown |
                  EProceduralAnimationMask.Aiming |
                  EProceduralAnimationMask.Breathing);
        #region Constructor / Disposal

        public MakcuAimbot(MemDMA memory)
        {
            _memory = memory;

            _worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = "MakcuAimbotWorker"
            };
            _worker.Start();

            Debug.WriteLine("[MakcuAimbot] Started");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                Debug.WriteLine("[MakcuAimbot] Disposed");
            }
        }
        public void OnRaidEnd()
        {
            _lastRecoilAmount = 1.0f;
            _lastSwayAmount = 1.0f;
            ResetTarget();
        }
        #endregion

        #region Main Loop

        private void WorkerLoop()
        {
            _debugStatus = "Worker starting...";

            while (!_disposed)
            {
                try
                {
                    // 1) Check if we're in raid with a valid local player
                    if (!_memory.InRaid)
                    {
                        _debugStatus = "Not in raid";
                        ResetTarget();
                        Thread.Sleep(100);
                        continue;
                    }

                    if (_memory.LocalPlayer is not LocalPlayer localPlayer)
                    {
                        _debugStatus = "LocalPlayer == null";
                        ResetTarget();
                        Thread.Sleep(50);
                        continue;
                    }

                    // 3) Check if aimbot is enabled
                    if (!Config.Enabled)
                    {
                        _debugStatus = "Aimbot disabled (NoRecoil still active if enabled)";
                        ResetTarget();
                        Thread.Sleep(100);
                        continue;
                    }


                    if (!App.Config.MemWrites.MemoryAimEnabled && !Device.connected)
                    {
                        _debugStatus = "Makcu device NOT connected (enable MemoryAim to use without device)";
                        ResetTarget();
                        Thread.Sleep(250);
                        continue;
                    }

                    if (_memory.Game is not LocalGameWorld game)
                    {
                        _debugStatus = "Game instance == null";
                        ResetTarget();
                        Thread.Sleep(50);
                        continue;
                    }

                    // 4) Check engagement for AIMBOT only
                    if (!IsEngaged)
                    {
                        _debugStatus = "Waiting for aim key (IsEngaged == false)";
                        ResetTarget();
                        Thread.Sleep(10);
                        continue;
                    }

                    // 5) Weapon / Fireport check
                    _debugStatus = "Updating FirearmManager...";
                    localPlayer.UpdateFirearmManager();

                    if (localPlayer.FirearmManager?.FireportPosition is not Vector3 fireportPos)
                    {
                        _debugStatus = "No valid weapon / fireport";
                        ResetTarget();
                        _hasLastFireport = false;
                        Thread.Sleep(16);
                        continue;
                    }

                    _lastFireportPos = fireportPos;
                    _hasLastFireport = true;

                    // 6) Target acquisition
                    if (_lockedTarget == null || !IsTargetValid(_lockedTarget, localPlayer))
                    {
                        _debugStatus = "Scanning for target...";
                        _lockedTarget = FindBestTarget(game, localPlayer);

                        if (_lockedTarget == null)
                        {
                            _debugStatus = "No target in FOV / range";
                            Thread.Sleep(10);
                            continue;
                        }
                    }

                    _debugStatus = $"Target locked: {_lockedTarget.Name}";

                    // 7) Ballistics
                    _lastBallistics = GetBallisticsInfo(localPlayer);
                    if (_lastBallistics == null || !_lastBallistics.IsAmmoValid)
                    {
                        _debugStatus = $"Target {_lockedTarget.Name} - No valid ammo/ballistics";
                    }
                    else
                    {
                        _debugStatus = $"Target {_lockedTarget.Name} - Ballistics OK";
                    }

                    // 8) Aim
                    AimAtTarget(localPlayer, _lockedTarget, fireportPos);
                    _isEngaged = true;

                    Thread.Sleep(8); // ~125Hz
                }
                catch (Exception ex)
                {
                    _debugStatus = $"Error: {ex.Message}";
                    Debug.WriteLine($"[MakcuAimbot] Error: {ex}");
                    ResetTarget();
                    Thread.Sleep(100);
                }
            }

            _debugStatus = "Worker stopped";
        }

        #endregion

        #region Targeting

        private AbstractPlayer FindBestTarget(LocalGameWorld game, LocalPlayer localPlayer)
        {
            var candidates = new List<TargetCandidate>();
            _lastCandidateCount = 0;

            // reset per-stage counters
            _dbgTotalPlayers = 0;
            _dbgEligibleType = 0;
            _dbgWithinDistance = 0;
            _dbgHaveSkeleton = 0;
            _dbgW2SPassed = 0;

            foreach (var player in game.Players)
            {
                _dbgTotalPlayers++;

                if (!ShouldTargetPlayer(player, localPlayer))
                    continue;

                _dbgEligibleType++;

                var distance = Vector3.Distance(localPlayer.Position, player.Position);
                if (distance > Config.MaxDistance)
                    continue;

                _dbgWithinDistance++;

                // Check if skeleton exists
                if (player.Skeleton?.BoneTransforms == null)
                    continue;

                _dbgHaveSkeleton++;

                // Check if any bone is within FOV
                float bestFovForThisPlayer = float.MaxValue;
                bool anyBoneProjected = false;

                foreach (var bone in player.Skeleton.BoneTransforms.Values)
                {
                    // IMPORTANT: use same W2S style as ESP б· "in" + default flags
                    if (CameraManager.WorldToScreen(in bone.Position, out var screenPos))
                    {
                        anyBoneProjected = true;
                        float fovDist = CameraManager.GetFovMagnitude(screenPos);
                        if (fovDist < bestFovForThisPlayer)
                        {
                            bestFovForThisPlayer = fovDist;
                        }
                    }
                }

                if (anyBoneProjected)
                    _dbgW2SPassed++;

                if (bestFovForThisPlayer < Config.FOV)
                {
                    candidates.Add(new TargetCandidate
                    {
                        Player = player,
                        FOVDistance = bestFovForThisPlayer,
                        WorldDistance = distance
                    });
                }
            }

            _lastCandidateCount = candidates.Count;

            if (candidates.Count == 0)
                return null;

            // Select best target based on mode
            return Config.Targeting switch
            {
                MakcuConfig.TargetingMode.ClosestToCrosshair => candidates.MinBy(x => x.FOVDistance).Player,
                MakcuConfig.TargetingMode.ClosestDistance => candidates.MinBy(x => x.WorldDistance).Player,
                _ => candidates.MinBy(x => x.FOVDistance).Player
            };
        }

private bool ShouldTargetPlayer(AbstractPlayer player, LocalPlayer localPlayer)
{
    // DEBUG: Log every rejection reason for first few players
    bool isDebugPlayer = _dbgTotalPlayers <= 3;
    
    if (isDebugPlayer)
        Debug.WriteLine($"\n[Makcu] === Checking Player #{_dbgTotalPlayers} ===");
    
    // Don't target self
    if (player == localPlayer)
    {
        if (isDebugPlayer) Debug.WriteLine($"  ? REJECTED: player == localPlayer");
        return false;
    }
    
    if (player is LocalPlayer)
    {
        if (isDebugPlayer) Debug.WriteLine($"  ? REJECTED: player is LocalPlayer");
        return false;
    }
    
    if (!player.IsActive)
    {
        if (isDebugPlayer) Debug.WriteLine($"  ? REJECTED: !IsActive");
        return false;
    }
    
    if (!player.IsAlive)
    {
        if (isDebugPlayer) Debug.WriteLine($"  ? REJECTED: !IsAlive");
        return false;
    }

    // Check player type filters
    if (player.Type == PlayerType.Teammate)
    {
        if (isDebugPlayer) Debug.WriteLine($"  ? REJECTED: Type is Teammate");
        return false;
    }

    if (isDebugPlayer)
    {
        Debug.WriteLine($"  Player Type: {player.Type}");
        Debug.WriteLine($"  Config Filters:");
        Debug.WriteLine($"    PMC={Config.TargetPMC}, PScav={Config.TargetPlayerScav}");
        Debug.WriteLine($"    AI={Config.TargetAIScav}, Boss={Config.TargetBoss}, Raider={Config.TargetRaider}");
    }

    bool shouldTarget = player.Type switch
    {
        PlayerType.PMC => Config.TargetPMC,
        PlayerType.PScav => Config.TargetPlayerScav,
        PlayerType.AIScav => Config.TargetAIScav,
        PlayerType.AIBoss => Config.TargetBoss,
        PlayerType.AIRaider => Config.TargetRaider,
        PlayerType.Default => Config.TargetAIScav,
        _ => false
    };
    
    if (isDebugPlayer)
    {
        if (shouldTarget)
            Debug.WriteLine($"  ? ACCEPTED!");
        else
            Debug.WriteLine($"  ? REJECTED: Type {player.Type} not in filters or default case");
    }
    
    return shouldTarget;
}
        private bool IsTargetValid(AbstractPlayer target, LocalPlayer localPlayer)
        {
            _lastIsTargetValid = false;
            _lastLockedTargetFov = float.NaN;

            if (target == null || !target.IsActive || !target.IsAlive)
                return false;

            var distance = Vector3.Distance(localPlayer.Position, target.Position);
            if (distance > Config.MaxDistance)
                return false;

            // Check if skeleton exists
            if (target.Skeleton?.BoneTransforms == null)
                return false;

            // Compute min FOV distance for this target
            float minFov = float.MaxValue;
            bool anyBoneProjected = false;

            foreach (var bone in target.Skeleton.BoneTransforms.Values)
            {
                if (CameraManager.WorldToScreen(in bone.Position, out var screenPos))
                {
                    anyBoneProjected = true;
                    float fovDist = CameraManager.GetFovMagnitude(screenPos);
                    if (fovDist < minFov)
                        minFov = fovDist;
                }
            }

            if (!anyBoneProjected)
            {
                _lastLockedTargetFov = float.NaN;
                return false;
            }

            _lastLockedTargetFov = minFov;

            if (minFov < Config.FOV)
            {
                _lastIsTargetValid = true;
                return true;
            }

            return false;
        }

        private void ResetTarget()
        {
            if (_lockedTarget != null)
            {
                _lockedTarget = null;
            }
        }

        #endregion

        #region Aiming

        private void AimAtTarget(LocalPlayer localPlayer, AbstractPlayer target, Vector3 fireportPos)
        {
            // Check if skeleton exists
            if (target.Skeleton?.BoneTransforms == null)
                return;

            // Get target bone position
            if (!target.Skeleton.BoneTransforms.TryGetValue(Config.TargetBone, out var boneTransform))
            {
                // Fallback to chest if configured bone not found
                if (!target.Skeleton.BoneTransforms.TryGetValue(Bones.HumanSpine3, out boneTransform))
                    return;
            }

            Vector3 targetPos = boneTransform.Position;

            // Apply ballistics prediction if enabled
            if (Config.EnablePrediction && localPlayer.FirearmManager != null)
            {
                targetPos = PredictHitPoint(localPlayer, target, fireportPos, targetPos);
            }

            // ? Check if MemoryAim is enabled
            if (App.Config.MemWrites.Enabled && App.Config.MemWrites.MemoryAimEnabled)
            {
                ApplyMemoryAim(localPlayer, targetPos);
                Debug.WriteLine($"[MakcuAimbot] Using MemoryAim for target {target.Name}");
                return;
            }

            // Original Makcu device aiming (only if MemoryAim is disabled)
            // Convert to screen space
            if (!CameraManager.WorldToScreen(ref targetPos, out var screenPos, false))
                return;

            // Calculate delta from center
            var center = CameraManager.ViewportCenter;
            float deltaX = screenPos.X - center.X;
            float deltaY = screenPos.Y - center.Y;

            // Convert to mouse movement
            int moveX = (int)Math.Round(deltaX);
            int moveY = (int)Math.Round(deltaY);

            // Apply movement
            if (moveX != 0 || moveY != 0)
            {
                Device.move(moveX, moveY);
                Debug.WriteLine($"[MakcuAimbot] Aiming at target {target.Name}: Move({moveX}, {moveY})");
            }
        }
private void ApplyMemoryAim(LocalPlayer localPlayer, Vector3 targetPosition)
{
    try
    {
        var firearmManager = localPlayer.FirearmManager;
        if (firearmManager == null)
            return;

        var fireportPos = firearmManager.FireportPosition;
        if (!fireportPos.HasValue || fireportPos.Value == Vector3.Zero)
            return;

        var fpPos = fireportPos.Value;

        // 1) Desired angle (same CalcAngle as old cheat)
        Vector2 aimAngle = CalcAngle(fpPos, targetPosition);

        // 2) Current view angles from MovementContext._rotation (NEW OFFSET)
        ulong movementContext = localPlayer.MovementContext;
        Vector2 viewAngles = Memory.ReadValue<Vector2>(
            movementContext + Offsets.MovementContext._rotation, // 0xC4 in your dump
            false
        );

        // 3) Delta and normalization (same as old)
        Vector2 delta = aimAngle - viewAngles;
        NormalizeAngle(ref delta);

        // 4) Gun angle mapping (identical to old ApplySilentAim)
        Vector3 gunAngle = new Vector3(
            DegToRad(delta.X) / 1.5f,
            0.0f,
            DegToRad(delta.Y) / 1.5f
        );

        // 5) Write to _shotDirection (same as old 0x22C)
        ulong shotDirectionAddr = localPlayer.PWA + Offsets.ProceduralWeaponAnimation._shotDirection;
        if (!MemDMA.IsValidVirtualAddress(shotDirectionAddr))
            return;

        // Keep the exact weird mapping you had before
        Vector3 writeVec = new Vector3(gunAngle.X, -1.0f, gunAngle.Z * -1.0f);
        Memory.WriteValue(shotDirectionAddr, writeVec);

        Debug.WriteLine($"[MemoryAim] Fireport: {fpPos}");
        Debug.WriteLine($"[MemoryAim] Target:   {targetPosition}");
        Debug.WriteLine($"[MemoryAim] AimAngle: {aimAngle}, ViewAngles: {viewAngles}, жд={delta}");
        Debug.WriteLine($"[MemoryAim] WriteVec: {writeVec}");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[MemoryAim] Error: {ex}");
    }
}


private static Vector2 CalcAngle(Vector3 from, Vector3 to)
{
    Vector3 delta = from - to;
    float length = delta.Length();

    return new Vector2(
        RadToDeg((float)-Math.Atan2(delta.X, -delta.Z)),
        RadToDeg((float)Math.Asin(delta.Y / length))
    );
}

private static void NormalizeAngle(ref Vector2 angle)
{
    NormalizeAngle(ref angle.X);
    NormalizeAngle(ref angle.Y);
}

private static void NormalizeAngle(ref float angle)
{
    while (angle > 180.0f) angle -= 360.0f;
    while (angle < -180.0f) angle += 360.0f;
}

private static float DegToRad(float degrees)
    => degrees * ((float)Math.PI / 180.0f);

private static float RadToDeg(float radians)
    => radians * (180.0f / (float)Math.PI);

        private Vector3 PredictHitPoint(LocalPlayer localPlayer, AbstractPlayer target, Vector3 fireportPos, Vector3 targetPos)
        {
            try
            {
                // Get ballistics info from weapon
                var ballistics = GetBallisticsInfo(localPlayer);
                if (ballistics == null || !ballistics.IsAmmoValid)
                    return targetPos;

                // Get target velocity (only for player scavs and PMCs that have movement)
                Vector3 targetVelocity = Vector3.Zero;
                if (target is ObservedPlayer)
                {
                    try
                    {
                        targetVelocity = _memory.ReadValue<Vector3>(
                            target.MovementContext + Offsets.ObservedMovementController.Velocity,
                            false);
                    }
                    catch
                    {
                        // Velocity read failed, use zero
                    }
                }

                // Run ballistics simulation
                var sim = BallisticsSimulation.Run(ref fireportPos, ref targetPos, ballistics);

                // Apply prediction
                Vector3 predictedPos = targetPos;

                // Add drop compensation
                //predictedPos.Y += sim.DropCompensation;

                // Add lead for moving targets
                if (targetVelocity != Vector3.Zero)
                {
                    float speed = targetVelocity.Length();
                    if (speed > 0.5f) // Only predict if moving faster than 0.5 m/s
                    {
                        Vector3 lead = targetVelocity * sim.TravelTime;
                        predictedPos += lead;
                    }
                }

                return predictedPos;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MakcuAimbot] Prediction failed: {ex}");
                return targetPos;
            }
        }

        private BallisticsInfo GetBallisticsInfo(LocalPlayer localPlayer)
        {
            try
            {
                var firearmManager = localPlayer.FirearmManager;
                if (firearmManager == null)
                {
                    Debug.WriteLine("[MakcuAimbot] FirearmManager is null");
                    return null;
                }

                var hands = firearmManager.HandsController;
                if (hands.Item2 == false) // Not a weapon
                {
                    Debug.WriteLine("[MakcuAimbot] HandsController is not a weapon");
                    return null;
                }

                ulong itemBase = _memory.ReadPtr(hands.Item1 + Offsets.ItemHandsController.Item, false);
                ulong itemTemplate = _memory.ReadPtr(itemBase + Offsets.LootItem.Template, false);

                // Get ammo template
                var ammoTemplate = FirearmManager.MagazineManager.GetAmmoTemplateFromWeapon(itemBase);
                if (ammoTemplate == 0)
                {
                    Debug.WriteLine("[MakcuAimbot] No ammo template found");
                    return null;
                }

                // Read ballistics data
                var ballistics = new BallisticsInfo
                {
                    BulletMassGrams = _memory.ReadValue<float>(ammoTemplate + Offsets.AmmoTemplate.BulletMassGram, false),
                    BulletDiameterMillimeters = _memory.ReadValue<float>(ammoTemplate + Offsets.AmmoTemplate.BulletDiameterMilimeters, false),
                    BallisticCoefficient = _memory.ReadValue<float>(ammoTemplate + Offsets.AmmoTemplate.BallisticCoeficient, false)
                };

                // Calculate bullet velocity with mods
                float baseSpeed = _memory.ReadValue<float>(ammoTemplate + Offsets.AmmoTemplate.InitialSpeed, false);
                float velMod = _memory.ReadValue<float>(itemTemplate + Offsets.WeaponTemplate.Velocity, false);

                // Recursively add attachment velocity modifiers
                AddAttachmentVelocity(itemBase, ref velMod);

                ballistics.BulletSpeed = baseSpeed * (1f + (velMod / 100f));

                return ballistics;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MakcuAimbot] Failed to get ballistics: {ex}");
                return null;
            }
        }

        private void AddAttachmentVelocity(ulong itemBase, ref float velocityModifier)
        {
            try
            {
                var slotsPtr = _memory.ReadPtr(itemBase + Offsets.LootItemMod.Slots, false);
                using var slots = MonoArray<ulong>.Create(slotsPtr, true);

                if (slots.Count > 100) // Sanity check
                    return;

                foreach (var slot in slots.Span)
                {
                    var containedItem = _memory.ReadPtr(slot + Offsets.Slot.ContainedItem, false);
                    if (containedItem == 0)
                        continue;

                    var itemTemplate = _memory.ReadPtr(containedItem + Offsets.LootItem.Template, false);
                    velocityModifier += _memory.ReadValue<float>(itemTemplate + Offsets.ModTemplate.Velocity, false);

                    // Recurse
                    AddAttachmentVelocity(containedItem, ref velocityModifier);
                }
            }
            catch
            {
                // Ignore errors in attachment recursion
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Set to true while the aim-key is held (from hotkey/ui).
        /// </summary>
        public bool IsEngaged { get; set; }

        private readonly struct TargetCandidate
        {
            public AbstractPlayer Player { get; init; }
            public float FOVDistance { get; init; }
            public float WorldDistance { get; init; }
        }

        #region Debug Overlay

        /// <summary>
        /// Draws debug information on the ESP overlay.
        /// </summary>
        public void DrawDebug(SKCanvas canvas, LocalPlayer localPlayer)
        {
            try
            {
                var lines = new List<string>();

                // Header
                lines.Add("=== MAKCU AIMBOT DEBUG ===");
                lines.Add($"Status:       {_debugStatus}");
                lines.Add($"Key State:    {(IsEngaged ? "ENGAGED" : "Idle")}");
                lines.Add($"Device:       {(Device.connected ? "Connected" : "Disconnected")}");
                lines.Add($"Enabled:      {(Config.Enabled ? "TRUE" : "FALSE")}");
                lines.Add($"InRaid:       {_memory.InRaid}");
                lines.Add("");

                // LocalPlayer / Firearm / Fireport info
                if (localPlayer != null)
                {
                    lines.Add($"LocalPlayer:  OK @ {localPlayer.Position}");
                    lines.Add($"FirearmMgr:   {(localPlayer.FirearmManager != null ? "OK" : "NULL")}");
                }
                else
                {
                    lines.Add("LocalPlayer:  NULL");
                    lines.Add("FirearmMgr:   n/a");
                }

                if (_hasLastFireport)
                {
                    lines.Add($"FireportPos:  {_lastFireportPos}");
                }
                else
                {
                    lines.Add("FireportPos:  (no valid fireport)");
                }

                lines.Add("");

                // Per-stage filter stats
                lines.Add("Players (this scan):");
                lines.Add($"  Total:      {_dbgTotalPlayers}");
                lines.Add($"  Type OK:    {_dbgEligibleType}");
                lines.Add($"  InDist:     {_dbgWithinDistance}");
                lines.Add($"  Skeleton:   {_dbgHaveSkeleton}");
                lines.Add($"  W2S OK:     {_dbgW2SPassed}");
                lines.Add($"  Candidates: {_lastCandidateCount}");
                lines.Add("");

                // Target info / FOV diagnostics
                lines.Add($"Config FOV:   {Config.FOV:F1} (pixels from screen center)");
                lines.Add($"TargetValid:  {_lastIsTargetValid}");

                if (_lockedTarget != null && localPlayer != null)
                {
                    var dist = Vector3.Distance(localPlayer.Position, _lockedTarget.Position);
                    lines.Add($"Locked Target: {_lockedTarget.Name} [{_lockedTarget.Type}]");
                    lines.Add($"  Distance:   {dist:F1}m");
                    if (!float.IsNaN(_lastLockedTargetFov) && !float.IsInfinity(_lastLockedTargetFov))
                        lines.Add($"  FOVDist:    {_lastLockedTargetFov:F1}");
                    else
                        lines.Add("  FOVDist:    n/a");

                    lines.Add($"  TargetBone: {Config.TargetBone}");

                    if (_lockedTarget is ObservedPlayer obs)
                    {
                        lines.Add($"  Health:     {obs.HealthStatus}");
                    }
                }
                else
                {
                    lines.Add("Locked Target: None");
                }

                lines.Add("");

                // Ballistics Info
                if (_lastBallistics != null && _lastBallistics.IsAmmoValid)
                {
                    lines.Add("Ballistics:");
                    lines.Add($"  BulletSpeed: {(_lastBallistics.BulletSpeed):F1} m/s");
                    lines.Add($"  Mass:        {_lastBallistics.BulletMassGrams:F2} g");
                    lines.Add($"  BC:          {_lastBallistics.BallisticCoefficient:F3}");
                    lines.Add($"  Prediction:  {(Config.EnablePrediction ? "ON" : "OFF")}");
                }
                else
                {
                    lines.Add("Ballistics:   No / invalid ammo");
                }

                lines.Add("");

                // Settings / filters
                lines.Add("Settings:");
                lines.Add($"  MaxDist:    {Config.MaxDistance:F0}m");
                lines.Add($"  Targeting:  {Config.Targeting}");
                lines.Add("");
                lines.Add("Target Filters:");
                lines.Add($"  PMC:    {Config.TargetPMC}   PScav: {Config.TargetPlayerScav}");
                lines.Add($"  AI:     {Config.TargetAIScav}   Boss:  {Config.TargetBoss}   Raider: {Config.TargetRaider}");
                
                lines.Add("");
                lines.Add("No Recoil:");
                lines.Add($"  Enabled:    {(App.Config.MemWrites.NoRecoilEnabled ? "ON" : "OFF")}");
                if (App.Config.MemWrites.NoRecoilEnabled)
                {
                    lines.Add($"  Recoil:     {App.Config.MemWrites.NoRecoilAmount:F0}%");
                    lines.Add($"  Sway:       {App.Config.MemWrites.NoSwayAmount:F0}%");
                }
                float x = 10;
                float y = 30;
                float lineHeight = 18;

                using var bgPaint = new SKPaint
                {
                    Color = new SKColor(0, 0, 0, 180),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };

                using var textPaint = new SKPaint
                {
                    Color = SKColors.White,
                    TextSize = 14,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal)
                };

                using var headerPaint = new SKPaint
                {
                    Color = SKColors.Yellow,
                    TextSize = 14,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
                };

                using var shadowPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    TextSize = 14,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 3,
                    Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
                };

                // Background size
                float maxWidth = 0;
                foreach (var line in lines)
                {
                    float width = textPaint.MeasureText(line);
                    if (width > maxWidth) maxWidth = width;
                }

                canvas.DrawRect(x - 5, y - 20, maxWidth + 25, lines.Count * lineHeight + 20, bgPaint);

                // Text with shadow (fake bold / shading)
                foreach (var line in lines)
                {
                    var paint = line.StartsWith("===") ||
                                line == "Ballistics:" ||
                                line == "Settings:" ||
                                line == "Target Filters:" ||
                                line == "Players (this scan):"
                        ? headerPaint
                        : textPaint;

                    canvas.DrawText(line, x + 1.5f, y + 1.5f, shadowPaint);
                    canvas.DrawText(line, x, y, paint);
                    y += lineHeight;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MakcuAimbot] DrawDebug error: {ex}");
            }
        }
        #endregion
        #endregion
        
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

        public static Vector3 Multiply(this Quaternion q, Vector3 v)
        {
            float x = q.X * 2.0f;
            float y = q.Y * 2.0f;
            float z = q.Z * 2.0f;
            float xx = q.X * x;
            float yy = q.Y * y;
            float zz = q.Z * z;
            float xy = q.X * y;
            float xz = q.X * z;
            float yz = q.Y * z;
            float wx = q.W * x;
            float wy = q.W * y;
            float wz = q.W * z;
        
            Vector3 res;
            res.X = (1.0f - (yy + zz)) * v.X + (xy - wz) * v.Y + (xz + wy) * v.Z;
            res.Y = (xy + wz) * v.X + (1.0f - (xx + zz)) * v.Y + (yz - wx) * v.Z;
            res.Z = (xz - wy) * v.X + (yz + wx) * v.Y + (1.0f - (xx + yy)) * v.Z;
        
            return res;
        }
    }  
}
