namespace VisitAsyncUtils.CliTools
{
    using System.Linq;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.MSBuild;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    using VisitAsyncUtils;

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
                    where node is StructDeclarationSyntax || node is ClassDeclarationSyntax || node is RecordDeclarationSyntax
                    select node;
                foreach (var node in nodes)
                {
                    var symbol = model.GetDeclaredSymbol(node);
                    var attributes = symbol.GetAttributes();

                    if (attributes.Any(a => a.AttributeClass.Name == nameof(AcceptVisitAsyncAttribute)))
                    {
                        Console.WriteLine($"发现目标类: {symbol.ToDisplayString()}");
                    }
                }
            }
        }
    }
}
