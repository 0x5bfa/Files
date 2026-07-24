// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using Files.Core.Capabilities;
using OwlCore.Storage;

namespace Files.Core.Capabilities.Previews;

public sealed class ExtensionPreviewContentTypeResolver : IPreviewContentTypeResolver
{
	private readonly IReadOnlyDictionary<string, PreviewContentType> contentTypes;

	public ExtensionPreviewContentTypeResolver(
		IEnumerable<KeyValuePair<string, string>> mappings)
	{
		ArgumentNullException.ThrowIfNull(mappings);

		var resolvedTypes = new Dictionary<string, PreviewContentType>(
			StringComparer.OrdinalIgnoreCase);

		foreach (var mapping in mappings)
		{
			ValidateExtension(mapping.Key);
			var contentType = new PreviewContentType(mapping.Value);

			if (!resolvedTypes.TryAdd(mapping.Key, contentType))
			{
				throw new ArgumentException(
					$"The extension '{mapping.Key}' is registered more than once.",
					nameof(mappings));
			}
		}

		contentTypes = resolvedTypes;
	}

	public bool TryResolve(
		CapabilityContext context,
		out PreviewContentType contentType)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (context.CoreModel is IFile file
			&& contentTypes.TryGetValue(Path.GetExtension(file.Name), out var resolvedType))
		{
			contentType = resolvedType;
			return true;
		}

		contentType = null!;
		return false;
	}

	private static void ValidateExtension(string extension)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(extension);

		if (extension.Length < 2
			|| extension[0] != '.'
			|| extension.Any(char.IsWhiteSpace)
			|| extension.IndexOf('.', 1) >= 0
			|| extension.Contains('/')
			|| extension.Contains('\\'))
		{
			throw new ArgumentException(
				$"The extension '{extension}' is invalid.",
				nameof(extension));
		}
	}
}
