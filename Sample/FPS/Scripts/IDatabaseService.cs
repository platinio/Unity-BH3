using System;
using System.Collections.Generic;

namespace Platinio.Share
{
    public interface IDatabaseService<T>
    {
        public Action<T> OnAdd { get; set; }
        public Action<T> OnRemove { get; set; }
        public IEnumerable<T> Items { get; }

        void Add(T item);
        void Remove(T item);
    }
}