// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Files.Core.ItemFeatures.Thumbnails;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.Graphics.GdiPlus;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

/// <summary>
/// Extracts Windows Shell thumbnails and materializes them as PNG bytes.
/// </summary>
[SupportedOSPlatform("windows6.0.6000")]
public sealed class WindowsShellThumbnailBackend
{
	private static readonly Lazy<nuint> gdiPlusToken = new(StartGdiPlus);
	private static readonly Lazy<Guid?> pngEncoder = new(FindPngEncoder);

	internal unsafe WindowsThumbnailPayload? GetThumbnail(
		IShellItemImageFactory imageFactory,
		ThumbnailRequest request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(imageFactory);
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();

		return request.Mode switch
		{
			ThumbnailMode.Icon => TryGetImage(
				imageFactory,
				request.RequestedSize,
				SIIGBF.SIIGBF_ICONONLY,
				isFallback: false,
				cancellationToken),
			ThumbnailMode.Content => TryGetImage(
				imageFactory,
				request.RequestedSize,
				SIIGBF.SIIGBF_THUMBNAILONLY,
				isFallback: false,
				cancellationToken),
			ThumbnailMode.PreferContent => TryGetImage(
				imageFactory,
				request.RequestedSize,
				SIIGBF.SIIGBF_THUMBNAILONLY,
				isFallback: false,
				cancellationToken)
				?? TryGetImage(
					imageFactory,
					request.RequestedSize,
					SIIGBF.SIIGBF_ICONONLY,
					isFallback: true,
					cancellationToken),
			_ => throw new ArgumentOutOfRangeException(nameof(request.Mode)),
		};
	}

	private static unsafe WindowsThumbnailPayload? TryGetImage(
		IShellItemImageFactory imageFactory,
		int requestedSize,
		SIIGBF flags,
		bool isFallback,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		HBITMAP bitmap = default;
		var result = imageFactory.GetImage(
			new SIZE(requestedSize, requestedSize),
			flags,
			&bitmap);

		if (result.Failed || bitmap.IsNull)
		{
			if (!bitmap.IsNull)
			{
				PInvoke.DeleteObject(bitmap);
			}

			return null;
		}

		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			var content = WindowsPngEncoder.Encode(bitmap, cancellationToken);

			return content is null
				? null
				: new WindowsThumbnailPayload(content, "image/png", isFallback);
		}
		finally
		{
			PInvoke.DeleteObject(bitmap);
		}
	}

	private static unsafe Guid? FindPngEncoder()
	{
		if (PInvoke.GdipGetImageEncodersSize(out var count, out var size) is not Status.Ok
			|| count is 0
			|| size is 0)
		{
			return null;
		}

		var codecs = (ImageCodecInfo*)NativeMemory.Alloc(size);

		try
		{
			if (PInvoke.GdipGetImageEncoders(count, size, codecs) is not Status.Ok)
			{
				return null;
			}

			for (var index = 0U; index < count; index++)
			{
				if (codecs[index].FormatID == PInvoke.ImageFormatPNG)
				{
					return codecs[index].Clsid;
				}
			}

			return null;
		}
		finally
		{
			NativeMemory.Free(codecs);
		}
	}

	private static nuint StartGdiPlus()
	{
		var input = new GdiplusStartupInput
		{
			GdiplusVersion = 1,
		};
		var output = default(GdiplusStartupOutput);
		nuint token = 0;

		var result = PInvoke.GdiplusStartup(ref token, input, ref output);
		if (result is not Status.Ok)
		{
			throw new InvalidOperationException(
				$"Failed to initialize GDI+. Status: {result}.");
		}

		return token;
	}

	private static class WindowsPngEncoder
	{
		public static unsafe byte[]? Encode(
			HBITMAP bitmap,
			CancellationToken cancellationToken)
		{
			_ = gdiPlusToken.Value;
			var encoder = pngEncoder.Value;
			if (encoder is not { } encoderClsid)
			{
				return null;
			}

			cancellationToken.ThrowIfCancellationRequested();

			GpBitmap* gpBitmap = null;
			var createResult = PInvoke.GdipCreateBitmapFromHBITMAP(
				bitmap,
				default,
				&gpBitmap);

			if (createResult is not Status.Ok || gpBitmap is null)
			{
				return null;
			}

			try
			{
				var streamResult = PInvoke.CreateStreamOnHGlobal(
					HGLOBAL.Null,
					true,
					out IStream stream);

				if (streamResult.Failed)
				{
					return null;
				}

				cancellationToken.ThrowIfCancellationRequested();

				if (PInvoke.GdipSaveImageToStream(
					(GpImage*)gpBitmap,
					stream,
					&encoderClsid,
					(EncoderParameters*)null) is not Status.Ok)
				{
					return null;
				}

				if (stream.Stat(out var stat, STATFLAG.STATFLAG_NONAME).Failed
					|| stat.cbSize > int.MaxValue)
				{
					return null;
				}

				var content = GC.AllocateUninitializedArray<byte>((int)stat.cbSize);
				stream.Seek(0, SeekOrigin.Begin);

				if (content.Length is not 0)
				{
					fixed (byte* buffer = content)
					{
						if (stream.Read(buffer, (uint)content.Length).Failed)
						{
							return null;
						}
					}
				}

				cancellationToken.ThrowIfCancellationRequested();
				return content;
			}
			finally
			{
				PInvoke.GdipDisposeImage((GpImage*)gpBitmap);
			}
		}
	}
}

internal sealed record WindowsThumbnailPayload(
	byte[] Content,
	string ContentType,
	bool IsFallback);
