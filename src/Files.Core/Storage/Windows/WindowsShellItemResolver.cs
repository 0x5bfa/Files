// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Win32;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;

namespace Files.Core.Storage.Windows;

/// <summary>
/// Materializes Shell items on the scheduler and never returns a COM object to callers.
/// </summary>
internal sealed unsafe class WindowsShellItemResolver
{
	private readonly IWindowsShellScheduler scheduler;

	public WindowsShellItemResolver(IWindowsShellScheduler scheduler)
	{
		ArgumentNullException.ThrowIfNull(scheduler);
		this.scheduler = scheduler;
	}

	public Task<T> InvokeAsync<T>(
		WindowsItemLocator locator,
		Func<IShellItem, T> action,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(locator);
		ArgumentNullException.ThrowIfNull(action);

		return scheduler.InvokeAsync(
			() => InvokeCore(locator, action),
			cancellationToken);
	}

	public Task<T> InvokeConcurrentAsync<T>(
		WindowsItemLocator locator,
		Func<IShellItem, T> action,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(locator);
		ArgumentNullException.ThrowIfNull(action);

		return scheduler.InvokeConcurrentAsync(
			() => InvokeCore(locator, action),
			cancellationToken);
	}

	public Task<T> InvokeAsync<T>(
		string parsingName,
		Func<IShellItem, T> action,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(parsingName);
		ArgumentNullException.ThrowIfNull(action);

		return scheduler.InvokeAsync(
			() =>
			{
				var result = PInvoke.SHCreateItemFromParsingName(
					parsingName,
					null,
					out IShellItem shellItem);

				result.ThrowOnFailure();
				return action(shellItem);
			},
			cancellationToken);
	}

	public Task<T> InvokeConcurrentAsync<T>(
		string parsingName,
		Func<IShellItem, T> action,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(parsingName);
		ArgumentNullException.ThrowIfNull(action);

		return scheduler.InvokeConcurrentAsync(
			() =>
			{
				var result = PInvoke.SHCreateItemFromParsingName(
					parsingName,
					null,
					out IShellItem shellItem);

				if (result.Failed)
				{
					return default!;
				}

				return action(shellItem);
			},
			cancellationToken);
	}

	private static T InvokeCore<T>(
		WindowsItemLocator locator,
		Func<IShellItem, T> action)
	{
		var shellItem = TryCreateFromPidl(locator)
			?? CreateFromParsingName(locator.ParsingName);

		return shellItem is null ? default! : action(shellItem);
	}

	private static unsafe IShellItem? TryCreateFromPidl(WindowsItemLocator locator)
	{
		if (locator.AbsolutePidl.IsEmpty)
		{
			return null;
		}

		fixed (byte* pidlBytes = locator.AbsolutePidl.Span)
		{
			var interfaceId = typeof(IShellItem).GUID;
			void* itemPointer = null;
			var result = PInvoke.SHCreateItemFromIDList(
				(ITEMIDLIST*)pidlBytes,
				&interfaceId,
				out object itemObject);

			if (result.Failed || itemObject is not IShellItem shellItem)
			{
				return null;
			}

			return shellItem;
		}
	}

	private static IShellItem? CreateFromParsingName(string parsingName)
	{
		var result = PInvoke.SHCreateItemFromParsingName(
			parsingName,
			null,
			out IShellItem shellItem);

		return result.Succeeded ? shellItem : null;
	}
}
