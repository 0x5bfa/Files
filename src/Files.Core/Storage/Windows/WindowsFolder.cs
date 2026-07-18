// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using OwlCore.Storage;
using Windows.Win32;
using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

public sealed class WindowsFolder : WindowsStorable, IChildFolder
{
	internal WindowsFolder(IShellItem shellItem)
		: base(shellItem)
	{
	}

	public async IAsyncEnumerable<IStorableChild> GetItemsAsync(
		StorableType type = StorableType.All,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await Task.CompletedTask.ConfigureAwait(false);

		if (type is StorableType.None)
		{
			yield break;
		}

		var result = ShellItem.BindToHandler(null, PInvoke.BHID_EnumItems, out IEnumShellItems? enumerator);
		result.ThrowOnFailure();

		if (enumerator is null)
		{
			throw new InvalidOperationException("The Shell folder returned no item enumerator.");
		}

		var children = new IShellItem[1];

		try
		{
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();
				result = enumerator.Next(children);

				if (result == global::Windows.Win32.Foundation.HRESULT.S_FALSE)
				{
					yield break;
				}

				result.ThrowOnFailure();

				var child = WindowsStorableFactory.Create(children[0]);
				var include = child switch
				{
					WindowsFile => type.HasFlag(StorableType.File),
					WindowsFolder => type.HasFlag(StorableType.Folder),
					_ => false,
				};

				if (include)
				{
					yield return child;
				}
				else
				{
					child.Dispose();
				}
			}
		}
		finally
		{
			enumerator = null;
		}
	}
}
