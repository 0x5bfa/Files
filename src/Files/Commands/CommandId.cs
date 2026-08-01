// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Commands;

public readonly record struct CommandId
{
	public CommandId(string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		Value = value;
	}

	public string Value { get; }

	public override string ToString() => Value;
}
