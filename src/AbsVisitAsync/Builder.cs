namespace NsAbsVisitAsync
{
    using System;
    using System.Threading;

    using Cysharp.Threading.Tasks;

    using NsAnyLR;

    public interface IBuilderError
    {
        public Exception AsException();
    }

    public interface IBuilder<T> : IDisposable
    {
        public UniTask<Result<T, IBuilderError>> TryBuildAsync(CancellationToken token = default);

        public UniTask<Result<uint, IBuilderError>> GetVariantTypeAsync(
            ReadOnlyMemory<Type> variantTypes,
            CancellationToken token = default
        );
    }

    public interface IBuilderProvider<T>
    {
        public UniTask<IBuilder<U>> GetMemberBuilderAsync<U>(
            IBuilder<T> parent,
            uint remainingMemberCount,
            string key,
            CancellationToken token = default
        );

        public UniTask<IBuilder<U>> GetVariantBuilderAsync<U>(
            IBuilder<T> parent,
            CancellationToken token = default
        );
    }
}