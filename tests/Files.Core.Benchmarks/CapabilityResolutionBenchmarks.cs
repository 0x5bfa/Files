// Copyright (c) Files Community
// Licensed under the MIT License.

using BenchmarkDotNet.Attributes;
using Files.Core.Capabilities;
using Files.Core.Storage;
using OwlCore.Storage;

namespace Files.Core.Benchmarks;

[MemoryDiagnoser]
public class CapabilityResolutionBenchmarks
{
	[Params(1, 4, 16)]
	public int ContributorCount { get; set; }

	private CapabilityPipeline pipeline = null!;
	private CapabilityContext context = null!;
	private ICapabilitySet hotSet = null!;

	[GlobalSetup]
	public void Setup()
	{
		var source = new BenchmarkStorageSource();
		var coreModel = new BenchmarkStorable("item", "Item");
		var reference = new StorableReference(source.SourceId, coreModel.Id);
		context = new CapabilityContext(source, coreModel, reference);

		var builder = new CapabilityPipelineBuilder();
		for (var index = 0; index < ContributorCount; index++)
		{
			var value = index.ToString();
			builder.AddContributor<BenchmarkCapability>(
				new DelegateCapabilityContributor<BenchmarkCapability>(_ => new BenchmarkCapability(value)),
				priority: index);
		}

		pipeline = builder
			.SetComposer<BenchmarkCapability>(new PriorityCapabilityComposer<BenchmarkCapability>())
			.Build();
		hotSet = pipeline.CreateSet(context);
		_ = hotSet.Get<BenchmarkCapability>();
	}

	[GlobalCleanup]
	public void Cleanup() => hotSet.Dispose();

	[Benchmark(Baseline = true)]
	public string ColdResolution()
	{
		using var set = pipeline.CreateSet(context);
		return set.Get<BenchmarkCapability>()!.Value;
	}

	[Benchmark]
	public string CachedResolution() => hotSet.Get<BenchmarkCapability>()!.Value;
}

internal sealed class BenchmarkCapability
{
	public BenchmarkCapability(string value) => Value = value;

	public string Value { get; }
}

internal sealed class BenchmarkStorable : IStorable
{
	public BenchmarkStorable(string id, string name)
	{
		Id = id;
		Name = name;
	}

	public string Id { get; }

	public string Name { get; }
}

internal sealed class BenchmarkStorageSource : IStorageSource
{
	public StorageSourceId SourceId { get; } = new("benchmark");

	public string ProviderId => "benchmark";

	public string DisplayName => "Benchmark";

	public async IAsyncEnumerable<IFolder> GetRootsAsync(
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await Task.CompletedTask.ConfigureAwait(false);
		yield break;
	}

	public bool CanResolve(StorageAddress address) => false;

	public ValueTask<IStorable> ResolveAsync(
		StorageAddress address,
		CancellationToken cancellationToken = default)
		=> throw new NotSupportedException();

	public ValueTask<IStorable> ResolveAsync(
		StorableReference reference,
		CancellationToken cancellationToken = default)
		=> throw new NotSupportedException();

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
