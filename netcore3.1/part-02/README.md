# Part 2 - Deploy to AWS environment - AWS CLI

In the previous part we created the bare minimum code to make a Lambda function to execute locally using sam cli, it worked on simulated environment, but will it work when deployed to real AWS environment ?

We can achieve this in two different ways: using purely AWS CLI or using SAM CLI. In this part we'll see how to deploy to AWS using purely AWS CLI.

With previous versions of SAM local, the question was a clear NO, but more recent versions are creating a more realistic local environment that actually replicates what to expect when our code is deployed to a real lambda.

## Preparing to use AWS CLI

Here are a few tweaks to make our life easier:

* Make sure that **TargetFramework** is set to **netcoreapp3.1** instead of netstandard*, this creates a dependency with **Microsoft.NETCore.App** which is required by the Lambda runtime.
* Add **GenerateRuntimeConfigurationFiles** property to the .csproj file set to true in order to generate the **.runtimeconfig.json** file required by .net core runtime.
* Make sure that a reference to **Amazon.Lambda.Core** is added so it's included in the package when we run ```dotnet publish``` command.

There an interesting post about the technicalities on [how .net core works and references the runtime libraries](https://natemcmaster.com/blog/2017/12/21/netcore-primitives/). 

The file project.lambda.csproj should look like this:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netcoreapp3.1</TargetFramework>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.Core" Version="1.1.0" />
  </ItemGroup>

</Project>
```

After this, let's run again ```dotnet publish``` without the /p:GenerateRuntimeConfigurationFiles=true. This time if we inspect the files created, we can see all the files. 

All the following commands will assume we are in the project.lambda directory previously created.

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

## Creating IAM Role for Lambda

AWS provides a powerful command line interface to interact with the resources we provision. In the case of Lambda, we should use [aws lambda create-function](https://docs.aws.amazon.com/cli/latest/reference/lambda/create-function.html) but because of the dependency with other resources required by Lambda, we can't use that command just yet.

Every Lambda function needs a IAM Role (mechanism to get temporary credentials to access resources on our behalf) defining what level of permissions our function will have.

At the very minimum, a Lambda function will write to CloudWatch logs, for this kind of scenarios AWS have prepared a ready-made policy (AWSLambdaExecute). To create a role, first we need to stablish the trust relationship policy document and then attach the policy document specifying the permissions that whoever is assuming that role will get.

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
$ aws iam create-role \
--role-name HelloLambdaRole \
--assume-role-policy-document "$ROLE_POLICY_DOCUMENT"
```
This command should output a JSON with all the information about the newly created role. We'll take note of the Arn as we'll need it in the next step to attach the policy.

```json
{
    "Role": {
        "Path": "/",
        "RoleName": "HelloLambdaRole",
        "RoleId": "AROA6GR2ABCDEFABCDEF",
        "Arn": "arn:aws:iam::123123123123:role/HelloLambdaRole",
        "CreateDate": "2020-04-26T10:40:30+00:00",
        "AssumeRolePolicyDocument": {
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
    }
}
```

With the role being created, let's attach the policy, in this case we'll use a managed policy provided by Amazon ```arn:aws:iam::aws:policy/AWSLambdaExecute```. Although it's not strictly necessary, it's a good practice for troubleshooting, so let's add it.

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
$ zip -j lambda bin/Debug/netcoreapp3.1/publish/*

  adding: Amazon.Lambda.Core.dll (deflated 46%)
  adding: project.lambda.deps.json (deflated 58%)
  adding: project.lambda.dll (deflated 62%)
  adding: project.lambda.pdb (deflated 21%)
  adding: project.lambda.runtimeconfig.json (deflated 23%)
```

This creates a file named ```lambda.zip``` in the current directory. Now we are good to go to create the Lambda function on AWS.

```shell
$ aws lambda create-function \
--function-name HelloLambda \
--zip-file fileb://lambda.zip \
--role arn:aws:iam::123123123123:role/HelloLambdaRole \
--handler project.lambda::project.lambda.Function::HelloHandler \
--runtime dotnetcore3.1

{
    "FunctionName": "HelloLambda",
    "FunctionArn": "arn:aws:lambda:eu-west-1:123123123123:function:HelloLambda",
    "Runtime": "dotnetcore3.1",
    "Role": "arn:aws:iam::123123123123:role/HelloLambdaRole",
    "Handler": "project.lambda::project.lambda.Function::HelloHandler",
    "CodeSize": 11246,
    "Description": "",
    "Timeout": 3,
    "MemorySize": 128,
    "LastModified": "2020-04-26T10:44:16.365+0000",
    "CodeSha256": "TMwz98oDQBdntBWKzqs6aLMq/nZq3z+bJAfgFEGN9fg=",
    "Version": "$LATEST",
    "TracingConfig": {
        "Mode": "PassThrough"
    },
    "RevisionId": "ba0b2194-6a75-479d-8296-584133afc7f6",
    "State": "Active",
    "LastUpdateStatus": "Successful"
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
    "StatusCode": 200,
    "ExecutedVersion": "$LATEST"
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