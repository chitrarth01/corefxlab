// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
    [DebuggerTypeProxy(typeof(ReadOnlyBufferDebuggerView<>))]
    public struct ReadOnlyBuffer<T>
    {
        readonly OwnedBuffer<T> _owner;
        readonly T[] _array;
        readonly int _index;
        readonly int _length;

        internal ReadOnlyBuffer(OwnedBuffer<T> owner,int index, int length)
        {
            _array = null;
            _owner = owner;
            _index = index;
            _length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyBuffer(T[] array)
        {
            if (array == null)
                BufferPrimitivesThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);

            _array = array;
            _owner = null;
            _index = 0;
            _length = array.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyBuffer(T[] array, int start)
        {
            if (array == null)
                BufferPrimitivesThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);

            int arrayLength = array.Length;
            if ((uint)start > (uint)arrayLength)
                BufferPrimitivesThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);

            _array = array;
            _owner = null;
            _index = start;
            _length = arrayLength - start;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyBuffer(T[] array, int start, int length)
        {
            if (array == null)
                BufferPrimitivesThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
            if ((uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))
                BufferPrimitivesThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);

            _array = array;
            _owner = null;
            _index = start;
            _length = length;
        }

        internal ReadOnlyBuffer(OwnedBuffer<T> owner, T[] array, int index, int length)
        {
            _array = array;
            _owner = owner;
            _index = index;
            _length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyBuffer<T>(T[] array)
        {
            return new ReadOnlyBuffer<T>(array, 0, array.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyBuffer<T>(ArraySegment<T> arraySegment)
        {
            return new ReadOnlyBuffer<T>(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
        }

        public static ReadOnlyBuffer<T> Empty { get; } = OwnedBuffer<T>.EmptyArray;

        public int Length => _length;

        public bool IsEmpty => Length == 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyBuffer<T> Slice(int start)
        {
            if ((uint)start > (uint)_length)
                BufferPrimitivesThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);

            return new ReadOnlyBuffer<T>(_owner, _array, _index + start, _length - start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyBuffer<T> Slice(int start, int length)
        {
            if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
                BufferPrimitivesThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);

            return new ReadOnlyBuffer<T>(_owner, _array, _index + start, length);
        }

        public ReadOnlySpan<T> Span
        {
            get {
                if (_array != null) return new ReadOnlySpan<T>(_array, _index, _length);
                return _owner.AsSpan(_index, _length);
            }
        }

        public BufferHandle Retain(bool pin = false)
        {
            BufferHandle bufferHandle;
            if (pin)
            {
                if (_owner != null)
                {
                    bufferHandle = _owner.Pin(_index);
                }
                else
                {
                    var handle = GCHandle.Alloc(_array, GCHandleType.Pinned);
                    unsafe
                    {
                        var pointer = OwnedBuffer<T>.Add((void*)handle.AddrOfPinnedObject(), _index);
                        bufferHandle = new BufferHandle(null, pointer, handle);
                    }
                }
            }
            else
            {
                if (_owner != null)
                {
                    _owner.Retain();
                }
                bufferHandle = new BufferHandle(_owner);
            }
            return bufferHandle;
        }

        public T[] ToArray() => Span.ToArray();

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool DangerousTryGetArray(out ArraySegment<T> arraySegment)
        {
            if (_owner != null && _owner.TryGetArray(out var segment))
            {
                arraySegment = new ArraySegment<T>(segment.Array, segment.Offset + _index, _length);
                return true;
            }

            if (_array != null)
            {
                arraySegment = new ArraySegment<T>(_array, _index, _length);
                return true;
            }

            arraySegment = default(ArraySegment<T>);
            return false;
        }

        public void CopyTo(Span<T> span) => Span.CopyTo(span);

        public void CopyTo(Buffer<T> buffer) => Span.CopyTo(buffer.Span);

        public bool TryCopyTo(Span<T> span) => Span.TryCopyTo(span);

        public bool TryCopyTo(Buffer<T> buffer) => Span.TryCopyTo(buffer.Span);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj)
        {
            if (obj is ReadOnlyBuffer<T>)
            {
                var other = (ReadOnlyBuffer<T>)obj;
                return Equals(other);
            }
            else if (obj is Buffer<T>)
            {
                var other = (Buffer<T>)obj;
                return Equals(other);
            }
            else
            {
                return false;
            }
        }
        
        public bool Equals(ReadOnlyBuffer<T> other)
        {
            return
                _array == other._array &&
                _owner == other._owner &&
                _index == other._index &&
                _length == other._length;
        }

        public static bool operator ==(ReadOnlyBuffer<T> left, ReadOnlyBuffer<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ReadOnlyBuffer<T> left, ReadOnlyBuffer<T> right)
        {
            return !left.Equals(right);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode()
        {
            if (_owner != null)
            {
                return HashingHelper.CombineHashCodes(_owner.GetHashCode(), _index.GetHashCode(), _length.GetHashCode());
            }
            return HashingHelper.CombineHashCodes(_array.GetHashCode(), _index.GetHashCode(), _length.GetHashCode());
        }
    }
}
