// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;
using Files.Core.Storage.Archives;
using Files.Core.Storage.Windows;
using OwlCore.Storage;

namespace Files.Core.Capabilities.Archives;

public sealed class ArchiveSourceCapabilityContributor
	: ICapabilityContributor<IArchiveSource>
{
	private static readonly string[] DefaultExtensions =
	[
		".7z",
		".gz",
		".jar",
		".lzh",
		".mrpack",
		".rar",
		".tar",
		".zip",
	];

	private readonly IReadOnlyList<string> extensions;

	public ArchiveSourceCapabilityContributor(
		IEnumerable<string>? extensions = null)
	{
		var extensionArray = (extensions ?? DefaultExtensions)
			.Select(NormalizeExtension)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();
		if (extensionArray.Length is 0)
		{
			throw new ArgumentException(
				"At least one archive extension is required.",
				nameof(extensions));
		}

		this.extensions = Array.AsReadOnly(extensionArray);
	}

	public IArchiveSource? Create(CapabilityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// SevenZip archive entries belong to a scoped mount that is not
		// registered in FilesDataRoot. Nested archives require an explicit
		// mount-chain contract rather than a reference that becomes stale
		// when the containing browse context is replaced.
		if (context.CoreModel is IArchiveEntry
			|| context.CoreModel is IArchiveSource)
		{
			return null;
		}

		var isArchiveFile = context.CoreModel is IFile;
		var isShellArchiveFolder =
			context.Source is WindowsStorageSource
			&& context.CoreModel is IFolder
			&& context.CoreModel is IWindowsStorable
			{
				IsStream: true,
			};
		if (!isArchiveFile && !isShellArchiveFolder)
		{
			return null;
		}

		var extensionSource = context.CoreModel
			is IWindowsStorable windowsStorable
				? windowsStorable.FileSystemPath
					?? windowsStorable.ParsingName
				: context.CoreModel.Name;
		return extensions.Any(
			extension => extensionSource.EndsWith(
				extension,
				StringComparison.OrdinalIgnoreCase))
			? new ArchiveSource(context.Reference)
			: null;
	}

	private static string NormalizeExtension(string extension)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(extension);
		var trimmedExtension = extension.Trim();
		return trimmedExtension[0] is '.'
			? trimmedExtension
			: $".{trimmedExtension}";
	}

	private sealed record ArchiveSource(
		StorableReference Archive)
		: IArchiveSource;
}
