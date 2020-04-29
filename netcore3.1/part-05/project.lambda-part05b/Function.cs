using System.IO;
using System.Text;
using Amazon.Lambda.Core;

namespace project.lambda
{
    public class Function
    {
	    public Stream HelloHandler(Stream input, ILambdaContext context)
        {
            StreamReader reader = new StreamReader(input);
            var text = reader.ReadToEnd();
            context.Logger.LogLine($"Received input: {text}");
            return new MemoryStream(Encoding.UTF8.GetBytes($"Hello {text}!"));
        }
    }
}
