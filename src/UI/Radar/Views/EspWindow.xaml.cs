/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using LoneEftDmaRadar.DMA;
using LoneEftDmaRadar.Tarkov.GameWorld.Camera;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.UI.Misc;
using LoneEftDmaRadar.UI.Radar.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LoneEftDmaRadar.UI.Windows
{
    public partial class EspWindow : Window
    {
        private bool _isRunning;
        private int _frameCount;
        private DateTime _lastFpsUpdate = DateTime.UtcNow;
        private float _currentFps;
        private readonly MonitorInfo _monitor;

        // Cached paints to avoid allocations every frame
        private readonly Dictionary<PlayerType, SKPaint> _paintCache = new();
        private SKPaint _textPaint;
        private SKPaint _textOutlinePaint;
        private SKPaint _boxOutlinePaint;
        private SKPaint _lootPaint;
        private SKPaint _containerPaint;
        /// <summary>
        /// All Filtered Loot on the map.
        /// </summary>
        private static IEnumerable<LootItem> Loot => Memory?.Loot?.FilteredLoot;

        /// <summary>
        /// All Static Containers on the map.
        /// </summary>
        private static IEnumerable<StaticLootContainer> Containers => Memory?.Loot?.StaticContainers;
        public EspWindow(MonitorInfo monitor)
        {
            try
            {
                InitializeComponent();

                _monitor = monitor ?? MonitorInfo.GetPrimaryMonitor();

                System.Diagnostics.Debug.WriteLine($"EspWindow created for monitor: {_monitor?.DisplayName ?? "null"}");

                InitializePaintCache();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in EspWindow constructor: {ex}");
                throw;
            }
        }
        private void InitializePaintCache()
        {
            // Get colors from config
            _paintCache[PlayerType.Teammate] = new SKPaint
            {
                Color = EspViewModel.GetColor(App.Config.ESP.ColorTeammate),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntialias = true
            };
            _paintCache[PlayerType.PMC] = new SKPaint
            {
                Color = EspViewModel.GetColor(App.Config.ESP.ColorPMC),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntialias = true
            };
            _paintCache[PlayerType.AIScav] = new SKPaint
            {
                Color = EspViewModel.GetColor(App.Config.ESP.ColorAIScav),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntialias = true
            };
            _paintCache[PlayerType.AIRaider] = new SKPaint
            {
                Color = EspViewModel.GetColor(App.Config.ESP.ColorAIRaider),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntialias = true
            };
            _paintCache[PlayerType.AIBoss] = new SKPaint
            {
                Color = EspViewModel.GetColor(App.Config.ESP.ColorAIBoss),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntialias = true
            };
            _paintCache[PlayerType.PScav] = new SKPaint
            {
                Color = EspViewModel.GetColor(App.Config.ESP.ColorPScav),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntialias = true
            };
            _paintCache[PlayerType.SpecialPlayer] = new SKPaint
            {
                Color = EspViewModel.GetColor(App.Config.ESP.ColorSpecialPlayer),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntialias = true
            };

            _textPaint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = 14f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };

            _textOutlinePaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 14f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3f
            };

            _boxOutlinePaint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3f,
                IsAntialias = true
            };

            _lootPaint = new SKPaint
            {
                Color = EspViewModel.GetColor(App.Config.ESP.ColorLoot),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntialias = true
            };

            _containerPaint = new SKPaint
            {
                Color = EspViewModel.GetColor(App.Config.ESP.ColorContainer),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntialias = true
            };
        }

