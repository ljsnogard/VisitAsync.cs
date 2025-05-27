namespace VisitAsyncUtils.CliTools
{
    using System;
    using System.Threading.Tasks;

    internal class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                await Console.Out.WriteLineAsync("请提供项目文件 (*.csproj) 路径");
                return;
            }

            var projPath = args[0];
            var scanSettings = new ScanSettings();
            var codeGenSettings = new CodeGenSettings();

            var scanGen = await ScanGen.OpenAsync(projPath, scanSettings, codeGenSettings);

            await scanGen.GenAsync();
        }
    }
}
