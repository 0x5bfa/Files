// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Files.App.Utils.Shell
{
	public sealed partial class ThreadWithMessageQueue : Disposable
	{
		private readonly BlockingCollection<IInternal> messageQueue;

		private readonly Thread thread;
		private readonly object lifetimeLock = new();
		private readonly AutoResetEvent workAvailable = new(false);
		private bool shutdownRequested;

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				lock (lifetimeLock)
				{
					if (!shutdownRequested)
					{
						shutdownRequested = true;
						messageQueue.CompleteAdding();
						workAvailable.Set();
					}
				}

				if (Thread.CurrentThread != thread)
					thread.Join();
			}
		}

		public Task<V> PostMethod<V>(Func<V> payload)
		{
			var message = new Internal<V>(payload);
			Enqueue(message);

			return message.Task;
		}

		public Task PostMethod(Action payload)
		{
			var message = new ActionInternal(payload);
			Enqueue(message);

			return message.Task;
		}

		public ThreadWithMessageQueue()
		{
			messageQueue = new BlockingCollection<IInternal>(new ConcurrentQueue<IInternal>());

			thread = new Thread(Run);

			thread.SetApartmentState(ApartmentState.STA);

			// Do not prevent app from closing
			thread.IsBackground = true;

			thread.Start();
		}

		private void Enqueue(IInternal message)
		{
			lock (lifetimeLock)
			{
				if (shutdownRequested)
				{
					message.Fail(new ObjectDisposedException(nameof(ThreadWithMessageQueue)));
					return;
				}

				messageQueue.Add(message);
				workAvailable.Set();
			}
		}

		private unsafe void Run()
		{
			var hr = PInvoke.OleInitialize(null);
			try
			{
				hr.ThrowOnFailure();
				HANDLE[] handles = [new(workAvailable.SafeWaitHandle.DangerousGetHandle())];
				while (!messageQueue.IsCompleted)
				{
					while (messageQueue.TryTake(out var message))
					{
						message.Invoke();
						DispatchPendingMessages();
					}

					if (!messageQueue.IsCompleted)
					{
						var result = PInvoke.MsgWaitForMultipleObjectsEx(handles, uint.MaxValue, QUEUE_STATUS_FLAGS.QS_ALLINPUT,
							MSG_WAIT_FOR_MULTIPLE_OBJECTS_EX_FLAGS.MWMO_INPUTAVAILABLE);
						if (result == WAIT_EVENT.WAIT_FAILED)
							throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastPInvokeError());

						DispatchPendingMessages();
					}
				}
			}
			catch (Exception exception)
			{
				lock (lifetimeLock)
				{
					shutdownRequested = true;
					messageQueue.CompleteAdding();
				}

				while (messageQueue.TryTake(out var message))
					message.Fail(exception);
			}
			finally
			{
				if (hr.Succeeded)
					PInvoke.OleUninitialize();

				messageQueue.Dispose();
				workAvailable.Dispose();
			}
		}

		private static void DispatchPendingMessages()
		{
			while (PInvoke.PeekMessage(out var message, default, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
			{
				PInvoke.TranslateMessage(message);
				PInvoke.DispatchMessage(message);
			}
		}

		private interface IInternal
		{
			void Invoke();
			void Fail(Exception exception);
		}

		private sealed class Internal<T> : IInternal
		{
			private readonly Func<T> payload;
			private readonly TaskCompletionSource<T> taskCompletionSource =
				new(TaskCreationOptions.RunContinuationsAsynchronously);

			public Task<T> Task => taskCompletionSource.Task;

			public Internal(Func<T> payload)
			{
				this.payload = payload;
			}

			public void Invoke()
			{
				try
				{
					taskCompletionSource.TrySetResult(payload());
				}
				catch (Exception exception)
				{
					Fail(exception);
				}
			}

			public void Fail(Exception exception)
			{
				if (exception is OperationCanceledException canceled)
					taskCompletionSource.TrySetCanceled(canceled.CancellationToken);
				else
					taskCompletionSource.TrySetException(exception);
			}
		}

		private sealed class ActionInternal : IInternal
		{
			private readonly Action payload;
			private readonly TaskCompletionSource taskCompletionSource =
				new(TaskCreationOptions.RunContinuationsAsynchronously);

			public Task Task => taskCompletionSource.Task;

			public ActionInternal(Action payload)
			{
				this.payload = payload;
			}

			public void Invoke()
			{
				try
				{
					payload();
					taskCompletionSource.TrySetResult();
				}
				catch (Exception exception)
				{
					Fail(exception);
				}
			}

			public void Fail(Exception exception)
			{
				if (exception is OperationCanceledException canceled)
					taskCompletionSource.TrySetCanceled(canceled.CancellationToken);
				else
					taskCompletionSource.TrySetException(exception);
			}
		}
	}
}
