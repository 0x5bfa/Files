// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Models;
using Files.Core.ViewSettings;

namespace Files.Core.Browsing;

public sealed class BrowseSessionModel : IBrowseSessionModel
{
	private readonly IBrowseLocationResolver locationResolver;
	private readonly IViewSettingsStore? viewSettingsStore;
	private readonly Dictionary<BrowseLocation, BrowseViewSettings> sessionViewSettings = [];
	private readonly SemaphoreSlim navigationLock = new(1, 1);
	private bool isDisposed;

	public BrowseSessionModel(
		IBrowseLocationResolver locationResolver,
		IViewSettingsStore? viewSettingsStore = null)
	{
		ArgumentNullException.ThrowIfNull(locationResolver);
		this.locationResolver = locationResolver;
		this.viewSettingsStore = viewSettingsStore;
		Items = Array.Empty<IStorableModel>();
		ViewSettings = BrowseViewSettings.Default;
	}

	public BrowseLocation? Location { get; private set; }

	public IReadOnlyList<IStorableModel> Items { get; private set; }

	public BrowseViewSettings ViewSettings { get; private set; }

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
				var nextViewSettings = viewSettingsStore is null
					? sessionViewSettings.GetValueOrDefault(location, BrowseViewSettings.Default)
					: await viewSettingsStore
						.GetAsync(location, cancellationToken)
						.ConfigureAwait(false)
						?? BrowseViewSettings.Default;
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
				ViewSettings = nextViewSettings;
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

	public async ValueTask UpdateViewSettingsAsync(
		BrowseViewSettings settings,
		CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
		ArgumentNullException.ThrowIfNull(settings);

		await navigationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			if (Location is null)
			{
				throw new InvalidOperationException("View settings require an active browse location.");
			}

			if (viewSettingsStore is not null)
			{
				await viewSettingsStore
					.SetAsync(Location, settings, cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				sessionViewSettings[Location] = settings;
			}

			ViewSettings = settings;
			OnStateChanged();
		}
		finally
		{
			navigationLock.Release();
		}
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
		sessionViewSettings.Clear();
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
