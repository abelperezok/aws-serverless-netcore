# Lesson 04 - Complex input / output and logging

In the previous two lessons we we learned how to successfully deploy our code to real AWS environment and test the function by issuing the invoke command.

The function is so far not doing much, in fact, it's not even accepting any input, let's fix that.

## Allowing some input

At the moment we can only accept input of type Stream in our function, this is due to not having specified any serializer yet, but we'll go later to that part.

Let's add a new parameter to our function, in this case very uncreative name input of type Stream. Then we perform a basic reading via StreamReader just to get the whole content and send it to the output.

```csharp
        public Stream HelloHandler(Stream input)
        {
            StreamReader reader = new StreamReader(input);
            var text = reader.ReadToEnd();
            return new MemoryStream(Encoding.UTF8.GetBytes($"Hello {text}!"));
        }
```

This time, instead of a constant text it will output Hello followed by whatever we input to it. We have two methods available to test our latest changes: sam local and redeploy the stack to AWS following the same procedure as in the last lesson. Let's see both, but first, run ```dotnet publish```.

### Testing on AWS

* Run ```sam package ... ``` command.
* Run ```sam deploy ... ``` command.
* Grab the function name from the stack resources.
* Run this invoke command:
```shell
$ aws lambda invoke \
--function-name project-lambda-HelloLambda-XY7FZSVPIW0K \
--payload '{"name":"Abel"}' \
output.txt \
&& cat output.txt
```
Assuming your function name is 'project-lambda-HelloLambda-XY7FZSVPIW0K', we are passing some payload, which is a JSON to be passed to our function, since we are not doing any parsing, raw JSON will be in the output.

```
{
    "ExecutedVersion": "$LATEST", 
    "StatusCode": 200
}
Hello {"name":"Abel"}!
```

### Testing with SAM local

Alternatively, we can give a try to sam local, like we did in the very first lesson. Unlike the real AWS invoke command, sam doesn't restrict the input to JSON, we can test with any text. Also -n switch is to avoid the line break at the end of the text.

```shell
$ echo -n 'Abel' | sudo sam local invoke HelloLambda
2018-07-31 23:11:54 Reading invoke payload from stdin (you can also pass it from file with --event)
2018-07-31 23:11:54 Invoking project.lambda::project.lambda.Function::HelloHandler (dotnetcore2.1)
2018-07-31 23:11:54 Starting new HTTP connection (1): 169.254.169.254

Fetching lambci/lambda:dotnetcore2.1 Docker container image......
2018-07-31 23:11:56 Mounting /home/abel/serverless-project/src/project.lambda/bin/Debug/netcoreapp2.1/publish as /var/task:ro inside runtime container
START RequestId: 214f87d5-dd42-4f85-ab76-0f65a4856f35 Version: $LATEST
END  RequestId: 214f87d5-dd42-4f85-ab76-0f65a4856f35
REPORT RequestId 214f87d5-dd42-4f85-ab76-0f65a4856f35	Duration: 64 ms	Billed Duration: 100 ms	Memory Size 128 MB	Max Memory Used: 32 MB
Hello Abel!
```

## Adding some logging

Given that our Lambda functions will execute in a headless way, logging is essential for monitoring and telemetry purposes. Lambda runtime provides the mechanisms to log via the ```ILambdaContext``` interface which can be added as an optional parameter to our function.

This parameter provides a Logger property with a Log method receiving a string as parameter, very simple interface. This is how our function looks now.

```csharp
        public Stream HelloHandler(Stream input, ILambdaContext context)
        {
            StreamReader reader = new StreamReader(input);
            var text = reader.ReadToEnd();
            context.Logger.Log($"Received input: {text}");
            return new MemoryStream(Encoding.UTF8.GetBytes($"Hello {text}!"));
        }
```

Retesting using sam local:

```shell
$ echo -n 'Abel' | sudo sam local invoke HelloLambda

# removed some logs for brevity
START RequestId: 6b3bc37e-ffed-48f5-ae40-5cfc5cad1f23 Version: $LATEST
Received input: Abel
END  RequestId: 6b3bc37e-ffed-48f5-ae40-5cfc5cad1f23
# removed some logs for brevity
```

These are the same logs we can find in CloudWatch after the Lambda function has executed, but there is a shorter way to get them thanks to [sam logs](https://github.com/awslabs/aws-sam-cli#fetch-tail-and-filter-lambda-function-logs), it allows to see the logs given a function name and optionally a CloudFormation stack name.

```shell
$ sam logs -n HelloLambda --stack-name project-lambda
```

## Complex type input / output 

Reading from and writing to Streams doesn't seem to be a very clean approach to handle input and output in our function. If we follow that path we'll need also to manually handle serialization / deserialization process. It would be much easier if we can receive an specific object and return another object.

For this example we'll use the following classes to represent input and output.

```csharp
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
```

Before we can actually use them in our function, there are two steps to be done:

1- Set the serializer, as per AWS recommendation, we should set it at assembly level and if we wanted to change it for a specific function, it can be overridden at method level.

```csharp
// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
```

2- Add a reference to ```Amazon.Lambda.Serialization.Json``` package where the serializer is defined. 

```shell
$ dotnet add package Amazon.Lambda.Serialization.Json
```

The project file should look like this after adding the package.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netcoreapp2.1</TargetFramework>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.Core" Version="1.0.0" />
    <PackageReference Include="Amazon.Lambda.Serialization.Json" Version="1.3.0" />
  </ItemGroup>

</Project>
```

Let's put everything together with a bit of modification in the function itself, this time we'll receive a LambdaInput instance and return a LambdaOutput. Add some logging too.

```csharp
using System.IO;
using System.Text;
using Amazon.Lambda.Core;

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
        public LambdaOutput HelloHandler(LambdaInput input, ILambdaContext context)
        {
            context.Logger.Log($"Hello {input.Name}, you are now {input.Age}");
            return new LambdaOutput { Name = input.Name, Old = input.Age > 50 };
        }
    }
}
```

Once rebuilt and published, we can invoke and see the output.

```shell
$ aws lambda invoke --function-name project-lambda-HelloLambda-XY7FZSVPIW0K \
--payload '{"Name":"Abel","Age":"33"}' output.txt \
&& cat output.txt
{
    "ExecutedVersion": "$LATEST", 
    "StatusCode": 200
}
{"Name":"Abel","Old":false}
```

And we see function executing and if we want the logs, we can use again ```sam logs```

```shell
$ sam logs -n HelloLambda --stack-name project-lambda
```