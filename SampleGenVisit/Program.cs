﻿using SampleGenVisit;
using VisitAsyncUtils;

namespace AsyncVisit
{
    using System.Threading;
    using Cysharp.Threading.Tasks;

    /// <summary>
    /// Useful only for serde occations, not part of the visitor pattern.
    /// </summary>
    public readonly struct SerdeOptions
    {
        private readonly uint code_;

        private SerdeOptions(uint code)
            => this.code_ = code;

        public static readonly SerdeOptions Default = new(0);
        public static readonly SerdeOptions AsTup = new(1);
        public static readonly SerdeOptions AsArr = new(2);
        public static readonly SerdeOptions AsMap = new(3);
        public static readonly SerdeOptions AsStr = new(4);
    }

    public sealed class VisitAsyncAttribute : System.Attribute
    { }

    public readonly struct MyVisitorFactory<H, V> : IVisitorFactory<H, V>
        where V : IVisitor<H>
    {
        public UniTask<V> GetVisitorAsync(H host, CancellationToken token = default)
            => throw new NotImplementedException();
    }

    public sealed class SampleVisitor<H>: IVisitor<H>, IDisposable
    {
        public UniTask<bool> VisitAsync<T>(T val, string key = "", CancellationToken token = default)
        {
            return val switch
            {
                byte u8 => this.VisitU8Async(u8, key, token),
                ushort u16 => this.VisitU16Async(u16, key, token),
                uint u32 => this.VisitU32Async(u32, key, token),
                ulong u64 => this.VisitU64Async(u64, key, token),
                sbyte i8 => this.VisitI8Async(i8, key, token),
                short i16 => this.VisitI16Async(i16, key, token),
                int i32 => this.VisitI32Async(i32, key, token),
                long i64 => this.VisitI64Async(i64, key, token),
                string str => this.VisitStrAsync(str, key, token),
                T[] arr => this.VisitArrAsync(arr, key, token),
                _ => throw new NotImplementedException(),
            };
        }

        public void Dispose()
        {}

        public UniTask<bool> VisitU8Async(byte val, string key = "", CancellationToken token = default)
            => throw new NotImplementedException();

        public UniTask<bool> VisitU16Async(ushort val, string key = "", CancellationToken token = default)
            => throw new NotImplementedException();

        public UniTask<bool> VisitU32Async(uint val, string key = "", CancellationToken token = default)
            => throw new NotImplementedException();

        public UniTask<bool> VisitU64Async(ulong val, string key = "", CancellationToken token = default)
            => throw new NotImplementedException();

        public UniTask<bool> VisitI8Async(sbyte val, string key = "", CancellationToken token = default)
            => throw new NotImplementedException();

        public UniTask<bool> VisitI16Async(short val, string key = "", CancellationToken token = default)
            => throw new NotImplementedException();

        public UniTask<bool> VisitI32Async(int val, string key = "", CancellationToken token = default)
            => throw new NotImplementedException();

        public UniTask<bool> VisitI64Async(long val, string key = "", CancellationToken token = default)
            => throw new NotImplementedException();

        public UniTask<bool> VisitStrAsync(string val, string key = "", CancellationToken token = default)
            => throw new NotImplementedException();

        public UniTask<bool> VisitArrAsync<T>(T[] val, string key = "", CancellationToken token = default)
            => throw new NotImplementedException();
    }
}

namespace SampleGenVisit
{
    using System.Collections.Immutable;

    using AsyncVisit;

    [VisitAsync]
    public record struct SampleStruct
    {
        public ImmutableList<(string, System.Type)> Properties { get; init; }

        public string CanonicalName { get; init; }
    }
}

namespace SampleGenVisit.GeneratedVisitorUtils
{
    using AsyncVisit;
    using Cysharp.Threading.Tasks;
    using System.Collections.Immutable;

    using VisitAsyncUtils;

    public static class SampleGenVisitAccessExtensions
    {
        /// <summary>
        /// Generated method for <see cref="SampleStruct">SampleStruct</see> to accept an <see cref="IAsyncVisitor">IAsyncVisitor</see> to iterate its public properties and field.
        /// </summary>
        /// <param name="host">The object that will accept the visitor.</param>
        /// <param name="factory">The factory object that will provide the visitor for the host.</param>
        /// <param name="token">Cancellation token that will cancel the async visit.</param>
        /// <returns>Whether all async visit are successfully completed.</returns>
        public static async ValueTask<bool> AcceptVisitorAsync<F, V>(this SampleStruct host, F factory, CancellationToken token = default)
            where F : IVisitorFactory<SampleStruct, V>
            where V : IVisitor<SampleStruct>
        {
            using var visitor = await factory.GetVisitorAsync(host, token);
            if (!await visitor.VisitAsync(host.Properties, nameof(host.Properties), token))
                return false;
            if (!await visitor.VisitAsync(host.CanonicalName, nameof(host.CanonicalName), token))
                return false;
            return true;
        }

        public static ReadOnlyMemory<(string, System.Type, SerdeOptions)> SerdeSchema(this SampleStruct host)
        {
            return new[]
            {
                (nameof(host.Properties), typeof(ImmutableList<(string, System.Type)>), SerdeOptions.AsArr),
                (nameof(host.CanonicalName), typeof(string), SerdeOptions.AsStr),
            };
        }
    }
}

namespace Demo
{
    using AsyncVisit;

    internal class Program
    {
        static void Main(string[] args)
        {
            // var s = new SampleStruct();
            FnAcceptVisitorAsync
                < SampleStruct
                , MyVisitorFactory<SampleStruct, SampleVisitor<SampleStruct>>
                , SampleVisitor<SampleStruct>
                > f;

            f = AcceptVisitorAsyncExtensions.NotFound
                < SampleStruct
                , MyVisitorFactory<SampleStruct, SampleVisitor<SampleStruct>>
                , SampleVisitor<SampleStruct>
                >;

            Console.WriteLine($"Hello, {f.GetType()}");
        }
    }
}
