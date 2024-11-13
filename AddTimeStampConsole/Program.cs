// See https://aka.ms/new-console-template for more information
using AddTimeStampConsole;
using Xabe.FFmpeg;
using log4net;
using log4net.Config;
using System.Reflection;
//Console.WriteLine("Hello, World!");
//var inputVideoPath = string.Empty;// read from appsettings.json file
//Read all video files from inputVideoPath 
//Convert/Add timestamp in video
//Save it into outputVideoPath
//var outputVideoPath = string.Empty;// read from appsettings.json file

class Program
{

   private static readonly ILog log = LogManager.GetLogger(typeof(Program));


    static async Task Main(string[] args)
    {
        try
        {
            //BasicConfigurator.Configure();
            //var logRepository = LogManager.GetRepository(System.Reflection.Assembly.GetEntryAssembly());
            var configFilePath = Path.Combine(AppContext.BaseDirectory, "Logger.config");


            if (File.Exists(configFilePath))
            {
                XmlConfigurator.Configure(new FileInfo(configFilePath));
                Console.WriteLine("Logger configuration loaded successfully.");
            }
            else
            {
                Console.WriteLine($"Logger configuration file not found at: {configFilePath}");
                // Optionally, you can handle the missing file scenario here, such as by loading a default configuration
            }




            var inputVideoPath = Constants.InputVideoPath;
            var outputVideoPath = Constants.OutputVideoPath;

            // Check if input path exists
            if (!Directory.Exists(inputVideoPath))
            {
                Console.WriteLine("Input video path does not exist.");
                log.Info("Input video path does not exist.");
                return;
            }

            // Ensure output directory exists
            if (!Directory.Exists(outputVideoPath))
            {
                Directory.CreateDirectory(outputVideoPath);
            }

            string[] videoExtensions = { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".mts" };

            foreach (string dir in Directory.EnumerateDirectories(inputVideoPath, "*", SearchOption.AllDirectories))
            {
                // Get all .mp4 video files in the directory and subdirectories
                //var videoFiles = Directory.GetFiles(dir, "*.mp4", SearchOption.AllDirectories);
                var videoFiles = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                                              .Where(file => videoExtensions.Contains(Path.GetExtension(file).ToLower()));

                var nonVideoFiles = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                                              .Where(file => !videoExtensions.Contains(Path.GetExtension(file).ToLower()));

                if (nonVideoFiles.Count() > 0)
                {
                    //If zip or any image then move to output directory
                    foreach (var videoFile in nonVideoFiles)
                    {
                        var outputFile = CreateOutputDirectory(inputVideoPath, videoFile, outputVideoPath);
                        File.SetAttributes(inputVideoPath, FileAttributes.Normal);
                        if (File.Exists(outputFile))
                        {
                            File.Delete(outputFile); // Delete the destination file if it already exists
                        }
                        File.Move(videoFile, outputFile);
                    }
                }

                if (videoFiles != null && videoFiles.Count() == 0)
                {
                    Console.WriteLine("No video files found in the input path " + dir);
                    log.Info("No Video files found in the input path " + dir);
                    continue;
                }

                foreach (var videoFile in videoFiles)
                {
                    if (CheckIsFileNotInUse(videoFile))
                    {
                        Console.WriteLine($"Processing video: {videoFile}");
                        log.Info($"Processing video: {videoFile}");
                        var outputFile = CreateOutputDirectory(inputVideoPath, videoFile, outputVideoPath);

                        // Process the video and add a timestamp
                        try
                        {
                            await AddTimestampToVideo(videoFile, outputFile);
                            //Delete input file
                            File.Delete(videoFile);
                        }
                        catch (Exception ex)
                        {
                            //Move file to output directory
                            Console.WriteLine($"Error processing video {videoFile}: {ex.Message}");
                            log.Error($"Error processing video {videoFile}: {ex.Message}");
                            if (File.Exists(outputFile))
                            {
                                File.Delete(outputFile); // Delete the destination file if it already exists
                            }
                            File.Move(videoFile, outputFile);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Video file has some issues, {videoFile} cannot process for now.");
                        log.Warn($"Video file has some issues, {videoFile} cannot process for now.");
                    }
                }
            }

            Console.WriteLine("All videos processed.");
            log.Info("All videos processed.");
            

        }
        catch (Exception e)
        {
            Console.WriteLine("An error occurred: " + e.Message);
            log.Warn("An error occurred " + e.Message);
        }
        finally
        {
            Console.WriteLine("Process complete. Press any key to exit.");
            Console.ReadLine();
        }

    }
    // Function to add a timestamp to the video
    static async Task AddTimestampToVideo(string inputFile, string outputFile)
    {

        var mediaInfo = await FFmpeg.GetMediaInfo(inputFile);

        if (mediaInfo.CreationTime.HasValue)
        {
            var creationTimeOffset = new DateTimeOffset(mediaInfo.CreationTime.Value);
            long unixTimestamp = creationTimeOffset.ToUnixTimeSeconds();
            Console.WriteLine($"Unix Timestamp: {unixTimestamp}");

            string timestampFilter = $"drawtext=fontcolor=white:fontsize=16:box=1:boxcolor=black: x=10:y=10: text='%{{pts\\:gmtime\\:{unixTimestamp}\\:%d, %B %Y %I\\\\\\:%M\\\\\\:%S %p}}'";

            var conversion = FFmpeg.Conversions.New()
                .AddParameter($"-i \"{inputFile}\"")
                .AddParameter($"-vf \"{timestampFilter}\"")
                .AddParameter("-preset ultrafast")
                .AddParameter($"\"{outputFile}\"");

            await conversion.Start();
            Console.WriteLine($"Video saved to: {outputFile}");
            log.Info($"Vidio saved to : {outputFile}");
        }
        else
        {
            Console.WriteLine("No creation time found for this video.");
            log.Error("No creation time found for this video");
        }

    }

    static bool CheckIsFileNotInUse(string filePath)
    {
        bool isFileNotInUse = false;
        try
        {
            var fileSize = new FileInfo(filePath).Length;
            if (fileSize == 0)
            {
                return isFileNotInUse;
            }
            else
            {
                using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    isFileNotInUse = true;
                }
            }
        }
        catch (IOException)
        {
            // File is still locked (likely being written to)
            return isFileNotInUse;
        }
        return isFileNotInUse;
    }

    static string CreateOutputDirectory(string inputVideoPath, string videoFile, string outputVideoPath)
    {
        // Calculate relative path of the video file within input directory
        var relativePath = Path.GetRelativePath(inputVideoPath, videoFile);

        // Define the corresponding output path
        var outputFile = Path.Combine(outputVideoPath, relativePath);

        // Create the necessary subdirectory structure in the output path
        var outputDir = Path.GetDirectoryName(outputFile);
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
        return outputFile;
    }
}
