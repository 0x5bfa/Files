// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;
using Files.Core.Storage;
using OwlCore.Storage;

namespace Files.Core.Models;

public sealed class StorableModelFactory : IStorableModelFactory
{
	private readonly CapabilityPipeline capabilityPipeline;

	public StorableModelFactory(CapabilityPipeline? capabilityPipeline = null)
	{
		this.capabilityPipeline = capabilityPipeline ?? CapabilityPipeline.Empty;
	}

	public IStorableModel Create(IStorageSource source, IStorable coreModel)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(coreModel);

		ICapabilitySet? capabilities = null;

		try
		{
			var reference = new StorableReference(
				source.SourceId,
				coreModel.Id,
				(coreModel as IStorageAddressSource)?.Address);
			var context = new CapabilityContext(source, coreModel, reference);
			capabilities = capabilityPipeline.CreateSet(context);

			return coreModel switch
			{
				IFile file => new FileModel(file, reference, capabilities),
				IFolder folder => new FolderModel(source, folder, this, reference, capabilities),
				_ => new StorableModel(coreModel, reference, capabilities),
			};
		}
		catch
		{
			try
			{
				capabilities?.Dispose();
			}
			finally
			{
				if (coreModel is IDisposable disposableCoreModel)
				{
					disposableCoreModel.Dispose();
				}
			}

			throw;
		}
	}
}
