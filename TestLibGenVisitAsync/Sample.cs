namespace Sample
{
    using System.Collections.Generic;
    using System.Collections.Immutable;

    using VisitAsyncUtils;

    [AllowVisit]
    public record struct SampleStruct
    {
        public ImmutableList<(string, System.Type)> Properties { get; init; }

        public string CanonicalName { get; init; }
    }

    namespace InternalNamespace
    {
        [AllowVisit]
        public sealed class SampleClass
        {
            public LinkedList<uint> SampleProperty { get; }

            [IgnoreVisit]
            public nuint SeenButNotVisitible { get; }

            public SampleClass(LinkedList<uint> sampleProperty)
            {
                this.SampleProperty = sampleProperty;
                this.SeenButNotVisitible = 0;
            }

        }

        [AllowVisit]
        public interface ISamepleInterface
        {
            public string MyName { get; }

            public ISamepleInterface Neibourgh { get; }
        }

        namespace NestedInternalNamespace
        {
            [AllowVisit]
            public record SampleRecord
            {
                public ISamepleInterface InterfaceProperty { get; init; }
            }
        }
    }
}
