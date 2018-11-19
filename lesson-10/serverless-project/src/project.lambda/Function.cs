using System.Collections.Generic;
using System.IO;
using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace project.lambda
{
    public class LambdaInput
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    public class LambdaOutput
    {
        public string Name { get; set; }
        public bool Old { get; set; }
    }

    public class Function
    {
        public APIGatewayProxyResponse HelloHandler(APIGatewayProxyRequest apigProxyEvent, ILambdaContext context)
        {
            var input = JsonConvert.DeserializeObject<LambdaInput>(apigProxyEvent.Body);

            context.Logger.Log($"Hello {input.Name}, you are now {input.Age}");

            var output = new LambdaOutput { Name = input.Name, Old = input.Age > 50 };

            var apiOutput = new { Message = $"Dear {output.Name}, you are {(output.Old ? "": "not ")}old." };

            return new APIGatewayProxyResponse
            {
                Body = JsonConvert.SerializeObject(apiOutput),
                StatusCode = 200,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
    }
}