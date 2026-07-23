// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Browsing;

public sealed class BrowseLocationResolver : IBrowseLocationResolver
{
	private readonly IReadOnlyList<IBrowseLocationHandler> handlers;

	public BrowseLocationResolver(IEnumerable<IBrowseLocationHandler> handlers)
	{
		ArgumentNullException.ThrowIfNull(handlers);
		this.handlers = Array.AsReadOnly(handlers.ToArray());
	}

	public ValueTask<IBrowseLocationContext> OpenAsync(
		BrowseLocation location,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(location);

		var handler = handlers.FirstOrDefault(candidate => candidate.CanHandle(location))
			?? throw new InvalidOperationException($"No handler is registered for '{location.GetType().Name}'.");

		return handler.OpenAsync(location, cancellationToken);
	}
}
