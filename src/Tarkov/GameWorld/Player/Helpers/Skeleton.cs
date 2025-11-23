/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * MIT License - Copyright (c) 2025 Lone DMA
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
        private bool _verticesCachedAtLeastOnce; // Track if we've cached valid data
        private int _consecutiveFailures; // Track consecutive ESP draw failures
        private const int MAX_FAILURES_BEFORE_RESET = 10; // Reset after this many failures

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
                return _bones.Values.Max(b => b.Count - 1);
            }
        }

        /// <summary>
        /// Indicates if skeleton has been properly initialized with valid vertex data
        /// </summary>
        public bool IsInitialized => _verticesCachedAtLeastOnce && ValidateCachedVertices();

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
            _verticesCachedAtLeastOnce = false;
            _consecutiveFailures = 0;
        }

        /// <summary>
        /// Reset the entire skeleton if it appears corrupted or invalid
        /// </summary>
        public void ResetSkeleton()
        {
            try
            {
                //Debug.WriteLine($"[Skeleton] Resetting entire skeleton for player '{_player.Name}'");
                
                // Clear cached vertices
                _cachedVertices = null;
                _verticesCachedAtLeastOnce = false;
                _consecutiveFailures = 0;

                // Reset all bone transforms
                Span<uint> tiOffsets = stackalloc uint[AbstractPlayer.TransformInternalChainCount];
                
                // Reset root first
                Root = new UnityTransform(Root.TransformInternal);
                _bones[Unity.Structures.Bones.HumanBase] = Root;
                
                // Reset all other bones
                foreach (var bone in AllSkeletonBones.Span)
                {
                    try
                    {
                        _player.GetTransformInternalChain(bone, tiOffsets);
                        var tiBone = Memory.ReadPtrChain(_player.Base, true, tiOffsets);
                        _bones[bone] = new UnityTransform(tiBone);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ERROR resetting bone {bone} for player '{_player.Name}': {ex}");
                    }
                }
                
                //Debug.WriteLine($"[Skeleton] Reset complete for player '{_player.Name}'");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in ResetSkeleton for player '{_player.Name}': {ex}");
            }
        }

        /// <summary>
        /// Reset a specific Transform for this player.
        /// </summary>
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
        /// Validate that cached vertices contain reasonable data
        /// </summary>
        private bool ValidateCachedVertices()
        {
            if (_cachedVertices == null || _cachedVertices.Length == 0)
                return false;

            // Sample a few vertices to check for validity
            for (int i = 0; i < Math.Min(5, _cachedVertices.Length); i++)
            {
                var vertex = _cachedVertices[i];
                var pos = vertex.t; // Use 't' field for translation/position
                
                // Check for NaN/Infinity
                if (float.IsNaN(pos.X) || float.IsNaN(pos.Y) || float.IsNaN(pos.Z) ||
                    float.IsInfinity(pos.X) || float.IsInfinity(pos.Y) || float.IsInfinity(pos.Z))
                {
                    return false; // Don't log every check, just return false
                }
                
                // Check for unreasonable world coordinates (way outside normal game bounds)
                if (Math.Abs(pos.X) > 10000 || Math.Abs(pos.Y) > 10000 || Math.Abs(pos.Z) > 10000)
                {
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// Cache the shared vertices buffer (all bones in skeleton use the same buffer)
        /// Validates the data before caching
        /// </summary>
        public bool CacheVertices(ReadOnlySpan<TrsX> vertices)
        {
            if (vertices.Length == 0)
            {
                return false;
            }

            // Validate vertex data before caching
            bool hasValidData = false;
            for (int i = 0; i < Math.Min(5, vertices.Length); i++)
            {
                var pos = vertices[i].t; // Use 't' field for translation/position
                if (!float.IsNaN(pos.X) && !float.IsNaN(pos.Y) && !float.IsNaN(pos.Z) &&
                    !float.IsInfinity(pos.X) && !float.IsInfinity(pos.Y) && !float.IsInfinity(pos.Z) &&
                    Math.Abs(pos.X) < 10000 && Math.Abs(pos.Y) < 10000 && Math.Abs(pos.Z) < 10000)
                {
                    hasValidData = true;
                    break;
                }
            }

            if (!hasValidData)
            {
                return false; // Invalid data, don't cache
            }

            // Allocate or resize buffer if needed
            if (_cachedVertices == null || _cachedVertices.Length != vertices.Length)
            {
                _cachedVertices = new TrsX[vertices.Length];
                //Debug.WriteLine($"[Skeleton] Allocated vertex cache ({vertices.Length} vertices) for '{_player.Name}'");
            }
            
            vertices.CopyTo(_cachedVertices);
            
            if (!_verticesCachedAtLeastOnce)
            {
                _verticesCachedAtLeastOnce = true;
                //Debug.WriteLine($"[Skeleton] First valid vertex cache for '{_player.Name}'");
            }
            
            return true;
        }

        /// <summary>
        /// Updates the static ESP Widget Buffer with the current Skeleton Bone Screen Coordinates.
        /// Includes validation and auto-reset on persistent failures.
        /// </summary>
        public bool UpdateESPWidgetBuffer(float scaleX, float scaleY, out SKPoint[] buffer)
        {
            buffer = default;
            
            // Check if skeleton is initialized with valid data
            if (!IsInitialized)
            {
                // Silently fail - this is normal during early game load
                return false;
            }
            
            try
            {
                // Verify all required bones exist
                var requiredBones = new[]
                {
                    Unity.Structures.Bones.HumanSpine2,
                    Unity.Structures.Bones.HumanHead,
                    Unity.Structures.Bones.HumanNeck,
                    Unity.Structures.Bones.HumanLCollarbone,
                    Unity.Structures.Bones.HumanRCollarbone,
                    Unity.Structures.Bones.HumanLPalm,
                    Unity.Structures.Bones.HumanRPalm,
                    Unity.Structures.Bones.HumanSpine3,
                    Unity.Structures.Bones.HumanSpine1,
                    Unity.Structures.Bones.HumanPelvis,
                    Unity.Structures.Bones.HumanLFoot,
                    Unity.Structures.Bones.HumanRFoot,
                    Unity.Structures.Bones.HumanLThigh2,
                    Unity.Structures.Bones.HumanRThigh2,
                    Unity.Structures.Bones.HumanLForearm2,
                    Unity.Structures.Bones.HumanRForearm2
                };

                foreach (var bone in requiredBones)
                {
                    if (!_bones.ContainsKey(bone))
                    {
                        return false;
                    }
                }

                // Update each bone's position using the shared cached vertices buffer
                foreach (var kvp in _bones.Values)
                {
                    try
                    {
                        // Validate bone index before accessing cached vertices
                        if (kvp.Count > _cachedVertices.Length)
                        {
                            _consecutiveFailures++;
                            CheckAndResetIfNeeded();
                            return false;
                        }
                        
                        // Update position with cached vertices
                        // UpdatePosition returns ref Vector3, so we just call it
                        _ = kvp.UpdatePosition(_cachedVertices);
                        
                        // Validate the resulting position
                        var pos = kvp.Position;
                        if (float.IsNaN(pos.X) || float.IsNaN(pos.Y) || float.IsNaN(pos.Z) ||
                            float.IsInfinity(pos.X) || float.IsInfinity(pos.Y) || float.IsInfinity(pos.Z))
                        {
                            _consecutiveFailures++;
                            CheckAndResetIfNeeded();
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        //Debug.WriteLine($"[Skeleton] ERROR updating bone position for '{_player.Name}': {ex}");
                        _consecutiveFailures++;
                        CheckAndResetIfNeeded();
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
                {
                    _consecutiveFailures++;
                    CheckAndResetIfNeeded();
                    return false;
                }
                if (!CameraManager.WorldToScreen(in headPos, out var headScreen))
                {
                    _consecutiveFailures++;
                    CheckAndResetIfNeeded();
                    return false;
                }
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
                
                // Reset failure counter on success
                _consecutiveFailures = 0;
                return true;

                void ScaleAimviewPoint(SKPoint original, ref SKPoint result)
                {
                    result.X = original.X * scaleX;
                    result.Y = original.Y * scaleY;
                }
            }
            catch (Exception ex)
            {
                //Debug.WriteLine($"[Skeleton] ERROR updating ESP widget buffer for player '{_player.Name}': {ex}");
                _consecutiveFailures++;
                CheckAndResetIfNeeded();
                return false;
            }
        }

        /// <summary>
        /// Check if we need to reset the skeleton due to persistent failures
        /// </summary>
        private void CheckAndResetIfNeeded()
        {
            if (_consecutiveFailures >= MAX_FAILURES_BEFORE_RESET)
            {
                //Debug.WriteLine($"[Skeleton] {_consecutiveFailures} consecutive failures for '{_player.Name}', triggering reset");
                ResetSkeleton();
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