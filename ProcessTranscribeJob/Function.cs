using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using LambdaSharp;
using LambdaSharp.SimpleQueueService;
using LambdaSharp.Exceptions;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;

using Newtonsoft.Json;

using My.VideoIndexer.Common;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace My.VideoIndexer.ProcessTranscribeJob {

    public class Function : ALambdaQueueFunction<ProgressMessage> {

        //--- Fields ---
        private AmazonTranscribeServiceClient _transcribe;

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {
            _transcribe = new AmazonTranscribeServiceClient();

            // TO-DO: add function initialization and reading configuration settings
        }

        public override async Task ProcessMessageAsync(ProgressMessage message) {
            if(message.Type == MessageType.INDEXING_STARTED) {
                var indexingMessage = JsonConvert.DeserializeObject<IndexingStartedMessage>(message.Message);
                var jobName = indexingMessage.Job.TranscriptionJob.TranscriptionJobName;
                
                LogInfo($"Received job: {jobName}");

                var response = await _transcribe.GetTranscriptionJobAsync(new GetTranscriptionJobRequest{
                    TranscriptionJobName = jobName
                }, CancellationToken.None);

                // check the job status
                switch(response.TranscriptionJob.TranscriptionJobStatus.Value) {
                    case "COMPLETED":

                        var transciptUri = response.TranscriptionJob.Transcript.TranscriptFileUri;
                        LogInfo($"Job {jobName} has completed successfully. The results are here: {transciptUri}");
                        break;
                    case "FAILED":
                        LogWarn($"Job {jobName} has failed");
                        break;
                    case "IN_PROGRESS":

                        // if the job has not yet finished, let's fail the message so that it stays in the queue
                        throw new LambdaRetriableException($"Job {jobName} is still in progress");
                    default:
                        throw new Exception("Unknown TranscriptionJobStatus");
                }
            } else {
                LogInfo($"Skipping message type: {message.Type}");
            }
        }
    }
}
