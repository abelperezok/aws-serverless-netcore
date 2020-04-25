# Part 1 - Creating a Lambda Function from scratch

Let's start with the creation of our project folder, assuming we are in home directory.

```shell
$ mkdir serverless-project
$ cd serverless-project
```
This will be the folder where we'll store all our code projects. Now, create the traditional ```src``` and jump there.

```shell
$ mkdir src
$ cd src
```

## Create a class library project

The following command will create a standard .NET class library project which is nothing more than a .csproj and a sample class file. Let's name it ```project.lambda``` and change directory to the new project.

```shell
$ dotnet new classlib -f netcoreapp3.1 -o project.lambda

The template "Class library" was created successfully.

Processing post-creation actions...
Running 'dotnet restore' on project.lambda/project.lambda.csproj...
  Restore completed in 131.98 ms for /home/abel/serverless-project/src/project.lambda/project.lambda.csproj.

Restore succeeded.

$ cd project.lambda
```
This command should have created a file named Class1.cs which we don't need, it can be removed or replaced with the content below.

```shell
$ rm Class1.cs
```
## Add Lambda code

To create a Lambda function, all we need is a .NET class with a public method that returns a Stream.

In this case, no input is accepted and it will only return a "Hello Lambda!" message, typical hello world example. Let's create the class.

```shell
$ cat > Function.cs << EOF
using System.IO;
using System.Text;

namespace project.lambda
{
    public class Function
    {
        public Stream HelloHandler()
        {
            return new MemoryStream(Encoding.UTF8.GetBytes($"Hello Lambda!"));
        }
    }
}
EOF
```

## Package the code

Before packaging the code, there is one dependency to be added ```Amazon.Lambda.Core```, without it, Lambda execution will result an a mysterious runtime error, that not even enabling the debug flag will be explicit. 

```shell
$ dotnet add package Amazon.Lambda.Core
...
info : PackageReference for package 'Amazon.Lambda.Core' version '1.1.0' added to file '/home/abel/serverless-project/src/project.lambda/project.lambda.csproj'.
...
```

We can verify it builds by running dotnet build command. 

```shell
$ dotnet build
```

However, to be able to run this project as a Lambda function, we need to package all the dependencies, since they won't be present in the runtime environment. We can do that by running dotnet publish command and also including the flag ```GenerateRuntimeConfigurationFiles``` set to true so all runtime configuration json files are produced with the output. 

At this point, we are not concerned about debug or release configuration or runtime, so it'll default to debug.

```shell
$ dotnet publish /p:GenerateRuntimeConfigurationFiles=true

```
Let's have a look at our bin directory using the tree command after running both dotnet build and publish.
```shell
$ tree bin
bin
└── Debug
    └── netcoreapp3.1
        ├── project.lambda.deps.json
        ├── project.lambda.dll
        ├── project.lambda.pdb
        ├── project.lambda.runtimeconfig.dev.json
        ├── project.lambda.runtimeconfig.json
        └── publish
            ├── Amazon.Lambda.Core.dll
            ├── project.lambda.deps.json
            ├── project.lambda.dll
            ├── project.lambda.pdb
            └── project.lambda.runtimeconfig.json

3 directories, 10 files
```
All necessary files to run the lambda function will be inside publish directory.

## Create SAM template

At this point, we can test locally this new Lambda code. To do that, we need to create a [SAM template](https://github.com/awslabs/serverless-application-model) file with the following content or run this command:

```shell
cat > template.yaml << EOF
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31
Description: >
    Sample SAM Template

Globals:
  Function:
    Timeout: 60
    Runtime: dotnetcore3.1
    CodeUri: bin/Debug/netcoreapp3.1/publish/

Resources:
    HelloLambda:
      Type: AWS::Serverless::Function
      Properties:
        Handler: project.lambda::project.lambda.Function::HelloHandler

EOF
```

This template contains three blocks that we're interested in:
* Header: Includes format version and transform specification as well as a description.
* Globals: Includes common properties for all functions in this case.
* Resources: Includes all AWS resources that will be part of our application. For now, only a Serverless::Function.

Important points about this first template:
* Runtime is set to ```dotnetcore3.1```
* CodeUri points to our ```publish``` directory where it will expect to find all our files.
* Handler is expressed using the format ```Assembly::Namespace.ClassName::MethodName```

## Test the function locally using SAM local invoke

With the compiled assemblies and the template in place, we can invoke the function using ```sam cli``` to see the output.

```shell
$ echo '' | sam local invoke HelloLambda
Invoking project.lambda::project.lambda.Function::HelloHandler (dotnetcore3.1)

Fetching lambci/lambda:dotnetcore3.1 Docker container image......
Mounting /home/abel/serverless-project/src/project.lambda/bin/Debug/netcoreapp3.1/publish as /var/task:ro,delegated inside runtime container
START RequestId: 3e4e8714-70bb-118c-672c-216bcba88e32 Version: $LATEST
END RequestId: 3e4e8714-70bb-118c-672c-216bcba88e32
REPORT RequestId: 3e4e8714-70bb-118c-672c-216bcba88e32	Init Duration: 296.65 ms	Duration: 8.35 ms	Billed Duration: 100 ms	Memory Size: 128 MB	Max Memory Used: 36 MB	

Hello Lambda!
```

We can see at the very end of the console output, the message returned by the Lambda function.

The first time we run this command docker will download the image for the corresponding runtime and it will take longer, subsequent invocations won't take that long.

## Test the function locally using SAM Lambda local endpoint

Another way to take advantage of SAM local is to use Lambda local endpoint which runs a replica of the Lambda API locally, that means that we can use the exact same AWS CLI Lambda API calls only changing the endpoint url to the local one created by sam.

First we start the Lambda local endpoint (optionally we can send it to background) which by default will listen on localhost:3001.

```shell
$ sam local start-lambda &

Starting the Local Lambda Service. You can now invoke your Lambda Functions defined in your template through the endpoint.
2020-04-25 16:38:25  * Running on http://127.0.0.1:3001/ (Press CTRL+C to quit)
```

Now, using the Lambda CLI we call invoke as we would to test a lambda, but overriding the url endpoint. Next we examine the log.txt file et voilà!

```shell
$ aws lambda invoke --function-name HelloLambda --endpoint-url=http://127.0.0.1:3001 log.txt
$ cat log.txt 
Hello Lambda!
```
Tear down the endpoint, assuming that was the background process 1, let's bring it back to foreground and Ctrl+C to stop it.

```shell
$ fg 1
sudo sam local start-lambda
^C
```

We've just discovered what happens behind the scenes when we invoke Lambda functions.