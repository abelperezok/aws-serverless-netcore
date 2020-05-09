using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace project.lambda
{
    public class DynamoDbRepository
    {
        private IAmazonDynamoDB _dynamoDbClient = new AmazonDynamoDBClient();
        private const string PKPrefix = "ITEM";
        protected string SKPrefix = "METADATA";
        private string _tableName;

        public DynamoDbRepository(string tableName)
        {
            _tableName = tableName;
        }


        public async Task<List<Item>> GetItems()
        {
            var queryRequest = new QueryRequest
            {
                TableName = _tableName,
                IndexName = "GSI1",
                KeyConditionExpression = $"GSI1 = :pk_prefix",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":pk_prefix", new AttributeValue(PKPrefix) } }
            };

            var queryResponse = await _dynamoDbClient.QueryAsync(queryRequest);
            return queryResponse.Items.Select(FromDynamoDb).ToList();
        }

        public async Task<bool> AddItem(Item item)
        {
            var putItemRequest = new PutItemRequest
            {
                TableName = _tableName,
                Item = ToDynamoDb(item)
            };
            var result = await _dynamoDbClient.PutItemAsync(putItemRequest);
            return result.HttpStatusCode == HttpStatusCode.OK;
        }

        public async Task<bool> RemoveItem(string itemId)
        {
            var deleteRequest = new DeleteItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue(PKPrefix + "#" + itemId) },
                    { "SK", new AttributeValue(SKPrefix + "#" + itemId) },
                }
            };

            var result = await _dynamoDbClient.DeleteItemAsync(deleteRequest);
            return result.HttpStatusCode == HttpStatusCode.OK;
        }

        private Item FromDynamoDb(Dictionary<string, AttributeValue> item)
        {
            var result = new Item();
            result.Id = item["Id"].S;
            result.Name = item["Name"].S;
            return result;
        }

        private Dictionary<string, AttributeValue> ToDynamoDb(Item item)
        {
            var dbItem = new Dictionary<string, AttributeValue>();
            dbItem.Add("PK", new AttributeValue(PKPrefix + "#" + item.Id));
            dbItem.Add("SK", new AttributeValue(SKPrefix + "#" + item.Id));

            dbItem.Add("Id", new AttributeValue(item.Id));
            dbItem.Add("Name", new AttributeValue(item.Name));

            // for GSI query all 
            dbItem.Add("GSI1", new AttributeValue(PKPrefix));
            return dbItem;
        }

    }
}