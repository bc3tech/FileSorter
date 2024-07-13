namespace FileSorter;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ExifLib;

using PowerArgs;

class Program
{
    [TabCompletion, MyArgHook]
    class ProgramArgs
    {
        [HelpHook, ArgShortcut("?"), ArgShortcut("h"), ArgShortcut("--?"), ArgShortcut("--h"), ArgDescription("Shows help")]
        public bool Help { get; set; }

        [ArgShortcut("i"), ArgShortcut("--i"), ArgShortcut("in"), ArgShortcut("--in"), ArgDescription(@"The directory containing the files to process"), ArgDefaultValue(@"."), ArgExistingDirectory]
        public string InputDirectory { get; set; }

        [ArgShortcut("o"), ArgShortcut("--o"), ArgShortcut("out"), ArgShortcut("--out"), ArgDescription(@"The directory in which to create folders containing the sorted files by year & month"), ArgDefaultValue(@".\out")]
        public string OutputDirectory { get; set; }

        [ArgShortcut("p"), ArgShortcut("--p"), ArgDescription(@"To denote the folder being processed contains images whose EXIF date should be used, if possible"), ArgDefaultValue(false)]
        public bool IsPictures { get; set; }

        [ArgShortcut("whatif"), ArgShortcut("--whatif"), ArgDescription(@"Don't actually move files, just show what would happen if we were to move them"), ArgDefaultValue(false)]
        public bool NoOp { get; set; }

        [ArgShortcut("f"), ArgShortcut("--f"), ArgDescription(@"Automatically overwrite files in destination, if they exist"), ArgDefaultValue(false)]
        public bool Force { get; set; }

        [ArgShortcut("u"), ArgShortcut("--u"), ArgDescription(@"True to update the creation & write time to match EXIF time, false otherwise"), ArgDefaultValue(false)]
        public bool UpdateTimestamp { get; set; }

        [ArgShortcut("n"), ArgShortcut("--n"), ArgDescription(@"Don't move any files (useful with -u to update times only)"), ArgDefaultValue(false)]
        public bool NoMove { get; set; }

        [ArgShortcut("r"), ArgShortcut("--r"), ArgDescription(@"True to process all files in all subdirectories of Input Directory. Compatible only with -NoMove"), ArgDefaultValue(false)]
        public bool Recurse { get; set; }

        [ArgShortcut("y"), ArgShortcut("--y"), ArgShortcut("--confirm"), ArgDescription(@"Do not prompt to commence operation"), ArgDefaultValue(false)]
        public bool Confirm { get; set; }
    }

    class MyArgHook : ArgHook
    {
        public override void BeforeValidateDefinition(HookContext context)
        {
            context.Definition.IsNonInteractive = true;

            base.BeforeValidateDefinition(context);
        }

        public override void AfterPopulateProperties(HookContext context)
        {
            var input = context.Args as ProgramArgs;

            var errMsgs = new List<string>();
            if (input.Recurse && !input.NoMove)
            {
                errMsgs.Add("ERROR: Recurse is only available if NoMove is also true");
            }

            if (input.NoOp && input.Force)
            {
                errMsgs.Add("ERROR: NoOp and Force cannot both be true");
            }

            if (errMsgs.Any())
            {
                Console.WriteLine(string.Concat(string.Join(Environment.NewLine, errMsgs), Environment.NewLine));
                PrintUsage();
                context.CancelAllProcessing();
            }
        }
    }

    static void Main(string[] args)
    {
        ProgramArgs input;
        try
        {
            input = PowerArgs.Args.Parse<ProgramArgs>(args);

            if (input == null || input.Help)
            {
                // means client ran with '-h' to output help.
                return;
            }
        }
        catch (PowerArgs.ArgException ex)
        {
            Console.WriteLine(ex.Message);
            return;
        }

        var inputDirectory = string.IsNullOrEmpty(input.InputDirectory) ? Environment.CurrentDirectory : input.InputDirectory;
        var outputDirectory = string.IsNullOrEmpty(input.OutputDirectory) ? inputDirectory : input.OutputDirectory;

        var inputDirectoryInfo = new DirectoryInfo(inputDirectory);
        var filesToProcess = inputDirectoryInfo.EnumerateFiles(@"*", new EnumerationOptions
        {
            RecurseSubdirectories = input.Recurse,
            MatchCasing = MatchCasing.CaseInsensitive,
            MatchType = MatchType.Simple,
            ReturnSpecialDirectories = false
        });

        var outputDirectoryInfo = new DirectoryInfo(outputDirectory);

        if (!input.Confirm)
        {
            Console.WriteLine($@"About to process {filesToProcess.LongCount()} files(s) from {inputDirectoryInfo.FullName} into {outputDirectoryInfo.FullName} ...");
            Console.WriteLine(@"This operation cannot be undone! Press any key to continue or Esc to cancel");
            if (Console.ReadKey().Key == ConsoleKey.Escape)
            {
                return;
            }
        }

        Console.WriteLine(@"Processing files...");

        Parallel.ForEach(filesToProcess, fi =>
        {
            var timeToUse = new[] { fi.CreationTime, fi.LastWriteTime, fi.LastAccessTime }.OrderBy(i => i).First();

            if (input.IsPictures)
            {
                try
                {
                    using var exif = new ExifReader(fi.FullName);
                    exif.GetTagValue(ExifTags.DateTime, out DateTime time);
                    if (time != DateTime.MinValue)
                    {
                        if (input.UpdateTimestamp && time != fi.CreationTime)
                        {
                            Console.Write($@"Updating time on {fi.Name} from {timeToUse} -> {time} ...");
                            if (!input.NoOp)
                            {
                                fi.CreationTime = fi.LastWriteTime = time;
                            }

                            Console.WriteLine();
                        }

                        timeToUse = time;
                    }
                }
                catch { }
            }

            if (!input.NoMove && !input.Recurse)
            {
                var dirName = Path.Combine(timeToUse.ToString(@"yyyy"), timeToUse.ToString(@"MM MMMM"));
                var targetFolder = Path.Combine(outputDirectory, dirName);

                if (!input.NoOp)
                {
                    Directory.CreateDirectory(targetFolder);
                }

                Console.Write($@"{fi.Name} -> {dirName} ...");
                if (!input.NoOp)
                {
                    try
                    {
                        File.Move(fi.FullName, Path.Combine(targetFolder, fi.Name), input.Force);
                    }
                    catch (IOException ex)
                    {
                        Console.Error.WriteLine($@"Error: {ex.Message}");
                    }
                }

                Console.WriteLine();
            }
        });
    }

    private static void PrintUsage()
    {
        Console.Write(PowerArgs.ArgUsage.GenerateUsageFromTemplate<ProgramArgs>());
    }
}
