# Part 5 - Complex input / output and logging

In the previous two parts we learned how to successfully deploy our code to real AWS environment and test the function by issuing the invoke command.

The function is so far not doing much, in fact, it's not even accepting any input, let's fix that.

## Allowing some input

At the moment we can only accept input of type Stream in our function, this is due to not having specified any serializer yet, but we'll go there later.

Let's add a new parameter to our function, in this case very uncreative name input of type Stream. Then we perform a basic reading via StreamReader just to get the whole content and send it to the output.

```csharp
        public Stream HelloHandler(Stream input)
        {
            StreamReader reader = new StreamReader(input);
            var text = reader.ReadToEnd();
            return new MemoryStream(Encoding.UTF8.GetBytes($"Hello {text}!"));
        }
```

This time, instead of a constant text it will output Hello followed by whatever we input to it. We have three methods available to test our latest changes: dotnet lambda, AWS CLI and sam local.

### Testing on AWS

Let's do a quick deployment using dotnet lambda get it ready on the environment.

#### Deploy to AWS

As seen in the previous part, we'll use ```dotnet lambda``` to deploy to AWS.
```shell
$ dotnet lambda deploy-serverless --config-file serverless-config.json
```

Don't forget to grab the actual function name from the CloudFormation stack.
```shell
$ aws cloudformation describe-stack-resources --stack-name project-lambda \
--query StackResources[*].[ResourceType,PhysicalResourceId]

[
    [
        "AWS::Lambda::Function",
        "project-lambda-HelloLambda-1N5DP8MQFLJHT"
    ],
    [
        "AWS::IAM::Role",
        "project-lambda-HelloLambdaRole-BDBXWNSOMLUZ"
    ]
]
```

#### Invoke using dotnet lambda

Now invoke the function.
```shell    
$ dotnet lambda invoke-function \
--function-name project-lambda-HelloLambda-1N5DP8MQFLJHT \
--payload '{"name":"Abel"}' \
--region eu-west-1

Amazon Lambda Tools for .NET Core applications (4.0.0)
Project Home: https://github.com/aws/aws-extensions-for-dotnet-cli, https://github.com/aws/aws-lambda-dotnet
	
Payload:
Hello {"name":"Abel"}!

Log Tail:
START RequestId: eadedd76-b8ff-4926-a8b0-0b487b055f02 Version: $LATEST
END RequestId: eadedd76-b8ff-4926-a8b0-0b487b055f02
REPORT RequestId: eadedd76-b8ff-4926-a8b0-0b487b055f02	Duration: 74.94 ms	Billed Duration: 100 ms	Memory Size: 128 MB	Max Memory Used: 58 MB	Init Duration: 162.36 ms	
```

Assuming your function name is 'project-lambda-HelloLambda-1N5DP8MQFLJHT', we are passing some payload, which is a JSON to be passed to our function, since we are not doing any parsing, raw JSON is displayed in the output (after Payload in the output).

If instead, the payload was just a string text, it's automatically converted to a JSON string and that gives us some surprising quotes in the output. Here are some examples:

```shell
--payload "Abel"
--payload 'Abel'
--payload Abel

# will output Payload 
Hello "Abel"!
```

#### Invoke using AWS CLI

When testing with AWS CLI, the experience is a bit different, the payload is forced to be a JSON and on top of that it has to be in base 64 format, which is something that at least as of version "aws-cli/2.0.7" is not clear in the documentation.

Following the same example above, to make it compatible with AWS CLI

```shell
echo -n '{ "name": "Abel" }' | base64
```
Now, we just insert the previous command into the aws lambda invoke as the --payload argument.

```shell
$ aws lambda invoke \
--function-name project-lambda-HelloLambda-1N5DP8MQFLJHT \
--payload $(echo -n '{ "name": "Abel" }' | base64) output.txt \
&& cat output.txt

{
    "StatusCode": 200,
    "ExecutedVersion": "$LATEST"
}
Hello { "name": "Abel" }!
```

**Note here** that payload has to be a valid JSON, if for example, we supply the payload argument like this:

```shell
--payload $(echo -n "Abel" | base64)
```

We'll get an error like this one:

```shell
An error occurred (InvalidRequestContentException) when calling the Invoke operation: Could not parse request body into json: Unrecognized token 'Abel': was expecting ('true', 'false' or 'null')
 at [Source: (byte[])"Abel"; line: 1, column: 9]
```

### Testing with SAM local

Alternatively, we can give a try to sam local, like we did in the very first part. Unlike the real AWS invoke command, sam doesn't restrict the input to JSON, we can test with any text. Also -n switch is to avoid the line break at the end of the text.

