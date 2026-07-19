// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;
using Files.Core.Storage;
using OwlCore.Storage;

namespace Files.Core.Models;

public class StorableModel : IStorableModel
{
	private bool isDisposed;

	public StorableModel(
		IStorable coreModel,
		StorableReference reference,
		ICapabilitySet capabilities)
	{
		ArgumentNullException.ThrowIfNull(coreModel);
		ArgumentNullException.ThrowIfNull(reference);
		ArgumentNullException.ThrowIfNull(capabilities);

		CoreModel = coreModel;
		Reference = reference;
		Name = coreModel.Name;
		Capabilities = capabilities;
	}

	public IStorable CoreModel { get; }

	public StorableReference Reference { get; }

	public string Name { get; }

	public ICapabilitySet Capabilities { get; }

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (isDisposed)
		{
			return;
		}

		isDisposed = true;

		if (disposing)
		{
			try
			{
				Capabilities.Dispose();
			}
			finally
			{
				if (CoreModel is IDisposable coreModel)
				{
					coreModel.Dispose();
				}
			}
		}
	}
}
