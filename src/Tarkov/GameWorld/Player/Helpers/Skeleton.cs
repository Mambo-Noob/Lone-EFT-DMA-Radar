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

using LoneEftDmaRadar.Tarkov.GameWorld.Camera;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using static LoneEftDmaRadar.Tarkov.Unity.Structures.UnityTransform;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers
{
    /// <summary>
    /// Contains abstractions for drawing Player Skeletons.
    /// </summary>
    public sealed class Skeleton
    {
        private const int JOINTS_COUNT = 26;
        private static readonly SKPoint[] _espWidgetBuffer = new SKPoint[JOINTS_COUNT];
        
        /// <summary>
        /// All Skeleton Bones.
        /// </summary>
        public static ReadOnlyMemory<Bones> AllSkeletonBones { get; } = Enum.GetValues<SkeletonBones>().Cast<Bones>().ToArray();
        
        /// <summary>
        /// Maximum number of vertices needed to update all bones.
        /// </summary>
        public int MaxVertexCount => _bones.Values.Max(b => b.Count);
        
        private readonly Dictionary<Bones, UnityTransform> _bones;
        private readonly AbstractPlayer _player;
        private TrsX[] _cachedVertices; // Shared vertices buffer for all bones

        /// <summary>
        /// Skeleton Root Transform.
        /// </summary>
        public UnityTransform Root { get; private set; }

        /// <summary>
        /// All Transforms for this Skeleton (including Root).
        /// </summary>
        public IReadOnlyDictionary<Bones, UnityTransform> BoneTransforms => _bones;
        /// <summary>
        /// Maximum bone index across all bones in skeleton
        /// </summary>
        public int MaxBoneIndex
        {
            get
            {
                if (_bones.Count == 0) return 0;
                return _bones.Values.Max(b => b.Count - 1); // Count - 1 because Count is length, we want max index
            }
        }
        public Skeleton(AbstractPlayer player, ulong transformInternal)
        {
            _player = player;

            // Create root transform
            try
            {
                Root = new UnityTransform(transformInternal);
                _ = Root.UpdatePosition();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR creating root transform for player '{_player.Name}': {ex}");
                throw;
            }

            // Allocate dictionary for all bones
            var bones = new Dictionary<Bones, UnityTransform>(AllSkeletonBones.Length + 1)
            {
                [Unity.Structures.Bones.HumanBase] = Root
            };

            // Allocate all skeleton bones needed for ESP
            Span<uint> tiOffsets = stackalloc uint[AbstractPlayer.TransformInternalChainCount];
            foreach (var bone in AllSkeletonBones.Span)
            {
                try
                {
                    _player.GetTransformInternalChain(bone, tiOffsets);
                    var tiBone = Memory.ReadPtrChain(player.Base, true, tiOffsets);
                    bones[bone] = new UnityTransform(tiBone);
                    Debug.WriteLine($"Allocated bone {bone} at 0x{tiBone:X} for player '{_player.Name}'");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ERROR allocating bone {bone} for player '{_player.Name}': {ex}");
                    // Don't throw - continue with other bones
                }
            }

            _bones = bones;
        }

        /// <summary>
        /// Reset the Transform for this player.
        /// </summary>
        /// <param name="bone"></param>
        public void ResetTransform(Bones bone)
        {
            try
            {
                Debug.WriteLine($"Attempting to get new {bone} Transform for Player '{_player.Name}'...");
                var transform = new UnityTransform(_bones[bone].TransformInternal);
                _bones[bone] = transform;
                if ((bone is Unity.Structures.Bones.HumanBase))
                    Root = transform;
                Debug.WriteLine($"[OK] New {bone} Transform for Player '{_player.Name}'");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR resetting transform for bone {bone} on player '{_player.Name}': {ex}");
            }
        }

        /// <summary>
        /// Cache the shared vertices buffer (all bones in skeleton use the same buffer)
        /// </summary>
        public void CacheVertices(ReadOnlySpan<TrsX> vertices)
        {
            if (_cachedVertices == null || _cachedVertices.Length != vertices.Length)
            {
                _cachedVertices = new TrsX[vertices.Length];
            }
            
            vertices.CopyTo(_cachedVertices);
        }

        /// <summary>
        /// Updates the static ESP Widget Buffer with the current Skeleton Bone Screen Coordinates.<br />
        /// See <see cref="Skeleton._espWidgetBuffer"/><br />
        /// NOT THREAD SAFE!
        /// </summary>
        /// <param name="scaleX">X Scale Factor.</param>
        /// <param name="scaleY">Y Scale Factor.</param>
        /// <returns>True if successful, otherwise False.</returns>
public bool UpdateESPWidgetBuffer(float scaleX, float scaleY, out SKPoint[] buffer)
{
    buffer = default;
    
    // ✅ FIX: Check if vertices have been cached yet
    if (_cachedVertices == null || _cachedVertices.Length == 0)
    {
        // Silently fail - this is normal on first few frames
        return false;
    }
    
    try
    {
        // Check if all required bones exist
        if (!_bones.ContainsKey(Unity.Structures.Bones.HumanSpine2) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanHead) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanNeck) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanLCollarbone) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanRCollarbone) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanLPalm) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanRPalm) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanSpine3) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanSpine1) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanPelvis) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanLFoot) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanRFoot) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanLThigh2) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanRThigh2) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanLForearm2) ||
            !_bones.ContainsKey(Unity.Structures.Bones.HumanRForearm2))
        {
            return false;
        }

        // Update each bone's position using the shared cached vertices buffer
        foreach (var kvp in _bones.Values)
        {
            try
            {
                // ✅ FIX: Validate bone index before accessing cached vertices
                if (kvp.Count > _cachedVertices.Length)
                {
                    Debug.WriteLine($"ERROR: Bone index {kvp.Count} exceeds cached vertices length {_cachedVertices.Length} for '{_player.Name}'");
                    return false;
                }
                
                // All bones use the SAME vertices buffer with their own index
                _ = kvp.UpdatePosition(_cachedVertices);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR updating bone position for '{_player.Name}': {ex}");
                return false;
            }
        }

        // Get bone positions for WorldToScreen
        ref readonly var spine2Pos = ref _bones[Unity.Structures.Bones.HumanSpine2].Position;
        ref readonly var headPos = ref _bones[Unity.Structures.Bones.HumanHead].Position;
        ref readonly var neckPos = ref _bones[Unity.Structures.Bones.HumanNeck].Position;
        ref readonly var leftCollarPos = ref _bones[Unity.Structures.Bones.HumanLCollarbone].Position;
        ref readonly var rightCollarPos = ref _bones[Unity.Structures.Bones.HumanRCollarbone].Position;
        ref readonly var leftHandPos = ref _bones[Unity.Structures.Bones.HumanLPalm].Position;
        ref readonly var rightHandPos = ref _bones[Unity.Structures.Bones.HumanRPalm].Position;
        ref readonly var upperTorsoPos = ref _bones[Unity.Structures.Bones.HumanSpine3].Position;
        ref readonly var lowerTorsoPos = ref _bones[Unity.Structures.Bones.HumanSpine1].Position;
        ref readonly var pelvisPos = ref _bones[Unity.Structures.Bones.HumanPelvis].Position;
        ref readonly var leftFootPos = ref _bones[Unity.Structures.Bones.HumanLFoot].Position;
        ref readonly var rightFootPos = ref _bones[Unity.Structures.Bones.HumanRFoot].Position;
        ref readonly var leftKneePos = ref _bones[Unity.Structures.Bones.HumanLThigh2].Position;
        ref readonly var rightKneePos = ref _bones[Unity.Structures.Bones.HumanRThigh2].Position;
        ref readonly var leftElbowPos = ref _bones[Unity.Structures.Bones.HumanLForearm2].Position;
        ref readonly var rightElbowPos = ref _bones[Unity.Structures.Bones.HumanRForearm2].Position;

        // WorldToScreen all bones
        if (!CameraManager.WorldToScreen(in spine2Pos, out var midTorsoScreen, true, true))
            return false;
        if (!CameraManager.WorldToScreen(in headPos, out var headScreen))
            return false;
        if (!CameraManager.WorldToScreen(in neckPos, out var neckScreen))
            return false;
        if (!CameraManager.WorldToScreen(in leftCollarPos, out var leftCollarScreen))
            return false;
        if (!CameraManager.WorldToScreen(in rightCollarPos, out var rightCollarScreen))
            return false;
        if (!CameraManager.WorldToScreen(in leftHandPos, out var leftHandScreen))
            return false;
        if (!CameraManager.WorldToScreen(in rightHandPos, out var rightHandScreen))
            return false;
        if (!CameraManager.WorldToScreen(in upperTorsoPos, out var upperTorsoScreen))
            return false;
        if (!CameraManager.WorldToScreen(in lowerTorsoPos, out var lowerTorsoScreen))
            return false;
        if (!CameraManager.WorldToScreen(in pelvisPos, out var pelvisScreen))
            return false;
        if (!CameraManager.WorldToScreen(in leftFootPos, out var leftFootScreen))
            return false;
        if (!CameraManager.WorldToScreen(in rightFootPos, out var rightFootScreen))
            return false;
        if (!CameraManager.WorldToScreen(in leftKneePos, out var leftKneeScreen))
            return false;
        if (!CameraManager.WorldToScreen(in rightKneePos, out var rightKneeScreen))
            return false;
        if (!CameraManager.WorldToScreen(in leftElbowPos, out var leftElbowScreen))
            return false;
        if (!CameraManager.WorldToScreen(in rightElbowPos, out var rightElbowScreen))
            return false;
        
        // Build line buffer
        int index = 0;
        // Head to left foot
        ScaleAimviewPoint(headScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(neckScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(neckScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(upperTorsoScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(upperTorsoScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(midTorsoScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(midTorsoScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(lowerTorsoScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(lowerTorsoScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(pelvisScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(pelvisScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(leftKneeScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(leftKneeScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(leftFootScreen, ref _espWidgetBuffer[index++]);
        // Pelvis to right foot
        ScaleAimviewPoint(pelvisScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(rightKneeScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(rightKneeScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(rightFootScreen, ref _espWidgetBuffer[index++]);
        // Left collar to left hand
        ScaleAimviewPoint(leftCollarScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(leftElbowScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(leftElbowScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(leftHandScreen, ref _espWidgetBuffer[index++]);
        // Right collar to right hand
        ScaleAimviewPoint(rightCollarScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(rightElbowScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(rightElbowScreen, ref _espWidgetBuffer[index++]);
        ScaleAimviewPoint(rightHandScreen, ref _espWidgetBuffer[index++]);
        
        buffer = _espWidgetBuffer;
        return true;

        void ScaleAimviewPoint(SKPoint original, ref SKPoint result)
        {
            result.X = original.X * scaleX;
            result.Y = original.Y * scaleY;
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"ERROR updating ESP widget buffer for player '{_player.Name}': {ex}");
        return false;
    }
}

        /// <summary>
        /// All Skeleton Bones for ESP Drawing.
        /// </summary>
        public enum SkeletonBones : uint
        {
            Head = Unity.Structures.Bones.HumanHead,
            Neck = Unity.Structures.Bones.HumanNeck,
            UpperTorso = Unity.Structures.Bones.HumanSpine3,
            MidTorso = Unity.Structures.Bones.HumanSpine2,
            LowerTorso = Unity.Structures.Bones.HumanSpine1,
            LeftShoulder = Unity.Structures.Bones.HumanLCollarbone,
            RightShoulder = Unity.Structures.Bones.HumanRCollarbone,
            LeftElbow = Unity.Structures.Bones.HumanLForearm2,
            RightElbow = Unity.Structures.Bones.HumanRForearm2,
            LeftHand = Unity.Structures.Bones.HumanLPalm,
            RightHand = Unity.Structures.Bones.HumanRPalm,
            Pelvis = Unity.Structures.Bones.HumanPelvis,
            LeftKnee = Unity.Structures.Bones.HumanLThigh2,
            RightKnee = Unity.Structures.Bones.HumanRThigh2,
            LeftFoot = Unity.Structures.Bones.HumanLFoot,
            RightFoot = Unity.Structures.Bones.HumanRFoot
        }
    }
}