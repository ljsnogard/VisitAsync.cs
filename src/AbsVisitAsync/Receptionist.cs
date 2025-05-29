namespace NsAbsVisitAsync
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Cysharp.Threading.Tasks;

    using NsAnyLR;

    /// <summary>
    /// A STATELESS helper object that guides the visitor to traval through the
    /// internal structure (fields, properties) of the data.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IReceptionist<T>
    {
        public UniTask<bool> ReceptAsync(
            T data,
            IVisitor<T> visitor,
            IVisitorProvider provider,
            CancellationToken token = default
        );
    }

    public sealed class ReceptionistManager
    {
        private readonly SemaphoreSlim sema_;
        private readonly Dictionary<Type, object> map_;

        public ReceptionistManager()
        {
            this.sema_ = new(1, 1);
            this.map_ = new();
        }

        public async UniTask<bool> RegisterAsync<T, R>(bool shouldReplace = false, CancellationToken token = default)
            where R : IReceptionist<T>, new()
        {
            try
            {
                await this.sema_.WaitAsync(token);
                var key = typeof(T);
                if (shouldReplace)
                    this.map_.Remove(key);
                var hasExisting = this.map_.TryGetValue(typeof(T), out var recept);
                if (hasExisting)
                    return false;
                this.map_.Add(key, new R());
                return true;
            }
            finally
            {
                if (this.sema_.CurrentCount == 0)
                    this.sema_.Release();
            }
        }

        public async UniTask<Option<IReceptionist<T>>> GetAsync<T>(CancellationToken token = default)
        {
            try
            {
                await this.sema_.WaitAsync(token);
                var key = typeof(T);
                if (this.map_.TryGetValue(key, out var val) && val is IReceptionist<T> re)
                    return Option.Some(re);
                else
                    return Option.None();
            }
            finally
            {
                if (this.sema_.CurrentCount == 0)
                    this.sema_.Release();
            }
        }
    }
}