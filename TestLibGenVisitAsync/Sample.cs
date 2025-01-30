namespace TestLibGenVisitAsync
{
    using System.Collections.Generic;
    using System.Collections.Immutable;

    using VisitAsyncUtils;

    [AcceptVisitAsync]
    public record struct SampleStruct
    {
        public ImmutableList<(string, System.Type)> Properties { get; init; }

        public string CanonicalName { get; init; }
    }

    namespace InternalNamespace
    {
        [AcceptVisitAsync]
        public sealed class SampleClass
        {
            public LinkedList<uint> SampelProperty { get; init; }
        }
    }
}
