using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace project.lambda
{
    public class Item
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
    }

    public class Function
    {
        private const string TableName = "TableName";

        public async Task<List<Item>> GetHandler(ILambdaContext context)
        {
            var tableName = Environment.GetEnvironmentVariable(TableName);
            context.Logger.LogLine($"Using table {tableName}");

            var repository = new DynamoDbRepository(tableName);
            return await repository.GetItems();
        }

        public async Task<Result> PutHandler(Item item, ILambdaContext context)
        {
            var tableName = Environment.GetEnvironmentVariable(TableName);
            context.Logger.LogLine($"Using table {tableName}");

            var repository = new DynamoDbRepository(tableName);

            var result = await repository.AddItem(item);
            context.Logger.LogLine($"Result = {result}");
            
            return new Result { Success = result };
        }

        public async Task<Result> DeleteHandler(string id, ILambdaContext context)
        {
            var tableName = Environment.GetEnvironmentVariable(TableName);
            context.Logger.LogLine($"Using table {tableName}");

            var repository = new DynamoDbRepository(tableName);

            var result = await repository.RemoveItem(id);
            context.Logger.LogLine($"Result = {result}");

            return new Result { Success = result };
        }
    }
}
