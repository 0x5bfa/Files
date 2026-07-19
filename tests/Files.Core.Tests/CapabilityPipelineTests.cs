// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Files.Core.Tests;

[TestClass]
public sealed class CapabilityPipelineTests
{
	[TestMethod]
	public void GetIsLazyAndCached()
	{
		var factory = new TestModelFactory();
		var coreModel = new TestStorable("item", "Item");
		var reference = new Files.Core.Storage.StorableReference(factory.Source.SourceId, coreModel.Id);
		var context = new CapabilityContext(factory.Source, coreModel, reference);
		var createCount = 0;
		var capability = new TestCapability("capability", []);
		var pipeline = new CapabilityPipelineBuilder()
			.AddContributor<TestCapability>(
				new DelegateCapabilityContributor<TestCapability>(_ =>
				{
					createCount++;
					return capability;
				}))
			.Build();

		using var set = pipeline.CreateSet(context);
		Assert.AreEqual(0, createCount);

		Assert.AreSame(capability, set.Get<TestCapability>());
		Assert.AreSame(capability, set.Get<TestCapability>());
		Assert.AreEqual(1, createCount);
}

	[TestMethod]
	public void ModelOwnedCapabilitiesAreDisposedInReverseCreationOrder()
	{
		var factory = new TestModelFactory();
		var coreModel = new TestStorable("item", "Item");
		var reference = new Files.Core.Storage.StorableReference(factory.Source.SourceId, coreModel.Id);
		var context = new CapabilityContext(factory.Source, coreModel, reference);
		var disposalOrder = new List<string>();
		var candidate = new TestCapability("candidate", disposalOrder);
		var wrapper = new TestCapability("wrapper", disposalOrder);
		var pipeline = new CapabilityPipelineBuilder()
			.AddContributor<TestCapability>(
				new DelegateCapabilityContributor<TestCapability>(_ => candidate))
			.AddDecorator<TestCapability>(
				new DelegateCapabilityDecorator<TestCapability>((_, _) => wrapper))
			.Build();

		using (var set = pipeline.CreateSet(context))
		{
			Assert.AreSame(wrapper, set.Get<TestCapability>());
		}

		CollectionAssert.AreEqual(new[] { "wrapper", "candidate" }, disposalOrder);
}

	[TestMethod]
	public void ExternalCapabilitiesAreNotDisposedByTheSet()
	{
		var factory = new TestModelFactory();
		var coreModel = new TestStorable("item", "Item");
		var reference = new Files.Core.Storage.StorableReference(factory.Source.SourceId, coreModel.Id);
		var context = new CapabilityContext(factory.Source, coreModel, reference);
		var capability = new TestCapability("external", []);
		var pipeline = new CapabilityPipelineBuilder()
			.AddContributor<TestCapability>(
				new DelegateCapabilityContributor<TestCapability>(_ => capability),
				ownership: CapabilityOwnership.External)
			.Build();

		using (var set = pipeline.CreateSet(context))
		{
			Assert.AreSame(capability, set.Get<TestCapability>());
		}

		Assert.IsFalse(capability.IsDisposed);
}

	[TestMethod]
	public void MultipleCandidatesWithoutAComposerFailExplicitly()
	{
		var factory = new TestModelFactory();
		var coreModel = new TestStorable("item", "Item");
		var reference = new Files.Core.Storage.StorableReference(factory.Source.SourceId, coreModel.Id);
		var context = new CapabilityContext(factory.Source, coreModel, reference);
		var pipeline = new CapabilityPipelineBuilder()
			.AddContributor<TestCapability>(new DelegateCapabilityContributor<TestCapability>(_ => new TestCapability("one", [])))
			.AddContributor<TestCapability>(new DelegateCapabilityContributor<TestCapability>(_ => new TestCapability("two", [])))
			.Build();

		using var set = pipeline.CreateSet(context);
		Assert.Throws<InvalidOperationException>(() => set.Get<TestCapability>());
}

	[TestMethod]
	public void PriorityComposerRejectsTiesAtTheHighestPriority()
	{
		var factory = new TestModelFactory();
		var coreModel = new TestStorable("item", "Item");
		var reference = new Files.Core.Storage.StorableReference(factory.Source.SourceId, coreModel.Id);
		var context = new CapabilityContext(factory.Source, coreModel, reference);
		var pipeline = new CapabilityPipelineBuilder()
			.AddContributor<TestCapability>(new DelegateCapabilityContributor<TestCapability>(_ => new TestCapability("one", [])), priority: 10)
			.AddContributor<TestCapability>(new DelegateCapabilityContributor<TestCapability>(_ => new TestCapability("two", [])), priority: 10)
			.SetComposer<TestCapability>(new PriorityCapabilityComposer<TestCapability>())
			.Build();

		using var set = pipeline.CreateSet(context);
		Assert.Throws<InvalidOperationException>(() => set.Get<TestCapability>());
	}
}
