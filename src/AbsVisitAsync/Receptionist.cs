namespace NsAbsVisitAsync
{
    using System.Collections.Generic;
    using System.Linq;
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

        public UniTask<bool> RegisterAsync<T, R>(bool shouldReplace = false, CancellationToken token = default)
            where R : class, IReceptionist<T>, new()
        {
            return this.dict_.RegisterAsync<T, R>(shouldReplace, token);
        }

        public UniTask<Option<IReceptionist<T>>> GetAsync<T>(CancellationToken token = default)
            => this.dict_.GetAsync(TypedSingletonAsyncDict.TryType<IReceptionist<T>>, token);
    }

    public sealed class ListReceptionist<L, E> : IReceptionist<L>
        where L : IEnumerable<E>
    {
        public async UniTask<bool> ReceptAsync(
            L list,
            IVisitor<L> visitor,
            IVisitorProvider provider,
            CancellationToken token = default)
        {
            var uLen = unchecked((uint)list.Count());
            IVisitor<E> elemVisitor;
            if (provider is IListVisitorProvider<L, E> lp)
                elemVisitor = await lp.GetListElementVisitorAsync(visitor, uLen, token);
            else
                elemVisitor = await provider.GetMemberVisitorAsync<L, E>(visitor, uLen, string.Empty, token);

            foreach (var elem in list)
            {
                if (!await elemVisitor.VisitAsync(elem, token))
                    return false;
            }
            return true;
        }
    }

    public sealed class DictReceptionist<D, K, V> : IReceptionist<D>
        where D : IEnumerable<KeyValuePair<K, V>>
    {
        public async UniTask<bool> ReceptAsync(
            D dict,
            IVisitor<D> visitor,
            IVisitorProvider provider,
            CancellationToken token = default)
        {
            var uLen = unchecked((uint)dict.Count());
            IVisitor<KeyValuePair<K, V>> entryVisitor;
            if (provider is IDictionaryVisitorProvider<D, K, V> dp)
                entryVisitor = await dp.GetDictionaryEntriesVisitorAsync(visitor, uLen, token);
            else
                entryVisitor = await provider.GetMemberVisitorAsync<D, KeyValuePair<K, V>>(visitor, uLen, string.Empty, token);

            foreach (var pair in dict)
            {
                if (!await entryVisitor.VisitAsync(pair, token))
                    return false;
            }
            return true;
        }
    }
}