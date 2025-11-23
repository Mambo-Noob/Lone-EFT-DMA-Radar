/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Misc;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.Mono.Collections;
using LoneEftDmaRadar.Tarkov.Unity;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using SkiaSharp;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using VmmSharpEx;
using VmmSharpEx.Scatter;

namespace LoneEftDmaRadar.Tarkov.GameWorld.Camera
{
    public sealed class CameraManager
    {
        public ulong FPSCamera { get; }
        public ulong OpticCamera { get; }

        // Static debug copies
        public static ulong FPSCameraPtr { get; private set; }
        public static ulong OpticCameraPtr { get; private set; }
        public static ulong ActiveCameraPtr { get; private set; }

        // Matrix address pointers (via chain: GameObject+0x48→+0x18)
        private ulong _fpsMatrixAddress;
        private ulong _opticMatrixAddress;

        private bool OpticCameraActive =>
            Memory.ReadValue<bool>(OpticCamera + UnitySDK.UnityOffsets.MonoBehaviour_IsAddedOffset, false);

public CameraManager()
{
    try
    {
        Debug.WriteLine("=== CameraManager Initialization ===");
        Debug.WriteLine($"Unity Base: 0x{Memory.UnityBase:X}");
        Debug.WriteLine($"AllCameras Offset: 0x{UnitySDK.UnityOffsets.AllCameras:X}");

        // Calculate AllCameras address
        var allCamerasAddr = Memory.UnityBase + UnitySDK.UnityOffsets.AllCameras;
        Debug.WriteLine($"AllCameras Address: 0x{allCamerasAddr:X}");

        // Read the AllCameras pointer
        var allCamerasPtr = Memory.ReadPtr(allCamerasAddr, false);
        Debug.WriteLine($"AllCameras Ptr: 0x{allCamerasPtr:X}");

        if (allCamerasPtr == 0)
        {
            Debug.WriteLine("⚠️ CRITICAL: AllCameras pointer is NULL!");
            Debug.WriteLine("This means the AllCameras offset is likely wrong.");
            throw new InvalidOperationException("AllCameras pointer is NULL - offset may be outdated");
        }

        if (allCamerasPtr > 0x7FFFFFFFFFFF)
        {
            Debug.WriteLine($"⚠️ CRITICAL: AllCameras pointer is invalid: 0x{allCamerasPtr:X}");
            throw new InvalidOperationException($"Invalid AllCameras pointer: 0x{allCamerasPtr:X}");
        }

        // AllCameras is a List<Camera*>
        // Structure: +0x0 = items array pointer, +0x8 = count (int)
        var listItemsPtr = Memory.ReadPtr(allCamerasPtr + 0x0, false);
        var count = Memory.ReadValue<int>(allCamerasPtr + 0x8, false);

        Debug.WriteLine($"\nList Structure:");
        Debug.WriteLine($"  Items Pointer: 0x{listItemsPtr:X}");
        Debug.WriteLine($"  Count: {count}");

        if (listItemsPtr == 0)
        {
            Debug.WriteLine("⚠️ CRITICAL: List items pointer is NULL!");
            throw new InvalidOperationException("Camera list items pointer is NULL");
        }

        if (count <= 0)
        {
            Debug.WriteLine("⚠️ CRITICAL: Camera count is 0 or negative!");
            Debug.WriteLine("This usually means you're not in a raid yet.");
            throw new InvalidOperationException($"No cameras in list (count: {count})");
        }

        if (count > 100)
        {
            Debug.WriteLine($"⚠️ WARNING: Camera count seems high: {count}");
            Debug.WriteLine("This might indicate memory corruption or wrong structure.");
        }

        var (fps, optic) = FindCamerasByName(listItemsPtr, count);

        if (fps == 0 || optic == 0)
        {
            Debug.WriteLine("\n⚠️ CRITICAL: Could not find required cameras!");
            throw new InvalidOperationException(
                $"Could not find cameras. FPS: {(fps != 0 ? "Found" : "Missing")}, " +
                $"Optic: {(optic != 0 ? "Found" : "Missing")}");
        }

        FPSCamera = fps;
        OpticCamera = optic;

        Debug.WriteLine("\n=== Getting Matrix Addresses ===");
        _fpsMatrixAddress = GetMatrixAddress(FPSCamera);
        _opticMatrixAddress = GetMatrixAddress(OpticCamera);

        FPSCameraPtr = fps;
        OpticCameraPtr = optic;
        ActiveCameraPtr = 0;

        Debug.WriteLine($"\n✓ FPS Camera: 0x{FPSCamera:X}");
        Debug.WriteLine($"  GameObject: 0x{Memory.ReadPtr(FPSCamera + 0x50, false):X}");
        Debug.WriteLine($"  Matrix Address: 0x{_fpsMatrixAddress:X}");
        
        Debug.WriteLine($"✓ Optic Camera: 0x{OpticCamera:X}");
        Debug.WriteLine($"  GameObject: 0x{Memory.ReadPtr(OpticCamera + 0x50, false):X}");
        Debug.WriteLine($"  Matrix Address: 0x{_opticMatrixAddress:X}");

        VerifyViewMatrix(_fpsMatrixAddress, "FPS");
        VerifyViewMatrix(_opticMatrixAddress, "Optic");

        Debug.WriteLine("=== CameraManager Initialization Complete ===\n");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"⚠️ CameraManager initialization failed: {ex}");
        throw;
    }
}

