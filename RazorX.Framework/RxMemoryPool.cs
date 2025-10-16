using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RazorX.Framework;

internal sealed class RxMemoryPool(ArrayPool<char>? charPool = null) {
    private readonly ArrayPool<char> _charPool = charPool ?? ArrayPool<char>.Shared;

    public PooledStringBuilder RentStringBuilder(int initialCapacity = 16384) {
        return new PooledStringBuilder(initialCapacity, _charPool);
    }

    public RentedBuffer<char> RentCharBuffer(int minimumLength) {
        var buffer = _charPool.Rent(minimumLength);
        RxTelemetry.MemoryPoolRentCounter.Add(1,
            new KeyValuePair<string, object?>("buffer.size", buffer.Length));
        return new RentedBuffer<char>(buffer, _charPool);
    }
}

internal readonly struct RentedBuffer<T> : IDisposable {
    private readonly T[] _buffer;
    private readonly ArrayPool<T> _pool;

    public RentedBuffer(T[] buffer, ArrayPool<T> pool) {
        _buffer = buffer;
        _pool = pool;
    }

    public Span<T> Span => _buffer.AsSpan();

    public Memory<T> Memory => _buffer.AsMemory();

    public int Length => _buffer.Length;

    public void Dispose() {
        RxTelemetry.MemoryPoolReturnCounter.Add(1,
            new KeyValuePair<string, object?>("buffer.size", _buffer.Length));
        _pool.Return(_buffer, clearArray: true);
    }
}

internal sealed class PooledStringBuilder : IDisposable {
    private char[] _buffer;
    private int _position;
    private readonly ArrayPool<char> _pool;
    private bool _disposed;

    internal PooledStringBuilder(int initialCapacity, ArrayPool<char> pool) {
        Debug.Assert(initialCapacity > 0, "Initial capacity must be positive");
        _pool = pool;
        _buffer = pool.Rent(initialCapacity);
        RxTelemetry.MemoryPoolRentCounter.Add(1,
            new KeyValuePair<string, object?>("buffer.size", _buffer.Length));
        _position = 0;
        _disposed = false;
    }

    public int Length => _position;

    public int Capacity => _buffer.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(string? value) {
        if (string.IsNullOrEmpty(value)) {
            return;
        }
        EnsureCapacity(_position + value.Length);
        value.AsSpan().CopyTo(_buffer.AsSpan(_position));
        _position += value.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char value) {
        EnsureCapacity(_position + 1);
        _buffer[_position++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(ReadOnlySpan<char> value) {
        if (value.IsEmpty) {
            return;
        }
        EnsureCapacity(_position + value.Length);
        value.CopyTo(_buffer.AsSpan(_position));
        _position += value.Length;
    }

    private void EnsureCapacity(int requiredCapacity) {
        if (_buffer.Length >= requiredCapacity) {
            return;
        }
        var newCapacity = Math.Max(requiredCapacity, _buffer.Length * 2);
        var oldBufferSize = _buffer.Length;
        var newBuffer = _pool.Rent(newCapacity);
        RxTelemetry.MemoryPoolRentCounter.Add(1,
            new KeyValuePair<string, object?>("buffer.size", newBuffer.Length));
        _buffer.AsSpan(0, _position).CopyTo(newBuffer);
        _pool.Return(_buffer, clearArray: true);
        RxTelemetry.MemoryPoolReturnCounter.Add(1,
            new KeyValuePair<string, object?>("buffer.size", oldBufferSize));
        _buffer = newBuffer;
    }

    public override string ToString() {
        ObjectDisposedException.ThrowIf(_disposed, nameof(PooledStringBuilder));
        return new string(_buffer, 0, _position);
    }

    public void Clear() {
        ObjectDisposedException.ThrowIf(_disposed, nameof(PooledStringBuilder));
        _position = 0;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }
        var bufferSize = _buffer.Length;
        _pool.Return(_buffer, clearArray: true);
        RxTelemetry.MemoryPoolReturnCounter.Add(1,
            new KeyValuePair<string, object?>("buffer.size", bufferSize));
        _disposed = true;
    }
}
