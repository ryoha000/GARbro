using System;
using System.Collections.Generic;
using System.IO;
using GameRes;

namespace GARbro.Cli
{
    public static class ConvertCommand
    {
        public static Dictionary<string, object> Execute(CliOptions options)
        {
            if (string.IsNullOrEmpty(options.Input))
                return ResultBuilder.Error("convert", options.Input, ErrorCodes.INVALID_ARGUMENT, "--input is required");

            if (string.IsNullOrEmpty(options.TargetFormat))
                return ResultBuilder.Error("convert", options.Input, ErrorCodes.INVALID_ARGUMENT, "--to is required");

            if (string.IsNullOrEmpty(options.OutDir))
                return ResultBuilder.Error("convert", options.Input, ErrorCodes.INVALID_ARGUMENT, "--out directory is required");

            if (!File.Exists(options.Input))
                return ResultBuilder.Error("convert", options.Input, ErrorCodes.INPUT_NOT_FOUND, "Input path not found");

            var normalizedTarget = options.TargetFormat.ToLowerInvariant();
            if (normalizedTarget != "png" && normalizedTarget != "wav")
                return ResultBuilder.Error("convert", options.Input, ErrorCodes.INVALID_ARGUMENT, "requested target format is not supported");

            var overwriteError = CommandUtilities.ValidateOverwritePolicy("convert", options.Input, options.Overwrite);
            if (overwriteError != null)
                return overwriteError;

            var safeRootError = CommandUtilities.ValidateSafeRootOutput("convert", options.Input, options.OutDir, options.SafeRoot);
            if (safeRootError != null)
                return safeRootError;

            var result = ResultBuilder.Success("convert", options.Input);
            result["entry"] = options.Entry;
            result["target_format"] = normalizedTarget;
            result["output_dir"] = Path.GetFullPath(options.OutDir);

            try
            {
                if (!Directory.Exists(options.OutDir))
                    Directory.CreateDirectory(options.OutDir);

                if (!string.IsNullOrEmpty(options.Entry))
                    return ConvertArchiveEntry(options, result, normalizedTarget);
                return ConvertSingleFile(options, result, normalizedTarget);
            }
            catch (Exception ex)
            {
                return ResultBuilder.Error("convert", options.Input, ErrorCodes.CONVERSION_FAILED, ex.Message);
            }
        }

        static Dictionary<string, object> ConvertArchiveEntry(CliOptions options, Dictionary<string, object> result, string normalizedTarget)
        {
            Dictionary<string, object> openError;
            using (var arc = CommandUtilities.TryOpenArchive("convert", options.Input, out openError))
            {
                if (arc == null)
                    return openError;

                var entry = CommandUtilities.FindEntry(arc, options.Entry);
                if (entry == null)
                    return ResultBuilder.Error("convert", options.Input, ErrorCodes.ENTRY_NOT_FOUND, "Entry not found");

                return ConvertEntryCore(options, result, normalizedTarget, entry.Name, EntryTypeClassifier.Classify(entry),
                    output =>
                    {
                        if (normalizedTarget == "png")
                        {
                            using (var decoder = arc.OpenImage(entry))
                                ImageFormat.Png.Write(output, decoder.Image);
                        }
                        else
                        {
                            using (var input = arc.OpenEntry(entry))
                            using (var binary = BinaryStream.FromStream(input, entry.Name))
                            using (var sound = AudioFormat.Read(binary))
                            {
                                if (sound == null)
                                    throw new InvalidOperationException("input cannot be converted to the requested format");
                                AudioFormat.Wav.Write(sound, output);
                            }
                        }
                    });
            }
        }

