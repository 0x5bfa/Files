// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace Files.Core.Capabilities;

internal sealed class CapabilitySet : ICapabilitySet
{
	private static readonly object MissingCapability = new();

	private readonly object syncRoot = new();
	private readonly CapabilityPipeline pipeline;
	private readonly CapabilityContext context;
	private readonly Dictionary<Type, object> resolvedCapabilities = [];
	private readonly List<object> ownedInstances = [];
	private Task? disposeTask;
	private bool isDisposed;

	public CapabilitySet(CapabilityPipeline pipeline, CapabilityContext context)
	{
		ArgumentNullException.ThrowIfNull(pipeline);
		ArgumentNullException.ThrowIfNull(context);

		this.pipeline = pipeline;
		this.context = context;
	}

	public TCapability? Get<TCapability>()
		where TCapability : class
	{
		lock (syncRoot)
		{
			ObjectDisposedException.ThrowIf(isDisposed, this);

			if (resolvedCapabilities.TryGetValue(typeof(TCapability), out var cached))
			{
				return ReferenceEquals(cached, MissingCapability)
					? null
					: (TCapability)cached;
			}

			var resolution = pipeline.Resolve<TCapability>(context);

			foreach (var instance in resolution.OwnedInstances)
			{
				if (!ownedInstances.Any(existing => ReferenceEquals(existing, instance)))
				{
					ownedInstances.Add(instance);
				}
			}

			resolvedCapabilities.Add(
				typeof(TCapability),
				resolution.Capability ?? MissingCapability);

			return resolution.Capability;
		}
	}

	public bool TryGet<TCapability>([NotNullWhen(true)] out TCapability? capability)
		where TCapability : class
	{
		capability = Get<TCapability>();
		return capability is not null;
	}

	public void Dispose()
	{
		DisposeAsync().AsTask().GetAwaiter().GetResult();
	}

	public ValueTask DisposeAsync()
	{
		lock (syncRoot)
		{
			if (disposeTask is not null)
			{
				return new ValueTask(disposeTask);
			}

			isDisposed = true;
			var instances = ownedInstances.ToArray();
			ownedInstances.Clear();
			resolvedCapabilities.Clear();
			disposeTask = DisposeInstancesAsync(instances);
			GC.SuppressFinalize(this);
			return new ValueTask(disposeTask);
		}
	}

	internal static async Task DisposeInstancesAsync(IEnumerable<object> instances)
	{
		List<Exception>? exceptions = null;

		foreach (var instance in instances.Reverse())
		{
			try
			{
				if (instance is IAsyncDisposable asyncDisposable)
				{
					await asyncDisposable.DisposeAsync().ConfigureAwait(false);
				}
				else
				{
					(instance as IDisposable)?.Dispose();
				}
			}
			catch (Exception exception)
			{
				(exceptions ??= []).Add(exception);
			}
		}

		if (exceptions is not null)
		{
			throw new AggregateException("One or more capabilities could not be disposed.", exceptions);
		}
	}
}
