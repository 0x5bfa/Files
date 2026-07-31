// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;

namespace Files.App.Adapters.Core
{
	internal static class ThumbnailImageFactory
	{
		public static async Task<BitmapImage> CreateAsync(
			ReadOnlyMemory<byte> encodedImage)
		{
			using var managedStream = new MemoryStream(
				encodedImage.ToArray(),
				writable: false);
			using var randomAccessStream = managedStream.AsRandomAccessStream();
			var image = new BitmapImage();
			await image.SetSourceAsync(randomAccessStream);
			return image;
		}
	}
}
