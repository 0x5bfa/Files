// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;
using Files.Core.Capabilities.Properties;
using Files.Core.Models;
using Files.Core.Storage;
using Files.Core.Storage.Windows;
using OwlCore.Storage;

namespace Files.Core.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsPropertyTests
{
	[TestMethod]
	public async Task WindowsPropertyProviderReadsOnlyRequestedProperties()
	{
		var directoryPath = Path.Combine(Path.GetTempPath(), $"Files.Core.PropertyTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(directoryPath);
		var filePath = Path.Combine(directoryPath, "properties.bin");
		var content = new byte[37];
		File.WriteAllBytes(filePath, content);

		try
		{
			await using var scheduler = new WindowsShellScheduler();
			await using var source = new WindowsStorageSource(scheduler: scheduler);
			var coreModel = await source.ResolveAsync(
				new StorageAddress(WindowsStorageSource.FileAddressScheme, filePath));

			var pipeline = new CapabilityPipelineBuilder()
				.AddContributor<IPropertySource>(
					new PropertyProviderCapabilityContributor(new WindowsPropertyProvider()),
					origin: "Windows Property System")
				.SetComposer<IPropertySource>(new PropertySourceComposer())
				.Build();

			using var model = new StorableModelFactory(pipeline).Create(source, coreModel);
			var propertySource = model.Get<IPropertySource>();
			Assert.IsNotNull(propertySource);

			var properties = await propertySource.GetPropertiesAsync(
				new PropertyRequest(["System.Size"]));

			Assert.AreEqual((ulong)content.Length, (ulong)properties["System.Size"]!);
			Assert.AreEqual(1, properties.Count);
			Assert.IsFalse(properties.ContainsKey("System.DateModified"));
		}
		finally
		{
			Directory.Delete(directoryPath, recursive: true);
		}
	}
}
