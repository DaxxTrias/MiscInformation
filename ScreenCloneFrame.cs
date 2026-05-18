using System;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ExileCore2;
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
        private const int MaxCaptureDimension = 128;

        private readonly ExileCore2.Graphics _graphics;
        private GdiBitmap? _captureBitmap;
        private GdiGraphics? _captureGraphics;
        private Image<Rgba32>? _captureImage;
        private byte[] _pixelBuffer = Array.Empty<byte>();
        private DateTime _nextCaptureUtc = DateTime.MinValue;
        private bool _hasTexture;
        private int _bitmapWidth;
        private int _bitmapHeight;

        public ScreenCloneFrame(ExileCore2.Graphics graphics)
        {
            _graphics = graphics;
        }

        public void Render(CloneFrameSettings settings, RectangleF windowRect)
        {
            if (!settings.Enable.Value)
            {
                DisposeTexture();
                return;
            }

            var sourceSize = ToClampedCaptureSize(settings.SourceSize.Value);
            var targetSize = ToPositiveSize(settings.TargetSize.Value);
            if (sourceSize.X < 1 || sourceSize.Y < 1 || targetSize.X < 1 || targetSize.Y < 1)
                return;

            var sourcePosition = settings.SourcePosition.Value;
            var targetPosition = settings.TargetPosition.Value;
            var sourceRect = new RectangleF(sourcePosition.X, sourcePosition.Y, sourceSize.X, sourceSize.Y);
            var targetRect = new RectangleF(targetPosition.X, targetPosition.Y, targetSize.X, targetSize.Y);

            TryRefreshTexture(settings, windowRect, sourceRect);

            if (_hasTexture)
            {
                var opacity = Math.Clamp(settings.Opacity.Value, 0, 255);
                _graphics.DrawImage(TextureName, targetRect, GdiColor.FromArgb(opacity, GdiColor.White));
            }

            if (settings.DrawSourceOutline.Value)
                _graphics.DrawFrame(sourceRect, GdiColor.DeepSkyBlue, 1);

            if (settings.DrawTargetOutline.Value)
                _graphics.DrawFrame(targetRect, GdiColor.Gold, 1);
        }

        public void Dispose()
        {
            DisposeTexture();
            _captureGraphics?.Dispose();
            _captureGraphics = null;
            _captureBitmap?.Dispose();
            _captureBitmap = null;
            _captureImage?.Dispose();
            _captureImage = null;
            _pixelBuffer = Array.Empty<byte>();
        }

        private void TryRefreshTexture(CloneFrameSettings settings, RectangleF windowRect, RectangleF sourceRect)
        {
            var now = DateTime.UtcNow;
            if (now < _nextCaptureUtc)
                return;

            _nextCaptureUtc = now.AddMilliseconds(Math.Max(16, settings.RefreshIntervalMs.Value));

            var width = Math.Max(1, (int)MathF.Round(sourceRect.Width));
            var height = Math.Max(1, (int)MathF.Round(sourceRect.Height));

            try
            {
                EnsureBitmap(width, height);

                _captureGraphics!.CopyFromScreen(
                    (int)MathF.Round(windowRect.X + sourceRect.X),
                    (int)MathF.Round(windowRect.Y + sourceRect.Y),
                    0,
                    0,
                    _captureBitmap!.Size);

                CopyBitmapToImage(_captureBitmap!, _captureImage!);
                _graphics.AddOrUpdateImage(TextureName, _captureImage!);
                _hasTexture = true;
            }
            catch
            {
                DisposeTexture();
            }
        }

        private void EnsureBitmap(int width, int height)
        {
            if (_captureBitmap != null && _bitmapWidth == width && _bitmapHeight == height)
                return;

            DisposeTexture();
            _captureGraphics?.Dispose();
            _captureGraphics = null;
            _captureBitmap?.Dispose();
            _captureBitmap = new GdiBitmap(width, height, PixelFormat.Format32bppArgb);
            _captureGraphics = GdiGraphics.FromImage(_captureBitmap);
            _captureImage?.Dispose();
            _captureImage = new Image<Rgba32>(width, height);
            _bitmapWidth = width;
            _bitmapHeight = height;
            _pixelBuffer = new byte[width * height * 4];
        }

        private void CopyBitmapToImage(GdiBitmap bitmap, Image<Rgba32> image)
        {
            var rect = new GdiRectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                var stride = Math.Abs(data.Stride);
                var requiredBytes = stride * bitmap.Height;
                if (_pixelBuffer.Length < requiredBytes)
                    _pixelBuffer = new byte[requiredBytes];

                Marshal.Copy(data.Scan0, _pixelBuffer, 0, requiredBytes);

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
                                _pixelBuffer[pixelOffset + 2],
                                _pixelBuffer[pixelOffset + 1],
                                _pixelBuffer[pixelOffset],
                                _pixelBuffer[pixelOffset + 3]);
                        }
                    }
                });
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
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
        }

        private static Vector2 ToPositiveSize(Vector2 size)
        {
            return new Vector2(MathF.Max(1, size.X), MathF.Max(1, size.Y));
        }

        private static Vector2 ToClampedCaptureSize(Vector2 size)
        {
            return new Vector2(
                Math.Clamp(size.X, 1, MaxCaptureDimension),
                Math.Clamp(size.Y, 1, MaxCaptureDimension));
        }
    }
}
