// Copyright (c) Files Community
// Licensed under the MIT License.


namespace Files.App.Storage.Storables
{
	public partial class HomeFolder : IHomeFolder
	{
		public string Id => throw new NotImplementedException();

		public string Name => "Home";

		public IAsyncEnumerable<IStorableChild> GetItemsAsync(StorableType type = StorableType.All, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException();
		}
	}
}
