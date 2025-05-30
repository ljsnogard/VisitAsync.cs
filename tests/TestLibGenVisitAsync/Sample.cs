namespace Sample
{
    using System.Collections.Generic;

    public struct SampleStruct
    {
        public List<(string, System.Type)> Properties { get; set; }

        public string CanonicalName { get; set; }
    }

    namespace InternalNamespace
    {
        public sealed class SampleClass
        {
            public LinkedList<uint> SampleProperty { get; }

            public nuint SeenButNotVisitible { get; }

            public SampleClass(LinkedList<uint> sampleProperty)
            {
                this.SampleProperty = sampleProperty;
                this.SeenButNotVisitible = 0;
            }
        }

        internal sealed class AnInternalClass : ISampleInterface
        {
            public string MyName { get; set; }

            public ISampleInterface Neighbour { get; set; }

            public AnInternalClass(string myName, ISampleInterface neighbour)
            {
                this.MyName = myName;
                this.Neighbour = neighbour;
            }
        }

        public interface ISampleInterface
        {
            public string MyName { get; }

            public ISampleInterface Neighbour { get; }
        }

        namespace NestedInternalNamespace
        {
            public readonly struct SampleRecordImpl : ISampleInterface
            {
                public string MyName { get; }

                public ISampleInterface Neighbour { get; }

                public SampleRecordImpl(string myName, ISampleInterface neibourgh)
                {
                    this.MyName = myName;
                    this.Neighbour = neibourgh;
                }
            }

            public sealed class SampleInterfaceImpl : ISampleInterface
            {
                public string MyName { get; set; }

                public ISampleInterface Neighbour { get; set; }

                public int Terminal { get; set; }

                public SampleInterfaceImpl(string myName, ISampleInterface neighbour, int terminal)
                {
                    this.MyName = myName;
                    this.Neighbour = neighbour;
                    this.Terminal = terminal;
                }
            }
        }
    }
}
