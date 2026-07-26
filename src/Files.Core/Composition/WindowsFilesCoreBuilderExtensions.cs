// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.Versioning;
using Files.Core.Capabilities.Changes;
using Files.Core.Capabilities.Previews;
using Files.Core.Capabilities.Properties;
using Files.Core.Capabilities.Thumbnails;
using Files.Core.Storage.Archives;
using Files.Core.Storage.Windows;

namespace Files.Core.Composition;

/// <summary>
/// Adds the Windows Shell vertical slice to a Core runtime.
/// </summary>
[SupportedOSPlatform("windows6.0.6000")]
public static class WindowsFilesCoreBuilderExtensions
{
	private const string WindowsCapabilitiesFeature =
		"Files.Core.Windows.Capabilities";
	private const string WindowsShellPreviewsFeature =
		"Files.Core.Previews.WindowsShell";

	public static FilesCoreBuilder AddWindowsStorage(
		this FilesCoreBuilder builder,
		WindowsStorageSource? source = null,
		IPreviewStreamAccessPolicy? streamPreviewPolicy = null,
		IWindowsShellPreviewPolicy? shellPreviewPolicy = null,
		bool enablePreviews = true,
		bool enableArchives = true,
		IArchiveCredentialProvider? archiveCredentialProvider = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var windowsSource = source ?? new WindowsStorageSource();
		try
		{
			builder
				.AddStorageSource(windowsSource)
				.AddStorageOperationProvider(
					new WindowsStorageOperationProvider(windowsSource));
		}
		catch (Exception registrationError)
			when (source is null)
		{
			try
			{
				windowsSource
					.DisposeAsync()
					.AsTask()
					.GetAwaiter()
					.GetResult();
			}
			catch (Exception cleanupError)
			{
				throw new AggregateException(
					"Windows storage registration and cleanup failed.",
					registrationError,
					cleanupError);
			}

			throw;
		}

		if (builder.TryRegisterFeature(WindowsCapabilitiesFeature))
		{
			builder.Capabilities
				.AddContributor<IThumbnailSource>(
					new WindowsThumbnailCapabilityContributor(
						new WindowsShellThumbnailBackend()),
					priority: 100,
					origin: "Windows Shell")
				.AddContributor<IPropertySource>(
					new PropertyProviderCapabilityContributor(
						new WindowsPropertyProvider()),
					priority: 100,
					origin: "Windows Shell")
				.AddContributor<IFolderChangeSource>(
					new FolderChangeCapabilityContributor(),
					priority: 100,
					origin: "Windows Shell");
		}

		if (enablePreviews)
		{
			builder.AddDefaultStreamPreviews(
				streamPreviewPolicy ?? AllowPreviewStreamAccessPolicy.Instance);
			AddWindowsShellPreviews(
				builder,
				shellPreviewPolicy ?? AllowWindowsShellPreviewPolicy.Instance);
		}

		if (enableArchives)
		{
			builder.AddArchiveBrowsing(
				archiveCredentialProvider);
		}

		return builder;
	}

	private static void AddWindowsShellPreviews(
		FilesCoreBuilder builder,
		IWindowsShellPreviewPolicy policy)
	{
		if (!builder.TryRegisterFeature(WindowsShellPreviewsFeature))
		{
			return;
		}

		var handlerResolver = new WindowsPreviewHandlerResolver(
			new WindowsShellPreviewHandlerAssociation());
		var provider = new WindowsShellPreviewProvider(
			handlerResolver,
			policy);
		builder.Capabilities.AddContributor<IPreviewSource>(
			new PreviewProviderCapabilityContributor(provider),
			priority: 100,
			origin: "Windows Shell preview handler");

		var previewScheduler = new WindowsShellScheduler(
			concurrentWorkerCount: 1);
		builder.Own(previewScheduler);
		builder.SetWindowsShellPreviewSessionFactory(
			dataRoot => new WindowsShellPreviewSessionFactory(
				dataRoot,
				previewScheduler));
	}
}
