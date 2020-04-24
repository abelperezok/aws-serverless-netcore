# Lesson 04 - Deploy to AWS environment - dotnet lambda

In previous lessons, we've seen how to deploy Lambda functions to AWS environment by using purely AWS CLI and SAM CLI. There is another way to achieve this by using the [AWS Extensions for .NET CLI](https://github.com/aws/aws-extensions-for-dotnet-cli).

## Installing Amazon Lambda Tools

In order to use this extension, we have to modify the project file, adding a DotNetCliToolReference item.

```xml
  <ItemGroup>
    <DotNetCliToolReference Include="Amazon.Lambda.Tools" Version="2.2.0" />
  </ItemGroup>
```
This basically allows us to run a new set of commands under the dotnet CLI in the form of ```dotnet lambda ... ``` commands. We need to run ```dotnet restore``` before being able to use it for the first time.

## Deploying a function

If we only want to deploy a function in isolation, the command to run is ```dotnet lambda deploy-function```. Before trying to deploy a function, as seen in previous lesson, it's required to have already a role in place. Let's recap the role creation process.

### Creating the role

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

$ aws iam create-role \
--role-name HelloLambdaRole \
--assume-role-policy-document "$ROLE_POLICY_DOCUMENT"

$ aws iam attach-role-policy \
--policy-arn arn:aws:iam::aws:policy/AWSLambdaExecute \
--role-name HelloLambdaRole
```
### Deploying the function

The command ```deploy-function``` takes more parameters than its counterpart in AWS CLI because it doesn't assume any defaults, we have to be explicit with the settings when creating the function. Although these parameters can be stored in a configuration file to avoid long command lines.

In our example, it will look like this:

```shell
$ dotnet lambda deploy-function \
--function-name HelloLambda \
--s3-bucket abelperez-temp \
--s3-prefix project-lambda/ \
--configuration Release \
--framework netcoreapp2.1 \
--function-runtime dotnetcore2.1 \
--function-role HelloLambdaRole \
--function-handler project.lambda::project.lambda.Function::HelloHandler \
--function-memory-size 128 \
--function-timeout 30 \
--region eu-west-1
```

We can see that it looks like a mix of settings we've set in the template when using SAM and command line parameters when using AWS CLI. This command does four fundamental steps for us in one go.

* Invokes ```dotnet publish``` command using the specified configuration i.e. Release.
* Creates the zip file containing all binaries and dependencies.
* Uploads the package to S3 
* Creates the Lambda function on AWS referencing the zip file in S3.

### Invoking the function

Once it's done, we can use ```dotnet lambda invoke``` to invoke the function to test it's working.

```shell
$ dotnet lambda invoke-function \
--function-name HelloLambda \
--region eu-west-1

Amazon Lambda Tools for .NET Core applications (2.2.0)
Project Home: https://github.com/aws/aws-extensions-for-dotnet-cli, https://github.com/aws/aws-lambda-dotnet
	
Payload:
Hello Lambda!

Log Tail:
START RequestId: 9306db41-969a-11e8-89ad-9113e02c10be Version: $LATEST
END RequestId: 9306db41-969a-11e8-89ad-9113e02c10be
REPORT RequestId: 9306db41-969a-11e8-89ad-9113e02c10be	Duration: 0.14 ms Billed Duration: 100 ms 	Memory Size: 128 MB	Max Memory Used: 48 MB
```

### Reusing configuration parameters

If we run the previous ```dotnet lambda deploy-function``` command with the option ```--persist-config-file true``` it generates a file ```aws-lambda-tools-defaults.json``` with all the parameters we set in the command line.

```json
{
    "region" : "eu-west-1",
    "configuration" : "Release",
    "framework"     : "netcoreapp2.1",
    "function-name" : "HelloLambda",
    "function-handler" : "project.lambda::project.lambda.Function::HelloHandler",
    "function-memory-size" : 128,
    "function-role"        : "HelloLambdaRole",
    "function-timeout"     : 30,
    "function-runtime"     : "dotnetcore2.1",
    "s3-bucket"            : "abelperez-temp",
    "s3-prefix"            : "project-lambda/"
}
```

This way, we can just run the command without that many arguments, only those we intend to override. It doesn't even need to be named ```aws-lambda-tools-defaults.json```, let's say we want it as ```lambda-config.json```, the command would be in this case:

```shell
$ dotnet lambda deploy-function --config-file lambda-config.json
```
### Cleaning up 

As seen before, we should clean up resources provisioned when not used, with ```dotnet lambda delete-function``` we pass the function name and region or we can still use the same configuration file as above.

```shell
$ dotnet lambda delete-function \
--function-name HelloLambda \
--region eu-west-1
```

As before, detach the policy and delete the role.
```shell
$ aws iam detach-role-policy \
--role-name HelloLambdaRole \
--policy-arn arn:aws:iam::aws:policy/AWSLambdaExecute

