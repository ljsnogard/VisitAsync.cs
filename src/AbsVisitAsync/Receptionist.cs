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

    /// <summary>
    /// A manager for type-specific receptionist singleton.
    /// </summary>
    public sealed class ReceptionistManager
    {
        private readonly TypedSingletonAsyncDict dict_;

        public ReceptionistManager()
            => this.dict_ = new();

        public UniTask<bool> RegisterAsync<T, R>(
                bool shouldReplace = false,
                CancellationToken token = default)
            where R : class, IReceptionist<T>, new()
        {
            return this.dict_.RegisterAsync<T, R>(shouldReplace, token);
        }

        public UniTask<Option<IReceptionist<T>>> GetAsync<T>(CancellationToken token = default)
            => this.dict_.GetAsync(TypedSingletonAsyncDict.TryType<IReceptionist<T>>, token);
    }
}