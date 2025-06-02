namespace NsAbsVisitAsync
{
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
}