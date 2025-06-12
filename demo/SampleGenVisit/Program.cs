namespace NsManualGenVisit
{
    using System;
    using System.Threading;

    using Cysharp.Threading.Tasks;

    using NsAbsVisitAsync;
    using NsAnyLR;

    public static class ReceptionistInject
    {
        private static readonly ReceptionistManager manager_ = new NsAbsVisitAsync.ReceptionistManager();

        public static UniTask<Option<IReceptionist<T>>> GetAsync<T>(CancellationToken token = default)
            => manager_.GetAsync<T>(token);

        internal static UniTask<bool> RegisterAsync<T, R>(CancellationToken token = default)
            where R : class, IReceptionist<T>, new()
        {
            return manager_.RegisterAsync<T, R>(false, token);
        }
    }

    public static class BuilderInject
    {
        private static readonly BuilderManager manager_ = new NsAbsVisitAsync.BuilderManager();

        public static UniTask<Option<IBuilder<T>>> GetAsync<T>(CancellationToken token = default)
            => manager_.GetAsync<T>(token);

        internal static UniTask<bool> RegisterAsync<T, B>(CancellationToken token = default)
            where B : class, IBuilder<T>, new()
        {
            return manager_.RegisterAsync<T, B>(false, token);
        }
    }

    public sealed class Receptionist_SampleGenVisit_SampleStruct : IReceptionist<SampleGenVisit.SampleStruct>
    {
        static Receptionist_SampleGenVisit_SampleStruct()
            => ReceptionistInject.RegisterAsync<SampleGenVisit.SampleStruct, Receptionist_SampleGenVisit_SampleStruct>().Forget();

        public async UniTask<bool> ReceptAsync(
            SampleGenVisit.SampleStruct data,
            IVisitor<SampleGenVisit.SampleStruct> visitor,
            IVisitorProvider provider,
            CancellationToken token = default)
        {
            using var memberVisitorProvider = await provider.ProviderForMembersAsync(visitor, 2u, token);

            using var visitor_Properties = await memberVisitorProvider.GetMemberVisitorAsync<List<(string, System.Type)>>("Properties", token);
            var opt_receptionist_Properties = await ReceptionistInject.GetAsync<List<(string, System.Type)>>(token);
            if (!opt_receptionist_Properties.IsSome(out var receptionist_Properties))
                receptionist_Properties = new ListReceptionist<List<(string, System.Type)>, (string, System.Type)>();
            if (!await receptionist_Properties.ReceptAsync(data.Properties, visitor_Properties, provider, token))
                return false;

            using var visitor_CanonicalName = await memberVisitorProvider.GetMemberVisitorAsync<string>("CanonicalName", token);
            var opt_receptionist_CanonicalName = await ReceptionistInject.GetAsync<string>(token);
            if (!opt_receptionist_CanonicalName.IsSome(out var receptionist_CanonicalName))
                return false;
            if (!await receptionist_CanonicalName.ReceptAsync(data.CanonicalName, visitor_CanonicalName, provider, token))
                return false;

            return true;
        }
    }

    public sealed class Builder_SampleGenVisit_SampleStruct : IBuilder<SampleGenVisit.SampleStruct>
    {
        static Builder_SampleGenVisit_SampleStruct()
            => BuilderInject.RegisterAsync<SampleGenVisit.SampleStruct, Builder_SampleGenVisit_SampleStruct>().Forget();

        public async UniTask<Result<SampleGenVisit.SampleStruct, IParserError>> TryBuildAsync(
            IParser<SampleGenVisit.SampleStruct> parser,
            IParserProvider provider,
            CancellationToken token = default)
        {
            using var memberParserProvider = await provider.ProviderForMemberAsync(parser, 2u, token);

            using var parser_Properties = await memberParserProvider.GetMemberParserAsync<List<(string, System.Type)>>("Properties", token);
            var opt_builder_Properties = await BuilderInject.GetAsync<List<(string, Type)>>(token);
            if (!opt_builder_Properties.IsSome(out var builder_Properties))
                builder_Properties = new LstBuilder<List<(string, Type)>, (string, Type)>();
            var build_Properties_result = await builder_Properties.TryBuildAsync(parser_Properties, provider, token);
            if (!build_Properties_result.TryOk(out var build_Properties_ok, out var build_Propertis_error))
                return Result.Err(build_Propertis_error);

            using var parser_CanonicalName = await memberParserProvider.GetMemberParserAsync<string>("CanonicalName", token);
            var opt_builder_CanonicalName = await BuilderInject.GetAsync<string>(token);
            if (!opt_builder_CanonicalName.IsSome(out var builder_CanonicalName))
                builder_CanonicalName = new StrBuilder();
            var build_CanonicalName_result = await builder_CanonicalName.TryBuildAsync(parser_CanonicalName, provider, token);
            if (!build_CanonicalName_result.TryOk(out var build_CanonicalName_ok, out var build_CanonicalName_err))
                return Result.Err(build_CanonicalName_err);

            return Result.Ok(new SampleGenVisit.SampleStruct()
            {
                Properties = build_Properties_ok,
                CanonicalName = build_CanonicalName_ok
            });
        }
    }

    public sealed class Receptionist_SampleGenVisit_AnotherClass : IReceptionist<SampleGenVisit.AnotherClass>
    {
        public async UniTask<bool> ReceptAsync(
            SampleGenVisit.AnotherClass data,
            IVisitor<SampleGenVisit.AnotherClass> visitor,
            IVisitorProvider provider,
            CancellationToken token = default)
        {
            var memberVisitorProvider = await provider.ProviderForMembersAsync(visitor, 2u, token);

            using var visitor_Name = await memberVisitorProvider.GetMemberVisitorAsync<string>("Name", token);
            var opt_receptionist_Name = await ReceptionistInject.GetAsync<string>(token);
            if (!opt_receptionist_Name.IsSome(out var receptionist_Name))
                return false;
            if (!await receptionist_Name.ReceptAsync(data.Name, visitor_Name, provider, token))
                return false;

            using var visitor_MyList = await memberVisitorProvider.GetMemberVisitorAsync<List<uint>>("MyList", token);
            var receptionist_MyList = new ListReceptionist<List<uint>, uint>();
            if (!await receptionist_MyList.ReceptAsync(data.MyList, visitor_MyList, provider, token))
                return false;

            return true;
        }
    }

    public sealed class Receptionist_SampleGenVisit_ISampleInterface : IReceptionist<SampleGenVisit.ISampleInterface>
    {
        public async UniTask<bool> ReceptAsync(
            SampleGenVisit.ISampleInterface data,
            IVisitor<SampleGenVisit.ISampleInterface> visitor,
            IVisitorProvider provider,
            CancellationToken token = default)
        {
            var variantProvider = await provider.ProviderForVariantsAsync(visitor, token);
            if (data is SampleGenVisit.SampleStruct x_SampleStruct)
            {
                var opt_receptionist_SampleStruct = await ReceptionistInject.GetAsync<SampleGenVisit.SampleStruct>(token);
                if (!opt_receptionist_SampleStruct.IsSome(out var receptionist_SampleStruct))
                    return false;
                using var visitor_SampleStruct = await variantProvider.GetVariantVisitorAsync<SampleGenVisit.SampleStruct>(token);
                return await receptionist_SampleStruct.ReceptAsync(x_SampleStruct, visitor_SampleStruct, provider, token);
            }
            if (data is SampleGenVisit.AnotherClass x_AnotherClass)
            {
                var opt_receptionist_AnotherClass = await ReceptionistInject.GetAsync<SampleGenVisit.AnotherClass>(token);
                if (!opt_receptionist_AnotherClass.IsSome(out var receptionist_AnotherClass))
                    return false;
                using var visitor_AnotherClass = await variantProvider.GetVariantVisitorAsync<SampleGenVisit.AnotherClass>(token);
                return await receptionist_AnotherClass.ReceptAsync(x_AnotherClass, visitor_AnotherClass, provider, token);
            }
            return false;
        }
    }

    public sealed class Builder_SampleGenVisit_ISampleInterface : IBuilder<SampleGenVisit.ISampleInterface>
    {
        static async UniTask<Result<SampleGenVisit.ISampleInterface, IParserError>> B0(
            IVariantParserProvider<SampleGenVisit.ISampleInterface> provider,
            CancellationToken token = default)
        {
            using var parser_SampleStruct = await provider.GetVariantParserAsync<SampleGenVisit.SampleStruct>(token);
            var parse_SampleStruct_result = await parser_SampleStruct.TryParseAsync(token);
            return parse_SampleStruct_result.MapOk(x => x as SampleGenVisit.ISampleInterface);
        }

        static async UniTask<Result<SampleGenVisit.ISampleInterface, IParserError>> B1(
            IVariantParserProvider<SampleGenVisit.ISampleInterface> provider,
            CancellationToken token = default)
        {
            using var parser_AnotherClass = await provider.GetVariantParserAsync<SampleGenVisit.AnotherClass>(token);
            var parse_AnotherClass_result = await parser_AnotherClass.TryParseAsync(token);
            return parse_AnotherClass_result.MapOk(x => x as SampleGenVisit.ISampleInterface);
        }

        static readonly ReadOnlyMemory<Type> VARIANT_TYPES = new([
            typeof(SampleGenVisit.SampleStruct),
            typeof(SampleGenVisit.AnotherClass)
        ]);

        static readonly ReadOnlyMemory<Func<
            IVariantParserProvider<SampleGenVisit.ISampleInterface>,
            CancellationToken,
            UniTask<Result<SampleGenVisit.ISampleInterface, IParserError>>
        >> BRANCHES = new([B0, B1]);

        public async UniTask<Result<SampleGenVisit.ISampleInterface, IParserError>> TryBuildAsync(
            IParser<SampleGenVisit.ISampleInterface> parser,
            IParserProvider provider,
            CancellationToken token = default)
        {
            var variantParserProvider = await provider.ProviderForVariantsAsync(parser, token);
            var result = await variantParserProvider.FindVariantTypeAsync(VARIANT_TYPES, token);
            if (!result.TryOk(out var index, out var getVariantTypeError))
                return Result.Err(getVariantTypeError);

            var b = BRANCHES.Span[unchecked((int)index)];
            return await b(variantParserProvider, token);
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
