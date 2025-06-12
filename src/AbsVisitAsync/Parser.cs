namespace NsAbsVisitAsync
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Cysharp.Threading.Tasks;

    using NsAnyLR;

    public interface IParserError
    {
        public Exception AsException();
    }

    /// <summary>
    /// When parser tries to parse the element or entry after the last one is
    /// already parsed, the parser should return this error.
    /// </summary>
    public interface ICollectionEndedError : IParserError
    { }

    /// <summary>
    /// A parser knows where to get the data (e.g. `JSON`, `protobuf`) and
    /// transform it into the data type known in the programming language (e.g.
    /// string, integers). 
    /// </summary>
    /// <typeparam name="T">The C# data type to parse</typeparam>
    public interface IParser<T> : IDisposable
    {
        public UniTask<Result<T, IParserError>> TryParseAsync(CancellationToken token = default);
    }

    public interface IMemberParserProvider<T> : IDisposable
    {
        public UniTask<IParser<U>> GetMemberParserAsync<U>(
            string memberKey,
            CancellationToken token = default
        );
    }

    public interface IVariantParserProvider<T>
    {
        public UniTask<IParser<U>> GetVariantParserAsync<U>(CancellationToken token = default);

        /// <summary>
        /// The parser knows the data type, but the builder might not need it.
        /// So put some guesses and let the parser tell the builder whether the 
        /// correct answer is among the options.
        /// </summary>
        /// <param name="candidates">The guessing types.</param>
        /// <param name="token"></param>
        /// <returns></returns>
        public UniTask<Result<uint, IParserError>> FindVariantTypeAsync(
            ReadOnlyMemory<Type> candidates,
            CancellationToken token = default
        );
    }

    public interface IParserProvider
    {
        public UniTask<IMemberParserProvider<T>> ProviderForMemberAsync<T>(
            IParser<T> parent,
            uint membersCount,
            CancellationToken token = default
        );

        public UniTask<IVariantParserProvider<T>> ProviderForVariantsAsync<T>(
            IParser<T> parent,
            CancellationToken token = default
        );
    }

    public interface IListParserProvier<L, E>
        where L : IEnumerable<E>
    {
        public UniTask<IParser<E>> GetListElementParser(
            IParser<L> parent,
            CancellationToken token = default
        );
    }

    public interface IDictionaryParserProvier<D, K, V>
        where D : IEnumerable<KeyValuePair<K, V>>
    {
        public UniTask<IParser<KeyValuePair<K, V>>> GetDictEntriesParser(
            IParser<D> parent,
            CancellationToken token = default
        );
    }
}
