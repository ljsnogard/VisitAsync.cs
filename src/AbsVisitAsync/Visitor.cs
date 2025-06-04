namespace NsAbsVisitAsync
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Cysharp.Threading.Tasks;

    public interface IVisitor<T> : IDisposable
    {
        public UniTask<bool> VisitAsync(
            T data,
            CancellationToken token = default
        );
    }

    public interface IVisitorProvider
    {
        /// <summary>
        /// Get a visitor specifically for the data field or property of an object with type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="remainingItemCount">The count of the members to visit</param>
        /// <param name="memberKey">Usually the field name or property name of the member to visit</param>
        /// <param name="token">The cancellation token to cancel the task of getting the visitor</param>
        /// <returns>A value task that containing the visitor result.</returns>
        public UniTask<IVisitor<U>> GetMemberVisitorAsync<T, U>(
            IVisitor<T> parent,
            uint remainingMemberCount,
            string memberKey,
            CancellationToken token = default
        );

        public UniTask<IVisitor<U>> GetVariantVisitorAsync<T, U>(
            IVisitor<T> parent,
            uint remainingVariantCount,
            CancellationToken token = default
        );
    }

    public interface IListVisitorProvider<L, E>
        where L : IEnumerable<E>
    {
        public UniTask<IVisitor<E>> GetListElementVisitorAsync(
            IVisitor<L> parent,
            uint elementCount,
            CancellationToken token = default
        );
    }

    public interface IDictionaryVisitorProvider<D, K, V>
        where D : IEnumerable<KeyValuePair<K, V>>
    {
        public UniTask<IVisitor<KeyValuePair<K, V>>> GetDictionaryEntriesVisitorAsync(
            IVisitor<D> parent,
            uint entriesCount,
            CancellationToken token = default
        );
    }
}
