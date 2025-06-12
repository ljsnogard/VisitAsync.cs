namespace NsAbsVisitAsync
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Cysharp.Threading.Tasks;
    using NsAnyLR;

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
        /// Get a visitor provider prepared for data member of type T
        /// </summary>
        /// <typeparam name="T">The object type that contains data member</typeparam>
        /// <param name="parent">The visitor visiting the object data.</param>
        /// <param name="membersCount">The number of data members to visit.</param>
        /// <param name="token"></param>
        /// <returns></returns>
        public UniTask<IMemberVisitorProvider<T>> ProviderForMembersAsync<T>(
            IVisitor<T> parent,
            uint membersCount,
            CancellationToken token = default
        );

        /// <summary>
        /// Get a visitor provider prepared for variant types of type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parent"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public UniTask<IVariantVisitorProvider<T>> ProviderForVariantsAsync<T>(
            IVisitor<T> parent,
            CancellationToken token = default
        );

        public UniTask<IListParserProvier<L, E>> ProviderForListElementAsync<T, L, E>(
                IVisitor<T> parent,
                uint elementCount,
                CancellationToken token = default)
            where L : IEnumerable<E>;
    }

    public interface IMemberVisitorProvider<T> : IDisposable
    {
        public UniTask<IVisitor<U>> GetMemberVisitorAsync<U>(
            string memberKey,
            CancellationToken token = default
        );
    }

    public interface IVariantVisitorProvider<T>
    {
        public UniTask<IVisitor<U>> GetVariantVisitorAsync<U>(CancellationToken token = default);
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
