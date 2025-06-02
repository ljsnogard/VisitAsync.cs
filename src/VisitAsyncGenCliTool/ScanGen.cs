namespace NsAbsVisitAsync.NsCliTools
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;  // included within Microsoft.CodeAnalysis
    using System.Linq;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.MSBuild;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    using SymbolsDictionary = System.Collections.Immutable.ImmutableDictionary<
        Microsoft.CodeAnalysis.Document,
        System.Collections.Immutable.ImmutableList<Microsoft.CodeAnalysis.INamedTypeSymbol>
    >;

    public sealed class ScanGen
    {
        private readonly MSBuildWorkspace workspace_;

        private readonly string projPath_;

        private readonly Project project_;

        private readonly ScanSettings scanSettings_;

        private readonly CodeGenSettings codeGenSettings_;

        private ScanGen(
            MSBuildWorkspace workspace,
            string projPath,
            Project project,
            ScanSettings scanSettings,
            CodeGenSettings codeGenSettings)
        {
            this.workspace_ = workspace;
            this.projPath_ = projPath;
            this.project_ = project;
            this.scanSettings_ = scanSettings;
            this.codeGenSettings_ = codeGenSettings;
        }

        public static async Task<ScanGen> OpenAsync(
            string projPath,
            ScanSettings scanSettings,
            CodeGenSettings codeGenSettings)
        {
            var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projPath);
            if (project is null)
                throw new Exception();
            return new ScanGen(workspace, projPath, project, scanSettings, codeGenSettings);
        }

        public async Task GenAsync(CancellationToken token = default)
        {
            var projFolderPath = Path.GetDirectoryName(Path.GetFullPath(this.projPath_));
            if (projFolderPath is null)
                throw new Exception($"Faield to get project folder path from projPath({this.projPath_})");

            var codeGenFolderPath = $"{projFolderPath}{Path.DirectorySeparatorChar}{this.codeGenSettings_.CodeGenFolderName}";

            SymbolsDictionary symbols;
            try
            {
                var buildingSymbols = new Dictionary<Document, ImmutableList<INamedTypeSymbol>>();
                foreach (var document in this.project_.Documents)
                {
                    if (document.FilePath is string documentPathStr)
                    {
                        if (documentPathStr.StartsWith(codeGenFolderPath) && documentPathStr.EndsWith(".gen.cs"))
                        {
                            Console.Out.WriteLine($"跳过生成文件: {documentPathStr}");
                            continue;
                        }
                    }
                    Console.WriteLine($"发现代码文件: {projFolderPath}{Path.DirectorySeparatorChar}{document.Name}");

                    var symbolsList = new List<INamedTypeSymbol>();
                    var model = await document.GetSemanticModelAsync();
                    if (model is null)
                    {
                        await Console.Out.WriteLineAsync($"Unexpected null model for document(Name: {document.Name})");
                        continue;
                    }
                    var root = await document.GetSyntaxRootAsync();
                    if (root is null)
                    {
                        await Console.Out.WriteLineAsync($"Unexpected null syntax root in document(Name: {document.Name})");
                        continue;
                    }
                    var nodes =
                        from node in root.DescendantNodes()
                        where node.IsQualifiedSyntaxNode()
                        select node;

                    foreach (var node in nodes)
                    {
                        var symbol = model.GetDeclaredSymbol(node);
                        if (symbol is not INamedTypeSymbol namedTypeSymbol)
                            continue;
                        if (namedTypeSymbol.DeclaredAccessibility != Accessibility.Public)
                        {
                            Console.WriteLine($"\t跳过非公开类型: {symbol.ToDisplayString()}");
                            continue;
                        }
                        if (namedTypeSymbol.IsStatic)
                        {
                            Console.WriteLine($"\t跳过静态类型: {symbol.ToDisplayString()}");
                            continue;
                        }
                        else
                            Console.WriteLine($"\t发现目标类型: {symbol.ToDisplayString()}");

                        symbolsList.Add(namedTypeSymbol);

                        foreach (var member in namedTypeSymbol.GetVisitableDataMembers())
                            Console.WriteLine($"\t\t目标成员: {member.ToDisplayParts().Last()}");
                    }
                    if (symbolsList.Any())
                        buildingSymbols.Add(document, symbolsList.ToImmutableList());
                }
                symbols = buildingSymbols.ToImmutableDictionary();
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }

            var genFileNameSpace = $"{this.project_.Name}.NsGenVisit";
            var receptionistInjectTypeName = "ReceptionistInject";
            var builderInjectTypeName = "BuilderInject";

            foreach (var kv in symbols)
            {
                var document = kv.Key;
                var symbolsList = kv.Value;
                try
                {
                    if (!Directory.Exists(codeGenFolderPath))
                        Directory.CreateDirectory(codeGenFolderPath);

                    if (document.FilePath is null)
                        throw new Exception($"Document(Name: {document.Name}) file path does not exists");
                    var documentId = document
                        .FilePath
                        .Remove(startIndex: 0, count: projFolderPath.Length + 1)
                        .Replace('/', '_')
                        .Replace('\\', '_')
                        .Replace('.', '_');

                    var genFileName = $"{documentId}.gen.cs";
                    var absFilePath = $"{codeGenFolderPath}{Path.DirectorySeparatorChar}{genFileName}";

                    await Console.Out.WriteLineAsync($"正在为 {document.Name} 生成代码文件 {absFilePath} (documentId: {documentId})");

                    /// https://stackoverflow.com/questions/1225857/write-string-to-text-file-and-ensure-it-always-overwrites-the-existing-content/1225869
                    /// * If the file exists, this overwrites it.
                    /// * If the file does not exist, this creates it.
                    File.WriteAllText(absFilePath, string.Empty);

                    /// Exclusively open file and write generated code into it.
                    using var file = new FileStream(absFilePath, FileMode.Open, FileAccess.Write, FileShare.None);
                    await GenerateFileLevelCodeAsync(
                        codeGenSettings: this.codeGenSettings_,
                        project: this.project_,
                        symbolsDict: symbols,
                        fileStream: file,
                        document: document,
                        genFileNameSpace: genFileNameSpace,
                        receptionistInjectTypeName: receptionistInjectTypeName,
                        builderInjectTypeName: builderInjectTypeName
                    );
                }
                catch (Exception e)
                {
                    await Console.Out.WriteLineAsync($"{e}");
                    throw;
                }
            }

            await GenerateInjectCodeAsync(
                codeGenSettings: this.codeGenSettings_,
                genFileNameSpace: genFileNameSpace,
                codeGenFolderPath: codeGenFolderPath,
                receptionistInjectTypeName: receptionistInjectTypeName,
                builderInjectTypeName: builderInjectTypeName
            );
        }

        /// <summary>
        /// Generate code lines in a single file based on the types that are also in a single file.
        /// </summary>
        /// <param name="codeGenSettings"></param>
        /// <param name="project"></param>
        /// <param name="symbolsDict"></param>
        /// <param name="documentId"></param>
        /// <param name="fileStream"></param>
        /// <param name="document"></param>
        /// <param name="genFileNameSpace"></param>
        /// <param name="receptionistInjectTypeName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        static async ValueTask GenerateFileLevelCodeAsync(
            CodeGenSettings codeGenSettings,
            Project project,
            SymbolsDictionary symbolsDict,
            FileStream fileStream,
            Document document,
            string genFileNameSpace,
            string receptionistInjectTypeName,
            string builderInjectTypeName)
        {
            if (!symbolsDict.TryGetValue(document, out var symbolsList))
                throw new Exception();
            try
            {
                var topComment =
@$"/// <auto-generated>
///     Generated by VisitAsyncGenCliTool at {DateTimeOffset.Now} (DateTimeOffset).
///     Changes will be lost once regenerated.
/// </auto-generated>

";
                await fileStream.WriteAsync(topComment);

                var nsLine = $"namespace {genFileNameSpace}\n{{\n";
                await fileStream.WriteAsync(nsLine);

                var usingLine = @$"
using System;
using System.Threading; // To use CancellationToken

using {codeGenSettings.UsingLineStr}; 

using NsAbsVisitAsync;
using NsAnyLR;

";
                await fileStream.WriteAsync(usingLine);

                var tab = codeGenSettings.TabStr;
                var tab2 = $"{tab}{tab}";
                foreach (var typeSymbol in symbolsList)
                {
                    if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
                        throw new Exception($"Expecting {nameof(INamedTypeSymbol)}, but {typeSymbol.GetType().FullName} encountered.");

                    await Receptionist_GenereateForDeclTypeAsync(codeGenSettings, project, symbolsDict, fileStream, document, namedTypeSymbol, receptionistInjectTypeName);
                    await Builder_GenerateForDeclTypeAsync(codeGenSettings, project, symbolsDict, fileStream, document, namedTypeSymbol, builderInjectTypeName);
                }

                // Write the closing right brace of the namespace
                await fileStream.WriteAsync("}\n\n");
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }
        }

        static async ValueTask Receptionist_GenereateForDeclTypeAsync(
            CodeGenSettings codeGenSettings,
            Project project,
            SymbolsDictionary symbolsDict,
            Stream fileStream,
            Document document,
            INamedTypeSymbol data_NamedTypeSymbol,
            string injectTypeName)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";

            var dataTypeFullName = data_NamedTypeSymbol.ToDisplayString();
            var receptionistKlassSuffix = data_NamedTypeSymbol.MangledNameAsIdentifierComponent();
            var genTypeName = $"Receptionist_{receptionistKlassSuffix}";

            var clsDeclLine = $"public sealed class {genTypeName} : IReceptionist<{dataTypeFullName}>\n" + "{\n";
            await fileStream.WriteAsync(clsDeclLine);

            // Code gen for the static constructor of the receptionist class
            // static Receptionist_GenTypeName()
            //     => ReceptionistInject.Register<dataTypeFullName, Receptionist_GenTypeName>();
            if (true)
            {
                var ctorStaticDecl0 = $"static {genTypeName}()";
                var ctorStaticImpl0 = $"=> {injectTypeName}.RegisterAsync<{dataTypeFullName}, {genTypeName}>().Forget();";

                await fileStream.WriteAsync($"{tab}{ctorStaticDecl0}\n");
                await fileStream.WriteAsync($"{tab}{tab}{ctorStaticImpl0}\n");
            }

            var methDecl0 = $"public async {codeGenSettings.TasksTypeStr_Receptionist} ReceptAsync(";
            var methDeclParamData = $"{dataTypeFullName} data,";
            var methDeclParamVisitor = $"IVisitor<{dataTypeFullName}> visitor,";

            var visitorProviderVarName = "provider";
            var methDeclParamFactory = $"{nameof(IVisitorProvider)} {visitorProviderVarName},";
            var methDeclParamToken = "CancellationToken token = default)";

            await fileStream.WriteAsync($"\n{tab}{methDecl0}\n");
            await fileStream.WriteAsync($"{tab2}{methDeclParamData}\n");
            await fileStream.WriteAsync($"{tab2}{methDeclParamVisitor}\n");
            await fileStream.WriteAsync($"{tab2}{methDeclParamFactory}\n");
            await fileStream.WriteAsync($"{tab2}{methDeclParamToken}\n");
            await fileStream.WriteAsync($"{tab}{{\n");

            if (data_NamedTypeSymbol.ShouldGenForVariantTypes())
            {
                await Receptionist_GenerateForAbstractTypeAsync(
                    codeGenSettings: codeGenSettings,
                    project: project,
                    symbolsDict: symbolsDict,
                    ioStream: fileStream,
                    document: document,
                    injectTypeName: injectTypeName,
                    memberVisitorProviderVarName: visitorProviderVarName,
                    namedTypeSymbol: data_NamedTypeSymbol,
                    genTypeName: genTypeName
                );
            }
            else
            {
                await Receptionist_GenerateForConcreteTypeAsync(
                    codeGenSettings: codeGenSettings,
                    project: project,
                    ioStream: fileStream,
                    document: document,
                    injectTypeName: injectTypeName,
                    memberVisitorProviderParamName: visitorProviderVarName,
                    namedTypeSymbol: data_NamedTypeSymbol,
                    genTypeName: genTypeName
                );
            }

            await fileStream.WriteAsync("}\n\n");
            await fileStream.FlushAsync();
        }

        static async ValueTask Receptionist_GenerateForConcreteTypeAsync(
            CodeGenSettings codeGenSettings,
            Project project,
            Stream ioStream,
            Document document,
            string injectTypeName,
            string memberVisitorProviderParamName,
            INamedTypeSymbol namedTypeSymbol,
            string genTypeName)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";

            var members = namedTypeSymbol.GetVisitableDataMembers();
            var memberCount = unchecked((uint)members.Count());
            if (memberCount > 0u)
            {
                foreach (var member in namedTypeSymbol.GetVisitableDataMembers())
                {
                    await Receptionist_GenerateForMemberSymbolAsync(
                        codeGenSettings,
                        ioStream,
                        injectTypeName: injectTypeName,
                        memberVisitorProviderParamName: memberVisitorProviderParamName,
                        namedTypeSymbol,
                        member,
                        memberCount
                    );
                    memberCount -= 1u;
                }
            }
            else
                await Receptionist_GenerateCodeForEmptyTypeSymbolAsync(codeGenSettings, ioStream, namedTypeSymbol);

            var enclosingLine = $"{tab2}return true;\n{tab}" + "}\n";
            await ioStream.WriteAsync(enclosingLine);
        }

        static async ValueTask Receptionist_GenerateForAbstractTypeAsync(
            CodeGenSettings codeGenSettings,
            Project project,
            SymbolsDictionary symbolsDict,
            Stream ioStream,
            Document document,
            string injectTypeName,
            string memberVisitorProviderVarName,
            INamedTypeSymbol namedTypeSymbol,
            string genTypeName)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";

            var dataVarName = "data";
            var implementations = namedTypeSymbol.FindImplementations(symbolsDict);
            if (!implementations.Any())
            {
                await Receptionist_GenerateCodeForEmptyTypeSymbolAsync(codeGenSettings, ioStream, namedTypeSymbol);
                return;
            }
            else
            {
                var count = unchecked((uint)implementations.Count());
                foreach (var subTypeSymbol in implementations)
                {
                    await Receptionist_GenerateForSubTypeAsync(
                        codeGenSettings: codeGenSettings,
                        ioStream: ioStream,
                        injectTypeName: injectTypeName,
                        dataVarName: dataVarName,
                        memberVisitorProviderParamName: memberVisitorProviderVarName,
                        dataTypeSymbol: namedTypeSymbol,
                        subTypeSymbol: subTypeSymbol,
                        count
                    );
                    count -= 1u;
                }
            }
            if (true)
            {
                var dataTypeFullName = namedTypeSymbol.ToDisplayString();
                var braceBegin = "{";
                var initErrMsg = $"var m = $\"Unsupported type {{{dataVarName}.GetType()}} encountered when visiting {{typeof({dataTypeFullName})}}\";";
                var stmtThrow = "throw new NotSupportedException(m);";
                var braceEnd = "}";
                var enclosingElseStmt = $"{tab2}else\n{tab2}{braceBegin}\n{tab2}{tab}{initErrMsg}\n{tab2}{tab}{stmtThrow}\n{tab2}{braceEnd}\n{tab}{braceEnd}\n";
                await ioStream.WriteAsync(enclosingElseStmt);
            }
        }

        static async ValueTask Receptionist_GenerateForMemberSymbolAsync(
            CodeGenSettings codeGenSettings,
            Stream ioStream,
            string injectTypeName,
            string memberVisitorProviderParamName,
            INamedTypeSymbol namedTypeSymbol,
            ISymbol memberSymbol,
            uint memberSymbolsCount)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";

            var visitorVarName = $"visitor_{memberSymbol.Name}";
            var memberTypeSymbol = memberSymbol switch
            {
                IFieldSymbol fieldSymbol => fieldSymbol.Type,
                IPropertySymbol propertySymbol => propertySymbol.Type,
                _ => throw new Exception($"unsupported symbol type {memberSymbol.GetType()}"),
            };
            var dataTypeFullName = namedTypeSymbol.ToDisplayString();
            var memberTypeFullName = memberTypeSymbol.ToDisplayString();
            var visitorUsingStmt = $"using var {visitorVarName} = await {memberVisitorProviderParamName}.GetMemberVisitorAsync<{dataTypeFullName}, {memberTypeFullName}>(visitor, {memberSymbolsCount}u, \"{memberSymbol.Name}\", token);";

            var receptionistVarName = $"receptionist_{memberSymbol.Name}";
            var optRecptVarName = $"opt_{receptionistVarName}";
            var optRecpDeclStmt = $"var {optRecptVarName} = await {injectTypeName}.GetAsync<{memberTypeFullName}>(token);";
            var optRecpIfStmt = $"if (!{optRecptVarName}.IsSome(out var {receptionistVarName}))";
            var recpSuccIfStmt = $"if (!await {receptionistVarName}.ReceptAsync(data.{memberSymbol.Name}, {visitorVarName}, {memberVisitorProviderParamName}, token))";
            var retLine = "return false;";

            await ioStream.WriteAsync($"{tab2}{visitorUsingStmt}\n");
            await ioStream.WriteAsync($"{tab2}{optRecpDeclStmt}\n");
            await ioStream.WriteAsync($"{tab2}{optRecpIfStmt}\n");
            await ioStream.WriteAsync($"{tab2}{tab}{retLine}\n");
            await ioStream.WriteAsync($"{tab2}{recpSuccIfStmt}\n");
            await ioStream.WriteAsync($"{tab2}{tab}{retLine}\n\n");
        }

        static async ValueTask Receptionist_GenerateForSubTypeAsync(
            CodeGenSettings codeGenSettings,
            Stream ioStream,
            string injectTypeName,
            string dataVarName,
            string memberVisitorProviderParamName,
            INamedTypeSymbol dataTypeSymbol,
            INamedTypeSymbol subTypeSymbol,
            uint variantsCount)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";
            var dataTypeFullName = dataTypeSymbol.ToDisplayString();
            var subTypeFullName = subTypeSymbol.ToDisplayString();
            var subTypeShortName = subTypeSymbol.Name;
            var dataVarNewName = $"x_{subTypeShortName}";
            var visitorVarName = $"visitor_{subTypeShortName}";

            var visitorUsingStmt = $"using var {visitorVarName} = await {memberVisitorProviderParamName}.GetVariantVisitorAsync<{dataTypeFullName}, {subTypeFullName}>(visitor, {variantsCount}u, token);";
            var receptionistVarName = $"receptionist_{subTypeShortName}";
            var optRecptVarName = $"opt_{receptionistVarName}";
            var optRecpDeclStmt = $"var {optRecptVarName} = await {injectTypeName}.GetAsync<{subTypeFullName}>(token);";
            var optRecpIfStmt = $"if (!{optRecptVarName}.IsSome(out var {receptionistVarName}))";
            var retFalseStmt = "return false;";
            var retRecpStmt = $"return await {receptionistVarName}.ReceptAsync({dataVarNewName}, {visitorVarName}, {memberVisitorProviderParamName}, token);";

            try
            {
                var hostCastIfStmt = $"if ({dataVarName} is {subTypeFullName} {dataVarNewName})";
                await ioStream.WriteAsync($"{tab2}{hostCastIfStmt}\n");
                await ioStream.WriteAsync($"{tab2}{{\n");
                await ioStream.WriteAsync($"{tab2}{tab}{visitorUsingStmt}\n");
                await ioStream.WriteAsync($"{tab2}{tab}{optRecpDeclStmt}\n");
                await ioStream.WriteAsync($"{tab2}{tab}{optRecpIfStmt}\n");
                await ioStream.WriteAsync($"{tab2}{tab2}{retFalseStmt}\n");
                await ioStream.WriteAsync($"{tab2}{tab}{retRecpStmt}\n");
                await ioStream.WriteAsync($"{tab2}}}\n");
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }
        }

        static async ValueTask Receptionist_GenerateCodeForEmptyTypeSymbolAsync(
            CodeGenSettings codeGenSettings,
            Stream ioStream,
            INamedTypeSymbol namedTypeSymbol)
        {
            var tab = codeGenSettings.TabStr;
            var commentLine = $"// type {namedTypeSymbol.Name} has no any appropriate members nor subtypes to visit";
            var visitLine = "await System.Threading.Tasks.Task.CompletedTask;";
            var stmt = $"{tab}{tab}{commentLine}\n{tab}{tab}{visitLine}\n";
            await ioStream.WriteAsync(stmt);
        }

        static async ValueTask GenerateInjectCodeAsync(
            CodeGenSettings codeGenSettings,
            string genFileNameSpace,
            string codeGenFolderPath,
            string receptionistInjectTypeName,
            string builderInjectTypeName)
        {
            var ns = genFileNameSpace.Replace('.', '_');
            var genFileName = $"{ns}_Inject.gen.cs";
            var absFilePath = $"{codeGenFolderPath}{Path.DirectorySeparatorChar}{genFileName}";

            File.WriteAllText(absFilePath, string.Empty);
            using var file = new FileStream(absFilePath, FileMode.Open, FileAccess.Write, FileShare.None);
            var topComment =
@$"/// <auto-generated>
///     Generated by VisitAsyncGenCliTool at {DateTimeOffset.Now} (DateTimeOffset).
///     Changes will be lost once regenerated.
/// </auto-generated>

";
            var code =
@$"namespace {genFileNameSpace}
{{
    using System.Threading;

    using Cysharp.Threading.Tasks;

    using NsAnyLR;
    using NsAbsVisitAsync;

    public static class {receptionistInjectTypeName}
    {{
        private static readonly NsAbsVisitAsync.ReceptionistManager manager_ = new NsAbsVisitAsync.ReceptionistManager();

        public static UniTask<Option<IReceptionist<T>>> GetAsync<T>(CancellationToken token = default)
            => manager_.GetAsync<T>(token);

        internal static UniTask<bool> RegisterAsync<T, R>(CancellationToken token = default)
            where R : class, IReceptionist<T>, new()
        {{
            return manager_.RegisterAsync<T, R>(false, token);
        }}
    }}

    public static class {builderInjectTypeName}
    {{
        private static readonly NsAbsVisitAsync.BuilderManager manager_ = new NsAbsVisitAsync.BuilderManager();

        public static UniTask<Option<IBuilder<T>>> GetAsync<T>(CancellationToken token = default)
            => manager_.GetAsync<T>(token);

        internal static UniTask<bool> RegisterAsync<T, B>(CancellationToken token = default)
            where B : class, IBuilder<T>, new()
        {{
            return manager_.RegisterAsync<T, B>(false, token);
        }}
    }}
}}
";
            await file.WriteAsync(topComment);
            await file.WriteAsync(code);
            await file.FlushAsync();
        }

        static async ValueTask Builder_GenerateForDeclTypeAsync(
            CodeGenSettings codeGenSettings,
            Project project,
            SymbolsDictionary symbolsDict,
            Stream fileStream,
            Document document,
            INamedTypeSymbol data_NamedTypeSymbol,
            string builderInjectTypeName)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";

            var dataTypeFullName = data_NamedTypeSymbol.ToDisplayString();
            var builderClassSuffix = data_NamedTypeSymbol.MangledNameAsIdentifierComponent();
            var genTypeName = $"Builder_{builderClassSuffix}";

            var clsDeclLine = $"public sealed class {genTypeName} : IBuilder<{dataTypeFullName}>\n" + "{\n";
            await fileStream.WriteAsync(clsDeclLine);

            // Code gen for the static constructor of the receptionist class
            // static Builder_GenTypeName()
            //     => BuilderInject.RegisterAsync<dataTypeFullName, builder_GenTypeName>();
            if (true)
            {
                var ctorStaticDecl0 = $"static {genTypeName}()";
                var ctorStaticImpl0 = $"=> {builderInjectTypeName}.RegisterAsync<{dataTypeFullName}, {genTypeName}>().Forget();";

                await fileStream.WriteAsync($"{tab}{ctorStaticDecl0}\n");
                await fileStream.WriteAsync($"{tab2}{ctorStaticImpl0}\n");
            }
            if (data_NamedTypeSymbol.ShouldGenForVariantTypes())
            {
                await Builder_GenerateForVariantsAsync(
                    codeGenSettings: codeGenSettings,
                    project: project,
                    symbolsDict: symbolsDict,
                    ioStream: fileStream,
                    document: document,
                    builderInjectParamName: builderInjectTypeName,
                    memberParserProviderParamName: "provider",
                    dataTypeSymbol: data_NamedTypeSymbol,
                    genTypeName: genTypeName
                );
            }
            else
            {
                await Builder_GenerateForConcreteTypeAsync(
                    codeGenSettings: codeGenSettings,
                    project: project,
                    ioStream: fileStream,
                    document: document,
                    injectTypeName: builderInjectTypeName,
                    memberParserProviderParamName: "provider",
                    dataTypeSymbol: data_NamedTypeSymbol,
                    genTypeName: genTypeName
                );
            }
            await fileStream.WriteAsync("}\n\n");
            await fileStream.FlushAsync();
        }

        static async ValueTask Builder_GenerateForConcreteTypeAsync(
            CodeGenSettings codeGenSettings,
            Project project,
            Stream ioStream,
            Document document,
            string injectTypeName,
            string memberParserProviderParamName,
            INamedTypeSymbol dataTypeSymbol,
            string genTypeName)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";

            var datTypStr = dataTypeSymbol.ToDisplayString();
            var resTypStr = $"Result<{datTypStr}, IParserError>";
            var parserVarName = "parser";
            var providerVarName = "provider";

            var methDeclL0 = $"public async {codeGenSettings.GetTaskTypeStr_Builder(resTypStr)} TryBuildAsync(";
            var methDeclL1 = $"IParser<{datTypStr}> {parserVarName},";
            var methDeclL2 = $"IParserProvider {providerVarName},";
            var methDeclL3 = "CancellationToken token = default)";

            await ioStream.WriteAsync($"\n{tab}{methDeclL0}\n");
            await ioStream.WriteAsync($"{tab2}{methDeclL1}\n");
            await ioStream.WriteAsync($"{tab2}{methDeclL2}\n");
            await ioStream.WriteAsync($"{tab2}{methDeclL3}\n");
            await ioStream.WriteAsync($"{tab}{{\n");

            var members = dataTypeSymbol.GetVisitableDataMembers();
            var memberCount = unchecked((uint)members.Count());
            if (memberCount > 0u)
            {
                foreach (var member in dataTypeSymbol.GetVisitableDataMembers())
                {
                    await Builder_GenerateForMemberAsync(
                        codeGenSettings: codeGenSettings,
                        ioStream: ioStream,
                        parserParamName: parserVarName,
                        memberParserProviderParamName: memberParserProviderParamName,
                        dataTypeSymbol: dataTypeSymbol,
                        memberSymbol: member,
                        memberCount: memberCount
                    );
                    memberCount -= 1u;
                }
            }
            await ioStream.WriteAsync($"{tab2}return await {parserVarName}.TryParseAsync(token);\n");
            await ioStream.WriteAsync($"{tab}}}\n");
        }

        static async ValueTask Builder_GenerateForMemberAsync(
            CodeGenSettings codeGenSettings,
            Stream ioStream,
            string parserParamName,
            string memberParserProviderParamName,
            INamedTypeSymbol dataTypeSymbol,
            ISymbol memberSymbol,
            uint memberCount)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";

            var parserVarName = $"parser_{memberSymbol.Name}";
            var memberTypeSymbol = memberSymbol switch
            {
                IFieldSymbol fieldSymbol => fieldSymbol.Type,
                IPropertySymbol propertySymbol => propertySymbol.Type,
                _ => throw new Exception($"unsupported symbol type {memberSymbol.GetType()}"),
            };
            var dataTypeFullName = dataTypeSymbol.ToDisplayString();
            var memberName = memberSymbol.Name;
            var memberTypeFullName = memberTypeSymbol.ToDisplayString();
            var parserUsingStmt = $"using var {parserVarName} = await {memberParserProviderParamName}.GetMemberParserAsync<{dataTypeFullName}, {memberTypeFullName}>({parserParamName}, {memberCount}u, \"{memberName}\", token);";

            var parseResulVarName = $"parse_{memberName}_result";
            var parseErrVarName = $"parse_{memberName}_err";
            var parseResultDeclStmt = $"var {parseResulVarName} = await {parserVarName}.TryParseAsync(token);";
            var parseResultIfErrStmt = $"if ({parseResulVarName}.IsErr(out var {parseErrVarName}))";
            var retLine = $"return Result.Err({parseErrVarName});";

            await ioStream.WriteAsync($"{tab2}{parserUsingStmt}\n");
            await ioStream.WriteAsync($"{tab2}{parseResultDeclStmt}\n");
            await ioStream.WriteAsync($"{tab2}{parseResultIfErrStmt}\n");
            await ioStream.WriteAsync($"{tab2}{tab}{retLine}\n\n");
        }

        static async ValueTask Builder_GenerateForVariantsAsync(
            CodeGenSettings codeGenSettings,
            Project project,
            SymbolsDictionary symbolsDict,
            Stream ioStream,
            Document document,
            string builderInjectParamName,
            string memberParserProviderParamName,
            INamedTypeSymbol dataTypeSymbol,
            string genTypeName)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";

            var datTypFullName = dataTypeSymbol.ToDisplayString();
            var parserParamName = "parser";

            var implementations = dataTypeSymbol.FindImplementations(symbolsDict);
            if (!implementations.Any())
            {
                return;
            }
            var index = 0u;
            foreach (var subTypeSymbol in implementations)
            {
                await Builder_GenerateSubTypeBranchAsync(
                    codeGenSettings: codeGenSettings,
                    ioStream: ioStream,
                    index: index,
                    parserParamName: parserParamName,
                    memberParserProviderParamName: memberParserProviderParamName,
                    dataTypeSymbol: dataTypeSymbol,
                    variantTypeSymbol: subTypeSymbol
                );
                index += 1u;
            }

            var retTaskTypStr = codeGenSettings.GetTaskTypeStr_Builder($"Result<{datTypFullName}, IParserError>");
            var variantTypesSpanVarName = "VARIANT_TYPES";
            if (true)
            {
                var declVariantTypesSpan = $"static readonly ReadOnlyMemory<Type> {variantTypesSpanVarName} = new(new Type[] {{";
                var typesLine = new List<string>();
                foreach (var subTypeSymbol in implementations)
                {
                    var variantTypeFullName = subTypeSymbol.ToDisplayString();
                    var t = $"typeof({variantTypeFullName})";
                    typesLine.Add(t);
                }
                var declSpanElems = typesLine.Aggregate((p, n) => $"{p},\n{tab2}{n}");

                await ioStream.WriteAsync($"{tab}{declVariantTypesSpan}\n");
                await ioStream.WriteAsync($"{tab2}{declSpanElems}\n");
                await ioStream.WriteAsync($"{tab}}});\n\n");
            }

            var variantBranchSpanVarName = "BRANCHES";
            var funcTypeName = $"Func<IParser<{datTypFullName}>, IParserProvider, {nameof(CancellationToken)}, {retTaskTypStr}>";
            if (true)
            {
                var s = string.Join(", ", Enumerable.Range(0, implementations.Count()).Select(i => $"B{i}"));
                var declSpanL0 = $"static readonly ReadOnlyMemory<{funcTypeName}> {variantBranchSpanVarName} = new(";
                var declSpanL1 = $"new {funcTypeName}[] {{ {s} }}";
                var declSpanL2 = ");";

                await ioStream.WriteAsync($"{tab}{declSpanL0}\n");
                await ioStream.WriteAsync($"{tab2}{declSpanL1}\n");
                await ioStream.WriteAsync($"{tab}{declSpanL2}\n\n");
            }
            if (true)
            {
                var methDeclL0 = $"public async {retTaskTypStr} TryBuildAsync(";
                var methDeclL1 = $"IParser<{datTypFullName}> {parserParamName},";
                var methDeclL2 = $"IParserProvider {memberParserProviderParamName},";
                var methDeclL3 = "CancellationToken token = default)";

                await ioStream.WriteAsync($"{tab}{methDeclL0}\n");
                await ioStream.WriteAsync($"{tab2}{methDeclL1}\n");
                await ioStream.WriteAsync($"{tab2}{methDeclL2}\n");
                await ioStream.WriteAsync($"{tab2}{methDeclL3}\n");
                await ioStream.WriteAsync($"{tab}{{\n");

                var body = @$"
        var result = await {parserParamName}.GetVariantTypeAsync({variantTypesSpanVarName}, token);
        if (!result.TryOk(out var index, out var getVariantTypeError))
            return Result.Err(getVariantTypeError);

        var b = {variantBranchSpanVarName}.Span[unchecked((int)index)];
        return await b({parserParamName}, {memberParserProviderParamName}, token);
";
                await ioStream.WriteAsync($"{body}{tab}}}\n");
            }
        }

        static async ValueTask Builder_GenerateSubTypeBranchAsync(
            CodeGenSettings codeGenSettings,
            Stream ioStream,
            uint index,
            string parserParamName,
            string memberParserProviderParamName,
            INamedTypeSymbol dataTypeSymbol,
            INamedTypeSymbol variantTypeSymbol)
        {
            var tab = codeGenSettings.TabStr;
            var tab2 = $"{tab}{tab}";

            var datTypFullName = dataTypeSymbol.ToDisplayString();
            var resTypStr = $"Result<{datTypFullName}, IParserError>";
            var parserVarName = "parser";
            var providerVarName = "provider";

            var methDeclL0 = $"static async {codeGenSettings.GetTaskTypeStr_Builder(resTypStr)} B{index}(";
            var methDeclL1 = $"IParser<{datTypFullName}> {parserVarName},";
            var methDeclL2 = $"IParserProvider {providerVarName},";
            var methDeclL3 = "CancellationToken token = default)";

            await ioStream.WriteAsync($"\n{tab}{methDeclL0}\n");
            await ioStream.WriteAsync($"{tab2}{methDeclL1}\n");
            await ioStream.WriteAsync($"{tab2}{methDeclL2}\n");
            await ioStream.WriteAsync($"{tab2}{methDeclL3}\n");
            await ioStream.WriteAsync($"{tab}{{\n");

            var variantTypeFullName = variantTypeSymbol.ToDisplayString();
            var variantParserVarName = $"parser_{variantTypeSymbol.Name}";
            var parseResultVarName = $"parse_{variantTypeSymbol.Name}_result";

            var variantParserUsingStmt = $"using var {variantParserVarName} = await {memberParserProviderParamName}.GetVariantParserAsync<{datTypFullName}, {variantTypeFullName}>({parserParamName}, token);";
            var parseResultDeclStmt = $"var {parseResultVarName} = await {variantParserVarName}.TryParseAsync(token);";
            var retStmt = $"return {parseResultVarName}.MapOk(x => x as {datTypFullName});";

            await ioStream.WriteAsync($"{tab2}{variantParserUsingStmt}\n");
            await ioStream.WriteAsync($"{tab2}{parseResultDeclStmt}\n");
            await ioStream.WriteAsync($"{tab2}{retStmt}\n");
            await ioStream.WriteAsync($"{tab}}}\n");
        }
    }

    internal static class DocumentQueryExt
    {
        /// <summary>
        /// <para>判断一个语法节点是否符为以下其中一种：</para>
        /// <para>1. 声明 struct </para>
        /// <para>2. 声明 class </para>
        /// <para>3. 声明 record </para>
        /// <para>4. 声明 interface </para>
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static bool IsQualifiedSyntaxNode(this SyntaxNode node)
        {
            if (node is ClassDeclarationSyntax clsDecl)
                return !clsDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
            return
                node is StructDeclarationSyntax ||
                node is RecordDeclarationSyntax ||
                node is InterfaceDeclarationSyntax;
        }

        /// <summary>
        /// 从一个具名类型中找出 Visitor 模式中需要访问的字段或者属性，即所有 public 的非 static 的字段或者属性
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static IEnumerable<ISymbol> GetVisitableDataMembers(this INamedTypeSymbol symbol)
        {
            return
                from m in symbol.GetMembers()
                where m.DeclaredAccessibility == Accessibility.Public &&
                    !m.IsStatic &&
                    (m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property)
                select m;
        }
    }

    internal static class StreamWriteStrUtf8Ext
    {
        public static async ValueTask<int> WriteAsync(this Stream stream, string content, CancellationToken token = default)
        {
            try
            {
                var strBytes = Encoding.UTF8.GetBytes(content);
                await stream.WriteAsync(strBytes, token);
                return strBytes.Length;
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"{e}");
                throw;
            }
        }
    }

    internal static class SymbolInherictOrImplExt
    {
        public static ImmutableList<INamedTypeSymbol> FindImplementations(
            this INamedTypeSymbol targetType,
            SymbolsDictionary symbolsDict,
            CancellationToken cancellationToken = default)
        {
            var query =
                from symbolsList in symbolsDict.Values
                from s in symbolsList
                where s is INamedTypeSymbol symbol && symbol.InheritsOrImplements(targetType)
                select s as INamedTypeSymbol;
            return query.ToImmutableList();
        }

        public static bool InheritsOrImplements(this INamedTypeSymbol typeSymbol, INamedTypeSymbol targetSymbol)
        {
            // Check interface implementation
            if (targetSymbol.TypeKind == TypeKind.Interface)
            {
                return typeSymbol.AllInterfaces.Any(i =>
                    SymbolEqualityComparer.Default.Equals(i, targetSymbol));
            }
            // Check class inheritance
            if (targetSymbol.TypeKind == TypeKind.Class && targetSymbol.IsAbstract)
            {
                var baseType = typeSymbol.BaseType;
                while (baseType != null)
                {
                    if (SymbolEqualityComparer.Default.Equals(baseType, targetSymbol))
                        return true;
                    baseType = baseType.BaseType;
                }
            }

            return false;
        }

        public static string MangledNameAsIdentifierComponent(this INamedTypeSymbol typeSymbol)
        {
            return typeSymbol
                .ToDisplayString()
                .Replace('.', '_');
        }

        public static bool ShouldGenForVariantTypes(this INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.TypeKind == TypeKind.Interface
                || (typeSymbol.TypeKind == TypeKind.Class && !typeSymbol.IsSealed);
        }
    }
}