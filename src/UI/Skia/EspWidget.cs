/*
 * Lone EFT DMA Radar - ESP Widget
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using LoneEftDmaRadar.Tarkov.GameWorld.Camera;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using SkiaSharp;
using SkiaSharp.Views.WPF;

namespace LoneEftDmaRadar.UI.Skia
{
    public sealed class EspWidget : IDisposable
    {
        private readonly SKGLElement _skElement;

        public EspWidget(SKGLElement skElement)
        {
            _skElement = skElement;
            _skElement.PaintSurface += OnPaintSurface;
        }

        private static LocalPlayer _LocalPlayer => Memory.LocalPlayer;
        private static IReadOnlyCollection<AbstractPlayer> AllPlayers => Memory.Players;
        private static bool InRaid => Memory.InRaid;

        private void OnPaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            if (!InRaid || !CameraManager.EspRunning)
                return;

            if (_LocalPlayer is not LocalPlayer localPlayer)
                return;

            try
            {
                DrawPlayers(canvas, localPlayer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ESP Render Error: {ex}");
            }
        }

private void DrawPlayers(SKCanvas canvas, LocalPlayer localPlayer)
{
    var players = AllPlayers?
        .Where(p => p.IsActive && p.IsAlive && p != localPlayer)
        .ToList();

    if (players == null || players.Count == 0)
        return;

    int skeletonsDrawn = 0;

    foreach (var player in players)
    {
        try
        {
            if (DrawPlayer(canvas, player, localPlayer))
            {
                skeletonsDrawn++;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error drawing player '{player.Name}': {ex}");
        }
    }

    if (skeletonsDrawn == 0)
    {
        Debug.WriteLine($"[ESP] {players.Count} players in raid but 0 skeletons drawn.");
    }
}


        /// <summary>
        /// Draw a single player. Returns true if something was actually drawn.
        /// </summary>
/// <summary>
/// Draw a single player. Returns true if something was actually drawn.
/// </summary>
private bool DrawPlayer(SKCanvas canvas, AbstractPlayer player, LocalPlayer localPlayer)
{
    var skeleton = player.Skeleton;
    if (skeleton?.BoneTransforms == null || skeleton.BoneTransforms.Count == 0)
        return false;

    // Safely get head and pelvis
    if (!skeleton.BoneTransforms.TryGetValue(Bones.HumanHead, out var head) ||
        !skeleton.BoneTransforms.TryGetValue(Bones.HumanPelvis, out var pelvis))
    {
        return false;
    }

    var headPos   = head.Position;
    var pelvisPos = pelvis.Position;

    // World-space sanity check
    if (!IsFinite(headPos) || !IsFinite(pelvisPos))
        return false;

    // Optional: skip totally insane world distances (wigged out bones far from player)
    float worldHeight = Math.Abs(pelvisPos.Y - headPos.Y);
    if (worldHeight <= 0.01f || worldHeight > 5.0f * 3.0f) // head¨Cpelvis difference > ~15m? nope.
        return false;

    // World to screen
    if (!CameraManager.WorldToScreen(in headPos, out var headScreen, true))
        return false;

    if (!CameraManager.WorldToScreen(in pelvisPos, out var pelvisScreen, true))
        return false;

    // Screen-space sanity check
    if (!IsFinite(headScreen) || !IsFinite(pelvisScreen))
        return false;

    // Get screen size from canvas
    var bounds   = canvas.LocalClipBounds;
    float scrW   = bounds.Width;
    float scrH   = bounds.Height;

    // Calculate box dimensions
    float height = Math.Abs(pelvisScreen.Y - headScreen.Y);
    if (height <= 0.1f)
        return false;

    float width  = height * 0.5f;

    // Reject insane sizes (this is what usually causes lines across the whole screen)
    // - Taller than 2x screen height
    // - Wider than screen width
    if (height > scrH * 2.0f || width > scrW * 1.2f)
        return false;

    var boxRect = new SKRect(
        headScreen.X - width / 2f,
        headScreen.Y,
        headScreen.X + width / 2f,
        pelvisScreen.Y
    );

    var paint = GetPaint(player);

    // Draw box
    canvas.DrawRect(boxRect, paint);

    // Draw head dot
    if (App.Config.ESP.ShowHeadDot)
    {
        canvas.DrawCircle(headScreen.X, headScreen.Y, 3f, paint);
    }

    // Draw name
    if (App.Config.ESP.ShowNames)
    {
        var namePos = new SKPoint(boxRect.MidX, boxRect.Top - 5);
        canvas.DrawText(player.Name, namePos, SKTextAlign.Center, SKFonts.UIRegular, paint);
    }

    // Draw distance
    if (App.Config.ESP.ShowDistance)
    {
        float distance = Vector3.Distance(localPlayer.Position, player.Position);
        var distText   = $"{distance:F0}m";
        var distPos    = new SKPoint(boxRect.MidX, boxRect.Bottom + 15);
        canvas.DrawText(distText, distPos, SKTextAlign.Center, SKFonts.UIRegular, paint);
    }

    return true;
}


        private static SKPaint GetPaint(AbstractPlayer player)
        {
            return player.Type switch
            {
                PlayerType.Teammate      => SKPaints.PaintTeammate,
                PlayerType.PMC           => SKPaints.PaintPMC,
                PlayerType.AIScav        => SKPaints.PaintScav,
                PlayerType.AIRaider      => SKPaints.PaintRaider,
                PlayerType.AIBoss        => SKPaints.PaintBoss,
                PlayerType.PScav         => SKPaints.PaintPScav,
                PlayerType.SpecialPlayer => SKPaints.PaintAimviewWidgetWatchlist,
                _                        => SKPaints.PaintPMC
            };
        }
        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X) &&
                   !float.IsNaN(v.Y) && !float.IsInfinity(v.Y) &&
                   !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }

        private static bool IsFinite(SKPoint p)
        {
            return !float.IsNaN(p.X) && !float.IsInfinity(p.X) &&
                   !float.IsNaN(p.Y) && !float.IsInfinity(p.Y);
        }
        public void Dispose()
        {
            _skElement.PaintSurface -= OnPaintSurface;
        }
    }
}
