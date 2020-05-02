using System;
using System.Collections.Generic;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

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
        //Sample input to test the lambda function in isolation 
        //{"QueryStringParameters": {"name":"Abel","age":"33"}} 

        public APIGatewayProxyResponse HelloHandler(APIGatewayProxyRequest apigProxyEvent, ILambdaContext context)
        {
            // Some validation here as it will blow up 
            // if query string params are not supplied !
            
            var input = new LambdaInput { 
                Name = apigProxyEvent.QueryStringParameters["name"], 
                Age =  Convert.ToInt32(apigProxyEvent.QueryStringParameters["age"])
            };

            context.Logger.LogLine($"Hello {input.Name}, you are now {input.Age}");

            var output = new LambdaOutput { Name = input.Name, Old = input.Age > 50 };

            // Logic that used to be on the API integration response template
            var apiOutput = new { Message = $"Dear {output.Name}, you are {(output.Old ? "": "not ")}old." };
            
            return new APIGatewayProxyResponse
            {
                Body = JsonSerializer.Serialize(apiOutput),
                StatusCode = 200,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
    }
}