        private static ulong GetMatrixAddress(ulong cameraPtr)
        {
            // Camera + 0x48 → GameObject
            var gameObject = Memory.ReadPtr(cameraPtr + 0x50, false);
            if (gameObject == 0 || gameObject > 0x7FFFFFFFFFFF)
                throw new InvalidOperationException($"Invalid GameObject: 0x{gameObject:X}");

            // GameObject + 0x48 → Pointer1
            var ptr1 = Memory.ReadPtr(gameObject + 0x50, false);
            if (ptr1 == 0 || ptr1 > 0x7FFFFFFFFFFF)
                throw new InvalidOperationException($"Invalid Ptr1 (GameObject+0x50): 0x{ptr1:X}");

            // Pointer1 + 0x18 → matrixAddress
            var matrixAddress = Memory.ReadPtr(ptr1 + 0x18, false);
            if (matrixAddress == 0 || matrixAddress > 0x7FFFFFFFFFFF)
                throw new InvalidOperationException($"Invalid matrixAddress (Ptr1+0x18): 0x{matrixAddress:X}");

            return matrixAddress;
        }

        private static void VerifyViewMatrix(ulong matrixAddress, string name)
        {
            try
            {
                Debug.WriteLine($"\n{name} Matrix @ 0x{matrixAddress:X}:");

                // Read ViewMatrix at +0x118
                var vm = Memory.ReadValue<Matrix4x4>(matrixAddress + 0x118, false);

                // Check if normalized
                float rightMag = MathF.Sqrt(vm.M11 * vm.M11 + vm.M12 * vm.M12 + vm.M13 * vm.M13);
                float upMag = MathF.Sqrt(vm.M21 * vm.M21 + vm.M22 * vm.M22 + vm.M23 * vm.M23);
                float fwdMag = MathF.Sqrt(vm.M31 * vm.M31 + vm.M32 * vm.M32 + vm.M33 * vm.M33);

                Debug.WriteLine($"  M44: {vm.M44:F6}");
                Debug.WriteLine($"  Translation: ({vm.M41:F2}, {vm.M42:F2}, {vm.M43:F2})");
                Debug.WriteLine($"  Right mag: {rightMag:F4}, Up mag: {upMag:F4}, Fwd mag: {fwdMag:F4}");
                Debug.WriteLine($"  Right: ({vm.M11:F3}, {vm.M12:F3}, {vm.M13:F3})");
                Debug.WriteLine($"  Up: ({vm.M21:F3}, {vm.M22:F3}, {vm.M23:F3})");
                Debug.WriteLine($"  Forward: ({vm.M31:F3}, {vm.M32:F3}, {vm.M33:F3})");

                bool isValid = rightMag > 0.9f && rightMag < 1.1f &&
                               upMag > 0.9f && upMag < 1.1f &&
                               fwdMag > 0.9f && fwdMag < 1.1f &&
                               Math.Abs(vm.M44 - 1.0f) < 0.1f;

                Debug.WriteLine($"  ✓ Valid: {isValid}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR verifying ViewMatrix for {name}: {ex}");
            }
        }

private static (ulong fpsCamera, ulong opticCamera) FindCamerasByName(ulong listItemsPtr, int count)
{
    ulong fpsCamera = 0;
    ulong opticCamera = 0;

    Debug.WriteLine($"\n=== Searching for Cameras ===");
    Debug.WriteLine($"List Items Ptr: 0x{listItemsPtr:X}");
    Debug.WriteLine($"Camera Count: {count}");
    Debug.WriteLine($"Scanning cameras...\n");

    for (int i = 0; i < Math.Min(count, 100); i++)
    {
        try
        {
            // Each item in the array is a pointer (8 bytes)
            ulong cameraEntryAddr = listItemsPtr + (uint)(i * 0x8);
            var cameraPtr = Memory.ReadPtr(cameraEntryAddr, false);
            
            if (cameraPtr == 0 || cameraPtr > 0x7FFFFFFFFFFF)
            {
                Debug.WriteLine($"  [{i:D2}] Invalid camera pointer: 0x{cameraPtr:X}");
                continue;
            }

            // Camera+0x50 -> GameObject
            var gameObjectPtr = Memory.ReadPtr(cameraPtr + 0x50, false);
            if (gameObjectPtr == 0 || gameObjectPtr > 0x7FFFFFFFFFFF)
            {
                Debug.WriteLine($"  [{i:D2}] Camera 0x{cameraPtr:X} -> Invalid GameObject: 0x{gameObjectPtr:X}");
                continue;
            }

            // GameObject+0x78 -> Name string pointer
            var namePtr = Memory.ReadPtr(gameObjectPtr + UnitySDK.UnityOffsets.GameObject_NameOffset, false);
            if (namePtr == 0 || namePtr > 0x7FFFFFFFFFFF)
            {
                Debug.WriteLine($"  [{i:D2}] GameObject 0x{gameObjectPtr:X} -> Invalid name ptr: 0x{namePtr:X}");
                continue;
            }

            // Read the name string
            var name = Memory.ReadUtf8String(namePtr, 64, false);
            if (string.IsNullOrEmpty(name) || name.Length < 3)
            {
                Debug.WriteLine($"  [{i:D2}] Name pointer 0x{namePtr:X} -> Invalid/empty name");
                continue;
            }

            Debug.WriteLine($"  [{i:D2}] Camera: '{name}'");
            Debug.WriteLine($"       @ 0x{cameraPtr:X}");
            Debug.WriteLine($"       GameObject: 0x{gameObjectPtr:X}");

            // Check for FPS Camera
            bool isFPS = name.Contains("FPS", StringComparison.OrdinalIgnoreCase) &&
                        name.Contains("Camera", StringComparison.OrdinalIgnoreCase);
            
            // Check for Optic Camera  
            bool isOptic = (name.Contains("Optic", StringComparison.OrdinalIgnoreCase) ||
                           name.Contains("BaseOptic", StringComparison.OrdinalIgnoreCase)) &&
                          name.Contains("Camera", StringComparison.OrdinalIgnoreCase);

            if (isFPS)
            {
                fpsCamera = cameraPtr;
                Debug.WriteLine($"       ✓✓✓ MATCHED AS FPS CAMERA ✓✓✓");
            }
            
            if (isOptic)
            {
                opticCamera = cameraPtr;
                Debug.WriteLine($"       ✓✓✓ MATCHED AS OPTIC CAMERA ✓✓✓");
            }

            if (fpsCamera != 0 && opticCamera != 0)
            {
                Debug.WriteLine($"\n✓ Both cameras found! Stopping search at index {i}.");
                break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"  [{i:D2}] Exception: {ex.Message}");
        }
    }

    Debug.WriteLine($"\n=== Search Results ===");
    Debug.WriteLine($"  FPS Camera:   {(fpsCamera != 0 ? $"✓ Found @ 0x{fpsCamera:X}" : "✗ NOT FOUND")}");
    Debug.WriteLine($"  Optic Camera: {(opticCamera != 0 ? $"✓ Found @ 0x{opticCamera:X}" : "✗ NOT FOUND")}");

    return (fpsCamera, opticCamera);
}

