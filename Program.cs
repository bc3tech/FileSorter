using System;
using System.IO;
using System.Linq;
using ExifLib;
using PowerArgs;

namespace FileSorter
{
    class Program
    {
        [TabCompletion]
        class ProgramArgs
        {
            [HelpHook, ArgShortcut("?"), ArgShortcut("h"), ArgDescription("Shows help")]
            public bool Help { get; set; }

            [ArgShortcut("in"), ArgDescription(@"The directory containing the files to process"), ArgDefaultValue(@"")]
            public string InputDirectory { get; set; }

            [ArgShortcut("out"), ArgDescription(@"The directory to create folders containing the sorted files by year & month"), ArgDefaultValue(@"")]
            public string OutputDirectory { get; set; }

            [ArgShortcut("p"), ArgDescription(@"To denote the folder being processed contains images whose EXIF date should be used, if possible"), ArgDefaultValue(false)]
            public bool IsPictures { get; set; }

            [ArgShortcut("whatif"), ArgDescription(@"Don't actually move files, just show what would happen if we were to move them"), ArgDefaultValue(false)]
            public bool NoOp { get; set; }

            [ArgShortcut("f"), ArgShortcut("y"), ArgShortcut("confirm"), ArgDescription(@"Automatically overwrite files in destination, if they exist"), ArgDefaultValue(false)]
            public bool Force { get; set; }

            [ArgShortcut("u"), ArgDescription(@"True to update the creation & write time to match EXIF time, false otherwise"), ArgDefaultValue(false)]
            public bool UpdateTimestamp { get; set; }

            [ArgShortcut("n"), ArgDescription(@"Don't move any files (useful with -u to update times only)"), ArgDefaultValue(false)]
            public bool NoMove { get; set; }
        }

        static void Main(string[] args)
        {
            var input = PowerArgs.Args.Parse<ProgramArgs>(args);

            if (input == null)
            {
                // means client ran with '-h' to output help.
                return;
            }

            var inputDirectory = string.IsNullOrEmpty(input.InputDirectory) ? Environment.CurrentDirectory : input.InputDirectory;
            var outputDirectory = string.IsNullOrEmpty(input.OutputDirectory) ? inputDirectory : input.OutputDirectory;

            var di = new DirectoryInfo(inputDirectory);

            Console.WriteLine(@"Processing files...");

            foreach (var fi in di.EnumerateFiles(@"*", new System.IO.EnumerationOptions
            {
                RecurseSubdirectories = false,
                MatchCasing = MatchCasing.CaseInsensitive,
                MatchType = MatchType.Simple,
                ReturnSpecialDirectories = false
            }))
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

                if (!input.NoMove)
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
            }
        }
    }
}
