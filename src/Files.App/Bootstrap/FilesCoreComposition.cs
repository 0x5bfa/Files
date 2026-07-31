// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Composition;

namespace Files.App.Bootstrap
{
	internal static class FilesCoreComposition
	{
		public static FilesCoreRuntime CreateRuntime()
		{
			return new FilesCoreBuilder()
				.AddWindowsStorage()
				.Build();
		}
	}
}
