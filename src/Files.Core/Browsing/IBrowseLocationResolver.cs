// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Browsing;

public interface IBrowseLocationResolver
{
	ValueTask<IBrowseLocationContext> OpenAsync(
		BrowseLocation location,
		CancellationToken cancellationToken = default);
}
