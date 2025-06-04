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
            using var visitor_Properties = await provider.GetMemberVisitorAsync<SampleGenVisit.SampleStruct, List<(string, System.Type)>>(visitor, 2u, nameof(SampleGenVisit.SampleStruct.Properties), token);
            var receptionist_Properties = new ListReceptionist<List<(string, System.Type)>, (string, System.Type)>();
            // var opt_receptionist_Properties = await ReceptionistInject.GetAsync<List<(string, System.Type)>>(token);
            // if (!opt_receptionist_Properties.IsSome(out var receptionist_Properties))
            //     return false;
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

    public sealed class Builder_SampleGenVisit_SampleStruct : IBuilder<SampleGenVisit.SampleStruct>
    {
        static Builder_SampleGenVisit_SampleStruct()
            => BuilderInject.RegisterAsync<SampleGenVisit.SampleStruct, Builder_SampleGenVisit_SampleStruct>().Forget();

        public async UniTask<Result<SampleGenVisit.SampleStruct, IParserError>> TryBuildAsync(
            IParser<SampleGenVisit.SampleStruct> parser,
            IParserProvider provider,
            CancellationToken token = default)
        {
            using var parser_Properties = await provider.GetMemberParserAsync<SampleGenVisit.SampleStruct, List<(string, System.Type)>>(parser, 2u, "Properties", token);
            var parse_Properties_result = await parser_Properties.TryParseAsync(token);
            if (parse_Properties_result.IsErr(out var build_Propertis_error))
                return Result.Err(build_Propertis_error);

            using var builder_CanonicalName = await provider.GetMemberParserAsync<SampleGenVisit.SampleStruct, string>(parser, 1u, "CanonicalName", token);
            var build_CanonicalName_result = await builder_CanonicalName.TryParseAsync(token);
            if (build_CanonicalName_result.IsErr(out var build_CanonicalName_error))
                return Result.Err(build_CanonicalName_error);

            return await parser.TryParseAsync(token);
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

    public sealed class Receptionist_SampleGenVisit_ISampleInterface : IReceptionist<SampleGenVisit.ISampleInterface>
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

    public sealed class Builder_SampleGenVisit_ISampleInterface : IBuilder<SampleGenVisit.ISampleInterface>
    {
        static async UniTask<Result<SampleGenVisit.ISampleInterface, IParserError>> B0(
            IParser<SampleGenVisit.ISampleInterface> parser,
            IParserProvider provider,
            CancellationToken token = default)
        {
            using var parser_SampleStruct = await provider.GetVariantParserAsync<SampleGenVisit.ISampleInterface, SampleGenVisit.SampleStruct>(parser, token);
            var parse_SampleStruct_result = await parser_SampleStruct.TryParseAsync(token);
            return parse_SampleStruct_result.MapOk(x => x as SampleGenVisit.ISampleInterface);
        }

        static async UniTask<Result<SampleGenVisit.ISampleInterface, IParserError>> B1(
            IParser<SampleGenVisit.ISampleInterface> parser,
            IParserProvider provider,
            CancellationToken token = default)
        {
            using var parser_AnotherClass = await provider.GetVariantParserAsync<SampleGenVisit.ISampleInterface, SampleGenVisit.AnotherClass>(parser, token);
            var parse_AnotherClass_result = await parser_AnotherClass.TryParseAsync(token);
            return parse_AnotherClass_result.MapOk(x => x as SampleGenVisit.ISampleInterface);
        }

        static readonly ReadOnlyMemory<Type> VARIANT_TYPES = new([
            typeof(SampleGenVisit.SampleStruct),
            typeof(SampleGenVisit.AnotherClass)
        ]);

        static readonly ReadOnlyMemory<Func<
            IParser<SampleGenVisit.ISampleInterface>,
            IParserProvider,
            CancellationToken,
            UniTask<Result<SampleGenVisit.ISampleInterface, IParserError>>
        >> BRANCHES = new([B0, B1]);

        public async UniTask<Result<SampleGenVisit.ISampleInterface, IParserError>> TryBuildAsync(
            IParser<SampleGenVisit.ISampleInterface> parser,
            IParserProvider provider,
            CancellationToken token = default)
        {
            var result = await parser.GetVariantTypeAsync(VARIANT_TYPES, token);
            if (!result.TryOk(out var index, out var getVariantTypeError))
                return Result.Err(getVariantTypeError);

            var b = BRANCHES.Span[unchecked((int)index)];
            return await b(parser, provider, token);
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
