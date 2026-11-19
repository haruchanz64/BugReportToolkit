using System;
using System.Collections.Generic;

namespace BugReporter.Core
{
    /// <summary>
    /// Thread-safe fixed-capacity ring buffer that overwrites the oldest items when full,
    /// avoiding repeated reallocations during gameplay capture.
    /// </summary>
    public sealed class RingBuffer<T>
    {
        private readonly T[] _buffer;
        private readonly object _lock = new object();
        private int _head;
        private int _count;

        public int Capacity => _buffer.Length;

        public int Count
        {
            get
            {
                lock (_lock)
                    return _count;
            }
        }

        public RingBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
            _buffer = new T[capacity];
        }

        public void Add(T item)
        {
            lock (_lock)
            {
                _buffer[_head] = item;
                _head = (_head + 1) % _buffer.Length;
                if (_count < _buffer.Length)
                    _count++;
            }
        }

        public List<T> Snapshot()
        {
            lock (_lock)
            {
                var result = new List<T>(_count);
                if (_count == 0)
                    return result;

                int start = _count == _buffer.Length ? _head : 0;
                for (int i = 0; i < _count; i++)
                {
                    int idx = (start + i) % _buffer.Length;
                    result.Add(_buffer[idx]);
                }
                return result;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _head = 0;
                _count = 0;
            }
        }
    }
}
