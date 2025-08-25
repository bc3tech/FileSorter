namespace FileSorter;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.WindowsAPICodePack.Shell.PropertySystem;

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

        [ArgShortcut("--whatif"), ArgDescription(@"Don't actually move files, just show what would happen if we were to move them"), ArgDefaultValue(false)]
        public bool NoOp { get; set; }

        [ArgShortcut("f"), ArgShortcut("--f"), ArgDescription(@"Automatically overwrite files in destination, if they exist"), ArgDefaultValue(false)]
        public bool Force { get; set; }

        [ArgShortcut("u"), ArgShortcut("--u"), ArgDescription(@"True to update the creation & write time to match EXIF time, false otherwise"), ArgDefaultValue(false)]
        public bool UpdateTimestamp { get; set; }

        [ArgShortcut("--max-adjust"), ArgDescription(@"The maximum number of days of change to allow for when updating timestamps on files"), ArgDefaultValue(3650)]
        public uint MaxAdjustmentDays { get; set; }

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

            if (errMsgs.Count is not 0)
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
            input = Args.Parse<ProgramArgs>(args);

            if (input is null || input.Help)
            {
                // means client ran with '-h' to output help.
                return;
            }
        }
        catch (ArgException ex)
        {
            Console.WriteLine(ex.Message);
            return;
        }

        var inputDirectory = string.IsNullOrEmpty(input.InputDirectory) ? Environment.CurrentDirectory : input.InputDirectory;
        var outputDirectory = string.IsNullOrEmpty(input.OutputDirectory) ? inputDirectory : input.OutputDirectory;

        var inputDirectoryInfo = new DirectoryInfo(inputDirectory);
        IEnumerable<FileInfo> filesToProcess = inputDirectoryInfo.EnumerateFiles(@"*", new EnumerationOptions
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

        Parallel.ForEach(filesToProcess, fi => ProcessFile(fi, input, outputDirectory));
    }

    private static void ProcessFile(FileInfo fi, ProgramArgs input, string outputDirectory)
    {
        try
        {
            DateTime timeToUse = determineTimeToUse(out DateTime fileTime, out DateTime embeddedTime);

            if (input.IsPictures)
            {
                if (timeToUse != DateTime.MinValue && input.UpdateTimestamp)
                {
                    setTimestamp();
                }
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
                    File.Move(fi.FullName, Path.Combine(targetFolder, fi.Name), input.Force);
                }

                Console.WriteLine();
            }

            DateTime determineTimeToUse(out DateTime fileTime, out DateTime embeddedTime)
            {
                // Pick creation time unless it's before unix epoch, then pick last modified unless it's lower than unix epoch, then pick last accessed
                fileTime = fi.CreationTime;
                if (fileTime <= DateTime.UnixEpoch)
                {
                    fileTime = fi.LastWriteTime;
                }

                if (fileTime <= DateTime.UnixEpoch)
                {
                    fileTime = fi.LastAccessTime;
                }

                embeddedTime = getEmbeddedTimestamp(fi.FullName);

                // return the earliest time that is not minvalue
                return new[] { fileTime, embeddedTime }.Where(t => t > DateTime.UnixEpoch).Order().FirstOrDefault(DateTime.MinValue);
            }

            static DateTime getEmbeddedTimestamp(string fileName)
            {
                var ps = Microsoft.WindowsAPICodePack.Shell.ShellFile.FromFilePath(fileName);
                return ps.Properties.System.Photo.DateTaken.Value ?? ps.Properties.System.Media.DateEncoded.Value ?? DateTime.MinValue;
            }

            void setTimestamp()
            {
                if (fileTime != timeToUse)
                {
                    if (Math.Abs((fileTime - timeToUse).TotalDays) > input.MaxAdjustmentDays)
                    {
                        Console.WriteLine($@"WARNING: File time on {fi.FullName} is too far off from calculated time ({fileTime.ToShortDateString()} vs {timeToUse.ToShortDateString()}), skipping ...");
                    }
                    else
                    {
                        Console.WriteLine($@"Updating filesystem time on {fi.FullName} -> {timeToUse} ...");
                        if (!input.NoOp)
                        {
                            fi.CreationTime = fi.LastWriteTime = timeToUse;
                        }
                    }
                }

                if (getEmbeddedTimestamp(fi.FullName) != timeToUse)
                {
                    var ps = Microsoft.WindowsAPICodePack.Shell.ShellFile.FromFilePath(fi.FullName);
                    Console.WriteLine($@"Updating embedded time on {fi.FullName} -> {timeToUse} ...");
                    if (!input.NoOp)
                    {
                        try
                        {
                            ps.Properties.System.Media.DateEncoded.Value = timeToUse;
                        }
                        catch { }

                        try
                        {
                            using ShellPropertyWriter w = ps.Properties.GetPropertyWriter();
                            w.WriteProperty(ps.Properties.System.Photo.DateTaken, timeToUse);
                        }
                        catch { }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($@"Error processing {fi.FullName}: {ex.Message}");
        }
    }

    private static void PrintUsage() => Console.Write(ArgUsage.GenerateUsageFromTemplate<ProgramArgs>());
}
