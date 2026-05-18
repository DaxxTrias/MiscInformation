using System;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ExileCore2;
using ExileCore2.PoEMemory.Elements;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using RectangleF = ExileCore2.Shared.RectangleF;
using GdiBitmap = System.Drawing.Bitmap;
using GdiColor = System.Drawing.Color;
using GdiGraphics = System.Drawing.Graphics;
using GdiRectangle = System.Drawing.Rectangle;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Vector2 = System.Numerics.Vector2;

namespace MiscInformation
{
    internal sealed class ScreenCloneFrame : IDisposable
    {
        private const string TextureNamePrefix = "MiscInformation.CloneFrame.";
        private const int SkillBarCaptureChildLimit = 8;
        private const int MaxCaptureDimension = 512;

        private readonly ExileCore2.Graphics _graphics;
        private readonly CaptureSlot[] _captureSlots = new CaptureSlot[SkillBarCaptureChildLimit];
        private readonly RectangleF[] _sourceRects = new RectangleF[SkillBarCaptureChildLimit];
        private readonly int[] _sourceChildIndices = new int[SkillBarCaptureChildLimit];
        private readonly string[] _textureNames = new string[SkillBarCaptureChildLimit];
        private DateTime _nextCaptureUtc = DateTime.MinValue;

        public ScreenCloneFrame(ExileCore2.Graphics graphics)
        {
            _graphics = graphics;

            for (var i = 0; i < SkillBarCaptureChildLimit; i++)
            {
                _captureSlots[i] = new CaptureSlot();
                _textureNames[i] = $"{TextureNamePrefix}{i}";
            }
        }

        public void Render(CloneFrameSettings settings, RectangleF windowRect, SkillBarElement skillBar)
        {
            if (!settings.Enable.Value)
            {
                DisposeTextures();
                return;
            }

            var targetSize = ToPositiveSize(settings.TargetSize.Value);
            if (targetSize.X < 1 || targetSize.Y < 1)
                return;

            if (!TryGetSelectedSkillSourceRects(settings, skillBar, out var selectedCount))
            {
                DisposeTextures();
                return;
            }

            var targetPosition = settings.TargetPosition.Value;
            DisposeUnselectedTextures(settings);
            TryRefreshTextures(settings, windowRect, selectedCount);

            var opacity = Math.Clamp(settings.Opacity.Value, 0, 255);
            var tint = GdiColor.FromArgb(opacity, GdiColor.White);
            for (var i = 0; i < selectedCount; i++)
            {
                var childIndex = _sourceChildIndices[i];
                var targetRect = new RectangleF(
                    targetPosition.X + targetSize.X * i,
                    targetPosition.Y,
                    targetSize.X,
                    targetSize.Y);

                if (_captureSlots[childIndex].HasTexture)
                    _graphics.DrawImage(_textureNames[childIndex], targetRect, tint);

                if (settings.DrawSourceOutline.Value)
                    _graphics.DrawFrame(_sourceRects[i], GdiColor.DeepSkyBlue, 1);

                if (settings.DrawTargetOutline.Value)
                    _graphics.DrawFrame(targetRect, GdiColor.Gold, 1);
            }
        }

        private bool TryGetSelectedSkillSourceRects(CloneFrameSettings settings, SkillBarElement skillBar, out int selectedCount)
        {
            selectedCount = 0;

            if (skillBar == null || skillBar.Address == 0 || !skillBar.IsValid || !skillBar.IsVisible)
                return false;

            var children = skillBar.Children;
            var availableChildCount = Math.Min(children.Count, SkillBarCaptureChildLimit);
            if (availableChildCount < 1)
                return false;

            for (var i = 0; i < availableChildCount; i++)
            {
                if (!IsCaptureEnabled(settings, i))
                    continue;

                var child = children[i];
                if (child == null || child.Address == 0 || !child.IsValid)
                    return false;

                var rect = child.GetClientRectCache;
                if (rect.Width <= 1 || rect.Height <= 1 || rect.Width > MaxCaptureDimension || rect.Height > MaxCaptureDimension)
                    return false;

                _sourceRects[selectedCount] = new RectangleF(rect.X, rect.Y, rect.Width, rect.Height);
                _sourceChildIndices[selectedCount] = i;
                selectedCount++;
            }

            return selectedCount > 0;
        }

        private static bool IsCaptureEnabled(CloneFrameSettings settings, int childIndex)
        {
            return childIndex switch
            {
                0 => settings.CaptureChild0.Value,
                1 => settings.CaptureChild1.Value,
                2 => settings.CaptureChild2.Value,
                3 => settings.CaptureChild3.Value,
                4 => settings.CaptureChild4.Value,
                5 => settings.CaptureChild5.Value,
                6 => settings.CaptureChild6.Value,
                7 => settings.CaptureChild7.Value,
                _ => false
            };
        }

        public void Dispose()
        {
            DisposeTextures();

            for (var i = 0; i < _captureSlots.Length; i++)
                _captureSlots[i].Dispose();
        }

