// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage.Windows;

namespace Files.Core.Capabilities.Previews;

public enum WindowsShellPreviewSessionState
{
	Created,
	Activating,
	Initialized,
	Previewing,
	Faulted,
	Disposed,
}

public sealed class WindowsShellPreviewSession : IWindowsShellPreviewSession
{
	private readonly WindowsPreviewTarget target;
	private readonly IWindowsPreviewHandlerController controller;
	private readonly IWindowsShellScheduler scheduler;
	private readonly object syncRoot = new();
	private Task? disposeTask;
	private WindowsShellPreviewSessionState state =
		WindowsShellPreviewSessionState.Created;

	internal WindowsShellPreviewSession(
		WindowsPreviewTarget target,
		IWindowsPreviewHandlerController controller,
		IWindowsShellScheduler scheduler)
	{
		ArgumentNullException.ThrowIfNull(target);
		ArgumentNullException.ThrowIfNull(controller);
		ArgumentNullException.ThrowIfNull(scheduler);

		this.target = target;
		this.controller = controller;
		this.scheduler = scheduler;
	}

	public WindowsShellPreviewSessionState State
	{
		get
		{
			lock (syncRoot)
			{
				return state;
			}
		}
	}

	public ValueTask SetBoundsAsync(
		WindowsPreviewBounds bounds,
		CancellationToken cancellationToken = default)
	{
		EnsurePreviewing();
		return new ValueTask(scheduler.InvokeOperationAsync(
			() =>
			{
				controller.SetBounds(bounds);
				return true;
			},
			cancellationToken));
	}

	public ValueTask SetThemeAsync(
		WindowsPreviewColor background,
		WindowsPreviewColor foreground,
		CancellationToken cancellationToken = default)
	{
		EnsurePreviewing();
		return new ValueTask(scheduler.InvokeOperationAsync(
			() =>
			{
				controller.SetTheme(background, foreground);
				return true;
			},
			cancellationToken));
	}

	public ValueTask SetFocusAsync(CancellationToken cancellationToken = default)
	{
		EnsurePreviewing();
		return new ValueTask(scheduler.InvokeOperationAsync(
			() =>
			{
				controller.SetFocus();
				return true;
			},
			cancellationToken));
	}

	public async ValueTask<nint> QueryFocusAsync(
		CancellationToken cancellationToken = default)
	{
		EnsurePreviewing();
		return await scheduler
			.InvokeOperationAsync(
				() => controller.QueryFocus(),
				cancellationToken)
			.ConfigureAwait(false);
	}

	public ValueTask<bool> TryTranslateAcceleratorAsync(
		nint messagePointer,
		CancellationToken cancellationToken = default)
	{
		EnsurePreviewing();
		return new ValueTask<bool>(scheduler.InvokeOperationAsync(
			() => controller.TryTranslateAccelerator(messagePointer),
			cancellationToken));
	}

	internal void TransitionTo(WindowsShellPreviewSessionState nextState)
	{
		lock (syncRoot)
		{
			if (state is WindowsShellPreviewSessionState.Disposed)
			{
				return;
			}

			state = nextState;
		}
	}

	internal void CleanupOnPreviewSta()
	{
		try
		{
			controller.Dispose();
		}
		finally
		{
			target.Dispose();
			lock (syncRoot)
			{
				state = WindowsShellPreviewSessionState.Disposed;
			}
		}
	}

	public ValueTask DisposeAsync()
	{
		lock (syncRoot)
		{
			if (disposeTask is not null)
			{
				return new ValueTask(disposeTask);
			}

			state = WindowsShellPreviewSessionState.Disposed;
			disposeTask = DisposeCoreAsync();
			return new ValueTask(disposeTask);
		}
	}

	private async Task DisposeCoreAsync()
	{
		try
		{
			await scheduler
				.InvokeOperationAsync(
					() =>
					{
						controller.Dispose();
						return true;
					})
				.ConfigureAwait(false);
		}
		finally
		{
			target.Dispose();
		}
	}

	private void EnsurePreviewing()
	{
		lock (syncRoot)
		{
			if (state is not WindowsShellPreviewSessionState.Previewing)
			{
				throw new ObjectDisposedException(nameof(WindowsShellPreviewSession));
			}
		}
	}
}
