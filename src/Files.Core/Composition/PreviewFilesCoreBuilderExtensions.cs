// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities.Previews;

namespace Files.Core.Composition;

/// <summary>
/// Adds storage-independent stream preview capabilities.
/// </summary>
public static class PreviewFilesCoreBuilderExtensions
{
	private const string DefaultStreamPreviewsFeature =
		"Files.Core.Previews.DefaultStreams";

	public static FilesCoreBuilder AddDefaultStreamPreviews(
		this FilesCoreBuilder builder,
		IPreviewStreamAccessPolicy? policy = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		if (!builder.TryRegisterFeature(DefaultStreamPreviewsFeature))
		{
			return builder;
		}

		var contentTypes = new ExtensionPreviewContentTypeResolver(
			new Dictionary<string, string>(
				StringComparer.OrdinalIgnoreCase)
			{
				[".bmp"] = "image/bmp",
				[".csv"] = "text/csv",
				[".gif"] = "image/gif",
				[".htm"] = "text/html",
				[".html"] = "text/html",
				[".jpeg"] = "image/jpeg",
				[".jpg"] = "image/jpeg",
				[".json"] = "application/json",
				[".md"] = "text/markdown",
				[".mkv"] = "video/x-matroska",
				[".mp3"] = "audio/mpeg",
				[".mp4"] = "video/mp4",
				[".pdf"] = "application/pdf",
				[".png"] = "image/png",
				[".svg"] = "image/svg+xml",
				[".txt"] = "text/plain",
				[".wav"] = "audio/wav",
				[".webm"] = "video/webm",
				[".webp"] = "image/webp",
				[".xml"] = "application/xml",
			});
		var provider = new StreamPreviewProvider(
			contentTypes,
			policy ?? AllowPreviewStreamAccessPolicy.Instance);
		builder.Capabilities.AddContributor<IPreviewSource>(
			new PreviewProviderCapabilityContributor(provider),
			priority: 200,
			origin: "Core stream preview");
		return builder;
	}
}
