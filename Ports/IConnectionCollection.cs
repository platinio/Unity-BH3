using System.Collections.Generic;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public interface IConnectionCollection<TGraph, TConnection, TSource, TDestination> : ICollection<TConnection>
        where TConnection : IConnection<TSource, TDestination>
        where TSource : IPort
        where TGraph : class, IGraph
    {
        IEnumerable<TConnection> this[TSource source] { get; }
        IEnumerable<TConnection> this[TDestination destination] { get; }
        IEnumerable<TConnection> WithSource(TSource source);
        IEnumerable<TConnection> WithDestination(TDestination destination);
    }
}