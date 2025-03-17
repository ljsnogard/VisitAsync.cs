namespace VisitAsyncUtils
{
    using System;
    using System.Threading;

    using Cysharp.Threading.Tasks;

    /// <summary>
    /// Visitor 模式中的访问者，用于访问某类型数据下的各异构组件.
    /// </summary>
    public interface IVisitor
    {
        UniTask<bool> VisitAsync<T>(T val, string key = "", CancellationToken token = default);
    }

    /// <summary>
    /// 提供具体的 Visitor 访问方法，针对相同的 T 可能存在零个或多个不同的 Visitor 访问方法
    /// </summary>
    /// <typeparam name="T">提供 Accept 方法的类型 </typeparam>
    /// <param name="host">提供 Accept 方法的对象</param>
    /// <param name="visitor">访问者对象</param>
    /// <param name="key">要访问的属性或成员变量的名字</param>
    /// <param name="token">用于取消异步访问的令牌</param>
    /// <returns>访问是否成功</returns>
    public delegate UniTask<bool> FnAcceptVisitAsync<T>(
        T host,
        IVisitor visitor,
        string key = "",
        CancellationToken token = default
    );

    /// <summary>
    /// 用于占位表示该类型没有提供可供 IVisitor 访问的 Accept 方法
    /// </summary>
    public static class AcceptVisitorAsyncExtensions
    {
        public static UniTask<bool> NotFound<T>(this T host, IVisitor visitor, string key = "", CancellationToken token = default)
            => throw new NotImplementedException($"No extension methods found for type \"{typeof(T)}\" with key \"{key}\".");
    }
}
