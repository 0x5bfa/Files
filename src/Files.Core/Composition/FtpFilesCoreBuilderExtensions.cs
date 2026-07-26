// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities.Previews;
using Files.Core.Capabilities.Properties;
using Files.Core.Storage.Archives;
using Files.Core.Storage.Ftp;

namespace Files.Core.Composition;

/// <summary>
/// Adds one configured FTP vertical slice to a Core runtime.
/// </summary>
public static class FtpFilesCoreBuilderExtensions
{
	public static FilesCoreBuilder AddFtpStorage(
		this FilesCoreBuilder builder,
		FtpConnectionProfile profile,
		IFtpCredentialProvider? credentialProvider = null,
		IFtpSessionFactory? sessionFactory = null,
		IPreviewStreamAccessPolicy? streamPreviewPolicy = null,
		bool enablePreviews = true,
		bool enableArchives = true,
		IArchiveCredentialProvider? archiveCredentialProvider = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(profile);

		var source = new FtpStorageSource(
			profile,
			credentialProvider,
			sessionFactory);
		try
		{
			RegisterStorage(builder, source);
		}
		catch (Exception registrationError)
		{
			try
			{
				source
					.DisposeAsync()
					.AsTask()
					.GetAwaiter()
					.GetResult();
			}
			catch (Exception cleanupError)
			{
				throw new AggregateException(
					"FTP storage registration and cleanup failed.",
					registrationError,
					cleanupError);
			}

			throw;
		}

		return AddFtpCapabilities(
			builder,
			source,
			streamPreviewPolicy,
			enablePreviews,
			enableArchives,
			archiveCredentialProvider);
	}

	public static FilesCoreBuilder AddFtpStorage(
		this FilesCoreBuilder builder,
		FtpStorageSource source,
		IPreviewStreamAccessPolicy? streamPreviewPolicy = null,
		bool enablePreviews = true,
		bool enableArchives = true,
		IArchiveCredentialProvider? archiveCredentialProvider = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(source);

		RegisterStorage(builder, source);
		return AddFtpCapabilities(
			builder,
			source,
			streamPreviewPolicy,
			enablePreviews,
			enableArchives,
			archiveCredentialProvider);
	}

	private static void RegisterStorage(
		FilesCoreBuilder builder,
		FtpStorageSource source)
	{
		builder
			.AddStorageSource(source)
			.AddStorageOperationProvider(
				new FtpStorageOperationProvider(source));
	}

	private static FilesCoreBuilder AddFtpCapabilities(
		FilesCoreBuilder builder,
		FtpStorageSource source,
		IPreviewStreamAccessPolicy? streamPreviewPolicy,
		bool enablePreviews,
		bool enableArchives,
		IArchiveCredentialProvider? archiveCredentialProvider)
	{
		builder.Capabilities.AddContributor<IPropertySource>(
			new PropertyProviderCapabilityContributor(
				new FtpPropertyProvider(source)),
			priority: 100,
			origin: $"FTP:{source.Profile.ConnectionId}");

		if (enablePreviews)
		{
			builder.AddDefaultStreamPreviews(
				streamPreviewPolicy
					?? AllowPreviewStreamAccessPolicy.Instance);
		}

		if (enableArchives)
		{
			builder.AddArchiveBrowsing(archiveCredentialProvider);
		}

		return builder;
	}
}
