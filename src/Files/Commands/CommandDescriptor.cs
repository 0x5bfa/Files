// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Commands;

public sealed record CommandDescriptor(
	CommandId Id,
	string LabelResourceKey,
	string IconKey,
	string Group,
	int Order);
