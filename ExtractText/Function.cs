using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

using Amazon.SQS;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;
using LambdaSharp;
using Newtonsoft.Json;

using My.VideoIndexer.Common;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace My.VideoIndexer.ExtractText {

    public class FunctionResponse { }

    public class Function : ALambdaFunction<S3Event, FunctionResponse> {

        //--- Fields ---
        private AmazonTranscribeServiceClient _transcribe;
        private IAmazonSQS _sqsClient;
        private string _notifyQueueUrl;
        private string _outputBucketName;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _transcribe = new AmazonTranscribeServiceClient();
            _outputBucketName = config.ReadS3BucketName("ExtractedTextBucket");
            _notifyQueueUrl = config.ReadSqsQueueUrl("ProgressQueue");
            _sqsClient = new AmazonSQSClient();
        }

        public override async Task<FunctionResponse> ProcessMessageAsync(S3Event request) {
            foreach(var record in request.Records) {
                LogInfo($"processing 's3://{record.S3.Bucket.Name}/{record.S3.Object.Key}'");
                
                // extract the video etag from the filename
                var videoEtag = Path.GetFileNameWithoutExtension(record.S3.Object.Key);
                
                try {

                    // kick off a transcribe job
                    var transcriptionResponse = await _transcribe.StartTranscriptionJobAsync(
                        new StartTranscriptionJobRequest{

                            // TODO: need a better name (add timestamp?) so that we can re-transcribe a transcribed video
                            TranscriptionJobName=$"transcribe-{videoEtag}",
                            Media = new Media{
                                MediaFileUri=$"https://s3-{record.AwsRegion}.amazonaws.com/{record.S3.Bucket.Name}/{record.S3.Object.Key}"
                            },
                            MediaFormat=MediaFormat.Mp3,
                            LanguageCode=LanguageCode.EnUS,
                            OutputBucketName=_outputBucketName
                        }, 
                        CancellationToken.None
                    );

                    // send a message to the progress queue
                    await _sqsClient.SendMessageAsync(new Amazon.SQS.Model.SendMessageRequest {
                        MessageBody = SerializeJson(new ProgressMessage {
                            Type = MessageType.INDEXING_STARTED,
                            Message = SerializeJson(new IndexingStartedMessage {
                                Job = transcriptionResponse
                            })
                        }),
                        QueueUrl = _notifyQueueUrl
                    });
                } catch(Exception e) {
                    LogError(e);
                }
            }
            return new FunctionResponse{ };
        }
    }
}
