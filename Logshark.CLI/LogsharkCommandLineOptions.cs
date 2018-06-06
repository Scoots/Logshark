﻿using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Logshark.CLI
{
    /// <summary>
    /// Command-line Logshark options.
    /// </summary>
    public class LogsharkCommandLineOptions
    {
        private string sanitizedTarget;

        [ParserState]
        public IParserState LastParserState { get; set; }

        [ValueOption(0)]
        public string Target
        {
            get
            {
                return sanitizedTarget;
            }
            set
            {
                sanitizedTarget = value.TrimEnd('"', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).TrimStart('"');
            }
        }

        [OptionList("args", Separator = ' ', DefaultValue = new string[] { }, HelpText = @"Set of custom arguments that will be passed to plugins. Ex: --args ""PluginName.MyCustomArg:MyValue""")]
        public IList<string> CustomArgs { get; set; }

        [Option("dbname", DefaultValue = "", HelpText = "Sets a custom name for the database instance where plugin data will be stored.  Default behavior is to generate a new database for each run.  If this argument is specified and the given database name already exists, the data generated by this run will be appended to it.")]
        public string DatabaseName { get; set; }

        [Option('d', "dropparsedlogset", DefaultValue = false, HelpText = "Drops the parsed logset from MongoDB at the end of the run.  Logsets parsed by previous runs will be ignored.")]
        public bool DropParsedLogset { get; set; }

        [Option('f', "forceparse", DefaultValue = false, HelpText = "Forces a fresh parse of the logset, overriding any existing data in MongoDB.")]
        public bool ForceParse { get; set; }

        [Option("id", DefaultValue = "", HelpText = "Sets a custom ID for the run that will be stored alongside the run metadata and tagged on any published workbooks.")]
        public string Id { get; set; }

        [Option('l', "listplugins", DefaultValue = false, HelpText = "Lists information about all available Logshark plugins.")]
        public bool ListPlugins { get; set; }

        [Option("localmongoport", DefaultValue = 27017, HelpText = "Port which the temporary local MongoDB process will run on.")]
        public int LocalMongoPort { get; set; }

        [OptionList("metadata", Separator = ' ', DefaultValue = new string[] { }, HelpText = @"Set of custom metadata key/value pairs that will stored in the resulting Mongo database. Ex: --metadata ""SalesforceId:SomeValue TFSDefect:SomeOtherValue""")]
        public IList<string> Metadata { get; set; }

        [OptionList("plugins", Separator = ' ', DefaultValue = new[] { "default" }, HelpText = @"List of plugins that will be run against the processed logset. Also accepts ""all"" to run all plugins, ""default"" to run the default plugin set, or ""none"" to bypass plugin execution.")]
        public IList<string> Plugins { get; set; }

        [Option("parseall", DefaultValue = false, HelpText = "Parse full logset into MongoDB.  If false, only the logs required for active plugins will be parsed.")]
        public bool ParseAll { get; set; }

        [Option('p', "publishworkbooks", DefaultValue = false, HelpText = "Publish resulting workbooks to Tableau Server.")]
        public bool PublishWorkbooks { get; set; }

        [Option("projectdescription", DefaultValue = "", HelpText = "Sets the Tableau Server project description where any workbooks will be published.")]
        public string ProjectDescription { get; set; }

        [Option("projectname", DefaultValue = "", HelpText = "Sets the Tableau Server project name where any workbooks will be published.")]
        public string ProjectName { get; set; }

        [Option('s', "startlocalmongo", DefaultValue = false, HelpText = "Start up a temporary local MongoDB process for this run. Recommended for small log sets only.")]
        public bool StartLocalMongo { get; set; }

        [Option("sitename", DefaultValue = "", HelpText = "Sets the Tableau Server site where any workbooks will be published. Overrides what is specified in Logshark.config.")]
        public string SiteName { get; set; }

        [OptionList("tags", Separator = ' ', DefaultValue = new string[] { }, HelpText = @"List of tags that will written to the resulting workbook(s). Ex: --tags ""MyTag MyOtherTag""")]
        public IList<string> WorkbookTags { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            // Header.
            AssemblyName assembly = Assembly.GetExecutingAssembly().GetName();
            string company = ((AssemblyCompanyAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyCompanyAttribute), false)).Company;
            var help = new HelpText
            {
                Heading = new HeadingInfo(assembly.Name, assembly.Version.ToString()),
                Copyright = new CopyrightInfo(company, DateTime.UtcNow.Year),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };

            // Usage body.
            help.AddPreOptionsLine(Environment.NewLine + "Usage:");
            help.AddPreOptionsLine(@"  logshark [TARGET] [OPTIONS].. | Processes target log directory, zip or hash.");
            help.AddPreOptionsLine(@"                                | Both absolute & relative paths are supported.");
            help.AddPreOptionsLine(Environment.NewLine + "Usage Examples:");
            help.AddPreOptionsLine(@"  logshark C:\Logs\logs.zip | Runs logshark on logs.zip and outputs locally.");
            help.AddPreOptionsLine(@"  logshark C:\Logs\Logset   | Runs logshark on existing unzipped log directory.");
            help.AddPreOptionsLine(@"  logshark logs.zip -p      | Runs logshark and publishes to Tableau Server.");

            // Options.
            help.AddPreOptionsLine(Environment.NewLine + "Options:");
            help.AddOptions(this);

            // Display helpful information about any parsing errors.
            if (LastParserState != null && LastParserState.Errors.Any())
            {
                var errors = help.RenderParsingErrorsText(this, indent: 2);

                if (!string.IsNullOrEmpty(errors))
                {
                    help.AddPostOptionsLine("ERROR(S):");
                    help.AddPostOptionsLine(errors);
                }
            }

            return help;
        }
    }
}