// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Models;

namespace Files.Core.Browsing;

public sealed class BrowseSessionModel : IBrowseSessionModel
{
	private readonly IBrowseLocationResolver locationResolver;
	private readonly SemaphoreSlim navigationLock = new(1, 1);
	private bool isDisposed;

	public BrowseSessionModel(IBrowseLocationResolver locationResolver)
	{
		ArgumentNullException.ThrowIfNull(locationResolver);
		this.locationResolver = locationResolver;
		Items = Array.Empty<IStorableModel>();
	}

	public BrowseLocation? Location { get; private set; }

	public IReadOnlyList<IStorableModel> Items { get; private set; }

	public bool IsLoading { get; private set; }

	public Exception? Error { get; private set; }

	public event EventHandler? StateChanged;

	public async ValueTask NavigateAsync(BrowseLocation location, CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
		ArgumentNullException.ThrowIfNull(location);

		await navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			IsLoading = true;
			Error = null;
			OnStateChanged();

			try
			{
				var nextItems = new List<IStorableModel>();
				var loaded = false;

				try
				{
					await foreach (var item in locationResolver.GetItemsAsync(location, cancellationToken).ConfigureAwait(false))
					{
						nextItems.Add(item);
					}

					loaded = true;
				}
				finally
				{
					if (!loaded)
					{
						DisposeItems(nextItems);
					}
				}

				var previousItems = Items;
				Location = location;
				Items = nextItems.AsReadOnly();
				DisposeItems(previousItems);
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				Error = exception;
				throw;
			}
			finally
			{
				IsLoading = false;
				OnStateChanged();
			}
		}
		finally
		{
			navigationLock.Release();
		}
	}

	public ValueTask RefreshAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);

		return Location is null
			? ValueTask.CompletedTask
			: NavigateAsync(Location, cancellationToken);
	}

	public void Dispose()
	{
		if (isDisposed)
		{
			return;
		}

		isDisposed = true;
		DisposeItems(Items);
		Items = Array.Empty<IStorableModel>();
		navigationLock.Dispose();
		GC.SuppressFinalize(this);
	}

	private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

	private static void DisposeItems(IEnumerable<IStorableModel> items)
	{
		foreach (var item in items)
		{
			item.Dispose();
		}
	}
}
