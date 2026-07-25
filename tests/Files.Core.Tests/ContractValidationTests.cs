// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities.Previews;
using Files.Core.Capabilities.Properties;
using Files.Core.Capabilities.Thumbnails;
using Files.Core.ViewSettings;

namespace Files.Core.Tests;

[TestClass]
public sealed class ContractValidationTests
{
	[TestMethod]
	public void CapabilityRequestsRejectUnknownEnumsAndInvalidIds()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new ThumbnailRequest(
				64,
				(ThumbnailMode)int.MaxValue));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new PreviewRequest(
				hydrationPolicy:
					(PreviewHydrationPolicy)int.MaxValue));
		Assert.Throws<ArgumentException>(
			() => new PropertyRequest(["System.Size", "System.Size"]));
		Assert.Throws<ArgumentException>(
			() => new PropertyRequest(["System.Size", " "]));
		Assert.Throws<ArgumentException>(
			() => new ThumbnailResult(
				ReadOnlyMemory<byte>.Empty,
				"image/png",
				false));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new StreamPreviewResult(
				new MemoryStream(),
				"text/plain",
				contentLength: -1));
	}

	[TestMethod]
	public void ViewSettingsRejectAmbiguousOrNonFiniteValues()
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new ViewColumnSettings(
				"System.Size",
				double.NaN,
				0));
		Assert.Throws<ArgumentException>(
			() => new BrowseViewSettings(
				columns:
				[
					new ViewColumnSettings("System.Size", 100, 0),
					new ViewColumnSettings("System.Size", 120, 1),
				]));
		Assert.Throws<ArgumentException>(
			() => new BrowseViewSettings(
				columns:
				[
					new ViewColumnSettings("System.Size", 100, 0),
					new ViewColumnSettings("System.DateModified", 120, 0),
				]));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new BrowseViewSettings(
				itemSize: double.PositiveInfinity));
	}
}
