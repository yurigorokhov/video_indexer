using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;
using LambdaSharp;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace My.VideoIndexer.ExtractText {

    public class FunctionResponse { 
        public StartTranscriptionJobResponse[] Responses;
    }

    public class Function : ALambdaFunction<S3Event, FunctionResponse> {

        //--- Fields ---
        private AmazonTranscribeServiceClient _transcribe = null;
        private string _outputBucketName = null;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _transcribe = new AmazonTranscribeServiceClient();
            _outputBucketName = config.ReadS3BucketName("ExtractedTextBucket");
        }

        public override async Task<FunctionResponse> ProcessMessageAsync(S3Event request) {
            var responses = new List<StartTranscriptionJobResponse>(request.Records.Count);
            foreach(var record in request.Records) {
                LogInfo($"processing 's3://{record.S3.Bucket.Name}/{record.S3.Object.Key}'");
                try {

                    // kick off a transcribe job
                    var transcriptionResponse = await _transcribe.StartTranscriptionJobAsync(
                        new StartTranscriptionJobRequest{
                            TranscriptionJobName=$"transcribe-{record.S3.Object.ETag}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                            Media=new Media{
                                MediaFileUri=$"https://s3-{record.AwsRegion}.amazonaws.com/{record.S3.Bucket.Name}/{record.S3.Object.Key}"
                            },
                            MediaFormat=MediaFormat.Mp3,
                            LanguageCode=LanguageCode.EnUS,
                            OutputBucketName=_outputBucketName
                        }, 
                        CancellationToken.None
                    );
                    responses.Add(transcriptionResponse);
                } catch(Exception e) {
                    LogError(e);
                }
            }
            return new FunctionResponse{ Responses = responses.ToArray() };
        }
    }
}
