namespace NsAbsVisitAsync
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    using Cysharp.Threading.Tasks;

    using NsAnyLR;

    /// <summary>
    /// A simple reader-writer lock that does not support upgradable readers.
    /// </summary>
    internal sealed class AsyncRwLockSlim
    {
        private readonly SemaphoreSlim sema_ = new SemaphoreSlim(1, 1);

        private readonly LinkedList<RwContext> ctxQueue_ = new();

        public async UniTask<Option<Guard>> LockReadAsync(CancellationToken token = default)
        {
            ReaderContext? ctx = null;
            bool isGuarded = false;
            try
            {
                await this.sema_.WaitAsync(token);
                isGuarded = true;

                if (this.ctxQueue_.Count == 0)
                {
                    ctx = new ReaderContext();
                    ctx.Node = this.ctxQueue_.AddLast(ctx);

                    var rc = ctx.IncreaseReadersCount();
                    Debug.Assert(rc == 0L);
                    return Option.Some(new Guard(this, ctx));
                }
                while (true)
                {
                    if (this.ctxQueue_.Last.Value is WriterContext)
                    {
                        ctx = new ReaderContext();
                        ctx.Node = this.ctxQueue_.AddLast(ctx);
                    }
                    if (this.ctxQueue_.Last.Value is ReaderContext tailReaderCtx)
                    {
                        tailReaderCtx.IncreaseReadersCount();
                        ctx = tailReaderCtx;
                        break;
                    }
                }
                if (ctx is ReaderContext rCtx)
                {
                    this.sema_.Release();
                    isGuarded = false;

                    return await rCtx.Tcs.Task.AsUniTask().AttachExternalCancellation(token);
                }
                throw new NotImplementedException("Unreachable code");
            }
            catch (OperationCanceledException)
            {
                if (ctx is ReaderContext readerCtx)
                {
                    var x = readerCtx.DecreaseReadersCount();
                    Debug.Assert(x > 0);
                }
                return Option.None();
            }
            finally
            {
                if (isGuarded)
                    this.sema_.Release();
            }
        }

        public async UniTask<Option<Guard>> LockWriteAsync(CancellationToken token = default)
        {
            WriterContext? ctx = null;
            bool isGuarded = false;
            try
            {
                await this.sema_.WaitAsync(token);
                isGuarded = true;

                while (true)
                {
                    if (this.ctxQueue_.Count == 0 || this.ctxQueue_.Last.Value is ReaderContext)
                    {
                        ctx = new WriterContext(token);
                        ctx.Node = this.ctxQueue_.AddLast(ctx);
                    }
                    if (this.ctxQueue_.Last.Value is WriterContext writerCtx)
                    {
                        ctx = writerCtx;
                        break;
                    }
                }
                if (ctx is WriterContext wCtx)
                {
                    this.sema_.Release();
                    isGuarded = false;

                    return await wCtx.Tcs.Task.AsUniTask().AttachExternalCancellation(token);
                }
                throw new NotImplementedException("Unreachable code");
            }
            catch (OperationCanceledException)
            {
                return Option.None();
            }
            finally
            {
                if (isGuarded)
                    this.sema_.Release();
            }
        }

        private async UniTask ReleaseAsync(RwContext ctx)
        {
            if (ctx.Node is not LinkedListNode<RwContext> node)
                throw new Exception("double dispose");
            if (node.List != this.ctxQueue_)
                throw new Exception("Unmatched list");
            if (this.ctxQueue_.First != node)
                throw new Exception("Illegal state");

            var isGuarded = false;
            try
            {
                await this.sema_.WaitAsync();
                isGuarded = true;

                switch (ctx)
                {
                    case WriterContext:
                        this.ctxQueue_.Remove(node);
                        break;
                    case ReaderContext rCtx:
                        var rc = rCtx.DecreaseReadersCount();
                        if (rc == 1L)
                            this.ctxQueue_.Remove(node);
                        else
                            return;
                        break;
                    default:
                        throw new Exception($"Unexpected ctx type: {ctx.GetType()}");
                }
                RwContext? optNextCtx = null;
                while (this.ctxQueue_.Count > 0)
                {
                    var headNode = this.ctxQueue_.First;

                    if (headNode.Value is WriterContext wCtx)
                    {
                        if (wCtx.IsCancelled)
                        {
                            this.ctxQueue_.Remove(headNode);
                            continue;
                        }
                        optNextCtx = wCtx;
                        break;
                    }
                    if (headNode.Value is ReaderContext rCtx)
                    {
                        if (rCtx.ReadersCount() <= 0L)
                        {
                            this.ctxQueue_.Remove(headNode);
                            continue;
                        }
                        optNextCtx = rCtx;
                        break;
                    }
                }
                if (optNextCtx is RwContext nextCtx)
                    nextCtx.Tcs.TrySetResult(new Guard(this, nextCtx));
            }
            finally
            {
                if (isGuarded)
                    sema_.Release();
            }
        }

        public struct Guard : IDisposable, IAsyncDisposable
        {
            private readonly AsyncRwLockSlim rwlock_;
            private RwContext? ctx_;

            internal Guard(AsyncRwLockSlim rwLock, RwContext ctx)
            {
                this.rwlock_ = rwLock;
                this.ctx_ = ctx;
            }

            public void Dispose()
                => this.DisposeAsync().AsUniTask().Forget();

            public async ValueTask DisposeAsync()
            {
                if (this.ctx_ is not RwContext ctx)
                    return;
                this.ctx_ = null;
                await this.rwlock_.ReleaseAsync(ctx);
            }
        }

        internal abstract class RwContext
        {
            private readonly TaskCompletionSource<Guard> tcs_;

            public RwContext()
            {
                this.tcs_ = new TaskCompletionSource<Guard>();
                this.Node = null;
            }

            public TaskCompletionSource<Guard> Tcs
                => this.tcs_;

            public LinkedListNode<RwContext>? Node { get; set; }
        }

        private sealed class ReaderContext : RwContext
        {
            private long readersCount_;

            public ReaderContext() : base()
                => this.readersCount_ = 0L;

            public long ReadersCount()
                => Interlocked.Read(ref this.readersCount_);

            public long DecreaseReadersCount()
                => Interlocked.Decrement(ref this.readersCount_);

            public long IncreaseReadersCount()
                => Interlocked.Increment(ref this.readersCount_);
        }

        private sealed class WriterContext : RwContext
        {
            private readonly CancellationToken tok_;

            public WriterContext(CancellationToken token) : base()
                => this.tok_ = token;

            public bool IsCancelled
                => this.tok_.IsCancellationRequested;
        }
    }
}