        static CameraManager()
        {
            MemDMA.ProcessStarting += MemDMA_ProcessStarting;
            MemDMA.ProcessStopped += MemDMA_ProcessStopped;
        }

        private static void MemDMA_ProcessStarting(object sender, EventArgs e) { }
        private static void MemDMA_ProcessStopped(object sender, EventArgs e) { }

        private bool CheckIfScoped(LocalPlayer localPlayer)
        {
            try
            {
                if (localPlayer is null || !OpticCameraActive)
                    return false;

                var opticsPtr = Memory.ReadPtr(localPlayer.PWA + Offsets.ProceduralWeaponAnimation._optics);
                using var optics = MonoList<VmmPointer>.Create(opticsPtr, true);

                if (optics.Count > 0)
                {
                    var pSightComponent = Memory.ReadPtr(optics[0] + Offsets.SightNBone.Mod);
                    var sightComponent = Memory.ReadValue<SightComponent>(pSightComponent);

                    if (sightComponent.ScopeZoomValue != 0f)
                        return sightComponent.ScopeZoomValue > 0f;

                    return sightComponent.GetZoomLevel() > 1f;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CheckIfScoped() ERROR: {ex}");
                return false;
            }
        }

public void OnRealtimeLoop(VmmScatter scatter, LocalPlayer localPlayer)
{
    try
    {
        IsADS = localPlayer?.CheckIfADS() ?? false;
        IsScoped = IsADS && CheckIfScoped(localPlayer);

        // Choose active matrix address
        ulong activeMatrixAddress = (IsADS && IsScoped) ? _opticMatrixAddress : _fpsMatrixAddress;
        ulong activeCamera = (IsADS && IsScoped) ? OpticCamera : FPSCamera;
        ActiveCameraPtr = activeCamera;

        // ✅ Prepare all reads (batched into single DMA operation)
        scatter.PrepareReadValue<Matrix4x4>(activeMatrixAddress + 0x120);
        scatter.PrepareReadValue<float>(FPSCamera + UnitySDK.UnityOffsets.Camera_FOVOffset);
        scatter.PrepareReadValue<float>(FPSCamera + UnitySDK.UnityOffsets.Camera_AspectRatioOffset);
        scatter.PrepareReadValue<float>(activeCamera + UnitySDK.UnityOffsets.Camera_ZoomLevelOffset);

        // ✅ FIX: Use Completed event instead of Execute() here!
        // This allows LocalGameWorld to Execute() once after all reads are prepared
        scatter.Completed += (sender, s) =>
        {
            try
            {
                // Read results when scatter completes
                if (s.ReadValue<Matrix4x4>(activeMatrixAddress + 0x120, out var vm))
                {
                    _viewMatrix.Update(ref vm);
                }

                if (s.ReadValue<float>(FPSCamera + UnitySDK.UnityOffsets.Camera_FOVOffset, out var fov))
                    _fov = fov;

                if (s.ReadValue<float>(FPSCamera + UnitySDK.UnityOffsets.Camera_AspectRatioOffset, out var aspect))
                    _aspect = aspect;

                if (s.ReadValue<float>(activeCamera + UnitySDK.UnityOffsets.Camera_ZoomLevelOffset, out var zoom))
                    _zoomLevel = zoom;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in CameraManager scatter callback: {ex}");
            }
        };
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"ERROR in CameraManager OnRealtimeLoop: {ex}");
    }
}

// Add field at top of class
private static float _zoomLevel = 1.0f;

// Add public property
public static float ZoomLevel => _zoomLevel;

