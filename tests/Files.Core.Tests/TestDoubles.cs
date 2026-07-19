// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using Files.Core.Browsing;
using Files.Core.Models;
using Files.Core.Storage;
using Files.Core.ViewSettings;
using OwlCore.Storage;

namespace Files.Core.Tests;

internal sealed class TestStorageSource : IStorageSource
{
	public StorageSourceId SourceId { get; } = new("test");

	public string ProviderId => "test";

	public string DisplayName => "Test";

	public async IAsyncEnumerable<IFolder> GetRootsAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await Task.CompletedTask.ConfigureAwait(false);
		yield break;
	}

	public bool CanResolve(StorageAddress address) => false;

	public ValueTask<IStorable> ResolveAsync(
		StorageAddress address,
		CancellationToken cancellationToken = default)
		=> throw new NotSupportedException();

	public ValueTask<IStorable> ResolveAsync(
		StorableReference reference,
		CancellationToken cancellationToken = default)
		=> throw new NotSupportedException();

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal class TestStorable : IStorable
{
	public TestStorable(string id, string name)
	{
		Id = id;
		Name = name;
	}

	public string Id { get; }

	public string Name { get; }
}

internal sealed class DisposableStorable : TestStorable, IDisposable
{
	public DisposableStorable(string id, string name)
		: base(id, name)
	{
	}

	public bool IsDisposed { get; private set; }

	public void Dispose() => IsDisposed = true;
}

internal sealed class TestCapability : IDisposable
{
	private readonly IList<string> disposalOrder;

	public TestCapability(string name, IList<string> disposalOrder)
	{
		Name = name;
		this.disposalOrder = disposalOrder;
	}

	public string Name { get; }

	public bool IsDisposed { get; private set; }

	public void Dispose()
	{
		if (IsDisposed)
		{
			return;
		}

		IsDisposed = true;
		disposalOrder.Add(Name);
	}
}

internal sealed class TestModelFactory
{
	private readonly TestStorageSource source = new();

	public TestStorageSource Source => source;

	public StorableModel CreateModel(string id, string name, out DisposableStorable coreModel)
	{
		coreModel = new DisposableStorable(id, name);
		var reference = new StorableReference(
			source.SourceId,
			coreModel.Id,
			new StorageAddress("test", coreModel.Id));
		var context = new Files.Core.Capabilities.CapabilityContext(source, coreModel, reference);
		return new StorableModel(
			coreModel,
			reference,
			Files.Core.Capabilities.CapabilityPipeline.Empty.CreateSet(context));
	}
}

internal sealed class TestBrowseLocationResolver : IBrowseLocationResolver
{
	public TestBrowseLocationResolver(IEnumerable<IStorableModel> items, Exception? exception = null)
	{
		Items = items.ToList();
		Exception = exception;
	}

	public IList<IStorableModel> Items { get; }

	public Exception? Exception { get; set; }

	public async IAsyncEnumerable<IStorableModel> GetItemsAsync(
		BrowseLocation location,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(location);
		cancellationToken.ThrowIfCancellationRequested();

		foreach (var item in Items)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return item;
			await Task.Yield();
		}

		if (Exception is not null)
		{
			throw Exception;
		}
	}
}

internal sealed class TestViewSettingsStore : IViewSettingsStore
{
	private readonly Dictionary<BrowseLocation, BrowseViewSettings> values = [];

	public ValueTask<BrowseViewSettings?> GetAsync(
		BrowseLocation location,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult(values.GetValueOrDefault(location));
	}

	public ValueTask SetAsync(
		BrowseLocation location,
		BrowseViewSettings settings,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		values[location] = settings;
		return ValueTask.CompletedTask;
	}
}
