// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Infrastructure;

public interface IUiDispatcher
{
	bool HasThreadAccess { get; }

	bool TryEnqueue(Action callback);
}
