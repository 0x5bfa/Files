// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.AppModels;
using Files.Core.Composition;
using Microsoft.Extensions.Logging;

namespace Files.App.Bootstrap
{
	/// <summary>
	/// Owns the process-wide Core runtime and the model for the main window.
	/// </summary>
	internal sealed class FilesAppCoreHost : IAsyncDisposable
	{
		private readonly ILogger<FilesAppCoreHost> logger;
		private readonly SemaphoreSlim mutationLock = new(1, 1);
		private readonly HashSet<Guid> leasedTabs = [];
		private readonly object disposalLock = new();
		private WindowModel? mainWindow;
		private Task? disposeTask;
		private bool isDisposed;

		public FilesAppCoreHost(ILogger<FilesAppCoreHost> logger)
		{
			ArgumentNullException.ThrowIfNull(logger);

			this.logger = logger;
			Runtime = FilesCoreComposition.CreateRuntime();
		}

		public FilesCoreRuntime Runtime { get; }

		public WindowModel? MainWindow => Volatile.Read(ref mainWindow);

		public async ValueTask InitializeAsync(
			CancellationToken cancellationToken = default)
		{
			await mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				ObjectDisposedException.ThrowIf(isDisposed, this);
				if (mainWindow is not null)
				{
					return;
				}

				mainWindow = await Runtime.Application
					.CreateWindowAsync(cancellationToken: cancellationToken)
					.ConfigureAwait(false);
			}
			finally
			{
				mutationLock.Release();
			}
		}

		public async ValueTask<CoreTabLease> AcquireTabAsync(
			CancellationToken cancellationToken = default)
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
			await mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				ObjectDisposedException.ThrowIf(isDisposed, this);
				var window = mainWindow
					?? throw new InvalidOperationException(
						"The Core window model has not been initialized.");
				var tab = window.Tabs.FirstOrDefault(
					candidate => !leasedTabs.Contains(candidate.Id));
				tab ??= await window
					.OpenTabAsync(cancellationToken: cancellationToken)
					.ConfigureAwait(false);
				leasedTabs.Add(tab.Id);
				return new CoreTabLease(this, tab);
			}
			finally
			{
				mutationLock.Release();
			}
		}

		public ValueTask DisposeAsync()
		{
			lock (disposalLock)
			{
				if (disposeTask is not null)
				{
					return new ValueTask(disposeTask);
				}

				isDisposed = true;
				disposeTask = DisposeCoreAsync();
				return new ValueTask(disposeTask);
			}
		}

		internal async ValueTask ReleaseTabAsync(
			Guid tabId,
			CancellationToken cancellationToken = default)
		{
			await mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				if (!leasedTabs.Remove(tabId) || isDisposed)
				{
					return;
				}

				if (mainWindow is { } window)
				{
					await window
						.CloseTabAsync(tabId, cancellationToken)
						.ConfigureAwait(false);
				}
			}
			finally
			{
				mutationLock.Release();
			}
		}

		internal void ReportBackgroundFailure(Exception exception, string operation)
		{
			logger.LogWarning(exception, "Core adapter operation {Operation} failed.", operation);
		}

		private async Task DisposeCoreAsync()
		{
			await mutationLock.WaitAsync().ConfigureAwait(false);
			try
			{
				leasedTabs.Clear();
				mainWindow = null;
			}
			finally
			{
				mutationLock.Release();
			}

			await Runtime.DisposeAsync().ConfigureAwait(false);
			mutationLock.Dispose();
			GC.SuppressFinalize(this);
		}
	}

	internal sealed class CoreTabLease : IAsyncDisposable
	{
		private FilesAppCoreHost? owner;

		internal CoreTabLease(FilesAppCoreHost owner, TabModel model)
		{
			this.owner = owner;
			Model = model;
		}

		public TabModel Model { get; }

		public ValueTask DisposeAsync()
		{
			var currentOwner = Interlocked.Exchange(ref owner, null);
			return currentOwner is null
				? ValueTask.CompletedTask
				: currentOwner.ReleaseTabAsync(Model.Id);
		}
	}
}
