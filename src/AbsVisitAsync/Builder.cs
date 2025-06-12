namespace NsAbsVisitAsync
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using Cysharp.Threading.Tasks;

    using NsAnyLR;

    /// <summary>
    /// A builder knows how to create an object upon the different types of
    /// data from the parsers under the builder's direction.
    /// </summary>
    /// <typeparam name="T">The data type to build</typeparam>
    public interface IBuilder<T>
    {
        public UniTask<Result<T, IParserError>> TryBuildAsync(
            IParser<T> parser,
            IParserProvider provider,
            CancellationToken token = default
        );
    }

    /// <summary>
    /// A manager for type-specific builder singleton.
    /// </summary>
    public sealed class BuilderManager
    {
        private readonly TypedSingletonAsyncDict dict_;

        public BuilderManager()
            => this.dict_ = new();

        public UniTask<bool> RegisterAsync<T, B>(bool shouldReplace = false, CancellationToken token = default)
            where B : class, IBuilder<T>, new()
        {
            return this.dict_.RegisterAsync<T, B>(shouldReplace, token);
        }

        public UniTask<Option<IBuilder<T>>> GetAsync<T>(CancellationToken token = default)
            => this.dict_.GetAsync(TypedSingletonAsyncDict.TryType<IBuilder<T>>, token);
    }

    public readonly struct LstBuilder<L, E> : IBuilder<L>
        where L : IEnumerable<E>
    {
        public async UniTask<Result<L, IParserError>> TryBuildAsync(
            IParser<L> parser,
            IParserProvider provider,
            CancellationToken token = default)
        {
            if (provider is not IListParserProvier<L, E> lp)
                return Result.Err<IParserError>(new ProviderError(""));
            var elemParser = await lp.GetListElementParser(parser, token);
            var parsedList = new LinkedList<E>();
            while (true)
            {
                var tryParse = await elemParser.TryParseAsync(token);
                if (!tryParse.TryOk(out var elem, out var err))
                {
                    if (err is ICollectionEndedError)
                        break;
                    else
                        return Result.Err(err);
                }
                parsedList.AddLast(elem);
            }
            if (parsedList.TryConvert(out L result))
                return Result.Ok(result);
            else
                throw new NotImplementedException();
        }
    }

    public readonly struct MapBuilder<D, K, V> : IBuilder<D>
        where D : IEnumerable<KeyValuePair<K, V>>
    {
        public async UniTask<Result<D, IParserError>> TryBuildAsync(
            IParser<D> parser,
            IParserProvider provider,
            CancellationToken token = default)
        {
            if (provider is not IDictionaryParserProvier<D, K, V> dp)
                return Result.Err<IParserError>(new ProviderError(""));
            var entryParser = await dp.GetDictEntriesParser(parser, token);
            var parsedList = new LinkedList<KeyValuePair<K, V>>();
            while (true)
            {
                var tryParse = await entryParser.TryParseAsync(token);
                if (!tryParse.TryOk(out var pair, out var err))
                {
                    if (err is ICollectionEndedError)
                        break;
                    else
                        return Result.Err(err);
                }
                parsedList.AddLast(pair);
            }
            if (parsedList.TryConvert(out D result))
                return Result.Ok(result);
            else
                throw new NotImplementedException();
        }
    }

    public readonly struct StrBuilder : IBuilder<string>
    {
        public UniTask<Result<string, IParserError>> TryBuildAsync(
            IParser<string> parser,
            IParserProvider provider,
            CancellationToken token = default)
        {
            return parser.TryParseAsync(token);
        }
    }

    public readonly struct ProviderError : IParserError
    {
        public readonly string Message;

        public ProviderError(string message)
            => this.Message = message;

        public Exception AsException()
            => new Exception(message: this.Message);
    }

    public static class GenericCollectionConvert
    {
        public interface ICollectionConverter
        { }

        public interface ICollectionConverter<C, E> : ICollectionConverter
            where C : IEnumerable<E>
        {
            public C Convert(IEnumerable<E> collection);
        }

        public sealed class ConverterCache
        {
            private readonly TypedSingletonAsyncDict dict_;

            public ConverterCache()
            {
                var d = new Dictionary<Type, ICollectionConverter>();
                this.dict_ = TypedSingletonAsyncDict.InitWithDictionary(d);
            }
        }

        public sealed class ListConverter<E> : ICollectionConverter<List<E>, E>
        {
            public List<E> Convert(IEnumerable<E> collection)
                => collection.ToList();
        }

        public static bool TryConvert<L, E>(this IEnumerable<E> collection, out L result)
            where L : IEnumerable<E>
        {
            if (collection is L casted)
            {
                result = casted;
                return true;
            }
            throw new NotImplementedException();
        }
    }
}