// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App2.ViewModels;
using Microsoft.UI.Dispatching;

namespace Files.App2.Commands;

public sealed class WindowCommandManager : IDisposable
{
	private readonly RootViewModel root;
	private readonly DispatcherQueue dispatcherQueue;
	private readonly Dictionary<CommandId, ICommandHandler> handlers;
	private readonly Dictionary<CommandId, CommandBindingViewModel> bindings = [];
	private readonly Dictionary<CommandId, CancellationTokenSource> activeCalls = [];
	private readonly CancellationTokenSource lifetime = new();
	private readonly object syncRoot = new();
	private int isDisposed;

	public WindowCommandManager(
		RootViewModel root,
		CommandRegistry registry,
		DispatcherQueue dispatcherQueue)
	{
		ArgumentNullException.ThrowIfNull(root);
		ArgumentNullException.ThrowIfNull(registry);
		ArgumentNullException.ThrowIfNull(dispatcherQueue);

		this.root = root;
		this.dispatcherQueue = dispatcherQueue;
		handlers = new(registry.CreateHandlers(root));
		foreach (var descriptor in registry.Descriptors)
		{
			bindings.Add(
				descriptor.Id,
				new CommandBindingViewModel(this, descriptor));
		}
	}

	public CommandBindingViewModel GetBinding(CommandId id)
	{
		EnsureActive();
		if (!bindings.TryGetValue(id, out var binding))
		{
			throw new KeyNotFoundException(
				$"The command ID '{id}' is not registered.");
		}

		return binding;
	}

	public void RefreshStates()
	{
		if (Volatile.Read(ref isDisposed) is not 0)
		{
			return;
		}

		if (!dispatcherQueue.HasThreadAccess)
		{
			if (!dispatcherQueue.TryEnqueue(
				() =>
				{
					if (Volatile.Read(ref isDisposed) is 0)
					{
						RefreshStates();
					}
				}))
			{
				throw new InvalidOperationException(
					"The Files.App2 UI dispatcher rejected command state updates.");
			}

			return;
		}

		var context = new CommandContext(root);
		foreach (var pair in handlers)
		{
			bindings[pair.Key].UpdateState(pair.Value.GetState(context));
		}
	}

	public async Task<CommandExecutionResult> ExecuteAsync(
		CommandId id,
		object? parameter = null,
		CancellationToken cancellationToken = default)
	{
		EnsureActive();
		if (!handlers.TryGetValue(id, out var handler))
		{
			throw new KeyNotFoundException(
				$"The command ID '{id}' is not registered.");
		}

		var context = new CommandContext(root, parameter);
		var state = handler.GetState(context);
		if (!state.IsVisible || !state.IsEnabled)
		{
			return CommandExecutionResult.Unsupported();
		}

		CancellationTokenSource callCancellation;
		lock (syncRoot)
		{
			if (handler.ConcurrencyPolicy is
				CommandConcurrencyPolicy.RejectWhileRunning
				&& activeCalls.ContainsKey(id))
			{
				return CommandExecutionResult.Unsupported();
			}

			if (handler.ConcurrencyPolicy is
				CommandConcurrencyPolicy.CancelPrevious
				&& activeCalls.TryGetValue(id, out var previous))
			{
				previous.Cancel();
			}

			callCancellation = CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken,
				lifetime.Token);
			activeCalls[id] = callCancellation;
		}

		try
		{
			var result = await handler
				.ExecuteAsync(context, callCancellation.Token)
				.ConfigureAwait(false);
			if (result.Status is CommandExecutionStatus.Failed
				&& result.Error is { } error)
			{
				ReportError(error);
			}

			return result;
		}
		catch (OperationCanceledException)
		{
			return CommandExecutionResult.Canceled();
		}
		catch (Exception exception)
		{
			ReportError(exception);
			return CommandExecutionResult.Failed(exception);
		}
		finally
		{
			lock (syncRoot)
			{
				if (activeCalls.TryGetValue(id, out var active)
					&& ReferenceEquals(active, callCancellation))
				{
					activeCalls.Remove(id);
				}
			}

			callCancellation.Dispose();
			if (Volatile.Read(ref isDisposed) is 0)
			{
				RefreshStates();
			}
		}
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref isDisposed, 1) is not 0)
		{
			return;
		}

		lifetime.Cancel();
		lock (syncRoot)
		{
			foreach (var call in activeCalls.Values)
			{
				call.Cancel();
				call.Dispose();
			}

			activeCalls.Clear();
		}

		bindings.Clear();
		handlers.Clear();
		lifetime.Dispose();
	}

	private void ReportError(Exception exception)
	{
		if (Volatile.Read(ref isDisposed) is not 0)
		{
			return;
		}

		if (dispatcherQueue.HasThreadAccess)
		{
			root.ReportOperationError(exception);
			return;
		}

		if (!dispatcherQueue.TryEnqueue(
			() => root.ReportOperationError(exception)))
		{
			throw new InvalidOperationException(
				"The Files.App2 UI dispatcher rejected a command error.",
				exception);
		}
	}

	private void EnsureActive() =>
		ObjectDisposedException.ThrowIf(
			Volatile.Read(ref isDisposed) is not 0,
			this);
}
