// Copyright (c) Files Community
// Licensed under the MIT License.

using System;

namespace Files.Shared.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class GeneratedVirtualMethodAttribute : Attribute
{
	public uint Index { get; init; } = 0U;
}
