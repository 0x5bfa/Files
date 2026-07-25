// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Archives;

public interface IArchiveProbe
{
	ValueTask<ArchiveProbeResult> ProbeAsync(
		ArchiveMountRequest request,
		CancellationToken cancellationToken = default);
}
