namespace AsyncVisit
{
    using System.Threading;

    public interface IAsyncVisitor
    {
        ValueTask<bool> VisitAsync<T>(T val, string key = "", CancellationToken token = default);
    }

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

    public sealed class SampleVisitor: IAsyncVisitor
    {
        public ValueTask<bool> VisitAsync<T>(T val, string key = "", CancellationToken token = default)
        {
            Console.WriteLine($"Key({key}): {val}");
            return ValueTask.FromResult(true);
        }
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

    using System.Collections.Immutable;

    public static class SampleGenVisitAccessExtensions
    {
        /// <summary>
        /// Generated method for <see cref="SampleStruct">SampleStruct</see> to accept an <see cref="IAsyncVisitor">IAsyncVisitor</see> to iterate its public properties and field.
        /// </summary>
        /// <param name="host">The object that will accept the visitor.</param>
        /// <param name="visitor">The object that will iterate visit.</param>
        /// <param name="token">Cancellation token that will cancel the async visit.</param>
        /// <returns>Whether all async visit are successfully completed.</returns>
        public static async ValueTask<bool> AcceptVisitorAsync<V>(this SampleStruct host, V visitor, CancellationToken token = default)
            where V : IAsyncVisitor
        {
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
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
        }
    }
}
