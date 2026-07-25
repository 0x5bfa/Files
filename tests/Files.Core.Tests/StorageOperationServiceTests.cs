// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;

namespace Files.Core.Tests;

[TestClass]
public sealed class StorageOperationServiceTests
{
	[TestMethod]
	public async Task SelectsFirstProviderThatCanHandleTheRequest()
	{
		var request = CreateRenameRequest();
		var first = new TestOperationProvider(canHandle: false);
		var second = new TestOperationProvider(canHandle: true);
		var service = new StorageOperationService([first, second]);

		Assert.IsTrue(service.CanHandle(request));
		var result = await service.ExecuteAsync(request);

		Assert.IsTrue(result.Succeeded);
		Assert.AreEqual(0, first.ExecuteCount);
		Assert.AreEqual(1, second.ExecuteCount);
	}

	[TestMethod]
	public async Task ReportsUnsupportedRequestAsFailedResult()
	{
		var service = new StorageOperationService(
			[new TestOperationProvider(canHandle: false)]);

		Assert.IsFalse(service.CanHandle(new UnknownOperationRequest()));
		var result = await service.ExecuteAsync(new UnknownOperationRequest());

		Assert.IsFalse(result.Succeeded);
		Assert.IsInstanceOfType<NotSupportedException>(result.Error);
		Assert.IsNull(result.ResultItem);
	}

	[TestMethod]
	public async Task MapsProviderExceptionToFailedResult()
	{
		var expected = new IOException("operation failed");
		var provider = new TestOperationProvider(
			canHandle: true,
			exception: expected);
		var service = new StorageOperationService([provider]);

		var result = await service.ExecuteAsync(CreateRenameRequest());

		Assert.IsFalse(result.Succeeded);
		Assert.AreSame(expected, result.Error);
	}

	[TestMethod]
	public async Task PropagatesCancellationBeforeProviderExecution()
	{
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var provider = new TestOperationProvider(canHandle: true);
		var service = new StorageOperationService([provider]);

		await Assert.ThrowsAsync<OperationCanceledException>(
			async () => await service.ExecuteAsync(
				CreateRenameRequest(),
				cancellationToken: cancellation.Token));

		Assert.AreEqual(0, provider.ExecuteCount);
	}

	[TestMethod]
	public async Task MapsNullProviderResultToFailedResult()
	{
		var service = new StorageOperationService(
			[new NullOperationProvider()]);

		var result = await service.ExecuteAsync(CreateRenameRequest());

		Assert.IsFalse(result.Succeeded);
		Assert.IsInstanceOfType<InvalidOperationException>(result.Error);
	}

	[TestMethod]
	public void RequestsRejectUnknownEnumValues()
	{
		var reference = CreateRenameRequest().Item;

		Assert.Throws<ArgumentOutOfRangeException>(
			() => new CreateItemOperationRequest(
				reference,
				"item",
				(StorageItemKind)int.MaxValue));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new CopyOperationRequest(
				reference,
				reference,
				conflictBehavior:
					(StorageConflictBehavior)int.MaxValue));
	}

	[TestMethod]
	public void ResultAndProgressRejectContradictoryState()
	{
		var reference = CreateRenameRequest().Item;

		Assert.Throws<ArgumentException>(
			() => new StorageOperationResult(
				Succeeded: true,
				ResultItem: reference,
				Error: new IOException("unexpected")));
		Assert.Throws<ArgumentNullException>(
			() => new StorageOperationResult(
				Succeeded: false,
				ResultItem: null));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new StorageOperationProgress(
				CompletedItems: 2,
				TotalItems: 1));
	}

	private static RenameOperationRequest CreateRenameRequest()
	{
		return new RenameOperationRequest(
			new StorableReference(
				new StorageSourceId("test"),
				"item-1",
				new StorageAddress("test", "item-1")),
			"renamed.txt");
	}

	private sealed record UnknownOperationRequest : StorageOperationRequest;

	private sealed class NullOperationProvider : IStorageOperationProvider
	{
		public bool CanHandle(StorageOperationRequest request) => true;

		public ValueTask<StorageOperationResult> ExecuteAsync(
			StorageOperationRequest request,
			IProgress<StorageOperationProgress>? progress = null,
			CancellationToken cancellationToken = default)
		{
			return ValueTask.FromResult<StorageOperationResult>(null!);
		}
	}

	private sealed class TestOperationProvider : IStorageOperationProvider
	{
		private readonly bool canHandle;
		private readonly Exception? exception;

		public TestOperationProvider(bool canHandle, Exception? exception = null)
		{
			this.canHandle = canHandle;
			this.exception = exception;
		}

		public int ExecuteCount { get; private set; }

		public bool CanHandle(StorageOperationRequest request) => canHandle;

		public ValueTask<StorageOperationResult> ExecuteAsync(
			StorageOperationRequest request,
			IProgress<StorageOperationProgress>? progress = null,
			CancellationToken cancellationToken = default)
		{
			ExecuteCount++;
			if (exception is not null)
			{
				throw exception;
			}

			return ValueTask.FromResult(new StorageOperationResult(true, null));
		}
	}
}
