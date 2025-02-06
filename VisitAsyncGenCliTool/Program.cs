namespace VisitAsyncUtils.CliTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.MSBuild;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    using VisitAsyncUtils;

    internal static class DocumentQueryExtensions
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
            => node is StructDeclarationSyntax || node is ClassDeclarationSyntax || node is RecordDeclarationSyntax || node is InterfaceDeclarationSyntax;

        /// <summary>
        /// 从一个具名类型中找出 Visitor 模式中需要访问的字段或者属性，即所有 public 的非 static 的字段或者属性
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static IEnumerable<ISymbol> GetVisitableMembers(this INamedTypeSymbol symbol)
        {
            return
                from m in symbol.GetMembers()
                where m.DeclaredAccessibility == Accessibility.Public &&
                    !m.IsStatic &&
                    (m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property)
                select m;
        }
    }

    internal class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("请提供项目文件 (*.csproj) 路径");
                return;
            }
            var projPath = args[0];
            var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projPath);

            foreach (var document in project.Documents)
            {
                var model = await document.GetSemanticModelAsync();
                var root = await document.GetSyntaxRootAsync();
                if (root is null)
                    throw new NullReferenceException(nameof(root));

                var nodes =
                    from node in root.DescendantNodes()
                    where node.IsQualifiedSyntaxNode()
                    select node;
                var symbols = new List<ISymbol>();
                foreach (var node in nodes)
                {
                    var symbol = model.GetDeclaredSymbol(node);
                    var attributes = symbol.GetAttributes();
                    if (attributes.Any(a => a.AttributeClass.Name == nameof(AcceptVisitAsyncAttribute)))
                        symbols.Add(symbol);
                }
                foreach (var symbol in symbols)
                {
                    if (symbol is not INamedTypeSymbol namedTypeSymbol)
                        throw new Exception($"Unexpected symbol type: {symbol.ToDisplayString()}");
                    else
                        Console.WriteLine($"发现目标类: {symbol.ToDisplayString()}");
                    foreach (var member in namedTypeSymbol.GetVisitableMembers())
                        Console.WriteLine($"\t目标成员: {member.ToDisplayParts().Last()}");
                }
            }
        }
    }
}
