// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Browsing;
using Files.Core.Capabilities;
using Files.Core.Capabilities.Archives;
using Files.Core.Storage.Archives;
using Files.Core.Storage.Archives.SevenZip;

namespace Files.Core.Composition;

public static class ArchiveFilesCoreBuilderExtensions
{
	private const string ArchiveBrowsingFeature =
		"Files.Core.Archives.Browsing";

	public static FilesCoreBuilder AddArchiveBrowsing(
		this FilesCoreBuilder builder,
		IArchiveCredentialProvider? credentialProvider = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var sevenZipBackend = new SevenZipArchiveBackend();
		return builder.AddArchiveBrowsing(
			[
				new WindowsShellArchiveBackend(),
				sevenZipBackend,
			],
			sevenZipBackend,
			credentialProvider);
	}

	public static FilesCoreBuilder AddArchiveBrowsing(
		this FilesCoreBuilder builder,
		IEnumerable<IArchiveBackend> backends,
		IArchiveProbe? probe = null,
		IArchiveCredentialProvider? credentialProvider = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(backends);

		if (!builder.TryRegisterFeature(ArchiveBrowsingFeature))
		{
			return builder;
		}

		var selector = new ArchiveBackendSelector(
			backends,
			probe);
		builder.Capabilities
			.SetComposer<IArchiveSource>(
				new PriorityCapabilityComposer<IArchiveSource>())
			.AddContributor<IArchiveSource>(
				new ArchiveSourceCapabilityContributor(),
				priority: 100,
				origin: "Archive browsing");
		builder.AddBrowseLocationHandler(
			dataRoot => new ArchiveBrowseLocationHandler(
				dataRoot,
				selector,
				credentialProvider));
		return builder;
	}
}
