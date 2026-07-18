// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using Files.Core.Models;

namespace Files.Core.Browsing;

public sealed class BrowseLocationResolver : IBrowseLocationResolver
{
	private readonly IReadOnlyList<IBrowseLocationHandler> handlers;

	public BrowseLocationResolver(IEnumerable<IBrowseLocationHandler> handlers)
	{
		ArgumentNullException.ThrowIfNull(handlers);
		this.handlers = Array.AsReadOnly(handlers.ToArray());
	}

	public async IAsyncEnumerable<IStorableModel> GetItemsAsync(
		BrowseLocation location,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(location);

		var handler = handlers.FirstOrDefault(candidate => candidate.CanHandle(location))
			?? throw new InvalidOperationException($"No handler is registered for '{location.GetType().Name}'.");

		await foreach (var item in handler.GetItemsAsync(location, cancellationToken).ConfigureAwait(false))
		{
			yield return item;
		}
	}
}
