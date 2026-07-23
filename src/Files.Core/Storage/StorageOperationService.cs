// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage;

/// <summary>
/// Routes requests to the first provider that supports them.
/// </summary>
public sealed class StorageOperationService : IStorageOperationService
{
	private readonly IReadOnlyList<IStorageOperationProvider> providers;

	public StorageOperationService(IEnumerable<IStorageOperationProvider> providers)
	{
		ArgumentNullException.ThrowIfNull(providers);

		this.providers = providers.ToArray();
		if (this.providers.Any(static provider => provider is null))
		{
			throw new ArgumentException(
				"The provider collection cannot contain null entries.",
				nameof(providers));
		}
	}

	public async ValueTask<StorageOperationResult> ExecuteAsync(
		StorageOperationRequest request,
		IProgress<StorageOperationProgress>? progress = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();

		IStorageOperationProvider? provider = null;
		try
		{
			provider = providers.FirstOrDefault(
				candidate => candidate.CanHandle(request));
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			return Failed(exception);
		}

		if (provider is null)
		{
			return Failed(new NotSupportedException(
				$"No storage operation provider can handle '{request.GetType().Name}'."));
		}

		try
		{
			return await provider
				.ExecuteAsync(request, progress, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			return Failed(exception);
		}
	}

	private static StorageOperationResult Failed(Exception exception)
	{
		return new StorageOperationResult(
			Succeeded: false,
			ResultItem: null,
			Error: exception);
	}
}
