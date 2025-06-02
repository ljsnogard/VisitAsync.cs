namespace NsAbsVisitAsync
{
    using System;
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
        /// Get a visitor 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="remainingItemCount">The count of the members to visit</param>
        /// <param name="key">Usually the field name or property name of the member to visit</param>
        /// <param name="token">The cancellation token to cancel the task of getting the visitor</param>
        /// <returns>A value task that containing the visitor result.</returns>
        public UniTask<IVisitor<U>> GetMemberVisitorAsync<T, U>(
            IVisitor<T> parent,
            uint remainingMemberCount,
            string key,
            CancellationToken token = default
        );

        public UniTask<IVisitor<U>> GetVariantVisitorAsync<T, U>(
            IVisitor<T> parent,
            uint remainingVariantCount,
            CancellationToken token = default
        );
    }
}
