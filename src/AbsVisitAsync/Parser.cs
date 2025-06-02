namespace NsAbsVisitAsync
{
    using System;
    using System.Threading;

    using Cysharp.Threading.Tasks;

    using NsAnyLR;

    public interface IParserError
    {
        public Exception AsException();
    }

    /// <summary>
    /// A parser knows where to get the data (e.g. `JSON`, `protobuf`) and
    /// transform it into the data type known in the programming language (e.g.
    /// string, integers). 
    /// </summary>
    /// <typeparam name="T">The C# data type to parse</typeparam>
    public interface IParser<T> : IDisposable
    {
        public UniTask<Result<T, IParserError>> TryParseAsync(CancellationToken token = default);

        /// <summary>
        /// The parser knows the data type, but the builder might not need it.
        /// So put some guesses and let the parser tell the builder whether the 
        /// correct answer is among the options.
        /// </summary>
        /// <param name="variantTypes">The guessing types.</param>
        /// <param name="token"></param>
        /// <returns></returns>
        public UniTask<Result<uint, IParserError>> GetVariantTypeAsync(
            ReadOnlyMemory<Type> variantTypes,
            CancellationToken token = default
        );
    }

    public interface IParserProvider
    {
        public UniTask<IParser<U>> GetMemberParserAsync<T, U>(
            IParser<T> parent,
            uint remainingMemberCount,
            string key,
            CancellationToken token = default
        );

        public UniTask<IParser<U>> GetVariantParserAsync<T, U>(
            IParser<T> parent,
            CancellationToken token = default
        );
    }
}