        private void TryRefreshTextures(CloneFrameSettings settings, RectangleF windowRect, int selectedCount)
        {
            var now = DateTime.UtcNow;
            if (now < _nextCaptureUtc)
                return;

            _nextCaptureUtc = now.AddMilliseconds(Math.Max(16, settings.RefreshIntervalMs.Value));

            for (var i = 0; i < selectedCount; i++)
            {
                var childIndex = _sourceChildIndices[i];
                var sourceRect = _sourceRects[i];
                var width = Math.Max(1, (int)MathF.Round(sourceRect.Width));
                var height = Math.Max(1, (int)MathF.Round(sourceRect.Height));
                var slot = _captureSlots[childIndex];

                try
                {
                    EnsureBitmap(slot, childIndex, width, height);

                    slot.Graphics!.CopyFromScreen(
                        (int)MathF.Round(windowRect.X + sourceRect.X),
                        (int)MathF.Round(windowRect.Y + sourceRect.Y),
                        0,
                        0,
                        slot.Bitmap!.Size);

                    if (TryCopyChangedBitmapToImage(slot, slot.Bitmap!, slot.Image!))
                    {
                        _graphics.AddOrUpdateImage(_textureNames[childIndex], slot.Image!);
                        slot.HasTexture = true;
                    }
                }
                catch
                {
                    DisposeTexture(slot, _textureNames[childIndex]);
                }
            }
        }

        private void EnsureBitmap(CaptureSlot slot, int childIndex, int width, int height)
        {
            if (slot.Bitmap != null && slot.BitmapWidth == width && slot.BitmapHeight == height)
                return;

            DisposeTexture(slot, _textureNames[childIndex]);
            slot.Graphics?.Dispose();
            slot.Graphics = null;
            slot.Bitmap?.Dispose();
            slot.Bitmap = new GdiBitmap(width, height, PixelFormat.Format32bppArgb);
            slot.Graphics = GdiGraphics.FromImage(slot.Bitmap);
            slot.Image?.Dispose();
            slot.Image = new Image<Rgba32>(width, height);
            slot.BitmapWidth = width;
            slot.BitmapHeight = height;
            slot.PixelBuffer = Array.Empty<byte>();
            slot.PreviousPixelBuffer = Array.Empty<byte>();
            slot.HasSnapshot = false;
        }

        private static bool TryCopyChangedBitmapToImage(CaptureSlot slot, GdiBitmap bitmap, Image<Rgba32> image)
        {
            var rect = new GdiRectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                var stride = Math.Abs(data.Stride);
                var requiredBytes = stride * bitmap.Height;
                if (slot.PixelBuffer.Length < requiredBytes)
                    slot.PixelBuffer = new byte[requiredBytes];

                if (slot.PreviousPixelBuffer.Length < requiredBytes)
                    slot.PreviousPixelBuffer = new byte[requiredBytes];

                Marshal.Copy(data.Scan0, slot.PixelBuffer, 0, requiredBytes);

                var currentPixels = slot.PixelBuffer.AsSpan(0, requiredBytes);
                var previousPixels = slot.PreviousPixelBuffer.AsSpan(0, requiredBytes);
                if (slot.HasSnapshot && currentPixels.SequenceEqual(previousPixels))
                    return false;

                image.ProcessPixelRows(accessor =>
                {
                    for (var y = 0; y < accessor.Height; y++)
                    {
                        var sourceY = data.Stride < 0 ? accessor.Height - 1 - y : y;
                        var sourceOffset = sourceY * stride;
                        var row = accessor.GetRowSpan(y);

                        for (var x = 0; x < row.Length; x++)
                        {
                            var pixelOffset = sourceOffset + x * 4;
                            row[x] = new Rgba32(
                                slot.PixelBuffer[pixelOffset + 2],
                                slot.PixelBuffer[pixelOffset + 1],
                                slot.PixelBuffer[pixelOffset],
                                slot.PixelBuffer[pixelOffset + 3]);
                        }
                    }
                });

                currentPixels.CopyTo(previousPixels);
                slot.HasSnapshot = true;
                return true;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private void DisposeTextures()
        {
            for (var i = 0; i < _captureSlots.Length; i++)
                DisposeTexture(_captureSlots[i], _textureNames[i]);
        }

        private void DisposeUnselectedTextures(CloneFrameSettings settings)
        {
            for (var i = 0; i < _captureSlots.Length; i++)
            {
                if (!IsCaptureEnabled(settings, i))
                    DisposeTexture(_captureSlots[i], _textureNames[i]);
            }
        }

        private void DisposeTexture(CaptureSlot slot, string textureName)
        {
            if (!slot.HasTexture)
                return;

            try
            {
                _graphics.DisposeTexture(textureName);
            }
            catch
            {
                // The host may already have cleared textures during plugin reload.
            }

            slot.HasTexture = false;
            slot.HasSnapshot = false;
        }

        private sealed class CaptureSlot : IDisposable
        {
            public GdiBitmap? Bitmap { get; set; }
            public GdiGraphics? Graphics { get; set; }
            public Image<Rgba32>? Image { get; set; }
            public byte[] PixelBuffer { get; set; } = Array.Empty<byte>();
            public byte[] PreviousPixelBuffer { get; set; } = Array.Empty<byte>();
            public bool HasTexture { get; set; }
            public bool HasSnapshot { get; set; }
            public int BitmapWidth { get; set; }
            public int BitmapHeight { get; set; }

            public void Dispose()
            {
                Graphics?.Dispose();
                Graphics = null;
                Bitmap?.Dispose();
                Bitmap = null;
                Image?.Dispose();
                Image = null;
                PixelBuffer = Array.Empty<byte>();
                PreviousPixelBuffer = Array.Empty<byte>();
                BitmapWidth = 0;
                BitmapHeight = 0;
                HasTexture = false;
                HasSnapshot = false;
            }
        }

        private static Vector2 ToPositiveSize(Vector2 size)
        {
            return new Vector2(MathF.Max(1, size.X), MathF.Max(1, size.Y));
        }
    }
}
