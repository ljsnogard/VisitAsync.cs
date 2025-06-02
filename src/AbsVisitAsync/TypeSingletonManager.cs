namespace NsAbsVisitAsync
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Cysharp.Threading.Tasks;

    using NsAnyLR;

    public sealed class TypedSingletonAsyncDict
    {
        private readonly SemaphoreSlim sema_;

        private readonly Dictionary<Type, object> map_;

        public static Option<T> TryType<T>(object obj)
        {
            if (obj is T re)
                return Option.Some(re);
            else
                return Option.None();
        }

        public TypedSingletonAsyncDict()
        {
            this.sema_ = new(1, 1);
            this.map_ = new();
        }

        public async UniTask<bool> RegisterAsync<T, X>(
                bool shouldReplace = false,
                CancellationToken token = default)
            where X : class, new()
        {
            try
            {
                await this.sema_.WaitAsync(token);
                var key = typeof(T);
                if (shouldReplace)
                    this.map_.Remove(key);
                var hasExisting = this.map_.TryGetValue(typeof(T), out var recept);
                if (hasExisting)
                    return false;
                this.map_.Add(key, new X());
                return true;
            }
            finally
            {
                if (this.sema_.CurrentCount == 0)
                    this.sema_.Release();
            }
        }

        public async UniTask<Option<T>> GetAsync<T>(
                Func<object, Option<T>> predicate,
                CancellationToken token = default)
            where T : class
        {
            try
            {
                await this.sema_.WaitAsync(token);
                var key = typeof(T);
                if (this.map_.TryGetValue(key, out var val))
                    return predicate(val);
                else
                    return Option.None();
            }
            finally
            {
                if (this.sema_.CurrentCount == 0)
                    this.sema_.Release();
            }
        }
    }
}