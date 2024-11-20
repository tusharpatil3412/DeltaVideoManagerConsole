// See https://aka.ms/new-console-template for more information
using AddTimeStampConsole;
using Xabe.FFmpeg;
using log4net;
using log4net.Config;
using System.Reflection;
using System.Diagnostics;
using Azure.AI.Vision.ImageAnalysis;
using Azure;
using System.Text.RegularExpressions;
using FFMpegCore.Enums;



class Program
{

   private static readonly ILog log = LogManager.GetLogger(typeof(Program));


    static async Task Main(string[] args)
    {
        try
        {
            //BasicConfigurator.Configure();
            //var logRepository = LogManager.GetRepository(System.Reflection.Assembly.GetEntryAssembly());
            //FFmpeg.SetExecutablesPath(@"..\..\..\FFmpeg"); // Update this path as needed

            var configFilePath = @"..\..\..\Logger.config";
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

            string[] videoExtensions = { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".mts",".webm" };

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

                        if (!await CheckIsTimestampExists(videoFile))
                        {
                            
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
                            var outputFile = CreateOutputDirectory(inputVideoPath, videoFile, outputVideoPath);
                            File.Move(videoFile, outputFile);
                            Console.WriteLine($"Video file has Timestamp Detected, {videoFile} ");
                            log.Info($"Video file has Timestamp Detected, {videoFile} ");
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
            File.Move(inputFile, outputFile);
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

static async Task<bool> CheckIsTimestampExists(string filePath)
{
    // Get media information using FFmpeg
    var mediaInfo = await FFmpeg.GetMediaInfo(filePath);
    
    // Split the video into 5 intervals for frame capture
    int frameCount = 5;
    int timeInterval = (int)Math.Floor(mediaInfo.Duration.TotalSeconds / frameCount);
    int[] captureTimes = Enumerable.Range(0, frameCount).Select(i => timeInterval * i + 1).ToArray();

    // Set up Azure Cognitive Services client
    string endpoint = "https://handwritten-poc.cognitiveservices.azure.com/";
    string key = "0e04fa88a25a421d87bd3f7b5796513e";
    ImageAnalysisClient client = new ImageAnalysisClient(
        new Uri(endpoint),
        new AzureKeyCredential(key));

    // Counter for frames with detected timestamps
    int timestampCount = 0;

        foreach (var (time, index) in captureTimes.Select((time, index) => (time, index + 1)))
        {
            using (var memoryStream = new MemoryStream())
            {
                var ffmpeg = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-ss {time} -i \"{filePath}\" -frames:v 1 -f image2pipe -vcodec png -",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                try
                {
                    using (var process = Process.Start(ffmpeg))
                    {
                        if (process != null)
                        {
                            await process.StandardOutput.BaseStream.CopyToAsync(memoryStream);
                            process.WaitForExit();
                        }
                    }

                    byte[] imageData = memoryStream.ToArray();
                    bool hasTimestamp = await AnalyzeImage(client, imageData, index);

                    if (hasTimestamp)
                    {
                        Console.WriteLine($"Timestamp detected in frame {index}");
                        timestampCount++; // Increment the counter if a timestamp is detected
                    }
                    else
                    {
                        Console.WriteLine($"No timestamp detected in frame {index}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error capturing frame at {time} seconds: {ex.Message}");
                }
            }
        }

        // Check if the timestamp count is 3 or more
        if (timestampCount >= 3)
        {
            Console.WriteLine($"Timestamps were detected in{timestampCount}");
            return true ;
        }
        else
        {
            Console.WriteLine("Timestamps were detected in less than 3 frames.");
            return false;
        }

        
}

    static async Task<bool> AnalyzeImage(ImageAnalysisClient client, byte[] imageData, int index)
    {
        try
        {
            BinaryData binaryData = BinaryData.FromBytes(imageData);
            ImageAnalysisResult result = client.Analyze(
                binaryData,
                VisualFeatures.Caption | VisualFeatures.Read,
                new ImageAnalysisOptions { GenderNeutralCaption = true });

            Console.WriteLine($"Results for Frame {index}:");

            // Regular expression to match common date and timestamp formats
            var datePattern = new Regex(@"\b(\d{1,2}([.:]\d{2}){0,2}(AM|PM)?|\d{4}-\d{2}-\d{2} \d{2}:\d{2}(:\d{2})?|(\w{3,9}) \d{1,2},? \d{4})\b", RegexOptions.IgnoreCase);


            // Process detected text and check for date/timestamp format
            foreach (DetectedTextBlock block in result.Read.Blocks)
            {
                foreach (DetectedTextLine line in block.Lines)
                {
                    Console.WriteLine($"   Line: '{line.Text}', Bounding Polygon: [{string.Join(" ", line.BoundingPolygon)}]");

                    // Check if the line text matches the date pattern
                    if (datePattern.IsMatch(line.Text))
                    {
                        Console.WriteLine(" Date or timestamp detected!");
                        return true; // Return true if a date/timestamp is found
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing frame {index}: {ex.Message}");
        }

        return false; // Return false if no date/timestamp is detected
    }
}
