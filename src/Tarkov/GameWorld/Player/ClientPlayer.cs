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
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.Mono.Collections;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using VmmSharpEx;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Player
{
    public class ClientPlayer : AbstractPlayer
    {
        /// <summary>
        /// EFT.Profile Address
        /// </summary>
        public ulong Profile { get; }
        /// <summary>
        /// ICharacterController
        /// </summary>
        public ulong CharacterController { get; }
        /// <summary>
        /// Procedural Weapon Animation
        /// </summary>
        public ulong PWA { get; }
        /// <summary>
        /// PlayerInfo Address (GClass1044)
        /// </summary>
        public ulong Info { get; }
        /// <summary>
        /// Player name.
        /// </summary>
        public override string Name { get; set; }
        /// <summary>
        /// Account UUID for Human Controlled Players.
        /// </summary>
        public override string AccountID { get; }
        /// <summary>
        /// Group that the player belongs to.
        /// </summary>
        public override int GroupID { get; protected set; } = -1;
        /// <summary>
        /// Player's Faction.
        /// </summary>
        public override Enums.EPlayerSide PlayerSide { get; protected set; }
        /// <summary>
        /// Player is Human-Controlled.
        /// </summary>
        public override bool IsHuman { get; }
        /// <summary>
        /// MovementContext / StateContext
        /// </summary>
        public override ulong MovementContext { get; }
        /// <summary>
        /// EFT.PlayerBody
        /// </summary>
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
        /// Corpse field address..
        /// </summary>
        public override ulong CorpseAddr { get; }
        /// <summary>
        /// Player Rotation Field Address (view angles).
        /// </summary>
        public override ulong RotationAddress { get; }

internal ClientPlayer(ulong playerBase) : base(playerBase)
{
    try
    {
        if (playerBase == 0)
            throw new InvalidOperationException("[ClientPlayer] playerBase is 0");

        playerBase.ThrowIfInvalidVirtualAddress(nameof(playerBase));

        // Profile / Info
        var profilePtrAddr = this + Offsets.Player.Profile;
        Profile = SafeReadPtr(profilePtrAddr, "[ClientPlayer] Profile");

        var infoPtrAddr = Profile + Offsets.Profile.Info;
        Info = SafeReadPtr(infoPtrAddr, "[ClientPlayer] Info");

        // Core components
        PWA  = SafeReadPtr(this + Offsets.Player.ProceduralWeaponAnimation, "[ClientPlayer] PWA");
        Body = SafeReadPtr(this + Offsets.Player._playerBody, "[ClientPlayer] Body");

        InventoryControllerAddr = this + Offsets.Player._inventoryController;
        HandsControllerAddr     = this + Offsets.Player._handsController;
        CorpseAddr              = this + Offsets.Player.Corpse;

        // Side
        var sideAddr = Info + Offsets.PlayerInfo.Side;
        PlayerSide = (Enums.EPlayerSide)SafeReadValue<int>(sideAddr, "[ClientPlayer] PlayerInfo.Side");
        if (!Enum.IsDefined(typeof(Enums.EPlayerSide), PlayerSide))
            throw new InvalidOperationException($"[ClientPlayer] Invalid PlayerSide value: {PlayerSide}");

        // Group / movement / rotation
        GroupID        = GetGroupNumber();
        MovementContext = GetMovementContext();
        RotationAddress = ValidateRotationAddr(MovementContext + Offsets.MovementContext._rotation);

        // Transform (CRITICAL)
        var ti = SafeReadPtrChain("[ClientPlayer] TransformInternal", this, _transformInternalChain);
        SkeletonRoot = new UnityTransform(ti);
        _ = SkeletonRoot.UpdatePosition();

        // Skeleton (optional)
        try
        {
            Skeleton = new Skeleton(this, SkeletonRoot.TransformInternal);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WARNING: Failed to initialize Skeleton for ClientPlayer (ESP will be unavailable): {ex}");
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[ClientPlayer] FAILED to initialize: {ex}");
        throw; // Let caller decide to skip/abort, but with good logs and no VmmException
    }
}
private const ulong MAX_USER_VA = 0x7FFF_FFFF_FFFF;

private static ulong SafeReadPtr(ulong addr, string fieldName)
{
    // 1) Do NOT call MemDMA if the address itself is bad
    if (addr == 0 || addr > MAX_USER_VA)
    {
        Debug.WriteLine($"{fieldName}: source address invalid 0x{addr:X}");
        throw new InvalidOperationException($"{fieldName}: source address invalid 0x{addr:X}");
    }

    try
    {
        var value = Memory.ReadPtr(addr, false);

        // 2) Validate the *result* as well so we don't propagate 0 / garbage
        if (value == 0 || value > MAX_USER_VA)
        {
            Debug.WriteLine($"{fieldName}: read invalid pointer 0x{value:X} from 0x{addr:X}");
            throw new InvalidOperationException(
                $"{fieldName}: read invalid pointer 0x{value:X} from 0x{addr:X}");
        }

        return value;
    }
    catch (VmmException vex)
    {
        Debug.WriteLine($"{fieldName}: VMM ReadPtr failed at 0x{addr:X}: {vex}");
        // Re-throw as normal .NET exception so outer code just sees InvalidOperation
        throw new InvalidOperationException(
            $"{fieldName}: VMM ReadPtr failed at 0x{addr:X}", vex);
    }
}

private static T SafeReadValue<T>(ulong addr, string fieldName) where T : unmanaged
{
    if (addr == 0 || addr > MAX_USER_VA)
    {
        Debug.WriteLine($"{fieldName}: source address invalid 0x{addr:X}");
        throw new InvalidOperationException($"{fieldName}: source address invalid 0x{addr:X}");
    }

    try
    {
        return Memory.ReadValue<T>(addr, false);
    }
    catch (VmmException vex)
    {
        Debug.WriteLine($"{fieldName}: VMM ReadValue<{typeof(T).Name}> failed at 0x{addr:X}: {vex}");
        throw new InvalidOperationException(
            $"{fieldName}: VMM ReadValue<{typeof(T).Name}> failed at 0x{addr:X}", vex);
    }
}

private static ulong SafeReadPtrChain(string fieldName, ulong root, params uint[] chain)
{
    if (root == 0 || root > MAX_USER_VA)
    {
        Debug.WriteLine($"{fieldName}: root invalid 0x{root:X}");
        throw new InvalidOperationException($"{fieldName}: root invalid 0x{root:X}");
    }

    try
    {
        var value = Memory.ReadPtrChain(root, false, chain);
        if (value == 0 || value > MAX_USER_VA)
        {
            Debug.WriteLine($"{fieldName}: ReadPtrChain returned invalid 0x{value:X} (root=0x{root:X})");
            throw new InvalidOperationException(
                $"{fieldName}: ReadPtrChain returned invalid 0x{value:X} (root=0x{root:X})");
        }
        return value;
    }
    catch (VmmException vex)
    {
        Debug.WriteLine($"{fieldName}: VMM ReadPtrChain failed (root=0x{root:X}): {vex}");
        throw new InvalidOperationException(
            $"{fieldName}: VMM ReadPtrChain failed (root=0x{root:X})", vex);
    }
}

        /// <summary>
        /// Gets player's Group Number.
        /// </summary>
        private int GetGroupNumber()
        {
            try
            {
                var groupIdPtr = Memory.ReadPtr(Info + Offsets.PlayerInfo.GroupId);
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
            var movementContext = Memory.ReadPtr(this + Offsets.Player.MovementContext);
            var player = Memory.ReadPtr(movementContext + Offsets.MovementContext.Player, false);
            if (player != this)
                throw new ArgumentOutOfRangeException(nameof(movementContext));
            return movementContext;
        }

        /// <summary>
        /// Get the Transform Internal Chain for this Player.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal override void GetTransformInternalChain(Bones bone, Span<uint> offsets)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(offsets.Length, AbstractPlayer.TransformInternalChainCount, nameof(offsets));
            offsets[0] = Offsets.Player._playerBody;
            offsets[1] = Offsets.PlayerBody.SkeletonRootJoint;
            offsets[2] = Offsets.DizSkinningSkeleton._values;
            offsets[3] = MonoList<byte>.ArrOffset;
            offsets[4] = MonoList<byte>.ArrStartOffset + (uint)bone * 0x8;
            offsets[5] = 0x10;
        }

        private static readonly uint[] _transformInternalChain =
        [
            Offsets.Player._playerBody,
            Offsets.PlayerBody.SkeletonRootJoint,
            Offsets.DizSkinningSkeleton._values,
            MonoList<byte>.ArrOffset,
            MonoList<byte>.ArrStartOffset + (uint)Bones.HumanBase * 0x8,
            0x10
        ];
    }
}