        static Dictionary<string, object> ConvertSingleFile(CliOptions options, Dictionary<string, object> result, string normalizedTarget)
        {
            var detectedType = DetectFileType(options.Input);
            return ConvertEntryCore(options, result, normalizedTarget, Path.GetFileName(options.Input), detectedType,
                output =>
                {
                    if (normalizedTarget == "png")
                    {
                        using (var file = BinaryStream.FromFile(options.Input))
                        {
                            var format = ImageFormat.FindFormat(file);
                            if (format == null)
                                throw new InvalidDataException("input cannot be converted to the requested format");
                            file.Position = 0;
                            var image = format.Item1.Read(file, format.Item2);
                            ImageFormat.Png.Write(output, image);
                        }
                    }
                    else
                    {
                        using (var file = BinaryStream.FromFile(options.Input))
                        using (var sound = AudioFormat.Read(file))
                        {
                            if (sound == null)
                                throw new InvalidDataException("input cannot be converted to the requested format");
                            AudioFormat.Wav.Write(sound, output);
                        }
                    }
                });
        }

        static Dictionary<string, object> ConvertEntryCore(CliOptions options, Dictionary<string, object> result, string normalizedTarget, string sourceName, string sourceType, Action<Stream> writeAction)
        {
            var mediaType = sourceType == "image" ? "image" : sourceType == "audio" ? "audio" : null;
            if (mediaType == null)
                return ResultBuilder.Error("convert", options.Input, ErrorCodes.INPUT_NOT_SUPPORTED, "input cannot be converted to the requested format");

            if (mediaType == "image" && normalizedTarget != "png")
                return ResultBuilder.Error("convert", options.Input, ErrorCodes.INPUT_NOT_SUPPORTED, "input cannot be converted to the requested format");
            if (mediaType == "audio" && normalizedTarget != "wav")
                return ResultBuilder.Error("convert", options.Input, ErrorCodes.INPUT_NOT_SUPPORTED, "input cannot be converted to the requested format");

            var outputName = Path.ChangeExtension(Path.GetFileName(sourceName), normalizedTarget);
            var outputPath = Path.GetFullPath(Path.Combine(options.OutDir, outputName));
            var safeRoot = string.IsNullOrEmpty(options.SafeRoot) ? Path.GetFullPath(options.OutDir) : Path.GetFullPath(options.SafeRoot);
            if (!CommandUtilities.IsFilePathInsideRoot(outputPath, safeRoot))
                return ResultBuilder.Error("convert", options.Input, ErrorCodes.OUTSIDE_SAFE_ROOT, "Output path is outside of the safe root.");

            var resolution = OverwritePolicyResolver.Resolve(outputPath, options.Overwrite);
            if (!resolution.ShouldWrite)
            {
                result["result"] = new Dictionary<string, object>
                {
                    { "status", "skipped_existing" },
                    { "output_path", resolution.OutputPath },
                    { "media_type", mediaType }
                };
                return result;
            }

            try
            {
                OverwritePolicyResolver.WriteFile(resolution.OutputPath, resolution.ReplaceExisting, writeAction);
            }
            catch (InvalidDataException)
            {
                return ResultBuilder.Error("convert", options.Input, ErrorCodes.INPUT_NOT_SUPPORTED, "input cannot be converted to the requested format");
            }
            catch (InvalidOperationException)
            {
                return ResultBuilder.Error("convert", options.Input, ErrorCodes.INPUT_NOT_SUPPORTED, "input cannot be converted to the requested format");
            }
            catch (InvalidFormatException)
            {
                return ResultBuilder.Error("convert", options.Input, ErrorCodes.INPUT_NOT_SUPPORTED, "input cannot be converted to the requested format");
            }

            result["result"] = new Dictionary<string, object>
            {
                { "status", "converted" },
                { "output_path", resolution.OutputPath },
                { "media_type", mediaType }
            };
            return result;
        }

        static string DetectFileType(string inputPath)
        {
            using (var file = BinaryStream.FromFile(inputPath))
            {
                var image = ImageFormat.FindFormat(file);
                if (image != null)
                    return "image";

                file.Position = 0;
                using (var sound = AudioFormat.Read(file))
                {
                    if (sound != null)
                        return "audio";
                }
            }
            return EntryTypeClassifier.Classify(new Entry { Name = inputPath });
        }
    }
}