It's important to note that the parameter -e requires an additional - to indicate that the input is coming from stdin and not from a file. 

```shell
$ echo -n 'Abel' | sam local invoke -e - HelloLambda
Reading invoke payload from stdin (you can also pass it from file with --event)
Invoking project.lambda::project.lambda.Function::HelloHandler (dotnetcore3.1)

Fetching lambci/lambda:dotnetcore3.1 Docker container image......
Mounting /home/abel/serverless-project/src/project.lambda/bin/Debug/netcoreapp3.1/publish as /var/task:ro,delegated inside runtime container
START RequestId: 62510191-2d2c-1932-7545-546550ca8f14 Version: $LATEST
END RequestId: 62510191-2d2c-1932-7545-546550ca8f14
REPORT RequestId: 62510191-2d2c-1932-7545-546550ca8f14	Init Duration: 274.98 ms	Duration: 8.23 ms	Billed Duration: 100 ms	Memory Size: 128 MB	Max Memory Used: 36 MB	

Hello Abel!
```

## Adding some logging

Given that our Lambda functions will execute in a headless way, logging is essential for monitoring and telemetry purposes. Lambda runtime provides the mechanisms to log via the ```ILambdaContext``` interface which can be added as an optional parameter to our function.

This parameter provides a Logger property with methods Log and LogLine receiving a string as parameter, very simple interface. This is how our function looks now. A using statement is also required ```using Amazon.Lambda.Core;```

```csharp
        public Stream HelloHandler(Stream input, ILambdaContext context)
        {
            StreamReader reader = new StreamReader(input);
            var text = reader.ReadToEnd();
            context.Logger.LogLine($"Received input: {text}");
            return new MemoryStream(Encoding.UTF8.GetBytes($"Hello {text}!"));
        }
```

Retesting using sam local:

```shell
$ echo -n 'Abel' | sam local invoke -e - HelloLambda

# removed some logs for brevity
START RequestId: 4253a6a8-cce8-184c-bea2-9e54e88713fa Version: $LATEST
Received input: Abel
END RequestId: 4253a6a8-cce8-184c-bea2-9e54e88713fa
REPORT RequestId: 4253a6a8-cce8-184c-bea2-9e54e88713fa	
# removed some logs for brevity
```

These are the same logs we can find in CloudWatch after the Lambda function has executed, but there is a shorter way to get them thanks to [sam logs](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/sam-cli-command-reference-sam-logs.html), it allows to see the logs given a function name and optionally a CloudFormation stack name.

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
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
```

2- Add a reference to ```Amazon.Lambda.Serialization.SystemTextJson``` package where the serializer is defined. 

```shell
$ dotnet add package Amazon.Lambda.Serialization.SystemTextJson
```

The project file should look like this after adding the package.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netcoreapp3.1</TargetFramework>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.Core" Version="1.1.0" />
    <PackageReference Include="Amazon.Lambda.Serialization.SystemTextJson" Version="2.0.0" />
  </ItemGroup>

</Project>
```

Let's put everything together with a bit of modification in the function itself, this time we'll receive a LambdaInput instance and return a LambdaOutput. As well as adding some logging to the output.

```csharp
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
```
Once rebuilt and republished, we can invoke and see the changes in the output.

A sample input would be in the following format, as per the input class defined above:

```json
{"Name":"Abel","Age":33}
```

**Note** that the Age as a numeric value is expressed without quotes. Now, let's invoke sam local with this sample input.

### Test locally using SAM local

Notice the extra - after the -e parameter.

```shell
$ echo -n '{"Name":"Abel","Age":33}' | sam local invoke -e - HelloLambda

Reading invoke payload from stdin (you can also pass it from file with --event)
Invoking project.lambda::project.lambda.Function::HelloHandler (dotnetcore3.1)

Fetching lambci/lambda:dotnetcore3.1 Docker container image......
Mounting /home/abel/serverless-project/src/project.lambda/bin/Debug/netcoreapp3.1/publish as /var/task:ro,delegated inside runtime container
START RequestId: e3fa51ce-86fb-17fb-ed98-9c08f358f085 Version: $LATEST
Hello Abel, you are now 33
END RequestId: e3fa51ce-86fb-17fb-ed98-9c08f358f085
REPORT RequestId: e3fa51ce-86fb-17fb-ed98-9c08f358f085	Init Duration: 284.82 ms	Duration: 26.11 ms	Billed Duration: 100 ms	Memory Size: 128 MB	Max Memory Used: 39 MB	

{"Name":"Abel","Old":false}
```