private void Window_Loaded(object sender, RoutedEventArgs e)
{
    try
    {
        System.Diagnostics.Debug.WriteLine("EspWindow loading...");

        // Set size from config
        var configWidth = App.Config.ESP.Resolution.Width;
        var configHeight = App.Config.ESP.Resolution.Height;

        // Validate resolution
        if (configWidth <= 0 || configHeight <= 0)
        {
            configWidth = _monitor?.Width ?? 1920;
            configHeight = _monitor?.Height ?? 1080;
            System.Diagnostics.Debug.WriteLine($"Invalid config resolution, using monitor: {configWidth}x{configHeight}");
        }

        // Account for WPF DPI scaling
        var presentationSource = PresentationSource.FromVisual(this);
        double dpiX = 1.0;
        double dpiY = 1.0;
        
        if (presentationSource?.CompositionTarget != null)
        {
            dpiX = presentationSource.CompositionTarget.TransformToDevice.M11;
            dpiY = presentationSource.CompositionTarget.TransformToDevice.M22;
            System.Diagnostics.Debug.WriteLine($"DPI Scaling: {dpiX}x, {dpiY}y");
        }

        // Set WPF window size (DPI-independent)
        Width = configWidth / dpiX;
        Height = configHeight / dpiY;

        System.Diagnostics.Debug.WriteLine($"Window size: {Width}x{Height} (WPF units)");
        System.Diagnostics.Debug.WriteLine($"Actual pixels: {configWidth}x{configHeight}");

        // Position on selected monitor
        if (_monitor != null)
        {
            Left = _monitor.Left / dpiX;
            Top = _monitor.Top / dpiY;
            System.Diagnostics.Debug.WriteLine($"Positioned window at ({Left}, {Top}) WPF units");
        }
        else
        {
            Left = 0;
            Top = 0;
            System.Diagnostics.Debug.WriteLine("No monitor info, positioning at (0, 0)");
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            System.Diagnostics.Debug.WriteLine("Set window style to transparent/layered");
        }

        CameraManager.EspRunning = true;
        CameraManager.UpdateViewportRes();
        _isRunning = true;

        CompositionTarget.Rendering += OnRendering;

        // ? NEW: Add keyboard handler for Escape key
        this.KeyDown += EspWindow_KeyDown;

        System.Diagnostics.Debug.WriteLine("EspWindow loaded successfully");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error in Window_Loaded: {ex}");
        System.Windows.MessageBox.Show($"Error loading ESP window: {ex.Message}\n\n{ex.StackTrace}", 
            "ESP Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

// ? NEW: Escape key handler
private void EspWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
{
    if (e.Key == System.Windows.Input.Key.Escape)
    {
        System.Diagnostics.Debug.WriteLine("Escape pressed - closing ESP window");
        this.Close();
    }
}

        private void OnRendering(object sender, EventArgs e)
        {
            if (_isRunning)
            {
                skiaCanvas.InvalidateVisual();
            }
        }

private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
{
    _isRunning = false;
    CompositionTarget.Rendering -= OnRendering;
    CameraManager.EspRunning = false;

    // ? NEW: Remove keyboard handler
    this.KeyDown -= EspWindow_KeyDown;

    // Dispose cached paints
    foreach (var paint in _paintCache.Values)
        paint?.Dispose();
    _paintCache.Clear();
    _textPaint?.Dispose();
    _textOutlinePaint?.Dispose();
    _boxOutlinePaint?.Dispose();
    _lootPaint?.Dispose();
    _containerPaint?.Dispose();
}

        private void UpdateFps()
        {
            _frameCount++;
            var now = DateTime.UtcNow;
            var delta = (now - _lastFpsUpdate).TotalSeconds;
            if (delta >= 1.0)
            {
                _currentFps = (float)(_frameCount / delta);
                _frameCount = 0;
                _lastFpsUpdate = now;
            }
        }

        private void SkiaCanvas_PaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            bool inRaid = Memory.InRaid;
            var localPlayer = Memory.LocalPlayer;
            var allPlayers = Memory.Players;

            UpdateFps();

            // ESP debug (camera etc.)
            if (App.Config.ESP.ShowDebug)
                DrawDebugOverlay(canvas, inRaid, localPlayer, allPlayers);

            // MAKCU DEBUG: draw this **even if not in raid / no localPlayer**
            if (App.Config.Makcu.ShowDebug && MemDMA.MakcuAimbot != null)
            {
                MemDMA.MakcuAimbot.DrawDebug(canvas, localPlayer);
            }

            // After this point we only do heavy stuff (players, loot, etc.)
            if (!_isRunning || !inRaid || localPlayer == null || allPlayers == null)
                return;

            try
            {
                // Draw Loot
                if (App.Config.ESP.ShowLoot && Memory.Loot?.FilteredLoot is IEnumerable<LootItem> loot)
                {
                    foreach (var item in Memory.Loot?.FilteredLoot)
                    {
                        try
                        {
                            if (ShouldDrawLoot(item, localPlayer))
                                DrawLoot(canvas, item, localPlayer);
                        }
                        catch { }
                    }
                }

                // Draw Containers
                if (App.Config.ESP.ShowContainers && Memory.Loot?.StaticContainers is IEnumerable<StaticLootContainer> containers)
                {
                    foreach (var container in Memory.Loot?.StaticContainers)
                    {
                        try
                        {
                            if (ShouldDrawContainer(container, localPlayer))
                                DrawContainer(canvas, container, localPlayer);
                        }
                        catch { }
                    }
                }

                // Players
                if (App.Config.ESP.ShowPlayers)
                {
                    foreach (var player in allPlayers)
                    {
                        if (!player.IsActive || !player.IsAlive || player is LocalPlayer)
                            continue;

                        try
                        {
                            if (ShouldDrawPlayer(player, localPlayer))
                                DrawPlayer(canvas, player, localPlayer);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error drawing player '{player.Name}': {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ESP Render Error: {ex}");
            }
        }


        #region Distance & Visibility Checks

        private bool ShouldDrawPlayer(AbstractPlayer player, LocalPlayer localPlayer)
        {
            // Check if player type is enabled
            bool typeEnabled = player.Type switch
            {
                PlayerType.PMC => App.Config.ESP.ShowPMC,
                PlayerType.Teammate => App.Config.ESP.ShowTeammate,
                PlayerType.PScav => App.Config.ESP.ShowPScav,
                PlayerType.AIScav => App.Config.ESP.ShowAIScav,
                PlayerType.AIRaider => App.Config.ESP.ShowAIRaider,
                PlayerType.AIBoss => App.Config.ESP.ShowAIBoss,
                PlayerType.SpecialPlayer => App.Config.ESP.ShowSpecialPlayer,
                _ => true
            };

            if (!typeEnabled)
                return false;

            // Check distance
            float maxDistance = player.Type switch
            {
                PlayerType.PMC => App.Config.ESP.MaxDistancePMC,
                PlayerType.Teammate => App.Config.ESP.MaxDistanceTeammate,
                PlayerType.PScav => App.Config.ESP.MaxDistancePScav,
                PlayerType.AIScav => App.Config.ESP.MaxDistanceAIScav,
                PlayerType.AIRaider => App.Config.ESP.MaxDistanceAIRaider,
                PlayerType.AIBoss => App.Config.ESP.MaxDistanceAIBoss,
                PlayerType.SpecialPlayer => App.Config.ESP.MaxDistanceSpecialPlayer,
                _ => 0
            };

            if (maxDistance > 0)
            {
                float distance = Vector3.Distance(localPlayer.Position, player.Position);
                if (distance > maxDistance)
                    return false;
            }

            return true;
        }

        private bool ShouldDrawLoot(LootItem item, LocalPlayer localPlayer)
        {
            float maxDistance = App.Config.ESP.MaxDistanceLoot;
            if (maxDistance > 0)
            {
                float distance = Vector3.Distance(localPlayer.Position, item.Position);
                if (distance > maxDistance)
                    return false;
            }
            return true;
        }

        private bool ShouldDrawContainer(StaticLootContainer container, LocalPlayer localPlayer)
        {
            // Check if container is tracked
            if (MainWindow.Instance?.Settings?.ViewModel is UI.Radar.ViewModels.SettingsViewModel vm)
            {
                if (!vm.ContainerIsTracked(container.ID ?? "NULL"))
                    return false;
            }

            float maxDistance = App.Config.ESP.MaxDistanceContainers;
            if (maxDistance > 0)
            {
                float distance = Vector3.Distance(localPlayer.Position, container.Position);
                if (distance > maxDistance)
                    return false;
            }
            return true;
        }

        #endregion

        #region Draw Methods

        private void DrawDebugOverlay(
            SKCanvas canvas,
            bool inRaid,
            LocalPlayer localPlayer,
            IReadOnlyCollection<AbstractPlayer> allPlayers)
        {
            var camInfo = CameraManager.GetDebugSnapshot();

            int totalPlayers = allPlayers?.Count ?? 0;
            int activeAlivePlayers = allPlayers?.Count(p => p.IsActive && p.IsAlive) ?? 0;

            var lines = new List<string>
            {
                $"ESP FPS: {_currentFps:F1}",
                $"InRaid: {inRaid} | LocalPlayer: {(localPlayer != null ? "OK" : "null")}",
                $"Players: {activeAlivePlayers}/{totalPlayers}",
                $"FOV={camInfo.Fov:F1} | Aspect={camInfo.Aspect:F3}",
                $"VM M44={camInfo.M44:F3}",
                $"Right=({camInfo.RightX:F2},{camInfo.RightY:F2},{camInfo.RightZ:F2})",
                $"Up=({camInfo.UpX:F2},{camInfo.UpY:F2},{camInfo.UpZ:F2})",
                $"Trans=({camInfo.TransX:F1},{camInfo.TransY:F1},{camInfo.TransZ:F1})"
            };

            using var textPaint = new SKPaint
            {
                Color = SKColors.Lime,
                TextSize = 14f,
                IsAntialias = true
            };

            using var shadowPaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 14f,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3f
            };

            float x = 10f;
            float y = 18f;
            const float lineHeight = 16f;

            foreach (var line in lines)
            {
                canvas.DrawText(line, x + 1f, y + 1f, shadowPaint);
                canvas.DrawText(line, x, y, textPaint);
                y += lineHeight;
            }
        }
        private bool TryGetPlayerBoxRect(AbstractPlayer player, out SKRect boxRect)
        {
            boxRect = default;

            if (player.Skeleton == null)
                return false;

            // Get head and feet positions for proper box sizing
            if (!player.Skeleton.BoneTransforms.TryGetValue(Tarkov.Unity.Structures.Bones.HumanHead, out var headTransform) ||
                !player.Skeleton.BoneTransforms.TryGetValue(Tarkov.Unity.Structures.Bones.HumanLFoot, out var leftFootTransform) ||
                !player.Skeleton.BoneTransforms.TryGetValue(Tarkov.Unity.Structures.Bones.HumanRFoot, out var rightFootTransform))
                return false;

            // Head (remember: bone is roughly mid-head, not the top of the skull)
            if (!CameraManager.WorldToScreen(in headTransform.Position, out var headScreen, true))
                return false;

            // Use average of both feet for base
            var avgFootPos = (leftFootTransform.Position + rightFootTransform.Position) / 2f;
            if (!CameraManager.WorldToScreen(in avgFootPos, out var footScreen, true))
                return false;

            float height = Math.Abs(footScreen.Y - headScreen.Y);
            if (height < 2f) // sanity
                return false;

            // Make box slightly wider than before (0.40 -> 0.45)
            float width = height * 0.45f;

            // Shift top up a bit so it actually covers the top of the head
            // (head bone ~middle of skull ¡ú push ~15% of height upwards).
            float top = headScreen.Y - height * 0.15f;

            // Optional: tiny padding at bottom so feet aren¡¯t exactly at the border
            float bottom = footScreen.Y + height * 0.05f;

            boxRect = new SKRect(
                headScreen.X - width / 2f,
                top,
                headScreen.X + width / 2f,
                bottom
            );

            return true;
        }
private void DrawPlayer(SKCanvas canvas, AbstractPlayer player, LocalPlayer localPlayer)
{
    var paint = GetPaint(player);

    // Try to compute a nice box rect once and reuse for box, names, distance, health
    SKRect boxRect;
    bool hasBoxRect = TryGetPlayerBoxRect(player, out boxRect);

    // Head dot (use box top if we have it, otherwise fall back to simple W2S on player position)
    if (App.Config.ESP.ShowHeadDot)
    {
        if (hasBoxRect)
        {
            // Slightly inside the top edge
            canvas.DrawCircle(boxRect.MidX, boxRect.Top + 4f, 4f, paint);
        }
        else if (CameraManager.WorldToScreen(in player.Position, out var headFallback))
        {
            canvas.DrawCircle(headFallback.X, headFallback.Y, 4f, paint);
        }
    }

    // Box itself (ONLY when ShowBox is enabled)
    if (hasBoxRect && App.Config.ESP.ShowBox)
    {
        canvas.DrawRect(boxRect, _boxOutlinePaint);
        canvas.DrawRect(boxRect, paint);
    }

    // Names / distance / health: draw whenever we have a valid boxRect,
    // even if ShowBox is disabled.
    if (hasBoxRect)
    {
        // Name
        if (App.Config.ESP.ShowNames && !string.IsNullOrWhiteSpace(player.Name))
        {
            _textPaint.Color = paint.Color;
            var namePos = new SKPoint(boxRect.MidX, boxRect.Top - 5);

            canvas.DrawText(player.Name, namePos.X, namePos.Y, _textOutlinePaint);
            canvas.DrawText(player.Name, namePos.X, namePos.Y, _textPaint);
        }

        // Distance
        if (App.Config.ESP.ShowDistance)
        {
            float distance = Vector3.Distance(localPlayer.Position, player.Position);
            var distText = $"{distance:F0}m";
            _textPaint.Color = paint.Color;
            var distPos = new SKPoint(boxRect.MidX, boxRect.Bottom + 15);

            canvas.DrawText(distText, distPos.X, distPos.Y, _textOutlinePaint);
            canvas.DrawText(distText, distPos.X, distPos.Y, _textPaint);
        }

        // Health
        if (App.Config.ESP.ShowHealth && player is ObservedPlayer observed)
        {
            var healthText = observed.HealthStatus.ToString();
            _textPaint.Color = paint.Color;

            _textPaint.TextAlign = SKTextAlign.Right;
            _textOutlinePaint.TextAlign = SKTextAlign.Right;

            var healthPos = new SKPoint(boxRect.Left - 5, boxRect.MidY);

            canvas.DrawText(healthText, healthPos.X, healthPos.Y, _textOutlinePaint);
            canvas.DrawText(healthText, healthPos.X, healthPos.Y, _textPaint);

            // Reset alignment
            _textPaint.TextAlign = SKTextAlign.Center;
            _textOutlinePaint.TextAlign = SKTextAlign.Center;
        }
    }

    // ? FIX: Skeleton with validation
    if (App.Config.ESP.ShowSkeleton && player.Skeleton != null)
    {
        try
        {
            if (player.Skeleton.UpdateESPWidgetBuffer(1f, 1f, out var points))
            {
                // Validate all points before drawing
                bool allPointsValid = true;
                for (int i = 0; i < points.Length; i++)
                {
                    var point = points[i];
                    
                    // Check if point is within reasonable screen bounds (with tolerance)
                    if (float.IsNaN(point.X) || float.IsNaN(point.Y) ||
                        float.IsInfinity(point.X) || float.IsInfinity(point.Y) ||
                        point.X < -1000 || point.X > CameraManager.Viewport.Width + 1000 ||
                        point.Y < -1000 || point.Y > CameraManager.Viewport.Height + 1000)
                    {
                        allPointsValid = false;
                        break;
                    }
                }

                // Only draw if all points are valid
                if (allPointsValid)
                {
                    canvas.DrawPoints(SKPointMode.Lines, points, paint);
                }
            }
        }
        catch (Exception ex)
        {
            // Log the exception for debugging
            System.Diagnostics.Debug.WriteLine($"Error drawing skeleton for {player.Name}: {ex.Message}");
        }
    }
}
        private void DrawPlayerBox(SKCanvas canvas, AbstractPlayer player, LocalPlayer localPlayer, SKPaint paint)
        {
            if (player.Skeleton == null)
                return;

            // Get head and feet positions for proper box sizing
            if (!player.Skeleton.BoneTransforms.TryGetValue(Tarkov.Unity.Structures.Bones.HumanHead, out var headTransform) ||
                !player.Skeleton.BoneTransforms.TryGetValue(Tarkov.Unity.Structures.Bones.HumanLFoot, out var leftFootTransform) ||
                !player.Skeleton.BoneTransforms.TryGetValue(Tarkov.Unity.Structures.Bones.HumanRFoot, out var rightFootTransform))
                return;

            if (!CameraManager.WorldToScreen(in headTransform.Position, out var headScreen, true))
                return;

            // Use average of both feet for base
            var avgFootPos = (leftFootTransform.Position + rightFootTransform.Position) / 2f;
            if (!CameraManager.WorldToScreen(in avgFootPos, out var footScreen, true))
                return;

            float height = Math.Abs(footScreen.Y - headScreen.Y);
            float width = height * 0.4f;

            var boxRect = new SKRect(
                headScreen.X - width / 2f,
                headScreen.Y,
                headScreen.X + width / 2f,
                footScreen.Y
            );

            // Draw box using cached paint
            canvas.DrawRect(boxRect, _boxOutlinePaint);
            canvas.DrawRect(boxRect, paint);

            // Draw name
            if (App.Config.ESP.ShowNames && !string.IsNullOrWhiteSpace(player.Name))
            {
                _textPaint.Color = paint.Color;
                var namePos = new SKPoint(boxRect.MidX, boxRect.Top - 5);

                canvas.DrawText(player.Name, namePos.X, namePos.Y, _textOutlinePaint);
                canvas.DrawText(player.Name, namePos.X, namePos.Y, _textPaint);
            }

            // Draw distance
            if (App.Config.ESP.ShowDistance)
            {
                float distance = Vector3.Distance(localPlayer.Position, player.Position);
                var distText = $"{distance:F0}m";
                _textPaint.Color = paint.Color;
                var distPos = new SKPoint(boxRect.MidX, boxRect.Bottom + 15);

                canvas.DrawText(distText, distPos.X, distPos.Y, _textOutlinePaint);
                canvas.DrawText(distText, distPos.X, distPos.Y, _textPaint);
            }

            // Draw health
            if (App.Config.ESP.ShowHealth && player is ObservedPlayer observed)
            {
                var healthText = observed.HealthStatus.ToString();
                _textPaint.Color = paint.Color;
                _textPaint.TextAlign = SKTextAlign.Right;
                _textOutlinePaint.TextAlign = SKTextAlign.Right;
                var healthPos = new SKPoint(boxRect.Left - 5, boxRect.MidY);

                canvas.DrawText(healthText, healthPos.X, healthPos.Y, _textOutlinePaint);
                canvas.DrawText(healthText, healthPos.X, healthPos.Y, _textPaint);

                // Reset alignment
                _textPaint.TextAlign = SKTextAlign.Center;
                _textOutlinePaint.TextAlign = SKTextAlign.Center;
            }
        }

        private void DrawLoot(SKCanvas canvas, LootItem item, LocalPlayer localPlayer)
        {
            if (!CameraManager.WorldToScreen(in item.Position, out var screen, true))
                return;

            // Draw box
            float size = 4f;
            var rect = new SKRect(screen.X - size, screen.Y - size, screen.X + size, screen.Y + size);
            canvas.DrawRect(rect, _boxOutlinePaint);
            canvas.DrawRect(rect, _lootPaint);

            // Draw name
            _textPaint.Color = _lootPaint.Color;
            var textPos = new SKPoint(screen.X, screen.Y - 10);
            canvas.DrawText(item.Name, textPos.X, textPos.Y, _textOutlinePaint);
            canvas.DrawText(item.Name, textPos.X, textPos.Y, _textPaint);

            // Draw distance
            float distance = Vector3.Distance(localPlayer.Position, item.Position);
            var distText = $"{distance:F0}m";
            var distPos = new SKPoint(screen.X, screen.Y + 20);
            canvas.DrawText(distText, distPos.X, distPos.Y, _textOutlinePaint);
            canvas.DrawText(distText, distPos.X, distPos.Y, _textPaint);
        }

        private void DrawContainer(SKCanvas canvas, StaticLootContainer container, LocalPlayer localPlayer)
        {
            if (!CameraManager.WorldToScreen(in container.Position, out var screen, true))
                return;

            // Draw box
            float size = 5f;
            var rect = new SKRect(screen.X - size, screen.Y - size, screen.X + size, screen.Y + size);
            canvas.DrawRect(rect, _boxOutlinePaint);
            canvas.DrawRect(rect, _containerPaint);

            // Draw name
            _textPaint.Color = _containerPaint.Color;
            var textPos = new SKPoint(screen.X, screen.Y - 10);
            var name = container.ID ?? "Container";
            canvas.DrawText(name, textPos.X, textPos.Y, _textOutlinePaint);
            canvas.DrawText(name, textPos.X, textPos.Y, _textPaint);

            // Draw distance
            float distance = Vector3.Distance(localPlayer.Position, container.Position);
            var distText = $"{distance:F0}m";
            var distPos = new SKPoint(screen.X, screen.Y + 20);
            canvas.DrawText(distText, distPos.X, distPos.Y, _textOutlinePaint);
            canvas.DrawText(distText, distPos.X, distPos.Y, _textPaint);
        }

        private SKPaint GetPaint(AbstractPlayer player)
        {
            // Return cached paint
            if (_paintCache.TryGetValue(player.Type, out var paint))
                return paint;

            // Fallback to PMC paint
            return _paintCache[PlayerType.PMC];
        }

        #endregion

        #region Win32 Interop

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        #endregion
    }
}