// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Diagnostics;

namespace Files.Core.AppModels;

internal static class ModelEvent
{
	public static void Raise(object sender, EventHandler? handlers)
	{
		if (handlers is null)
		{
			return;
		}

		foreach (EventHandler handler in handlers.GetInvocationList())
		{
			try
			{
				handler(sender, EventArgs.Empty);
			}
			catch (Exception error)
			{
				Trace.TraceError(error.ToString());
			}
		}
	}
}
