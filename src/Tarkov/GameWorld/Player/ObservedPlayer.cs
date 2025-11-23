/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
MIT License

Copyright (c) 2025 Lone DMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Misc.Services;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.Mono.Collections;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Radar.ViewModels;
using LoneEftDmaRadar.Web.ProfileApi;
using LoneEftDmaRadar.Web.ProfileApi.Schema;
using VmmSharpEx.Scatter;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Player
{
    public class ObservedPlayer : AbstractPlayer
    {
        /// <summary>
        /// Player's Profile & Stats.
        /// </summary>
        public PlayerProfile Profile { get; }
        /// <summary>
        /// ObservedPlayerController for non-clientplayer players.
        /// </summary>
        private ulong ObservedPlayerController { get; }
        public override ulong Body { get; }
        /// <summary>
        /// Inventory Controller field address.
        /// </summary>
        public override ulong InventoryControllerAddr { get; }
        /// <summary>
        /// Hands Controller field address.
        /// </summary>
        public override ulong HandsControllerAddr { get; }
        /// <summary>
        /// ObservedHealthController for non-clientplayer players.
        /// </summary>
        private ulong ObservedHealthController { get; }
        /// <summary>
        /// Player name.
        /// </summary>
        public override string Name
        {
            get
            {
                var name = Profile?.Name;
                return string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
            }
            set
            {
                if (Profile is PlayerProfile profile)
                    profile.Name = value;
            }
        }
        /// <summary>
        /// Type of player unit.
        /// </summary>
        public override PlayerType Type
        {
            get => Profile?.Type ?? PlayerType.Default;
            protected set
            {
                if (Profile is PlayerProfile profile)
                    profile.Type = value;
            }
        }
        /// <summary>
        /// Player Alerts.
        /// </summary>
        public override string Alerts
        {
            get => Profile?.Alerts;
            protected set
            {
                if (Profile is PlayerProfile profile)
                    profile.Alerts = value;
            }
        }
        /// <summary>
        /// Twitch.tv Channel URL for this player (if available).
        /// </summary>
        public string TwitchChannelURL => Profile?.TwitchChannelURL;
        /// <summary>
        /// True if player is TTV Streaming.
        /// </summary>
        public bool IsStreaming => TwitchChannelURL is not null;
        /// <summary>
        /// Account UUID for Human Controlled Players.
        /// </summary>
        public override string AccountID
        {
            get
            {
                if (Profile?.AccountID is string id)
                    return id;
                return "";
            }
        }
        /// <summary>
        /// Group that the player belongs to.
        /// </summary>
        public override int GroupID
        {
            get => Profile?.GroupID ?? -1;
            protected set
            {
                if (Profile is PlayerProfile profile)
                    profile.GroupID = value;
            }
        }
        /// <summary>
        /// Player's Faction.
        /// </summary>
        public override Enums.EPlayerSide PlayerSide
        {
            get => Profile?.PlayerSide ?? Enums.EPlayerSide.Savage;
            protected set
            {
                if (Profile is PlayerProfile profile)
                    profile.PlayerSide = value;
            }
        }
        /// <summary>
        /// Player is Human-Controlled.
        /// </summary>
        public override bool IsHuman { get; }
        /// <summary>
        /// MovementContext / StateContext
        /// </summary>
        public override ulong MovementContext { get; }
        /// <summary>
        /// Corpse field address..
        /// </summary>
        public override ulong CorpseAddr { get; }
        /// <summary>
        /// Player Rotation Field Address (view angles).
        /// </summary>
        public override ulong RotationAddress { get; }
        /// <summary>
        /// Player's Current Health Status
        /// </summary>
        public Enums.ETagStatus HealthStatus { get; private set; } = Enums.ETagStatus.Healthy;

        internal ObservedPlayer(ulong playerBase) : base(playerBase)
        {
            try
            {
                if (playerBase == 0)
                    throw new InvalidOperationException("[ObservedPlayer] playerBase is 0 (bad registered/observed list offset)");

                playerBase.ThrowIfInvalidVirtualAddress(nameof(playerBase));

                var localPlayer = Memory.LocalPlayer;
                ArgumentNullException.ThrowIfNull(localPlayer, nameof(localPlayer));

                // ObservedPlayerController
                ObservedPlayerController = SafeReadPtr(
                    this + Offsets.ObservedPlayerView.ObservedPlayerController,
                    "[ObservedPlayer] ObservedPlayerController");

                // Validate ObservedPlayerController.Player == this
                try
                {
                    var controllerPlayer = Memory.ReadValue<ulong>(
                        ObservedPlayerController + Offsets.ObservedPlayerController.Player);
                    if (controllerPlayer != this)
                    {
                        throw new InvalidOperationException(
                            $"[ObservedPlayer] ObservedPlayerController.Player mismatch. " +
                            $"Expected=0x{(ulong)this:X}, Actual=0x{controllerPlayer:X}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ObservedPlayer] ObservedPlayerController.Player check failed: {ex}");
                    throw;
                }

                // ObservedHealthController
                ObservedHealthController = SafeReadPtr(
                    ObservedPlayerController + Offsets.ObservedPlayerController.HealthController,
                    "[ObservedPlayer] ObservedHealthController");

                // Validate ObservedHealthController.Player == this
                try
                {
                    var healthPlayer = Memory.ReadValue<ulong>(
                        ObservedHealthController + Offsets.ObservedHealthController.Player);
                    if (healthPlayer != this)
                    {
                        throw new InvalidOperationException(
                            $"[ObservedPlayer] ObservedHealthController.Player mismatch. " +
                            $"Expected=0x{(ulong)this:X}, Actual=0x{healthPlayer:X}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ObservedPlayer] ObservedHealthController.Player check failed: {ex}");
                    throw;
                }

                // Body / controllers / corpse
                Body = SafeReadPtr(this + Offsets.ObservedPlayerView.PlayerBody, "[ObservedPlayer] Body");

                InventoryControllerAddr = ObservedPlayerController + Offsets.ObservedPlayerController.InventoryController;
                HandsControllerAddr     = ObservedPlayerController + Offsets.ObservedPlayerController.HandsController;
                CorpseAddr              = ObservedHealthController + Offsets.ObservedHealthController.PlayerCorpse;

                // Movement / rotation
                MovementContext = GetMovementContext();
                RotationAddress = ValidateRotationAddr(MovementContext + Offsets.ObservedPlayerStateContext.Rotation);

                // Transform (CRITICAL)
                var ti = SafeReadPtrChain("[ObservedPlayer] TransformInternal", this, _transformInternalChain);
                SkeletonRoot = new UnityTransform(ti);
                _ = SkeletonRoot.UpdatePosition();

                // Skeleton (optional)
                try
                {
                    Skeleton = new Skeleton(this, SkeletonRoot.TransformInternal);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WARNING: Failed to initialize Skeleton for ObservedPlayer (ESP will be unavailable): {ex}");
                }

                // AI / human flags
                bool isAI = SafeReadValue<bool>(
                    this + Offsets.ObservedPlayerView.IsAI,
                    "[ObservedPlayer] IsAI");
                IsHuman = !isAI;

                Profile = new PlayerProfile(this, GetAccountID());

                // Group ID
                GroupID = isAI ? -1 : GetGroupNumber();

                // Side / type / name
                var sideAddr = this + Offsets.ObservedPlayerView.Side;
                PlayerSide = (Enums.EPlayerSide)SafeReadValue<int>(sideAddr, "[ObservedPlayer] Side");
                if (!Enum.IsDefined(typeof(Enums.EPlayerSide), PlayerSide))
                    throw new InvalidOperationException($"[ObservedPlayer] Invalid PlayerSide value: {PlayerSide}");

                if (IsScav)
                {
                    if (isAI)
                    {
                        var voicePtr = SafeReadPtr(this + Offsets.ObservedPlayerView.Voice, "[ObservedPlayer] Voice");
                        string voice = Memory.ReadUnicodeString(voicePtr);
                        var role = GetAIRoleInfo(voice);
                        Name = role.Name;
                        Type = role.Type;
                    }
                    else
                    {
                        int pscavNumber = Interlocked.Increment(ref _lastPscavNumber);
                        Name = $"PScav{pscavNumber}";
                        Type = GroupID != -1 && GroupID == localPlayer.GroupID
                            ? PlayerType.Teammate
                            : PlayerType.PScav;
                    }
                }
                else if (IsPmc)
                {
                    Name = "PMC";
                    Type = GroupID != -1 && GroupID == localPlayer.GroupID
                        ? PlayerType.Teammate
                        : PlayerType.PMC;
                }
                else
                {
                    throw new NotImplementedException($"[ObservedPlayer] Unsupported PlayerSide: {PlayerSide}");
                }

                // Profile cache only for humans
                if (IsHuman)
                {
                    long acctIdLong = long.Parse(AccountID);
                    var cache = LocalCache.GetProfileCollection();
                    if (cache.FindById(acctIdLong) is EftProfileDto dto && dto.IsCachedRecent)
                    {
                        try
                        {
                            var profileData = dto.ToProfileData();
                            Profile.Data = profileData;
                            Debug.WriteLine($"[ObservedPlayer] Got Profile (Cached) '{acctIdLong}'!");
                        }
                        catch
                        {
                            _ = cache.Delete(acctIdLong); // Corrupted cache data, remove it
                            EFTProfileService.RegisterProfile(Profile); // Re-register for lookup
                        }
                    }
                    else
                    {
                        EFTProfileService.RegisterProfile(Profile);
                    }

                    PlayerHistoryViewModel.Add(this); // Log to player history
                }

                // Watchlist
                if (IsHumanHostile)
                {
                    if (MainWindow.Instance?.PlayerWatchlist?.ViewModel is PlayerWatchlistViewModel vm &&
                        vm.Watchlist.TryGetValue(AccountID, out var watchlistEntry))
                    {
                        Type = PlayerType.SpecialPlayer;
                        UpdateAlerts($"[Watchlist] {watchlistEntry.Reason} @ {watchlistEntry.Timestamp}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ObservedPlayer] FAILED to initialize: {ex}");
                throw;
            }
        }
        private static ulong SafeReadPtr(ulong addr, string fieldName)
        {
            if (addr == 0)
                throw new InvalidOperationException($"{fieldName}: source address is 0x0");

            var value = Memory.ReadPtr(addr, false);
            if (value == 0)
                throw new InvalidOperationException($"{fieldName}: read value is 0x0 (bad offset / not initialized)");

            return value;
        }

        private static T SafeReadValue<T>(ulong addr, string fieldName) where T : unmanaged
        {
            if (addr == 0)
                throw new InvalidOperationException($"{fieldName}: address is 0x0");

            return Memory.ReadValue<T>(addr, false);
        }

        /// <summary>
        /// Wrapper for ReadPtrChain that asserts non-zero and logs context.
        /// </summary>
        private static ulong SafeReadPtrChain(string fieldName, ulong root, params uint[] chain)
        {
            var value = Memory.ReadPtrChain(root, false, chain);
            if (value == 0)
                throw new InvalidOperationException($"{fieldName}: ReadPtrChain returned 0 (bad chain / offsets)");

            return value;
        }

        public override void EnsureSkeletonInitialized()
        {
            if (Skeleton != null && SkeletonRoot != null && SkeletonRoot.TransformInternal != 0)
                return;

            var now = DateTime.UtcNow;
            if (now < NextSkeletonRetryUtc)
                return;

            NextSkeletonRetryUtc = now.AddMilliseconds(500);

            try
            {
                if (SkeletonRoot == null || SkeletonRoot.TransformInternal == 0)
                {
                    var ti = SafeReadPtrChain("[ObservedPlayer] TransformInternal(retry)", this, _transformInternalChain);
                    SkeletonRoot = new UnityTransform(ti);
                    _ = SkeletonRoot.UpdatePosition();
                }

                if (Skeleton == null && SkeletonRoot.TransformInternal != 0)
                {
                    Skeleton = new Skeleton(this, SkeletonRoot.TransformInternal);
                    Debug.WriteLine($"[Skeleton] Reinitialized for ObservedPlayer '{Name}'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Skeleton] Reinit failed for ObservedPlayer '{Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Get Player's Account ID.
        /// </summary>
        /// <returns>Account ID Numeric String.</returns>
        private string GetAccountID()
        {
            if (!IsHuman)
                return "AI";
            var idPTR = Memory.ReadPtr(this + Offsets.ObservedPlayerView.AccountId);
            return Memory.ReadUnicodeString(idPTR);
        }

        /// <summary>
        /// Gets player's Group Number.
        /// </summary>
        private int GetGroupNumber()
        {
            try
            {
                var groupIdPtr = Memory.ReadPtr(this + Offsets.ObservedPlayerView.GroupID);
                string groupId = Memory.ReadUnicodeString(groupIdPtr);
                return _groups.GetOrAdd(
                    groupId,
                    _ => Interlocked.Increment(ref _lastGroupNumber));
            }
            catch { return -1; } // will return null if Solo / Don't have a team
        }

        /// <summary>
        /// Get Movement Context Instance.
        /// </summary>
        private ulong GetMovementContext()
        {
            var movementController = Memory.ReadPtrChain(ObservedPlayerController, true, Offsets.ObservedPlayerController.MovementController, Offsets.ObservedMovementController.ObservedPlayerStateContext);
            return movementController;
        }

        /// <summary>
        /// Refresh Player Information.
        /// </summary>
        public override void OnRegRefresh(VmmScatter scatter, ISet<ulong> registered, bool? isActiveParam = null)
        {
            if (isActiveParam is not bool isActive)
                isActive = registered.Contains(this);

            if (isActive)
            {
                UpdateHealthStatus();
                EnsureSkeletonInitialized(); // <── keep skeleton up to date here
            }

            base.OnRegRefresh(scatter, registered, isActive);
        }


        /// <summary>
        /// Get Player's Updated Health Condition
        /// Only works in Online Mode.
        /// </summary>
        private void UpdateHealthStatus()
        {
            try
            {
                var tag = (Enums.ETagStatus)Memory.ReadValue<int>(ObservedHealthController + Offsets.ObservedHealthController.HealthStatus);
                if ((tag & Enums.ETagStatus.Dying) == Enums.ETagStatus.Dying)
                    HealthStatus = Enums.ETagStatus.Dying;
                else if ((tag & Enums.ETagStatus.BadlyInjured) == Enums.ETagStatus.BadlyInjured)
                    HealthStatus = Enums.ETagStatus.BadlyInjured;
                else if ((tag & Enums.ETagStatus.Injured) == Enums.ETagStatus.Injured)
                    HealthStatus = Enums.ETagStatus.Injured;
                else
                    HealthStatus = Enums.ETagStatus.Healthy;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR updating Health Status for '{Name}': {ex}");
            }
        }

        /// <summary>
        /// Get the Transform Internal Chain for this Player.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void GetTransformInternalChain(Bones bone, Span<uint> offsets)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(offsets.Length, AbstractPlayer.TransformInternalChainCount, nameof(offsets));
            offsets[0] = Offsets.ObservedPlayerView.PlayerBody;
            offsets[1] = Offsets.PlayerBody.SkeletonRootJoint;
            offsets[2] = Offsets.DizSkinningSkeleton._values;
            offsets[3] = MonoList<byte>.ArrOffset;
            offsets[4] = MonoList<byte>.ArrStartOffset + (uint)bone * 0x8;
            offsets[5] = 0x10;
        }

        private static readonly uint[] _transformInternalChain =
        [
            Offsets.ObservedPlayerView.PlayerBody,
            Offsets.PlayerBody.SkeletonRootJoint,
            Offsets.DizSkinningSkeleton._values,
            MonoList<byte>.ArrOffset,
            MonoList<byte>.ArrStartOffset + (uint)Bones.HumanBase * 0x8,
            0x10
        ];
    }
}