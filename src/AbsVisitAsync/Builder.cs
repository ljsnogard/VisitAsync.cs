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
}