        #region SightComponent Structures

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private readonly struct SightComponent
        {
            [FieldOffset((int)Offsets.SightComponent._template)]
            private readonly ulong pSightInterface;

            [FieldOffset((int)Offsets.SightComponent.ScopesSelectedModes)]
            private readonly ulong pScopeSelectedModes;

            [FieldOffset((int)Offsets.SightComponent.SelectedScope)]
            private readonly int SelectedScope;

            [FieldOffset((int)Offsets.SightComponent.ScopeZoomValue)]
            public readonly float ScopeZoomValue;

            public readonly float GetZoomLevel()
            {
                try
                {
                    using var zoomArray = SightInterface.Zooms;
                    if (SelectedScope >= zoomArray.Count || SelectedScope is < 0 or > 10)
                        return -1.0f;

                    using var selectedScopeModes = MonoArray<int>.Create(pScopeSelectedModes, false);
                    int selectedScopeMode = SelectedScope >= selectedScopeModes.Count ? 0 : selectedScopeModes[SelectedScope];
                    ulong zoomAddr = zoomArray[SelectedScope] + MonoArray<float>.ArrBaseOffset + (uint)selectedScopeMode * 0x4;

                    float zoomLevel = Memory.ReadValue<float>(zoomAddr, false);
                    if (zoomLevel.IsNormalOrZero() && zoomLevel is >= 0f and < 100f)
                        return zoomLevel;

                    return -1.0f;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ERROR in GetZoomLevel: {ex}");
                    return -1.0f;
                }
            }

            public readonly SightInterface SightInterface => Memory.ReadValue<SightInterface>(pSightInterface);
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private readonly struct SightInterface
        {
            [FieldOffset((int)Offsets.SightInterface.Zooms)]
            private readonly ulong pZooms;

            public readonly MonoArray<ulong> Zooms => MonoArray<ulong>.Create(pZooms, true);
        }

        #endregion

        #region Static Interfaces

        private const int VIEWPORT_TOLERANCE = 800;
        private static readonly Lock _viewportSync = new();

        public static bool EspRunning { get; set; }
        public static Rectangle Viewport { get; private set; }
        public static SKPoint ViewportCenter => new SKPoint(Viewport.Width / 2f, Viewport.Height / 2f);
        public static bool IsScoped { get; private set; }
        public static bool IsADS { get; private set; }

        private static float _fov;
        private static float _aspect;
        private static readonly ViewMatrix _viewMatrix = new();

public static void UpdateViewportRes()
{
    lock (_viewportSync)
    {
        // ✅ FIX: Use actual configured resolution instead of hardcoded 1080p
        var width = (int)App.Config.ESP.Resolution.Width;
        var height = (int)App.Config.ESP.Resolution.Height;
        
        // Fallback to 1080p if invalid
        if (width <= 0 || height <= 0)
        {
            width = 1920;
            height = 1080;
        }
        
        Viewport = new Rectangle(0, 0, width, height);
        Debug.WriteLine($"[CameraManager] Viewport updated to {width}x{height}");
    }
}

public static bool WorldToScreen(
    ref readonly Vector3 worldPos,
    out SKPoint scrPos,
    bool onScreenCheck = false,
    bool useTolerance = false)
{
    try
    {
        float w = Vector3.Dot(_viewMatrix.Translation, worldPos) + _viewMatrix.M44;

        if (w < 0.098f)
        {
            scrPos = default;
            return false;
        }

        float x = Vector3.Dot(_viewMatrix.Right, worldPos) + _viewMatrix.M14;
        float y = Vector3.Dot(_viewMatrix.Up, worldPos) + _viewMatrix.M24;

        // ✅ FIX: Only use FOV-based calculation when scoped, ignore zoom level
        if (IsScoped)
        {
            float angleRadHalf = (MathF.PI / 180f) * _fov * 0.5f;
            float angleCtg = MathF.Cos(angleRadHalf) / MathF.Sin(angleRadHalf);

            x /= angleCtg * _aspect * 0.5f;
            y /= angleCtg * 0.5f;
            
            // DON'T multiply by _zoomLevel - FOV already handles zoom!
        }

        var center = ViewportCenter;
        scrPos = new()
        {
            X = center.X * (1f + x / w),
            Y = center.Y * (1f - y / w)
        };

        if (onScreenCheck)
        {
            int left = useTolerance ? Viewport.Left - VIEWPORT_TOLERANCE : Viewport.Left;
            int right = useTolerance ? Viewport.Right + VIEWPORT_TOLERANCE : Viewport.Right;
            int top = useTolerance ? Viewport.Top - VIEWPORT_TOLERANCE : Viewport.Top;
            int bottom = useTolerance ? Viewport.Bottom + VIEWPORT_TOLERANCE : Viewport.Bottom;

            if (scrPos.X < left || scrPos.X > right || scrPos.Y < top || scrPos.Y > bottom)
            {
                scrPos = default;
                return false;
            }
        }

        return true;
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"ERROR in WorldToScreen: {ex}");
        scrPos = default;
        return false;
    }
}

