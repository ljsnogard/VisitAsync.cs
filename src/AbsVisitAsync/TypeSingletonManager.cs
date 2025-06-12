namespace NsAbsVisitAsync
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using Cysharp.Threading.Tasks;

    using NsAnyLR;

    public sealed class TypedSingletonAsyncDict
    {
        private readonly AsyncRwLockSlim rwlock_;

        private readonly Dictionary<Type, object> map_;

        public static Option<T> TryType<T>(object obj)
        {
            if (obj is T re)
                return Option.Some(re);
            else
                return Option.None();
        }

        private TypedSingletonAsyncDict(Dictionary<Type, object> dict)
        {
            this.rwlock_ = new();
            this.map_ = dict;
        }

        public TypedSingletonAsyncDict() : this(new())
        { }

        public static TypedSingletonAsyncDict InitWithDictionary<X>(Dictionary<Type, X> dict)
        {
            var d = new Dictionary<Type, object>(dict.Comparer);
            foreach (var kv in dict)
            {
                if (kv.Value is object val)
                    d[kv.Key] = val;
            }
            return new(d);
        }

        public async UniTask<uint> RegisterAsync<X>(
                IEnumerable<(Type, X)> pairs,
                bool shouldReplace = true,
                CancellationToken token = default)
            where X : class
        {
            Option<AsyncRwLockSlim.Guard> optGuard = Option.None();
            try
            {
                optGuard = await this.rwlock_.LockWriteAsync(token);
                if (!optGuard.IsSome(out var guard))
                    return 0u;
                if (shouldReplace)
                {
                    foreach (var kv in pairs)
                    {
                        (var key, var val) = kv;
                        this.map_[key] = val;
                    }
                    return unchecked((uint)pairs.Count());
                }
                else
                {
                    var c = 0u;
                    foreach (var kv in pairs)
                    {
                        (var key, var val) = kv;
                        var hasExisting = this.map_.ContainsKey(key);
                        if (hasExisting)
                            continue;
                        this.map_.Add(key, val);
                        c += 1u;
                    }
                    return c;
                }
            }
            finally
            {
                if (optGuard.IsSome(out var guard))
                    await guard.DisposeAsync();
            }
        }

        public async UniTask<bool> RegisterAsync<T, X>(
                bool shouldReplace = false,
                CancellationToken token = default)
            where X : class, new()
        {
            var pairs = new (Type, X)[] { (typeof(T), new X()) };
            var c = await this.RegisterAsync(pairs, shouldReplace, token);
            return c == unchecked((uint)pairs.Length);
        }

        public async UniTask<Option<T>> GetAsync<T>(
                Func<object, Option<T>> predicate,
                CancellationToken token = default)
            where T : class
        {
            Option<AsyncRwLockSlim.Guard> optGuard = Option.None();
            try
            {
                optGuard = await this.rwlock_.LockReadAsync(token);
                if (!optGuard.IsSome(out var guard))
                    return Option.None();

                var key = typeof(T);
                if (this.map_.TryGetValue(key, out var val))
                    return predicate(val);
                else
                    return Option.None();
            }
            finally
            {
                if (optGuard.IsSome(out var guard))
                    await guard.DisposeAsync();
            }
        }
    }
}