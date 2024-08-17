using System.Collections;
using System.Collections.Immutable;

namespace WordGenLib
{

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "N/A")]
    public readonly struct ImmutableArraySegment<T>(ImmutableArray<T> array, int offset, int length) : IReadOnlyList<T>, IEnumerable<T>, IReadOnlyCollection<T>
        where T : notnull
    {
        private readonly ImmutableArray<T> _arr = array;
        private readonly int _offset = offset;
        private readonly int _length = length;

        public static implicit operator ImmutableArraySegment<T>(ImmutableArray<T> arr)
        {
            return new ImmutableArraySegment<T>(arr, 0, arr.Length);
        }

        public ImmutableArraySegment<T> Slice(int start, int length)
        {
            if (start == 0 && length == _length) return this;
            return new ImmutableArraySegment<T>(_arr, _offset + start, length);
        }

        public ImmutableArraySegment<T> Remove(T content)
        {
            if (_offset == 0 && _length == _arr.Length) return _arr.Remove(content);
            if (this.Contains(content)) return this.Where(word => !word.Equals(content)).ToImmutableArray();
            return this;
        }

        public ImmutableArraySegment<T> RemoveAll(Predicate<T> predicate)
        {
            if (_offset == 0 && _length == _arr.Length) return _arr.RemoveAll(predicate);
            if (_length < 100 && !this.Any(v => predicate(v))) return this;
            return this.Where(v => !predicate(v)).ToImmutableArray();
        }

        public (ImmutableArraySegment<T> FirstHalf, ImmutableArraySegment<T> SecondHalf) Split()
        {
            int firstHalfIndex = (1 + Length) / 2;
            return (this[..firstHalfIndex], this[firstHalfIndex.._length]);
        }

        public T this[int index] => _arr[index + _offset];

        public int Count => _length;
        public int Length => _length;

        public IEnumerator<T> GetEnumerator()
        {
            return new IasEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private class IasEnumerator(ImmutableArraySegment<T> ias) : IEnumerator<T>
        {
            private int _idx = -1;

            public T Current => ias[_idx];
            object IEnumerator.Current => Current!;

            public void Dispose()
            {
                _idx = -1;
            }

            public bool MoveNext()
            {
                _idx++;
                return _idx < ias.Length;
            }

            public void Reset()
            {
                _idx = -1;
            }
        }

        public static bool operator ==(ImmutableArraySegment<T> first, ImmutableArraySegment<T> second)
        {
            if (first._length != second._length) return false;
            if (first._arr == second._arr)  // ImmutableArray's operator== compares references of backing array.
            {
                return first._offset == second._offset;
            }

            int end = first._offset + first._length;
            for (int i = first._offset; i < end; ++i)
            {
                if (!first._arr[i].Equals(second._arr[i])) return false;
            }

            return true;
        }
        public static bool operator !=(ImmutableArraySegment<T> first, ImmutableArraySegment<T> second)
        {
            return !(first == second);
        }

        public override bool Equals(object? obj)
        {
            if (obj is ImmutableArraySegment<T> other) return this == other;
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_length, _offset, _arr);
        }
    }

}
