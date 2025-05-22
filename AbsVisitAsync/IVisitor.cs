namespace VisitAsyncUtils
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Cysharp.Threading.Tasks;

    /// <summary>
    /// Visitor 模式中的访问者，用于访问某类型数据下的各异构组件.
    /// </summary>
    public interface IVisitor<THost> : IDisposable
    {
        UniTask<bool> VisitAsync<TComponent>
            ( TComponent component
            , string key = ""
            , CancellationToken token = default
            );
    }

    public interface IVisitorFactory<THost, TVisitor>
        where TVisitor : IVisitor<THost>
    {
        UniTask<TVisitor> GetVisitorAsync(THost host, CancellationToken token = default);
    }

    public interface IRebindableVisitorFactory
    {
        UniTask<IVisitorFactory<THost, TVisitor>> GetFactoryAsync<THost, TVisitor>(CancellationToken token = default)
            where TVisitor : IVisitor<THost>;
    }
    
    /// <summary>
    /// 提供具体的 Visitor 访问方法，针对相同的 T 可能存在零个或多个不同的 Visitor 访问方法
    /// </summary>
    /// <typeparam name="THost">数据主体类型，即提供 Accept 方法的类型 </typeparam>
    /// <typeparam name="TVisitor">访问者对象的类型 </typeparam>
    /// <param name="host">提供 Accept 方法的对象</param>
    /// <param name="visitor">访问者对象</param>
    /// <param name="key">要访问的属性或成员变量的名字</param>
    /// <param name="token">用于取消异步访问的令牌</param>
    /// <returns>访问是否成功</returns>
    public delegate UniTask<bool> FnAcceptVisitorAsync<THost, TFactory, TVisitor>
        ( THost host
        , TFactory factory
        , string key = ""
        , CancellationToken token = default
        )
    where TFactory : IVisitorFactory<THost, TVisitor>
    where TVisitor : IVisitor<THost>;

    /// <summary>
    /// 提供具体的 Visitor 访问方法，针对相同的 T 可能存在零个或多个不同的 Visitor 访问方法
    /// </summary>
    /// <typeparam name="THost">数据主体类型，即提供 Accept 方法的类型 </typeparam>
    /// <param name="host">提供 Accept 方法的对象</param>
    /// <param name="visitor">访问者对象</param>
    /// <param name="key">要访问的属性或成员变量的名字</param>
    /// <param name="token">用于取消异步访问的令牌</param>
    /// <returns>访问是否成功</returns>
    public delegate UniTask<bool> FnAcceptVisitorAsync<THost>
        ( THost host
        , IVisitorFactory<THost, IVisitor<THost>> factory
        , string key = ""
        , CancellationToken token = default
        );

    /// <summary>
    /// 用于占位表示该类型没有提供可供 IVisitor 访问的 Accept 方法
    /// </summary>
    public static class AcceptVisitorAsyncExtensions
    {
        public static UniTask<bool> NotFound<THost, TFactory, TVisitor>
            ( this THost host
            , TFactory factory
            , string key = ""
            , CancellationToken token = default
            )
        where TFactory : IVisitorFactory<THost, TVisitor>
        where TVisitor : IVisitor<THost>
        {
            throw new NotImplementedException($"No extension methods found for type \"{typeof(THost)}\" with key \"{key}\".");
        }
        
        public static UniTask<bool> NotFound<THost>
            ( this THost host
            , IVisitorFactory<THost, IVisitor<THost>> factory
            , string key = ""
            , CancellationToken token = default
            )
        {
            throw new NotImplementedException($"No extension methods found for type \"{typeof(THost)}\" with key \"{key}\".");
        }

        public static UniTask<bool> RegisterAsync<THost>
            ( FnAcceptVisitorAsync<THost> acceptFn
            , CancellationToken token = default)
        {
            var cache = lazyCache_.Value;
            var d = acceptFn as Delegate;
            return cache.RegisterAsync(typeof(THost), d, token);
        }

        public static async UniTask<FnAcceptVisitorAsync<THost>> FindAsync<THost>(CancellationToken token = default)
        {
            var cache = lazyCache_.Value;
            var d = await cache.FindAsync(typeof(THost), token);
            if (d is FnAcceptVisitorAsync<THost> acceptFn)
                return acceptFn;
            else
                return NotFound;
        }

        private static readonly Lazy<FnCache> lazyCache_ = new Lazy<FnCache>(() => new FnCache());

        private sealed class FnCache
        {
            private readonly SemaphoreSlim sema_;

            private readonly Dictionary<Type, Delegate> dict_;

            public FnCache()
            {
                this.sema_ = new SemaphoreSlim(1, 1);
                this.dict_ = new Dictionary<Type, Delegate>();
            }

            public async UniTask<bool> RegisterAsync(Type t, Delegate d, CancellationToken token = default)
            {
                try
                {
                    await this.sema_.WaitAsync(token);
                    this.dict_.Add(t, d);
                    return true;
                }
                finally
                {
                    if (this.sema_.CurrentCount == 0)
                        this.sema_.Release();
                }
            }

            public async UniTask<Delegate> FindAsync(Type t, CancellationToken token = default)
            {
                try
                {
                    await this.sema_.WaitAsync(token);
                    if (this.dict_.TryGetValue(t, out var d))
                        return d;
                    else
                        return null;
                }
                finally
                {
                    if (this.sema_.CurrentCount == 0)
                        this.sema_.Release();
                }
            }
        }
    }
}
