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
        private const string TextureName = "MiscInformation.CloneFrame";
        private const int SkillBarCaptureChildLimit = 8;
        private const int MaxCaptureDimension = 512;

        private readonly ExileCore2.Graphics _graphics;
        private readonly RectangleF[] _sourceRects = new RectangleF[SkillBarCaptureChildLimit];
        private readonly int[] _sourceChildIndices = new int[SkillBarCaptureChildLimit];
        private GdiBitmap? _captureBitmap;
        private GdiGraphics? _captureGraphics;
        private Image<Rgba32>? _outputImage;
        private byte[] _captureBuffer = Array.Empty<byte>();
        private byte[] _outputBuffer = Array.Empty<byte>();
        private byte[] _previousOutputBuffer = Array.Empty<byte>();
        private DateTime _nextCaptureUtc = DateTime.MinValue;
        private bool _hasTexture;
        private bool _hasSnapshot;
        private int _captureBitmapWidth;
        private int _captureBitmapHeight;
        private int _outputWidth;
        private int _outputHeight;

        public ScreenCloneFrame(ExileCore2.Graphics graphics)
        {
            _graphics = graphics;
        }

        public void Render(CloneFrameSettings settings, RectangleF windowRect, SkillBarElement skillBar)
        {
            if (!settings.Enable.Value)
            {
                DisposeTexture();
                return;
            }

            var targetSize = ToPositiveSize(settings.TargetSize.Value);
            if (targetSize.X < 1 || targetSize.Y < 1)
                return;

            if (!TryGetSelectedSkillSourceRects(settings, skillBar, out var selectedCount, out var captureRect))
            {
                DisposeTexture();
                return;
            }

            var targetPosition = settings.TargetPosition.Value;
            TryRefreshTexture(settings, windowRect, selectedCount, captureRect);

            if (_hasTexture)
            {
                var targetRect = new RectangleF(
                    targetPosition.X,
                    targetPosition.Y,
                    targetSize.X * selectedCount,
                    targetSize.Y);

                var opacity = Math.Clamp(settings.Opacity.Value, 0, 255);
                _graphics.DrawImage(TextureName, targetRect, GdiColor.FromArgb(opacity, GdiColor.White));

                if (settings.DrawTargetOutline.Value)
                    _graphics.DrawFrame(targetRect, GdiColor.Gold, 1);
            }

            if (settings.DrawSourceOutline.Value)
            {
                for (var i = 0; i < selectedCount; i++)
                    _graphics.DrawFrame(_sourceRects[i], GdiColor.DeepSkyBlue, 1);
            }
        }

        private bool TryGetSelectedSkillSourceRects(CloneFrameSettings settings, SkillBarElement skillBar, out int selectedCount, out RectangleF captureRect)
        {
            selectedCount = 0;
            captureRect = RectangleF.Empty;

            if (skillBar == null || skillBar.Address == 0 || !skillBar.IsValid || !skillBar.IsVisible)
                return false;

            var children = skillBar.Children;
            var availableChildCount = Math.Min(children.Count, SkillBarCaptureChildLimit);
            if (availableChildCount < 1)
                return false;

            var left = float.MaxValue;
            var top = float.MaxValue;
            var right = float.MinValue;
            var bottom = float.MinValue;

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
                left = MathF.Min(left, rect.X);
                top = MathF.Min(top, rect.Y);
                right = MathF.Max(right, rect.X + rect.Width);
                bottom = MathF.Max(bottom, rect.Y + rect.Height);
                selectedCount++;
            }

            if (selectedCount == 0)
                return false;

            var width = right - left;
            var height = bottom - top;
            if (width <= 1 || height <= 1 || width > MaxCaptureDimension || height > MaxCaptureDimension)
                return false;

            captureRect = new RectangleF(left, top, width, height);
            return true;
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
            DisposeTexture();
            _captureGraphics?.Dispose();
            _captureGraphics = null;
            _captureBitmap?.Dispose();
            _captureBitmap = null;
            _outputImage?.Dispose();
            _outputImage = null;
            _captureBuffer = Array.Empty<byte>();
            _outputBuffer = Array.Empty<byte>();
            _previousOutputBuffer = Array.Empty<byte>();
            _hasSnapshot = false;
        }

        private void TryRefreshTexture(CloneFrameSettings settings, RectangleF windowRect, int selectedCount, RectangleF captureRect)
        {
            var now = DateTime.UtcNow;
            if (now < _nextCaptureUtc)
                return;

            _nextCaptureUtc = now.AddMilliseconds(Math.Max(16, settings.RefreshIntervalMs.Value));

            var captureWidth = Math.Max(1, (int)MathF.Round(captureRect.Width));
            var captureHeight = Math.Max(1, (int)MathF.Round(captureRect.Height));

            try
            {
                EnsureCaptureBitmap(captureWidth, captureHeight);

                _captureGraphics!.CopyFromScreen(
                    (int)MathF.Round(windowRect.X + captureRect.X),
                    (int)MathF.Round(windowRect.Y + captureRect.Y),
                    0,
                    0,
                    _captureBitmap!.Size);

                if (TryComposeChangedOutput(captureRect, selectedCount))
                {
                    _graphics.AddOrUpdateImage(TextureName, _outputImage!);
                    _hasTexture = true;
                }
            }
            catch
            {
                DisposeTexture();
            }
        }

        private void EnsureCaptureBitmap(int width, int height)
        {
            if (_captureBitmap != null && _captureBitmapWidth == width && _captureBitmapHeight == height)
                return;

            _captureGraphics?.Dispose();
            _captureGraphics = null;
            _captureBitmap?.Dispose();
            _captureBitmap = new GdiBitmap(width, height, PixelFormat.Format32bppArgb);
            _captureGraphics = GdiGraphics.FromImage(_captureBitmap);
            _captureBitmapWidth = width;
            _captureBitmapHeight = height;
            _captureBuffer = Array.Empty<byte>();
            _hasSnapshot = false;
        }

        private bool TryComposeChangedOutput(RectangleF captureRect, int selectedCount)
        {
            var bitmap = _captureBitmap!;
            var rect = new GdiRectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                var stride = Math.Abs(data.Stride);
                var requiredBytes = stride * bitmap.Height;
                if (_captureBuffer.Length < requiredBytes)
                    _captureBuffer = new byte[requiredBytes];

                Marshal.Copy(data.Scan0, _captureBuffer, 0, requiredBytes);
                var outputWidth = 0;
                var outputHeight = 0;
                for (var i = 0; i < selectedCount; i++)
                {
                    outputWidth += Math.Max(1, (int)MathF.Round(_sourceRects[i].Width));
                    outputHeight = Math.Max(outputHeight, Math.Max(1, (int)MathF.Round(_sourceRects[i].Height)));
                }

                EnsureOutputImage(outputWidth, outputHeight);
                Array.Clear(_outputBuffer, 0, outputWidth * outputHeight * 4);

                var destinationX = 0;
                for (var i = 0; i < selectedCount; i++)
                {
                    var sourceRect = _sourceRects[i];
                    var sourceX = Math.Max(0, (int)MathF.Round(sourceRect.X - captureRect.X));
                    var sourceY = Math.Max(0, (int)MathF.Round(sourceRect.Y - captureRect.Y));
                    var sourceWidth = Math.Max(1, (int)MathF.Round(sourceRect.Width));
                    var sourceHeight = Math.Max(1, (int)MathF.Round(sourceRect.Height));
                    ComposeSlot(data.Stride, stride, sourceX, sourceY, sourceWidth, sourceHeight, destinationX, outputWidth);
                    destinationX += sourceWidth;
                }

                var outputBytes = outputWidth * outputHeight * 4;
                var currentPixels = _outputBuffer.AsSpan(0, outputBytes);
                var previousPixels = _previousOutputBuffer.AsSpan(0, outputBytes);
                if (_hasSnapshot && currentPixels.SequenceEqual(previousPixels))
                    return false;

                currentPixels.CopyTo(previousPixels);
                _hasSnapshot = true;
                CopyOutputBufferToImage(outputWidth, outputHeight);
                return true;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private void ComposeSlot(int sourceDataStride, int sourceStride, int sourceX, int sourceY, int sourceWidth, int sourceHeight, int destinationX, int outputWidth)
        {
            var clampedWidth = Math.Min(sourceWidth, _captureBitmapWidth - sourceX);
            var clampedHeight = Math.Min(sourceHeight, _captureBitmapHeight - sourceY);
            if (sourceX < 0 || sourceY < 0 || clampedWidth <= 0 || clampedHeight <= 0)
                return;

            for (var y = 0; y < clampedHeight; y++)
            {
                var captureY = sourceY + y;
                if (captureY < 0 || captureY >= _captureBitmapHeight)
                    continue;

                var sourceRow = sourceDataStride < 0 ? _captureBitmapHeight - 1 - captureY : captureY;
                var sourceOffset = sourceRow * sourceStride + sourceX * 4;
                var destinationOffset = (y * outputWidth + destinationX) * 4;

                for (var x = 0; x < clampedWidth; x++)
                {
                    var sourcePixelOffset = sourceOffset + x * 4;
                    var destinationPixelOffset = destinationOffset + x * 4;
                    _outputBuffer[destinationPixelOffset] = _captureBuffer[sourcePixelOffset + 2];
                    _outputBuffer[destinationPixelOffset + 1] = _captureBuffer[sourcePixelOffset + 1];
                    _outputBuffer[destinationPixelOffset + 2] = _captureBuffer[sourcePixelOffset];
                    _outputBuffer[destinationPixelOffset + 3] = _captureBuffer[sourcePixelOffset + 3];
                }
            }
        }

        private void EnsureOutputImage(int width, int height)
        {
            if (_outputImage != null && _outputWidth == width && _outputHeight == height)
                return;

            DisposeTexture();
            _outputImage?.Dispose();
            _outputImage = new Image<Rgba32>(width, height);
            _outputWidth = width;
            _outputHeight = height;
            _outputBuffer = new byte[width * height * 4];
            _previousOutputBuffer = new byte[width * height * 4];
            _hasSnapshot = false;
        }

        private void CopyOutputBufferToImage(int width, int height)
        {
            _outputImage!.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    var sourceOffset = y * width * 4;
                    for (var x = 0; x < row.Length; x++)
                    {
                        var pixelOffset = sourceOffset + x * 4;
                        row[x] = new Rgba32(
                            _outputBuffer[pixelOffset],
                            _outputBuffer[pixelOffset + 1],
                            _outputBuffer[pixelOffset + 2],
                            _outputBuffer[pixelOffset + 3]);
                    }
                }
            });
        }

        private void DisposeTexture()
        {
            if (!_hasTexture)
                return;

            try
            {
                _graphics.DisposeTexture(TextureName);
            }
            catch
            {
                // The host may already have cleared textures during plugin reload.
            }

            _hasTexture = false;
            _hasSnapshot = false;
        }

        private static Vector2 ToPositiveSize(Vector2 size)
        {
            return new Vector2(MathF.Max(1, size.X), MathF.Max(1, size.Y));
        }
    }
}
