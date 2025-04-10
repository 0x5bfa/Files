// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App.Storage.Storables
{
	public partial class HomeFolder : IHomeFolder
	{
		public string Id => "Home"; // Will be "files://Home" in the future.

		public string Name => "Home";

		public IAsyncEnumerable<IStorableChild> GetItemsAsync(StorableType type = StorableType.Folder, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}

		public IAsyncEnumerable<IStorableChild> GetQuickAccessFolderAsync()
		{
			// {3936e9e4-d92c-4eee-a85a-bc16d5ea0819}



			throw new NotImplementedException();
		}
	}
}
