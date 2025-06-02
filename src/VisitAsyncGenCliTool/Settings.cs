namespace NsAbsVisitAsync.NsCliTools
{
    using System;
    using Microsoft.CodeAnalysis;

    public sealed class ScanSettings
    {
        private readonly bool isAttrRequired_;

        public ScanSettings(bool isAttrRequired = false)
            => this.isAttrRequired_ = isAttrRequired;

        public bool IsAttributeRequired
            => this.isAttrRequired_;
    }

    public sealed class CodeGenSettings
    {
        private readonly TasksTypeOptions tasksTypeOpts_;

        private readonly TabOptions tabOpts_;

        private readonly string usingLineStr_;

        private readonly string tasksTypeStr_;

        private readonly string tabStr_;

        private readonly string codeGenFolderName_;

        private static readonly string DEFAULT_CODE_GEN_FOLDER_NAME = "gen_visit";

        public CodeGenSettings(
            TasksTypeOptions tasksTypeOptions = TasksTypeOptions.UniTasks,
            TabOptions tabOptions = TabOptions.FourSpaces,
            string? codeGenFolderName = null)
        {
            this.tasksTypeOpts_ = tasksTypeOptions;
            this.tabOpts_ = tabOptions;

            this.usingLineStr_ = tasksTypeOptions.GetUsingLineStr();
            this.tasksTypeStr_ = tasksTypeOptions.GetTaskTypeStr();
            this.tabStr_ = tabOptions.GetTabStr();

            this.codeGenFolderName_ =
                string.IsNullOrEmpty(codeGenFolderName) || string.IsNullOrWhiteSpace(codeGenFolderName) ?
                    DEFAULT_CODE_GEN_FOLDER_NAME :
                    codeGenFolderName;
        }

        public TasksTypeOptions TasksTypeOption
            => this.tasksTypeOpts_;

        public TabOptions TabOptions
            => this.tabOpts_;

        public string UsingLineStr
            => this.usingLineStr_;

        public string TasksTypeStr_Receptionist
            => this.tasksTypeStr_;

        public string GetTaskTypeStr_Builder(string retTypeName)
            => this.tasksTypeOpts_.GetTaskTypeStr(retTypeName);

        public string TabStr
            => this.tabStr_;

        public string CodeGenFolderName
            => this.codeGenFolderName_;
    }

    public enum TasksTypeOptions
    {
        ValueTasks,
        UniTasks,
    }

    public static class TasksTypeOptionsExtensions
    {
        private static readonly string UsingSystem = "System.Threading.Tasks";
        private static readonly string UsingUniTask = "Cysharp.Threading.Tasks";

        private static readonly string ValueTaskStr = "ValueTask";
        private static readonly string UniTaskStr = "UniTask";

        public static string GetUsingLineStr(this TasksTypeOptions tt)
        {
            return tt switch {
                TasksTypeOptions.ValueTasks => UsingSystem,
                TasksTypeOptions.UniTasks => UsingUniTask,
                _ => throw new NotSupportedException($"Unknown option value: ({tt})"),
            };
        }

        public static string GetTaskTypeStr(this TasksTypeOptions tt, string? retValTypeStr = null)
        {
            if (retValTypeStr is not string retTypeStr)
                retTypeStr = "bool";
            return tt switch {
                TasksTypeOptions.ValueTasks => $"{ValueTaskStr}<{retTypeStr}>",
                TasksTypeOptions.UniTasks => $"{UniTaskStr}<{retTypeStr}>",
                _ => throw new NotSupportedException($"Unknown option value: ({tt})"),
            };
        }
    }

    public enum TabOptions
    {
        TwoSpaces,
        FourSpaces,
        TabChar,
    }

    public static class TabOptionsExtensions
    {
        public static string GetTabStr(this TabOptions tabOptions)
        {
            return tabOptions switch
            {
                TabOptions.TwoSpaces => "  ",
                TabOptions.FourSpaces => "    ",
                TabOptions.TabChar => "\t",
                _ => throw new NotSupportedException($"Unknown tab options value: {tabOptions}"),
            };
        }
    }
}