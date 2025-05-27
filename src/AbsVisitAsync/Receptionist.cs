namespace NsAbsVisitAsync
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;

    using Cysharp.Threading.Tasks;

    /// <summary>
    /// A STATELESS helper object that associate the data and its visitor.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IReceptionist<T>
    {
        public UniTask<bool> AcceptAsync(
            T data,
            IVisitor<T> visitor,
            IVisitorFactory<T> factor,
            CancellationToken token = default);
    }

    public sealed class UnfoundReceptionist<T> : IReceptionist<T>
    {
        public UniTask<bool> AcceptAsync(
                T data,
                IVisitor<T> visitor,
                IVisitorFactory<T> factory,
                CancellationToken token = default)
        {
            throw new Exception();
        }
    }

    public sealed class ReceptionistManager
    {
        private readonly ConcurrentDictionary<Type, object> map_;

        public ReceptionistManager()
            => this.map_ = new();

        public void RegisterReceptionist<T, R>()
            where R : class, IReceptionist<T>, new()
        {
            this.map_.AddOrUpdate(
                typeof(T),
                new R(),
                (k, v) => new R()
            );
        }

        public IReceptionist<T> GetReceptionist<T>()
        {
            if (this.map_.TryGetValue(typeof(T), out var val) && val is IReceptionist<T> re)
                return re;
            else
                return new UnfoundReceptionist<T>();
        }
    }
}