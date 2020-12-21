using System;
using System.IO;
using System.Linq;
using ExifLib;
using PowerArgs;

namespace FileSorter
{
    class Program
    {
        class ProgramArgs
        {
            [HelpHook, ArgShortcut("?"), ArgShortcut("h"), ArgDescription("Shows help")]
            public bool Help { get; set; }

            [ArgPosition(0), ArgShortcut("in"), ArgDescription(@"The directory containing the files to process"), ArgDefaultValue(null)]
            public string InputDirectory { get; set; }

            [ArgPosition(1), ArgShortcut("out"), ArgDescription(@"The directory to create folders containing the sorted files by year & month"), ArgDefaultValue(null)]
            public string OutputDirectory { get; set; }

            [ArgShortcut("p"), ArgDescription(@"To denote the folder being processed contains images whose EXIF date should be used, if possible"), ArgDefaultValue(false)]
            public bool IsPictures { get; set; }

            [ArgShortcut("whatif"), ArgDescription(@"Don't actually move files, just show what would happen if we were to move them"), ArgDefaultValue(false)]
            public bool NoOp { get; set; }

            [ArgShortcut("f"), ArgShortcut("y"), ArgShortcut("confirm"), ArgDescription(@"Automatically overwrite files in destination, if they exist"), ArgDefaultValue(false)]
            public bool Force { get; set; }
        }

        static void Main(string[] args)
        {
            var input = PowerArgs.Args.Parse<ProgramArgs>(args);

            if (input == null)
            {
                // means client ran with '-h' to output help.
                return;
            }

            var inputDirectory = input.InputDirectory ?? Environment.CurrentDirectory;
            var outputDirectory = input.OutputDirectory ?? inputDirectory;

            var di = new DirectoryInfo(inputDirectory);

            Console.WriteLine($@"Processing files...");

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
                            timeToUse = time;
                        }
                    }
                    catch { }
                }

                var dirName = Path.Combine(timeToUse.ToString(@"yyyy"), timeToUse.ToString($"MM MMMM"));
                var targetFolder = Path.Combine(outputDirectory, dirName);

                if (!input.NoOp)
                {
                    Directory.CreateDirectory(targetFolder);
                }

                Console.Write("{0} > {1} ...", fi.Name, dirName);
                if (!input.NoOp)
                {
                    try
                    {
                        File.Move(fi.FullName, Path.Combine(targetFolder, fi.Name), input.Force);
                        Console.WriteLine();
                    }
                    catch (IOException ex)
                    {
                        Console.Error.WriteLine($@"Error: {ex.Message}");
                    }
                }
            }
        }
    }
}
