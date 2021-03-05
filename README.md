# Filesorter

[![Build and Package](https://github.com/bc3tech/FileSorter/actions/workflows/build-and-pack.yaml/badge.svg)](https://github.com/bc3tech/FileSorter/actions/workflows/build-and-pack.yaml)

This is a utility I created to help sort that one folder you have with all those files in it.
In my case, it was my Photos folder so this tool also has the ability to inspect EXIF data of
pictures to sort them properly. Below is output from `filesorter -h`:

## Usage

```txt
Usage - FileSorter -options

GlobalOption               Description
Help (-?, -h)              Shows help
InputDirectory (-in)       The directory containing the files to process [Default=''] 
OutputDirectory (-out)     The directory to create folders containing the sorted files by year & month [Default=''] 
IsPictures (-p)            To denote the folder being processed contains images whose EXIF date should be used, if possible [Default='False'] 
NoOp (-whatif)             Don't actually move files, just show what would happen if we were to move them [Default='False'] 
Force (-f, -y, -confirm)   Automatically overwrite files in destination, if they exist [Default='False'] 
UpdateTimestamp (-u)       True to update the creation & write time to match EXIF time, false otherwise [Default='False'] 
NoMove (-n)                Don't move any files (useful with -u to update times only) [Default='False'] 
Recurse (-r)               True to process all files in all subdirectories of Input Directory. Compatible only with -NoMove (and -u) [Default='False'] 
```

### InputDirectory

If you want to run FileSorter on a directory other than the current one, pass that here with `-in`.
If you don't specify the output directory as well, Filesorter will assume it to be the same as the input directory.
I.e. Default = InputDirectory

### OutputDirectory

If you want the output folder structure to be put somewhere other than the input/current directory, specify that here with `-out`.

## IsPictures

If your folder is primarily pictures, give Filesorter the `-p` option and it'll try and get EXIF data from each file
to determine its *real* date & time.

### NoOp

If you'd just like to see what Filesorter plans on doing but don't *actually* want it to move any files, use pass `-whatif`.

### Force

If files encounter conflicts while being moved (e.g. "File already exists" in destination), the operation will fail.
If you want to overwrite the files in these cases, pass `-f`, `-y`, or `-confirm` to Filesorter.

### UpdateTimestamp

If you're processing pictures, Filesorter can update the date/time of the picture file to match that of the EXIF timestamp
embedded within, if you like. Pass `-u` to turn this on.

### NoMove

If you don't actually want to move files, set this option by passing `-n` to Filesorter. This is useful if you want to
do work - like update timestamps (`-u`) - but don't want files moved. It differs from `-whatif` in that it *will* do work,
it just won't move files.

### Recurse

If you want Filesorter to process all subdirectories of the input directory for files and sort them into the output directory,
pass `-r`. It's best to use `-out` as well to specify an output directory that is **not** under the input directory when using
this option.

## Installation

Filesorter is distributed via [Chocolatey](https://chocolatey.org) and can be installed via `choco install filesorter`
