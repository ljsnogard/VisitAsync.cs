namespace NsManualGenVisit
{
    using System.Threading;

    using Cysharp.Threading.Tasks;

    using NsAbsVisitAsync;

    public static class ReceptionistInject
    {
        private static readonly ReceptionistManager manager_ = new ReceptionistManager();

        public static IReceptionist<T> GetReceptionist<T>()
            => manager_.GetReceptionist<T>();

        internal static void Register<T, R>() where R : class, IReceptionist<T>, new()
            => manager_.RegisterReceptionist<T, R>();
    }

    public sealed class Receptionist_SampleGenVisit_MySampleData : IReceptionist<SampleGenVisit.SampleStruct>
    {
        static Receptionist_SampleGenVisit_MySampleData()
            => ReceptionistInject.Register<SampleGenVisit.SampleStruct, Receptionist_SampleGenVisit_MySampleData>();

        public async UniTask<bool> AcceptAsync(
                SampleGenVisit.SampleStruct data,
                IVisitor<SampleGenVisit.SampleStruct> visitor,
                IVisitorFactory<SampleGenVisit.SampleStruct> factory,
                CancellationToken token = default)
        {
            using var visitor_Properties = await factory.GetItemVisitorAsync<List<(string, System.Type)>>(visitor, 2u, nameof(SampleGenVisit.SampleStruct.Properties), token);
            if (!await visitor_Properties.VisitAsync(data.Properties, token))
                return false;

            using var visitor_CanonicalName = await factory.GetItemVisitorAsync<string>(visitor, 1u, nameof(SampleGenVisit.SampleStruct.CanonicalName), token);
            if (!await visitor_CanonicalName.VisitAsync(data.CanonicalName, token))
                return false;

            return true;
        }
    }

    public sealed class Receptionist_SampleGenVisit_AnotherClass : IReceptionist<SampleGenVisit.AnotherClass>
    {
        static Receptionist_SampleGenVisit_AnotherClass()
            => ReceptionistInject.Register<SampleGenVisit.AnotherClass, Receptionist_SampleGenVisit_AnotherClass>();

        public async UniTask<bool> AcceptAsync(
                SampleGenVisit.AnotherClass data,
                IVisitor<SampleGenVisit.AnotherClass> visitor,
                IVisitorFactory<SampleGenVisit.AnotherClass> factory,
                CancellationToken token = default)
        {
            using var visitor_Name = await factory.GetItemVisitorAsync<string>(visitor, 2u, nameof(SampleGenVisit.AnotherClass.Name), token);
            if (!await visitor_Name.VisitAsync(data.Name, token))
                return false;

            using var visitor_MyList = await factory.GetItemVisitorAsync<List<uint>>(visitor, 1u, nameof(SampleGenVisit.AnotherClass.MyList), token);
            if (!await visitor_MyList.VisitAsync(data.MyList, token))
                return false;

            return true;
        }
    }

    public sealed class Receptionist_SampleGenVisit_ISampleInterface : IReceptionist<SampleGenVisit.ISampleInterface>
    {
        static Receptionist_SampleGenVisit_ISampleInterface()
            => ReceptionistInject.Register<SampleGenVisit.ISampleInterface, Receptionist_SampleGenVisit_ISampleInterface>();

        public async UniTask<bool> AcceptAsync(
                SampleGenVisit.ISampleInterface data,
                IVisitor<SampleGenVisit.ISampleInterface> visitor,
                IVisitorFactory<SampleGenVisit.ISampleInterface> factory,
                CancellationToken token = default)
        {
            if (data is SampleGenVisit.SampleStruct x_SampleStruct)
            {
                using var visitor_SampleStruct = await factory.GetVariantVisitorAsync<SampleGenVisit.SampleStruct>(visitor, 2u, token);
                return await visitor_SampleStruct.VisitAsync(x_SampleStruct, token);
            }
            if (data is SampleGenVisit.AnotherClass x_AnotherClass)
            {
                using var visitor_AnotherClass = await factory.GetVariantVisitorAsync<SampleGenVisit.AnotherClass>(visitor, 1u, token);
                return await visitor_AnotherClass.VisitAsync(x_AnotherClass, token);
            }
            return false;
        }
    }
}

namespace SampleGenVisit
{
    using System.Collections.Generic;

    using NsAbsVisitAsync;

    public record struct SampleStruct : ISampleInterface
    {
        public List<(string, System.Type)> Properties { get; init; }

        public string CanonicalName { get; init; }
    }

    public sealed class AnotherClass : ISampleInterface
    {
        public string Name { get; set; }

        public List<uint> MyList { get; set; }

        public AnotherClass(string name, List<uint> myList)
        {
            this.Name = name;
            this.MyList = myList;
        }
    }

    public interface ISampleInterface
    { }
}

namespace Demo
{
    using NsAbsVisitAsync;

    internal class Program
    {
        static void Main(string[] args)
        { }
    }
}
