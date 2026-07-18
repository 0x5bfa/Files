// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using Windows.Win32.System.Com;

namespace Files.Core.Storage.Windows;

internal sealed unsafe class ShellReadStream : Stream
{
	private IStream? shellStream;
	private readonly long length;

	public ShellReadStream(IStream shellStream)
	{
		ArgumentNullException.ThrowIfNull(shellStream);

		this.shellStream = shellStream;
		STATSTG statistics = default;
		var result = shellStream.Stat(&statistics, STATFLAG.STATFLAG_NONAME);
		result.ThrowOnFailure();
		length = checked((long)statistics.cbSize);
	}

	private IStream NativeStream
	{
		get => shellStream ?? throw new ObjectDisposedException(nameof(ShellReadStream));
	}

	public override bool CanRead => shellStream is not null;

	public override bool CanSeek => shellStream is not null;

	public override bool CanWrite => false;

	public override long Length
	{
		get
		{
			_ = NativeStream;
			return length;
		}
	}

	public override long Position
	{
		get => Seek(0, SeekOrigin.Current);
		set => Seek(value, SeekOrigin.Begin);
	}

	public override void Flush()
	{
		_ = NativeStream;
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		ArgumentNullException.ThrowIfNull(buffer);
		ArgumentOutOfRangeException.ThrowIfNegative(offset);
		ArgumentOutOfRangeException.ThrowIfNegative(count);

		if (buffer.Length - offset < count)
		{
			throw new ArgumentException("The offset and count exceed the buffer length.", nameof(count));
		}

		if (count is 0)
		{
			return 0;
		}

		fixed (byte* destination = &buffer[offset])
		{
			uint bytesRead = 0;
			var result = NativeStream.Read(destination, checked((uint)count), &bytesRead);
			result.ThrowOnFailure();

			return checked((int)bytesRead);
		}
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		ulong position = 0;
		var result = NativeStream.Seek(offset, origin, &position);
		result.ThrowOnFailure();

		return checked((long)position);
	}

	public override void SetLength(long value) => throw new NotSupportedException();

	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	protected override void Dispose(bool disposing)
	{
		shellStream = null;
		base.Dispose(disposing);
	}
}
