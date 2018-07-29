# Deploy to AWS environment - AWS CLI

In the previous lesson we created the bare minimum code to make a Lambda function to execute locally using sam cli, it worked on simulated environment, but will it work when deployed to real AWS environment ?

We can achieve this in two different ways: using purely AWS CLI or using SAM CLI. In this lesson we'll see how to deploy to AWS using purely AWS CLI.

The quick answer to the previous question is NO. There are still a few things we need to adjust in our project file before it can be used in a real environment.

## Preparing to use AWS CLI

After some try and error process followed by some research, there are three things to be changed in the project file:

* **TargetFramework** should be **netcoreapp2.1** instead of netstandard2.0, this creates a dependency with **Microsoft.NETCore.App** which is required by the Lambda runtime.
* **GenerateRuntimeConfigurationFiles** has to be explicitly set to true in order to generate the **.runtimeconfig.json** file required by .net core runtime.
* **Amazon.Lambda.Core** needs to be referenced so it's included in the package when we run ```dotnet publish``` command.

There an interesting post about the technicalities on [how .net core works and references the runtime libraries](https://natemcmaster.com/blog/2017/12/21/netcore-primitives/). Let's fix these issues, first replace the first block &lt;PropertyGroup&gt; with the new values.

```xml
<TargetFramework>netcoreapp2.0</TargetFramework>
<GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
```

Now, add a reference to Amazon.Lambda.Core, even if at this point we are not explicitly using any of its classes, but we'll use them anyway in future lessons.

```shell
$ dotnet add package Amazon.Lambda.Core
```

The file project.lambda.csproj should look like this:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netcoreapp2.1</TargetFramework>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.Core" Version="1.0.0" />
  </ItemGroup>

</Project>
```

After this, let's run again ```dotnet publish```. This time if we inspect the files created, we can see the new files. All the following commands will assume we are in the project.lambda directory previously created.

```shell
$ tree bin
bin
└── Debug
    └── netcoreapp2.1
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

## Creating IAM Role for Lambda

AWS provides a powerful command line interface to interact with the resources we provision. In the case of Lambda, we should use [aws lambda create-function](https://docs.aws.amazon.com/cli/latest/reference/lambda/create-function.html) but because of the dependency with other resources required by Lambda, we can't use that command just yet.

Every Lambda function needs a IAM Role (mechanism to get temporary credentials to access resources on our behalf) defining what level of permissions our function will have.

At the very minimum, a Lambda function needs to write to CloudWatch logs, for this kind of scenarios AWS have prepared a ready-made policy (AWSLambdaExecute). To create a role, first we need to stablish the trust relationship policy document and then attach the policy document specifying the permissions that whoever is assuming that role will get.

This is the trust relationship policy document that grants Lambda service the AssumeRole action on our role.

```shell
$ read -r -d '' ROLE_POLICY_DOCUMENT << EOM
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Service": "lambda.amazonaws.com"
      },
      "Action": "sts:AssumeRole"
    }
  ]
}
EOM
```

Now, we can create the role using the variable previously created.

```shell
aws iam create-role \
--role-name HelloLambdaRole \
--assume-role-policy-document "$ROLE_POLICY_DOCUMENT"
```
This command should output a JSON with all the information about the newly created role. We'll take note of the Arn as we'll need it in the next step to attach the policy.

```json
{
    "Role": {
        "AssumeRolePolicyDocument": {
            "Version": "2012-10-17", 
            "Statement": [
                {
                    "Action": "sts:AssumeRole", 
                    "Effect": "Allow", 
                    "Principal": {
                        "Service": "lambda.amazonaws.com"
                    }
                }
            ]
        }, 
        "RoleId": "AROAIEE3OIPFY2EVRPRWO", 
        "CreateDate": "2018-07-28T19:48:10.356Z", 
        "RoleName": "HelloLambdaRole", 
        "Path": "/", 
        "Arn": "arn:aws:iam::123123123123:role/HelloLambdaRole"
    }
}
```

With the role being created, let's attach the policy, in this case we'll use a managed policy provided by Amazon ```arn:aws:iam::aws:policy/AWSLambdaExecute```.

```shell
$ aws iam attach-role-policy \
--policy-arn arn:aws:iam::aws:policy/AWSLambdaExecute \
--role-name HelloLambdaRole
```

Any Lambda function with this role will be able to perform basic operations, but very limited.

## Creating AWS Lambda Function

We need a way to provide the published code to Lambda so it knows where it is and what's in to execute. It can be in two different ways: a zip file we provide with the function or a reference to a zip file in a S3 bucket. In this case it's a small package and can be provided with the function. 

It's important to note that Lambda runtime expects a flat directory structure, meaning all the files must be at the top level inside the zip, hence the -j in the next command

```shell
$ zip -j lambda bin/Debug/netcoreapp2.1/publish/*

  adding: Amazon.Lambda.Core.dll (deflated 57%)
  adding: project.lambda.deps.json (deflated 70%)
  adding: project.lambda.dll (deflated 62%)
  adding: project.lambda.pdb (deflated 26%)
  adding: project.lambda.runtimeconfig.json (deflated 23%)
```

This creates a file named lambda.zip in the current directory. Now we are good to go to create the Lambda function on AWS.

```shell
$ aws lambda create-function \
--function-name HelloLambda \
--zip-file fileb://lambda.zip \
--role arn:aws:iam::123123123123:role/HelloLambdaRole \
--handler project.lambda::project.lambda.Function::HelloHandler \
--runtime dotnetcore2.1

{
    "TracingConfig": {
        "Mode": "PassThrough"
    }, 
    "CodeSha256": "L99AF0yByriXCnUIQrRpLdJ9ZOHxPCOzrbIwiw7dnRc=", 
    "FunctionName": "HelloLambda", 
    "CodeSize": 7023, 
    "RevisionId": "b3217387-576d-4bf6-81fe-09c7702b23d2", 
    "MemorySize": 128, 
    "FunctionArn": "arn:aws:lambda:eu-west-1:123123123123:function:HelloLambda", 
    "Version": "$LATEST", 
    "Role": "arn:aws:iam::123123123123:role/HelloLambdaRole", 
    "Timeout": 3, 
    "LastModified": "2018-07-29T22:07:49.929+0000", 
    "Handler": "project.lambda::project.lambda.Function::HelloHandler", 
    "Runtime": "dotnetcore2.1", 
    "Description": ""
}
```

The handler is exactly the same we used in the template.yaml and the role is the ARN from the command that created it.

## Invoking the function

To invoke a Lambda function on AWS we use the ```lambda invoke``` command, in this case, no payload is required and output.txt is the file that will contain the function result.

```shell
$ aws lambda invoke \
--function-name HelloLambda \
output.txt

{
    "ExecutedVersion": "$LATEST", 
    "StatusCode": 200
}
```
Now let's inspect the output file:

```shell
$ cat output.txt 
Hello Lambda!
```

Finally we have a basic Lambda function (for real) with the minimum amount of code and dependencies.

## Cleaning up

Since we've manually created these resources we need to clean after ourselves the same way. Let's start by deleting the Lambda function.

```shell
$ aws lambda delete-function \
--function-name HelloLambda
```

We also need to delete the role, but first it's required to detach all the policies. This is the AWS CLI error when trying to delete the role.

```
An error occurred (DeleteConflict) when calling the DeleteRole operation: Cannot delete entity, must detach all policies first.
```
Detach the policy.
```shell
$ aws iam detach-role-policy \
--role-name HelloLambdaRole \
--policy-arn arn:aws:iam::aws:policy/AWSLambdaExecute
```
Delete the role.
```shell
$ aws iam delete-role --role-name HelloLambdaRole
```

We can run this command to verify that it's actually gone.
```shell
$ aws iam list-roles --query Roles[*].RoleName
```