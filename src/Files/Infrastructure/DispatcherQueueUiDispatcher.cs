// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Dispatching;

namespace Files.Infrastructure;

public sealed class DispatcherQueueUiDispatcher : IUiDispatcher
{
	private readonly DispatcherQueue dispatcherQueue;

	public DispatcherQueueUiDispatcher(DispatcherQueue dispatcherQueue)
	{
		ArgumentNullException.ThrowIfNull(dispatcherQueue);
		this.dispatcherQueue = dispatcherQueue;
	}

	public bool HasThreadAccess => dispatcherQueue.HasThreadAccess;

	public bool TryEnqueue(Action callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
		return dispatcherQueue.TryEnqueue(() => callback());
	}
}
