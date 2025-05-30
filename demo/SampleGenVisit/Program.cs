namespace NsManualGenVisit
{
    using System.Threading;

    using Cysharp.Threading.Tasks;

    using NsAbsVisitAsync;
    using NsAnyLR;

    public static class ReceptionistInject
    {
        private static readonly ReceptionistManager manager_ = new ReceptionistManager();

        public static UniTask<Option<IReceptionist<T>>> GetAsync<T>(CancellationToken token = default)
            => manager_.GetAsync<T>(token);

        internal static UniTask<bool> RegisterAsync<T, R>(CancellationToken token = default) where R : IReceptionist<T>, new()
            => manager_.RegisterAsync<T, R>(false, token);
    }

    public readonly struct Receptionist_SampleGenVisit_SampleStruct : IReceptionist<SampleGenVisit.SampleStruct>
    {
        static Receptionist_SampleGenVisit_SampleStruct()
            => ReceptionistInject.RegisterAsync<SampleGenVisit.SampleStruct, Receptionist_SampleGenVisit_SampleStruct>().Forget();

        public async UniTask<bool> ReceptAsync(
            SampleGenVisit.SampleStruct data,
            IVisitor<SampleGenVisit.SampleStruct> visitor,
            IVisitorProvider provider,
            CancellationToken token = default)
        {
            using var visitor_Properties = await provider.GetMemberVisitorAsync<SampleGenVisit.SampleStruct, List<(string, System.Type)>>(visitor, 2u, nameof(SampleGenVisit.SampleStruct.Properties), token);
            var opt_receptionist_Properties = await ReceptionistInject.GetAsync<List<(string, System.Type)>>(token);
            if (!opt_receptionist_Properties.IsSome(out var receptionist_Properties))
                return false;
            if (!await receptionist_Properties.ReceptAsync(data.Properties, visitor_Properties, provider, token))
                return false;

            using var visitor_CanonicalName = await provider.GetMemberVisitorAsync<SampleGenVisit.SampleStruct, string>(visitor, 1u, nameof(SampleGenVisit.SampleStruct.CanonicalName), token);
            var opt_receptionist_CanonicalName = await ReceptionistInject.GetAsync<string>(token);
            if (!opt_receptionist_CanonicalName.IsSome(out var receptionist_CanonicalName))
                return false;
            if (!await receptionist_CanonicalName.ReceptAsync(data.CanonicalName, visitor_CanonicalName, provider, token))
                return false;

            return true;
        }
    }

    public readonly struct Weaver_SampleGenVisit_SampleStruct : IWeaver<SampleGenVisit.SampleStruct>
    {
        public async UniTask<Result<SampleGenVisit.SampleStruct, IBuilderError>> TryWeaveAsync(
            IBuilder<SampleGenVisit.SampleStruct> builder,
            IBuilderProvider<SampleGenVisit.SampleStruct> provider,
            CancellationToken token = default)
        {
            using var builder_Properties = await provider.GetMemberBuilderAsync<List<(string, System.Type)>>(builder, 2u, "Properties", token);
            var build_Properties_result = await builder_Properties.TryBuildAsync(token);
            if (build_Properties_result.IsErr(out var build_Propertis_error))
                return Result.Err(build_Propertis_error);

            using var builder_CanonicalName = await provider.GetMemberBuilderAsync<string>(builder, 1u, "CanonicalName", token);
            var build_CanonicalName_result = await builder_CanonicalName.TryBuildAsync(token);
            if (build_CanonicalName_result.IsErr(out var build_CanonicalName_error))
                return Result.Err(build_CanonicalName_error);

            return await builder.TryBuildAsync(token);
        }
    }

    public readonly struct Receptionist_SampleGenVisit_AnotherClass : IReceptionist<SampleGenVisit.AnotherClass>
    {
        public async UniTask<bool> ReceptAsync(
            SampleGenVisit.AnotherClass data,
            IVisitor<SampleGenVisit.AnotherClass> visitor,
            IVisitorProvider provider,
            CancellationToken token = default)
        {
            using var visitor_Name = await provider.GetMemberVisitorAsync<SampleGenVisit.AnotherClass, string>(visitor, 2u, nameof(SampleGenVisit.AnotherClass.Name), token);
            var opt_receptionist_Name = await ReceptionistInject.GetAsync<string>(token);
            if (!opt_receptionist_Name.IsSome(out var receptionist_Name))
                return false;
            if (!await receptionist_Name.ReceptAsync(data.Name, visitor_Name, provider, token))
                return false;

            using var visitor_MyList = await provider.GetMemberVisitorAsync<SampleGenVisit.AnotherClass, List<uint>>(visitor, 1u, nameof(SampleGenVisit.AnotherClass.MyList), token);
            if (!await visitor_MyList.VisitAsync(data.MyList, token))
                return false;

            return true;
        }
    }

    public readonly struct Receptionist_SampleGenVisit_ISampleInterface : IReceptionist<SampleGenVisit.ISampleInterface>
    {
        public async UniTask<bool> ReceptAsync(
            SampleGenVisit.ISampleInterface data,
            IVisitor<SampleGenVisit.ISampleInterface> visitor,
            IVisitorProvider provider,
            CancellationToken token = default)
        {
            if (data is SampleGenVisit.SampleStruct x_SampleStruct)
            {
                using var visitor_SampleStruct = await provider.GetVariantVisitorAsync<SampleGenVisit.ISampleInterface, SampleGenVisit.SampleStruct>(visitor, 2u, token);
                var opt_receptionist_SampleStruct = await ReceptionistInject.GetAsync<SampleGenVisit.SampleStruct>(token);
                if (!opt_receptionist_SampleStruct.IsSome(out var receptionist_SampleStruct))
                    return false;
                return await receptionist_SampleStruct.ReceptAsync(x_SampleStruct, visitor_SampleStruct, provider, token);
            }
            if (data is SampleGenVisit.AnotherClass x_AnotherClass)
            {
                using var visitor_AnotherClass = await provider.GetVariantVisitorAsync<SampleGenVisit.ISampleInterface, SampleGenVisit.AnotherClass>(visitor, 1u, token);
                var opt_receptionist_AnotherClass = await ReceptionistInject.GetAsync<SampleGenVisit.AnotherClass>(token);
                if (!opt_receptionist_AnotherClass.IsSome(out var receptionist_AnotherClass))
                    return false;
                return await receptionist_AnotherClass.ReceptAsync(x_AnotherClass, visitor_AnotherClass, provider, token);
            }
            return false;
        }
    }

    public readonly struct Weaver_SampleGenVisit_ISampleInterface : IWeaver<SampleGenVisit.ISampleInterface>
    {
        static async UniTask<Result<SampleGenVisit.ISampleInterface, IBuilderError>> B0(
            IBuilder<SampleGenVisit.ISampleInterface> builder,
            IBuilderProvider<SampleGenVisit.ISampleInterface> provider,
            CancellationToken token = default)
        {
            using var builder_SampleStruct = await provider.GetVariantBuilderAsync<SampleGenVisit.SampleStruct>(builder, token);
            var build_SampleStruct_result = await builder_SampleStruct.TryBuildAsync(token);
            return build_SampleStruct_result.MapOk(x => x as SampleGenVisit.ISampleInterface);
        }

        static async UniTask<Result<SampleGenVisit.ISampleInterface, IBuilderError>> B1(
            IBuilder<SampleGenVisit.ISampleInterface> builder,
            IBuilderProvider<SampleGenVisit.ISampleInterface> provider,
            CancellationToken token = default)
        {
            using var builder_AnotherClass = await provider.GetVariantBuilderAsync<SampleGenVisit.AnotherClass>(builder, token);
            var build_AnotherClass_result = await builder_AnotherClass.TryBuildAsync(token);
            return build_AnotherClass_result.MapOk(x => x as SampleGenVisit.ISampleInterface);
        }

        static readonly ReadOnlyMemory<Type> VARIANT_TYPES = new([
            typeof(SampleGenVisit.SampleStruct),
            typeof(SampleGenVisit.AnotherClass)
        ]);

        public async UniTask<Result<SampleGenVisit.ISampleInterface, IBuilderError>> TryWeaveAsync(
            IBuilder<SampleGenVisit.ISampleInterface> builder,
            IBuilderProvider<SampleGenVisit.ISampleInterface> factory,
            CancellationToken token = default)
        {
            var result = await builder.GetVariantTypeAsync(VARIANT_TYPES, token);
            if (!result.TryOk(out var index, out var getVariantTypeError))
                return Result.Err(getVariantTypeError);

            var a = new[] { B0, B1 };
            var b = a[index];
            return await b(builder, factory, token);
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
