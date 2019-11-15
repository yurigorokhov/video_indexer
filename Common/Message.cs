using Newtonsoft.Json;
using Amazon.TranscribeService.Model;

namespace My.VideoIndexer.Common {

    public enum MessageType {
        INDEXING_STARTED
    }

    public class ProgressMessage {

        //--- Properties ---
        [JsonProperty("type", Required = Required.Always)]
        public MessageType Type { get; set; }

        [JsonProperty("message", Required = Required.Always)]
        public string Message { get; set; }
    }

    public class IndexingStartedMessage {

        //--- Properties ---
        [JsonProperty("job", Required = Required.Always)]
        public StartTranscriptionJobResponse Job { get; set; }
    }

}