﻿using Serilog;
using Spectre.Console.Cli;
using System.ComponentModel;
using Spectre.Console;
using Caliburn.Micro;
using DaxStudio.UI.Utils;
using System.Reflection;
using DaxStudio.CommandLine.Infrastructure;
using Dax.Metadata;
using DaxStudio.CommandLine.Attributes;
using DaxStudio.CommandLine.Helpers;


namespace DaxStudio.CommandLine.Commands
{
    internal class VpaxCommand : Command<VpaxCommand.Settings>
    {
        public IEventAggregator EventAggregator { get; }

        // todo - cannot pass in connectionstring as vpax library does not support it
        internal class Settings : CommandSettingsFileBase
        {

            [CommandOption("-t|--excludeTom")]
            [Description("Setting this flag will exclude a .bim file inside the vpax file (which just contains additional metadata)")]
            public bool ExcludeTom { get; set; }
            [CommandOption("-r|--donotreadstatsfromdata")]
            [Description("Setting this flag will prevent the standard distinctcount queries that read the statistics from the data model")]
            public bool DoNotReadStatsFromData { get; set; }
            
            [CommandOption("-q|--readstatsfromdirectquery")]
            [Description("Setting this flag will force the execution of distinctcount queries that read the statistics from the data model (which is normally suppressed for DirectQuery models)")]
            public bool ReadStatsFromDirectQuery { get; set; }

            private string _dictionaryPath = string.Empty;
            [CommandOption("-i|--dictionaryFile")]
            [Description("Specify the file name for the output dictionary when creating an obfuscated vpax file. If this parameter is not used a .dict file will be created with the same name as the .ovpax file")]
            public string DictionaryPath { get { 
                    if (this.OutputFile.EndsWith(".ovpax", System.StringComparison.OrdinalIgnoreCase) && _dictionaryPath == string.Empty)
                    {
                        // calculate dictionary path
                        return ModelAnalyzer.GetDictPathForOvpax(OutputFile);
                    }
                    return _dictionaryPath;
                } 
                set 
                { 
                    _dictionaryPath = value; 
                } 
            }

            
            [CommandOption("-n|--InputDictionaryFile")]
            [Description("Specify the file name for the input dictionary when creating an obfuscated vpax file and you want to use an existing .dict file.")]
            public string InputDictionaryPath { get; set; } = string.Empty;

            [CommandOption("-l|--DirectLakeMode")]
            [DirectLakeModeDescription]
            public DirectLakeExtractionMode DirectLakeMode { get; set; } = DirectLakeExtractionMode.ResidentOnly;

            public override ValidationResult Validate()
            {
                if (DictionaryPath == InputDictionaryPath && !string.IsNullOrEmpty(InputDictionaryPath)) return ValidationResult.Error("You cannot specify the same name for both the input and output dictionaries");
                return base.Validate();
            }

            [CommandOption("-b|--StatsColumnBatchSize")]
            [Description("The number of columns to include in each batch of the column statistics queries")]
            public int StatsColumnBatchSize { get; set; }

        }
        
        public VpaxCommand(IEventAggregator eventAggregator)
        {
            EventAggregator = eventAggregator;
        }

        public ValidationResult Validate(CommandContext context, CommandSettings settings)
        {
            return base.Validate(context,(Settings)settings);
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            VersionInfo.Output();
            AnsiConsole.MarkupLine("Starting VPAX command");
            Log.Information("Starting VPAX command");
            AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(Spinner.Known.Star)
                .SpinnerStyle(Style.Parse("green bold"))
                .Start("Generating VPAX file...", ctx =>
                {
                    var appVersion = Assembly.GetEntryAssembly().GetName().Version.ToString();
                    var statsColumnBatchSize = settings.StatsColumnBatchSize==0 ? Dax.Model.Extractor.StatExtractor.DefaultColumnBatchSize :settings.StatsColumnBatchSize;
                    var connStr = settings.FullConnectionString;

                    // if requires Entra Auth add the AccessToken to the connection string
                    if (AccessTokenHelper.IsAccessTokenNeeded(connStr)) {
                        var token = AccessTokenHelper.GetAccessToken(connStr);
                        connStr = $"{connStr};Password={token.Token}";
                    }

                    ModelAnalyzer.ExportVPAX(connStr, settings.OutputFile,settings.DictionaryPath, settings.InputDictionaryPath, 
                        !settings.ExcludeTom, "DAX Studio Command Line", appVersion, !settings.DoNotReadStatsFromData, "Model", 
                        settings.ReadStatsFromDirectQuery, settings.DirectLakeMode, statsColumnBatchSize);
                    
                });
            AnsiConsole.MarkupLine("[green]Done![/]");
            Log.Information("Finished VPAX Command");
            return 0;
        }
    }
}
