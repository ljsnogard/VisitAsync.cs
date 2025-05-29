namespace NsAbsVisitAsync
{
    using System.Threading;

    using Cysharp.Threading.Tasks;

    using NsAnyLR;

    public interface IWeaver<T>
    {
        public UniTask<Result<T, IBuilderError>> TryWeaveAsync(
            IBuilder<T> builder,
            IBuilderProvider<T> factory,
            CancellationToken token = default
        );
    }
}