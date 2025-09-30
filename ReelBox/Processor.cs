using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ReelBox
{
    public class Processor
    {
        private string ffmpegPath;
        private string thumbnailsFolder;
        public static readonly List<string> SupportedVideoTypes = [".mp4", ".mkv", ".mov", ".avi"];
        public static readonly List<string> SupportedAudioTypes = [".mp3", ".wav"];
        public static readonly List<string> SupportedImageTypes = [".jpg", ".jpeg", ".png", ".gif"];
        public static readonly List<string> SupportedSubtitleTypes = [".srt", ".ass"];
        public static readonly List<string> AllSupportedTypes = SupportedVideoTypes.Concat(SupportedAudioTypes).Concat(SupportedImageTypes).Concat(SupportedSubtitleTypes).ToList();
        public static readonly int MaxThumbnailWidth = 196;
        public static readonly int MaxThumbnailHeight = 110;
        private Process? currentProcess;
        private bool hasBeenKilled;

        public Processor(string ffmpegPath)
        {
            this.ffmpegPath = ffmpegPath;
            thumbnailsFolder = Path.Join(Path.GetTempPath(), "ReelBoxThumbnails") + "/";
            Directory.CreateDirectory(thumbnailsFolder);
        }

        public static Medium GetMedium(string path)
        {
            var mediaType = Path.GetExtension(path).ToLower() switch
            {
                var ext when SupportedVideoTypes.Contains(ext) => MediaType.Video,
                var ext when SupportedAudioTypes.Contains(ext) => MediaType.Audio,
                var ext when SupportedImageTypes.Contains(ext) => MediaType.Image,
                var ext when SupportedSubtitleTypes.Contains(ext) => MediaType.Subtitle,
                _ => throw new ArgumentOutOfRangeException()
            };
            return new Medium
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                MediaType = mediaType,
                AvailableActions = mediaType switch
                {
                    MediaType.Video => Enum.GetValues<Action>(),
                    MediaType.Audio => [Action.Split, Action.Merge, Action.Compress, Action.Mix],
                    MediaType.Image => [Action.Compress, Action.Mix, Action.Tour],
                    MediaType.Subtitle => [Action.Mix],
                    _ => throw new ArgumentOutOfRangeException()
                }
            };
        }

        public async Task<MediaDetails> GetMediaDetails(string mediaPath, MediaType mediaType)
        {
            var details = new MediaDetails();
            details.FileSize = new FileInfo(mediaPath).Length;
            if (mediaType is MediaType.Video or MediaType.Audio)
            {
                await StartProcess($"-i \"{mediaPath}\"", null, (sender, args) =>
                {
                    if (string.IsNullOrWhiteSpace(args.Data) || hasBeenKilled) return;
                    Debug.WriteLine(args.Data);
                    if (details.Duration == null)
                    {
                        var matchCollection = Regex.Matches(args.Data, @"\s*Duration:\s(\d{2}:\d{2}:\d{2}\.\d{2}).+");
                        if (matchCollection.Count == 0) return;
                        details.Duration = matchCollection[0].Groups[1].Value;
                    }

                    {
                        var matchCollection = Regex.Matches(args.Data, @"\s*Chapter #0:(\d+).+");
                        if (matchCollection.Count != 0) details.ChapterCount++;
                        else
                        {
                            matchCollection = Regex.Matches(args.Data, @"\s*Stream #0:(\d+).*?: (\w+).+");
                            if (matchCollection.Count != 0)
                            {
                                var streamType = matchCollection[0].Groups[2].Value;
                                switch (streamType)
                                {
                                    case "Video":
                                        details.VideoCount++;
                                        break;
                                    case "Audio":
                                        details.AudioCount++;
                                        break;
                                    case "Subtitle":
                                        details.SubtitleCount++;
                                        break;
                                    case "Attachment":
                                        details.AttachmentCount++;
                                        break;
                                }
                            }
                        }
                    }
                });
            }

            if (mediaType is MediaType.Video or MediaType.Image)
            {
                var valuesSet = false;
                await StartProcess($"-i \"{mediaPath}\"", null, (sender, args) =>
                {
                    if (valuesSet || string.IsNullOrWhiteSpace(args.Data) || hasBeenKilled) return;
                    var matchCollection = Regex.Matches(args.Data, @"\s*Stream #\d+:\d+.*?: Video: .+?, (\d+x\d+)(?:.*?(\d+ kb/s))?(?:.*?(\d+?\.?\d*? fps))?");
                    if (matchCollection.Count == 0) return;
                    details.Resolution = matchCollection[0].Groups[1].Value;
                    if(matchCollection[0].Groups[2].Success) details.Bitrate = matchCollection[0].Groups[2].Value;
                    if (matchCollection[0].Groups[3].Success && (mediaType != MediaType.Image || Path.GetExtension(mediaPath) == ".gif"))
                        details.FPS = matchCollection[0].Groups[3].Value;
                    valuesSet = true;
                });

                var seekCommand = string.Empty;
                if (mediaType == MediaType.Video)
                {
                    var duration = TimeSpan.Parse(details.Duration);
                    var previewTimePoint = duration > TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : duration > TimeSpan.FromSeconds(2) ? TimeSpan.FromSeconds(2) : TimeSpan.Zero;
                    seekCommand = $"-ss {previewTimePoint} ";
                }
                details.ThumbnailPath = Path.Join(thumbnailsFolder, $"{Path.GetRandomFileName()}.png");
                var resSplit = details.Resolution.Split('x').Select(int.Parse).ToArray();
                var scaleCommand = resSplit[0] > resSplit[1] ? $"scale=w={MaxThumbnailWidth}:h=-1" : $"scale=w=-1:h={MaxThumbnailHeight}";
                await StartProcess($"{seekCommand}-i \"{mediaPath}\" -frames:v 1 -vf {scaleCommand} \"{details.ThumbnailPath}\"", null, null);
            }
            else if (mediaType == MediaType.Audio)
            {
                var valuesSet = false;

                await StartProcess($"-i \"{mediaPath}\"", null, (sender, args) =>
                {
                    Debug.WriteLine(args.Data);
                    if (valuesSet || string.IsNullOrWhiteSpace(args.Data) || hasBeenKilled) return;
                    var matchCollection = Regex.Matches(args.Data, @"\s*Stream #\d+:\d+.*?: Audio: .+?, (\d+ Hz).+?, (\d+ kb/s)");
                    if (matchCollection.Count == 0) return;
                    details.SampleRate = matchCollection[0].Groups[1].Value;
                    details.Bitrate = matchCollection[0].Groups[2].Value;
                    valuesSet = true;
                });
            }

            return details;
        }

        public async Task<string?> GetThumbnailPath(string mediaPath, bool isImage)
        {
            try
            {
                const int thumbnailHeight = 110;
                var seekCommand = string.Empty;
                if (!isImage)
                {
                    var duration = TimeSpan.MinValue;
                    await StartProcess($"-i \"{mediaPath}\"", null, (sender, args) =>
                    {
                        if (string.IsNullOrWhiteSpace(args.Data) || hasBeenKilled) return;
                        if (duration != TimeSpan.MinValue) return;
                        var matchCollection = Regex.Matches(args.Data, @"\s*Duration:\s(\d{2}:\d{2}:\d{2}\.\d{2}).+");
                        if (matchCollection.Count == 0) return;
                        duration = TimeSpan.Parse(matchCollection[0].Groups[1].Value);
                    });
                    var previewTimePoint = duration > TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : duration > TimeSpan.FromSeconds(2) ? TimeSpan.FromSeconds(2) : TimeSpan.Zero;
                    seekCommand = $"-ss {previewTimePoint} ";
                }
                var thumbnailPath = Path.Join(thumbnailsFolder, $"{Path.GetRandomFileName()}.png");
                await StartProcess($"{seekCommand}-i \"{mediaPath}\" -frames:v 1 -vf scale=w=-1:h={thumbnailHeight} \"{thumbnailPath}\"", null, null);
                return thumbnailPath;
            }
            catch (Exception )
            {
                return null;
            }
        }

        async Task StartProcess(string arguments, DataReceivedEventHandler? outputEventHandler, DataReceivedEventHandler? errorEventHandler)
        {
            Process ffmpeg = new()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                },
                EnableRaisingEvents = true
            };
            ffmpeg.OutputDataReceived += outputEventHandler;
            ffmpeg.ErrorDataReceived += errorEventHandler;
            ffmpeg.Start();
            ffmpeg.BeginErrorReadLine();
            ffmpeg.BeginOutputReadLine();
            currentProcess = ffmpeg;
            await ffmpeg.WaitForExitAsync();
            ffmpeg.Dispose();
            currentProcess = null;
        }
    }
}
