using System.IO;
using System.Text;
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
	    public LambdaOutput HelloHandler(LambdaInput input, ILambdaContext context)
        {
            context.Logger.LogLine($"Hello {input.Name}, you are now {input.Age}");
            return new LambdaOutput { Name = input.Name, Old = input.Age > 50 };
        }
    }
}
