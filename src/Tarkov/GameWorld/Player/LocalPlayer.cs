/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.Tarkov.Mono.Collections;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc;
using VmmSharpEx.Scatter;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Player
{
    public sealed class LocalPlayer : ClientPlayer
    {
        public static ulong HandsController { get; private set; }

        /// <summary>
        /// Firearm Manager for tracking weapon/ammo/ballistics.
        /// </summary>
        public FirearmManager FirearmManager { get; private set; }

        /// <summary>
        /// All Items on the Player's WishList.
        /// </summary>
        public static IReadOnlySet<string> WishlistItems => _wishlistItems;
        private static readonly HashSet<string> _wishlistItems = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Spawn Point.
        /// </summary>
        public string EntryPoint { get; }

        /// <summary>
        /// Profile ID (if Player Scav).
        /// Used for Exfils.
        /// </summary>
        public string ProfileId { get; }

        /// <summary>
        /// Player name.
        /// </summary>
        public override string Name
        {
            get => "localPlayer";
            set { }
        }

        /// <summary>
        /// Player is Human-Controlled.
        /// </summary>
        public override bool IsHuman { get; }

        public LocalPlayer(ulong playerBase) : base(playerBase)
        {
            string classType = ObjectClass.ReadName(this);
            if (!(classType == "LocalPlayer" || classType == "ClientPlayer"))
                throw new ArgumentOutOfRangeException(nameof(classType));

            IsHuman = true;
            
            // Initialize FirearmManager
            FirearmManager = new FirearmManager(this);

            if (IsPmc)
            {
                var entryPtr = Memory.ReadPtr(Info + Offsets.PlayerInfo.EntryPoint);
                EntryPoint = Memory.ReadUnicodeString(entryPtr);
            }
            else if (IsScav)
            {
                var profileIdPtr = Memory.ReadPtr(Profile + Offsets.Profile.Id);
                ProfileId = Memory.ReadUnicodeString(profileIdPtr);
            }
        }

        /// <summary>
        /// Update FirearmManager (call this in the realtime loop or update cycle).
        /// </summary>
        public void UpdateFirearmManager()
        {
            try
            {
                FirearmManager?.Update();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalPlayer] FirearmManager update failed: {ex}");
            }
        }

        /// <summary>
        /// Checks if LocalPlayer is Aiming (ADS).
        /// </summary>
        /// <returns>True if aiming (ADS), otherwise False.</returns>
        public bool CheckIfADS()
        {
            try
            {
                return Memory.ReadValue<bool>(PWA + Offsets.ProceduralWeaponAnimation._isAiming, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CheckIfADS() ERROR: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Extended realtime loop that includes FirearmManager updates.
        /// </summary>
        public override void OnRealtimeLoop(VmmScatter scatter)
        {
            base.OnRealtimeLoop(scatter);
            
            // Update hands controller
            scatter.PrepareReadPtr(HandsControllerAddr);
            scatter.Completed += (sender, s) =>
            {
                if (s.ReadPtr(HandsControllerAddr, out var hands))
                {
                    HandsController = hands;
                }
            };
        }
    }
}