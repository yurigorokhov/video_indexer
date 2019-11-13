using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;

using LambdaSharp;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace My.VideoIndexer.ExtractAudio {

    public class FunctionResponse { }

    public class Function : ALambdaFunction<S3Event, FunctionResponse> {

        //--- Fields ---
        private IAmazonS3 _s3Client;
        private string _destinationBucketName;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _s3Client = new AmazonS3Client();
            _destinationBucketName = config.ReadS3BucketName("ExtractedAudioBucket");

            // show contents of /opt folder; the 'ffmpeg' file must have execution permissions to be invocable
            LogInfo(Exec("/bin/bash", "-c \"ls -al /opt\"").Output);
        }

        public override async Task<FunctionResponse> ProcessMessageAsync(S3Event request) {
            foreach(var record in request.Records) {
                string originalFilePath = null;
                string convertedFilePath = null;
                try {

                    // download file to temporary storage
                    LogInfo($"processing 's3://{record.S3.Bucket.Name}/{record.S3.Object.Key}'");
                    var response = await _s3Client.GetObjectAsync(new GetObjectRequest {
                        BucketName = record.S3.Bucket.Name,
                        Key = record.S3.Object.Key
                    });
                    originalFilePath = $"/tmp/{Path.GetFileName(record.S3.Object.Key)}";
                    await response.WriteResponseStreamToFileAsync(
                        filePath: originalFilePath,
                        append: false,
                        cancellationToken: CancellationToken.None
                    );

                    LogInfo($"successfully downloaded file to: {originalFilePath}");

                    // run ffmpeg process to extract audio from video
                    LogInfo("invoking ffmpeg converter");
                    convertedFilePath = Path.ChangeExtension(originalFilePath, ".mp3");

                    // command to extract MP3 from video @ 192Kbps: ffmpeg -i video.mp4 -f mp3 -ab 192000 -vn music.mp3
                    var outcome = Exec("/opt/ffmpeg", $"-i \"{originalFilePath}\" -f mp3 -ab 192000 -vn \"{convertedFilePath}\"");
                    if(outcome.ExitCode != 0) {
                        LogWarn(
                            "FFmpeg exit code: {0}\nError Output: {1}\nStandard Output: {2}",
                            outcome.ExitCode,
                            outcome.Error,
                            outcome.Output
                        );
                        continue;
                    }

                    // upload converted file
                    var targetKey = Path.Combine("audio", Path.GetDirectoryName(record.S3.Object.Key), Path.GetFileName(convertedFilePath));
                    LogInfo($"uploading 's3://{_destinationBucketName}/{targetKey}'");
                    await _s3Client.PutObjectAsync(new PutObjectRequest {
                        BucketName = _destinationBucketName,
                        Key = targetKey,
                        FilePath = convertedFilePath
                    });
                } catch(Exception e) {
                    LogError(e);
                } finally {
                    DeleteFile(originalFilePath);
                    DeleteFile(convertedFilePath);
                }
            }
            return new FunctionResponse();
        }

        private (int ExitCode, string Output, string Error) Exec(string application, string arguments) {
            LogInfo($"executing: {application} {arguments}");
            using(var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = application,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            }) {
                process.Start();
                var output = Task.Run(() => process.StandardOutput.ReadToEndAsync());
                var error = Task.Run(() => process.StandardError.ReadToEndAsync());
                process.WaitForExit();
                return (ExitCode: process.ExitCode, Output: output.Result, Error: error.Result);
            }
        }

        private void DeleteFile(string filePath) {
            if(filePath == null) {
                return;
            }
            try {
                File.Delete(filePath);
            } catch {
                LogWarn("unable to delete: {0}", filePath);
            }
        }
    }
}
