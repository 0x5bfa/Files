// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

/// <summary>
/// Keeps a Shell enumerator private and routes all access through its creating STA lane.
/// </summary>
internal sealed class ShellFolderEnumerator : IAsyncDisposable
{
	private readonly IWindowsShellScheduler scheduler;
	private readonly IWindowsItemIdentityProvider identityProvider;
	private IEnumShellItems? enumerator;
	private bool isCompleted;
	private int isDisposed;

	public ShellFolderEnumerator(
		IWindowsShellScheduler scheduler,
		IEnumShellItems enumerator,
		IWindowsItemIdentityProvider identityProvider)
	{
		ArgumentNullException.ThrowIfNull(scheduler);
		ArgumentNullException.ThrowIfNull(enumerator);
		ArgumentNullException.ThrowIfNull(identityProvider);

		this.scheduler = scheduler;
		this.enumerator = enumerator;
		this.identityProvider = identityProvider;
	}

	public unsafe Task<IReadOnlyList<WindowsStorableSnapshot>> ReadNextAsync(
		int maximumCount,
		CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
		ObjectDisposedException.ThrowIf(Volatile.Read(ref isDisposed) != 0, this);

		return scheduler.InvokeAsync<IReadOnlyList<WindowsStorableSnapshot>>(
			() =>
			{
				if (isCompleted)
				{
					return Array.Empty<WindowsStorableSnapshot>();
				}

				var nativeEnumerator = enumerator
					?? throw new ObjectDisposedException(nameof(ShellFolderEnumerator));
				var snapshots = new List<WindowsStorableSnapshot>(maximumCount);
				var children = new IShellItem[1];
				uint fetched = 0;

				while (snapshots.Count < maximumCount)
				{
					cancellationToken.ThrowIfCancellationRequested();
					var result = nativeEnumerator.Next(1, children, &fetched);

					if (result == global::Windows.Win32.Foundation.HRESULT.S_FALSE)
					{
						isCompleted = true;
						break;
					}

					result.ThrowOnFailure();
					snapshots.Add(ShellItemHelpers.CreateSnapshot(children[0], identityProvider));
				}

				return snapshots;
			},
			cancellationToken);
	}

	public ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref isDisposed, 1) != 0)
		{
			return ValueTask.CompletedTask;
		}

		return new ValueTask(DisposeCoreAsync());
	}

	private async Task DisposeCoreAsync()
	{
		await scheduler.InvokeAsync(
			() =>
			{
				enumerator = null;
				return true;
			}).ConfigureAwait(false);

		GC.SuppressFinalize(this);
	}
}
