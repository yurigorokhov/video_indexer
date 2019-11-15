using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json;

namespace My.VideoIndexer.Common {
    public class IndexingStatus {

        //--- Properties ---
        public string VideoEtag { get; set; }
        public string VideoS3Key { get; set; }
        public string TranscriptionS3Key { get; set; }
    }

    public class IndexingStatusTable {

        //--- Fields ---
        private readonly IAmazonDynamoDB _dynamoDbClient;
        private readonly Table _table;

        //--- Constructors ---
        public IndexingStatusTable(
            string tableName,
            IAmazonDynamoDB dynamoDbClient
        ) {
            _dynamoDbClient = dynamoDbClient ?? throw new ArgumentNullException(nameof(dynamoDbClient));
            _table = Table.LoadTable(
                _dynamoDbClient,
                tableName ?? throw new ArgumentNullException(nameof(tableName))
            );
        }

        //--- Methods ---
        public Task PutRowAsync<T>(T record) => _table.PutItemAsync(Document.FromJson(JsonConvert.SerializeObject(record)));

        public async Task<T> GetRowAsync<T>(string id) {
            var record = await _table.GetItemAsync(id);
            return (record != null)
                ? JsonConvert.DeserializeObject<T>(record.ToJson())
                : default;
        }
        
        public Task UpdateRowAsync<T>(T record) => _table.UpdateItemAsync(Document.FromJson(JsonConvert.SerializeObject(record)));
    }
}