### Test on AWS using dotnet lambda

```shell
$ dotnet lambda invoke-function \
--function-name project-lambda-HelloLambda-1N5DP8MQFLJHT \
--payload '{"Name":"Abel","Age":33}' \
--region eu-west-1

Amazon Lambda Tools for .NET Core applications (4.0.0)
Project Home: https://github.com/aws/aws-extensions-for-dotnet-cli, https://github.com/aws/aws-lambda-dotnet
	
Payload:
{"Name":"Abel","Old":false}

Log Tail:
START RequestId: acbd54c2-efa1-43ac-ab36-cf35b6169360 Version: $LATEST
Hello Abel, you are now 33
END RequestId: acbd54c2-efa1-43ac-ab36-cf35b6169360
REPORT RequestId: acbd54c2-efa1-43ac-ab36-cf35b6169360	Duration: 105.31 ms	Billed Duration: 200 ms	Memory Size: 128 MB	Max Memory Used: 67 MB	
```


### Test on AWS using AWS CLI

```shell
$ aws lambda invoke \
--function-name project-lambda-HelloLambda-1N5DP8MQFLJHT \
--payload $(echo -n '{"Name":"Abel","Age":33}' | base64) output.txt \
&& cat output.txt

{
    "StatusCode": 200,
    "ExecutedVersion": "$LATEST"
}
{"Name":"Abel","Old":false}
```

#### Checking logs using AWS logs

First, get the log stream name given the lambda function name, the log group names usually go in the form of "/aws/lambda/lambda-function-name" 

```shell
$ aws logs describe-log-streams \
--log-group-name /aws/lambda/project-lambda-HelloLambda-1N5DP8MQFLJHT \
--max-items 1 --descending \
--query logStreams[0].logStreamName 

"2020/04/29/[$LATEST]95f5b75312d74e2ebb01814db08440d7"
```

With that value we can now request the logs events for that particular stream.


```shell
$ aws logs get-log-events \
--log-group-name /aws/lambda/project-lambda-HelloLambda-1N5DP8MQFLJHT \
--log-stream-name '2020/04/29/[$LATEST]9189b2a8f9174e2f8717fce480f8c63d'

{
    "events": [
        {
            "timestamp": 1588165714132,
            "message": "START RequestId: e911b1db-a0b3-4d3a-989d-05300dc7aec9 Version: $LATEST\n",
            "ingestionTime": 1588165723192
        },
        {
            "timestamp": 1588165714445,
            "message": "Hello Abel, you are now 33\n",
            "ingestionTime": 1588165723192
        },
        {
            "timestamp": 1588165714565,
            "message": "END RequestId: e911b1db-a0b3-4d3a-989d-05300dc7aec9\n",
            "ingestionTime": 1588165723192
        },
        {
            "timestamp": 1588165714565,
            "message": "REPORT RequestId: e911b1db-a0b3-4d3a-989d-05300dc7aec9\tDuration: 432.64 ms\tBilled Duration: 500 ms\tMemory Size: 128 MB\tMax Memory Used: 62 MB\tInit Duration: 164.98 ms\t\n",
            "ingestionTime": 1588165723192
        }
    ],
    "nextForwardToken": "f/35417278933556379891531773690545303018720618995118702595",
    "nextBackwardToken": "b/35417278923900157220568013870260337006663878463029182464"
}
```

#### Checking logs using SAM logs

As seen earlier, we can see the logs in a more straightforward way.

```shell
$ sam logs -n HelloLambda --stack-name project-lambda

2020/04/29/[$LATEST]9189b2a8f9174e2f8717fce480f8c63d 2020-04-29T13:08:34.132000 START RequestId: e911b1db-a0b3-4d3a-989d-05300dc7aec9 Version: $LATEST
2020/04/29/[$LATEST]9189b2a8f9174e2f8717fce480f8c63d 2020-04-29T13:08:34.445000 Hello Abel, you are now 33
2020/04/29/[$LATEST]9189b2a8f9174e2f8717fce480f8c63d 2020-04-29T13:08:34.565000 END RequestId: e911b1db-a0b3-4d3a-989d-05300dc7aec9
2020/04/29/[$LATEST]9189b2a8f9174e2f8717fce480f8c63d 2020-04-29T13:08:34.565000 REPORT RequestId: e911b1db-a0b3-4d3a-989d-05300dc7aec9	Duration: 432.64 ms	Billed Duration: 500 ms	Memory Size: 128 MB	Max Memory Used: 62 MB	Init Duration: 164.98 ms
```