$ aws iam delete-role --role-name HelloLambdaRole
```

## Deploying a serverless application

If we are relying more on our template file (most likely as our application grows) it makes more sense to use ```dotnet lambda deploy-serverless``` command instead. 

###  Running deploy-serverless

Here is an example of this command.

```shell
$ dotnet lambda deploy-serverless \
--s3-bucket abelperez-temp \
--s3-prefix project-lambda/ \
--configuration Release \
--framework netcoreapp2.1 \
--stack-name project-lambda \
--template template.yaml \
--region eu-west-1
```

It can be compared to the combination of commands ```sam package``` and ```sam deploy``` seen in the previous lesson. In this case it does both operations in one. After running it we can inspect what resources it has created on our behalf by invoking CloudFormation CLI.

```shell
$ aws cloudformation describe-stack-resources --stack-name project-lambda

{
    "StackResources": [
        {
            "StackId": "arn:aws:cloudformation:eu-west-1:12123123123:stack/project-lambda/cf6b7890-96a4-11e8-8049-503abe701c35", 
            "ResourceStatus": "CREATE_COMPLETE", 
            "ResourceType": "AWS::Lambda::Function", 
            "Timestamp": "2018-08-02T22:39:07.440Z", 
            "StackName": "project-lambda", 
            "PhysicalResourceId": "project-lambda-HelloLambda-SGW35OFPF7IY", 
            "LogicalResourceId": "HelloLambda"
        }, 
        {
            "StackId": "arn:aws:cloudformation:eu-west-1:123123123123:stack/project-lambda/cf6b7890-96a4-11e8-8049-503abe701c35", 
            "ResourceStatus": "CREATE_COMPLETE", 
            "ResourceType": "AWS::IAM::Role", 
            "Timestamp": "2018-08-02T22:39:04.774Z", 
            "StackName": "project-lambda", 
            "PhysicalResourceId": "project-lambda-HelloLambdaRole-1GKUF2YBP5TDY", 
            "LogicalResourceId": "HelloLambdaRole"
        }
    ]
}
```

We can see it has indeed created both the role and the function as it uses CloudFormation behind the scenes to create the stack based on our template. Essentially, it performs the following steps for us:

* Invokes ```dotnet publish``` command using the specified configuration i.e. Release.
* Creates the zip file containing all binaries and dependencies.
* Uploads the package to S3 
* Creates the CloudFormation stack using the specified template and wait for the stack to complete.

### Invoking the function

Once it's done, we can continue to use ```dotnet lambda invoke``` to invoke the function as we did before to test it's working. One detail to remember is that the function name has been changed by CloudFormation.

```shell
$ dotnet lambda invoke-function \
--function-name project-lambda-HelloLambda-SGW35OFPF7IY \
--region eu-west-1
```

### Reusing configuration parameters

Similarly, if we run the previous ```dotnet lambda deploy-serverless``` command with the option ```--persist-config-file true``` it generates a file ```aws-lambda-tools-defaults.json``` with all the parameters we set in the command line.

```json
{
    "region" : "eu-west-1",
    "configuration" : "Release",
    "framework"     : "netcoreapp2.1",
    "s3-bucket"     : "abelperez-temp",
    "s3-prefix"     : "project-lambda/",
    "template"      : "template.yaml",
    "stack-name"    : "project-lambda"
}
```

Same rule applies if we use a different file name i.e ```serverless-config.json```

```shell
$ dotnet lambda deploy-serverless --config-file serverless-config.json 
```

### Cleaning up 

In this case, it's a much simpler process ```dotnet lambda delete-serverless``` will take care of everything since we rely on CloudFormation to orchestrate all resources.

```shell
$ dotnet lambda delete-serverless --stack-name project-lambda --region eu-west-1
```
Or, if using configuration file.
```shell
$ dotnet lambda delete-serverless --config-file serverless-config.json
```