        public static CameraDebugSnapshot GetDebugSnapshot()
        {
            return new CameraDebugSnapshot
            {
                EspRunning = EspRunning,
                IsADS = IsADS,
                IsScoped = IsScoped,
                FPSCamera = FPSCameraPtr,
                OpticCamera = OpticCameraPtr,
                ActiveCamera = ActiveCameraPtr,
                Fov = _fov,
                Aspect = _aspect,
                M14 = _viewMatrix.M14,
                M24 = _viewMatrix.M24,
                M44 = _viewMatrix.M44,
                RightX = _viewMatrix.Right.X,
                RightY = _viewMatrix.Right.Y,
                RightZ = _viewMatrix.Right.Z,
                UpX = _viewMatrix.Up.X,
                UpY = _viewMatrix.Up.Y,
                UpZ = _viewMatrix.Up.Z,
                TransX = _viewMatrix.Translation.X,
                TransY = _viewMatrix.Translation.Y,
                TransZ = _viewMatrix.Translation.Z,
                ViewportW = Viewport.Width,
                ViewportH = Viewport.Height
            };
        }

        public readonly struct CameraDebugSnapshot
        {
            public bool EspRunning { get; init; }
            public bool IsADS { get; init; }
            public bool IsScoped { get; init; }
            public ulong FPSCamera { get; init; }
            public ulong OpticCamera { get; init; }
            public ulong ActiveCamera { get; init; }
            public float Fov { get; init; }
            public float Aspect { get; init; }
            public float M14 { get; init; }
            public float M24 { get; init; }
            public float M44 { get; init; }
            public float RightX { get; init; }
            public float RightY { get; init; }
            public float RightZ { get; init; }
            public float UpX { get; init; }
            public float UpY { get; init; }
            public float UpZ { get; init; }
            public float TransX { get; init; }
            public float TransY { get; init; }
            public float TransZ { get; init; }
            public int ViewportW { get; init; }
            public int ViewportH { get; init; }
        }
        /// <summary>
        /// Returns the FOV Magnitude (Length) between a point, and the center of the screen.
        /// </summary>
        /// <param name="point">Screen point to calculate FOV Magnitude of.</param>
        /// <returns>Screen distance from the middle of the screen (FOV Magnitude).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetFovMagnitude(SKPoint point)
        {
            return Vector2.Distance(ViewportCenter.AsVector2(), point.AsVector2());
        }
        #endregion
    }
}