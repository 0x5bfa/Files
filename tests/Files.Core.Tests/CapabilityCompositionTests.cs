// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using System.Text;
using Files.Core.Capabilities;
using Files.Core.Capabilities.Previews;
using Files.Core.Capabilities.Properties;
using Files.Core.Capabilities.Thumbnails;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Files.Core.Tests;

[TestClass]
public sealed class CapabilityCompositionTests
{
	[TestMethod]
	public async Task ThumbnailCompositionUsesPriorityAndStopsAfterSuccess()
	{
		var context = CreateContext();
		var lower = new TestThumbnailSource(null);
		var higher = new TestThumbnailSource("higher");
		var composer = new ThumbnailSourceComposer();
		var source = composer.Compose(context, [
			new CapabilityCandidate<IThumbnailSource>(lower, 10, "lower", CapabilityOwnership.External),
			new CapabilityCandidate<IThumbnailSource>(higher, 20, "higher", CapabilityOwnership.External),
		])!;

		var result = await source.GetThumbnailAsync(new ThumbnailRequest(64));
		Assert.IsNotNull(result);
		Assert.AreEqual("higher", Encoding.UTF8.GetString(result.Content.Span));
		Assert.AreEqual(1, higher.CallCount);
		Assert.AreEqual(0, lower.CallCount);
	}

	[TestMethod]
	public async Task ThumbnailCompositionFallsBackWhenHigherPriorityReturnsNull()
	{
		var context = CreateContext();
		var first = new TestThumbnailSource(null);
		var second = new TestThumbnailSource("fallback");
		var source = new ThumbnailSourceComposer().Compose(context, [
			new CapabilityCandidate<IThumbnailSource>(first, 20, "first", CapabilityOwnership.External),
			new CapabilityCandidate<IThumbnailSource>(second, 10, "second", CapabilityOwnership.External),
		])!;

		var result = await source.GetThumbnailAsync(new ThumbnailRequest(64));
		Assert.IsNotNull(result);
		Assert.AreEqual("fallback", Encoding.UTF8.GetString(result.Content.Span));
		Assert.AreEqual(1, first.CallCount);
		Assert.AreEqual(1, second.CallCount);
	}

	[TestMethod]
	public async Task PreviewCompositionRoutesByPriority()
	{
		var context = CreateContext();
		var first = new TestPreviewSource(null);
		var second = new TestPreviewSource("preview");
		var source = new PreviewSourceComposer().Compose(context, [
			new CapabilityCandidate<IPreviewSource>(first, 5, "first", CapabilityOwnership.External),
			new CapabilityCandidate<IPreviewSource>(second, 1, "second", CapabilityOwnership.External),
		])!;

		await using var result = await source.GetPreviewAsync(new PreviewRequest());
		Assert.IsNotNull(result);
		var streamResult = result as StreamPreviewResult;
		Assert.IsNotNull(streamResult);
		Assert.AreEqual("preview", await ReadTextAsync(streamResult!.Content));
	}

	[TestMethod]
	public async Task PreviewCompositionStopsFallbackAfterBlockedResult()
	{
		var context = CreateContext();
		var blocked = new BlockedPreviewSource();
		var fallback = new TestPreviewSource("fallback");
		var source = new PreviewSourceComposer().Compose(context, [
			new CapabilityCandidate<IPreviewSource>(blocked, 20, "blocked", CapabilityOwnership.External),
			new CapabilityCandidate<IPreviewSource>(fallback, 10, "fallback", CapabilityOwnership.External),
		])!;

		await using var result = await source.GetPreviewAsync(new PreviewRequest());

		Assert.IsInstanceOfType<BlockedPreviewResult>(result);
		Assert.AreEqual(0, fallback.CallCount);
	}

	[TestMethod]
	public async Task PropertyCompositionMergesSourcesWithHigherPriorityWinning()
	{
		var context = CreateContext();
		var low = new TestPropertySource(new Dictionary<string, object?>
		{
			["name"] = "low",
			["lowOnly"] = true,
		});
		var high = new TestPropertySource(new Dictionary<string, object?>
		{
			["name"] = "high",
			["Name"] = "case-sensitive",
		});
		var source = new PropertySourceComposer().Compose(context, [
			new CapabilityCandidate<IPropertySource>(low, 10, "low", CapabilityOwnership.External),
			new CapabilityCandidate<IPropertySource>(high, 20, "high", CapabilityOwnership.External),
		])!;

		var values = await source.GetPropertiesAsync(
			new PropertyRequest(["name", "Name", "lowOnly"]));
		Assert.AreEqual("high", values["name"]);
		Assert.AreEqual("case-sensitive", values["Name"]);
		Assert.AreEqual(true, values["lowOnly"]);
}

	private static CapabilityContext CreateContext()
	{
		var source = new TestStorageSource();
		var coreModel = new TestStorable("item", "Item");
		return new CapabilityContext(
			source,
			coreModel,
			new Files.Core.Storage.StorableReference(source.SourceId, coreModel.Id));
	}

	private static async Task<string> ReadTextAsync(Stream stream)
	{
		using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
		return await reader.ReadToEndAsync();
	}

	private sealed class TestThumbnailSource : IThumbnailSource
	{
		private readonly string? value;

		public TestThumbnailSource(string? value) => this.value = value;

		public int CallCount { get; private set; }

		public ValueTask<ThumbnailResult?> GetThumbnailAsync(
			ThumbnailRequest request,
			CancellationToken cancellationToken = default)
		{
			CallCount++;
			return ValueTask.FromResult<ThumbnailResult?>(value is null
				? null
				: new ThumbnailResult(
					Encoding.UTF8.GetBytes(value),
					"text/plain",
					false));
		}
	}

		private sealed class TestPreviewSource : IPreviewSource
	{
		private readonly string? value;

		public TestPreviewSource(string? value) => this.value = value;

		public int CallCount { get; private set; }

		public ValueTask<PreviewResult?> GetPreviewAsync(
			PreviewRequest request,
			CancellationToken cancellationToken = default)
		{
			CallCount++;
			return ValueTask.FromResult<PreviewResult?>(value is null
				? null
				: new StreamPreviewResult(
					new MemoryStream(Encoding.UTF8.GetBytes(value), writable: false),
					"text/plain"));
		}
	}

	private sealed class BlockedPreviewSource : IPreviewSource
	{
		public ValueTask<PreviewResult?> GetPreviewAsync(
			PreviewRequest request,
			CancellationToken cancellationToken = default)
			=> ValueTask.FromResult<PreviewResult?>(
				new BlockedPreviewResult(PreviewBlockReason.RequiresHydration));
	}

	private sealed class TestPropertySource : IPropertySource
	{
		private readonly IReadOnlyDictionary<string, object?> values;

		public TestPropertySource(IReadOnlyDictionary<string, object?> values) => this.values = values;

		public ValueTask<IReadOnlyDictionary<string, object?>> GetPropertiesAsync(
			PropertyRequest request,
			CancellationToken cancellationToken = default)
			=> ValueTask.FromResult(values);
	}
}
