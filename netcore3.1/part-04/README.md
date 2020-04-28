# Part 4 - Deploy to AWS environment - dotnet lambda

In previous parts, we've seen how to deploy Lambda functions to AWS environment by using purely AWS CLI and SAM CLI. There is another way to achieve this by using the [AWS Extensions for .NET CLI](https://github.com/aws/aws-extensions-for-dotnet-cli).

In this part we are going to explore how to deploy a Lambda function and how to deploy a serverless application using Amazon Lambda Tools.

## Installing Amazon Lambda Tools

In order to use this extension, we use the command ```dotnet tool``` to install globally the Amazon Lambda tools for .NET Core.

```shell
$ dotnet tool install -g Amazon.Lambda.Tools
```

This should make available a new set of commands under the dotnet CLI in the form of ```dotnet lambda ... ``` commands. 

You might find issues with the way the PATH is set, in my case, I had to manually add the tools folder to my PATH variable. 

```
export PATH=$PATH:/home/abel/.dotnet/tools
```
## Deploying a function

If we only want to deploy a function in isolation, the command to run is ```dotnet lambda deploy-function```. Before trying to deploy a function, as seen in previous parts, it's required to have already a role in place. Let's recap the role creation process.

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

The command ```deploy-function``` takes more parameters than its counterpart in AWS CLI because it assumes almost no defaults, we have to be explicit with the settings when creating the function. Although these parameters can be stored in a configuration file to avoid long command lines.

In our example, it will look like this:

```shell
$ dotnet lambda deploy-function \
--function-name HelloLambda \
--s3-bucket abelperez-temp \
--s3-prefix project-lambda/ \
--function-runtime dotnetcore3.1 \
--function-role HelloLambdaRole \
--function-handler project.lambda::project.lambda.Function::HelloHandler \
--function-memory-size 128 \
--function-timeout 30 \
--region eu-west-1

Amazon Lambda Tools for .NET Core applications (4.0.0)
Project Home: https://github.com/aws/aws-extensions-for-dotnet-cli, https://github.com/aws/aws-lambda-dotnet
	
Executing publish command
Deleted previous publish folder
... invoking 'dotnet publish', working folder '/home/abel/serverless-project/src/project.lambda/bin/Release/netcoreapp3.1/publish'
... publish: Microsoft (R) Build Engine version 16.5.0+d4cbfca49 for .NET Core
... publish: Copyright (C) Microsoft Corporation. All rights reserved.
... publish:   Restore completed in 39.68 ms for /home/abel/serverless-project/src/project.lambda/project.lambda.csproj.
... publish:   project.lambda -> /home/abel/serverless-project/src/project.lambda/bin/Release/netcoreapp3.1/linux-x64/project.lambda.dll
... publish:   project.lambda -> /home/abel/serverless-project/src/project.lambda/bin/Release/netcoreapp3.1/publish/
Changed permissions on published file (chmod +rx Amazon.Lambda.Core.dll).
Changed permissions on published file (chmod +rx project.lambda.pdb).
Changed permissions on published file (chmod +rx project.lambda.runtimeconfig.json).
Changed permissions on published file (chmod +rx project.lambda.dll).
Changed permissions on published file (chmod +rx project.lambda.deps.json).
Zipping publish folder /home/abel/serverless-project/src/project.lambda/bin/Release/netcoreapp3.1/publish to /home/abel/serverless-project/src/project.lambda/bin/Release/netcoreapp3.1/project.lambda.zip
... zipping:   adding: Amazon.Lambda.Core.dll (deflated 46%)
... zipping:   adding: project.lambda.pdb (deflated 21%)
... zipping:   adding: project.lambda.runtimeconfig.json (deflated 23%)
... zipping:   adding: project.lambda.dll (deflated 59%)
... zipping:   adding: project.lambda.deps.json (deflated 59%)
Created publish archive (/home/abel/serverless-project/src/project.lambda/bin/Release/netcoreapp3.1/project.lambda.zip).
Uploading to S3. (Bucket: abelperez-temp Key: project-lambda/HelloLambda-637236649112049148.zip)
... Progress: 100%
Creating new Lambda function HelloLambda
New Lambda function created
```

We can see that it looks like a mix of settings we've set in the template when using SAM and command line parameters when using AWS CLI. This command does four fundamental steps for us in one go.

* Invokes ```dotnet publish``` command defaulting to Release (unless set).
* Creates the zip file containing all binaries and dependencies.
* Uploads the package to S3 using the bucket and prefix set.
* Creates the Lambda function on AWS referencing the zip file in S3.

### Invoking the function

Once it's done, we can use ```dotnet lambda invoke``` to invoke the function to test it's working.

```shell
$ dotnet lambda invoke-function \
--function-name HelloLambda \
--region eu-west-1

Amazon Lambda Tools for .NET Core applications (4.0.0)
Project Home: https://github.com/aws/aws-extensions-for-dotnet-cli, https://github.com/aws/aws-lambda-dotnet
	
Payload:
Hello Lambda!

Log Tail:
START RequestId: 4ef782b3-eba6-4be2-9672-276883658e92 Version: $LATEST
END RequestId: 4ef782b3-eba6-4be2-9672-276883658e92
REPORT RequestId: 4ef782b3-eba6-4be2-9672-276883658e92	Duration: 67.20 ms	Billed Duration: 100 ms	Memory Size: 128 MB	Max Memory Used: 58 MB	Init Duration: 167.99 ms
```

### Reusing configuration parameters

If we run the previous ```dotnet lambda deploy-function``` command with the option ```--persist-config-file true``` it generates a file ```aws-lambda-tools-defaults.json``` with all the parameters we set in the command line.

```json
{
    "region" : "eu-west-1",
    "function-name" : "HelloLambda",
    "function-handler" : "project.lambda::project.lambda.Function::HelloHandler",
    "function-memory-size" : 128,
    "function-role"        : "HelloLambdaRole",
    "function-timeout"     : 30,
    "function-runtime"     : "dotnetcore3.1",
    "s3-bucket"            : "abelperez-temp",
    "s3-prefix"            : "project-lambda/"
}
```

This way, we can just run the command without any arguments. We can still pass some arguments, only those we intend to override. 

```shell
$ dotnet lambda deploy-function
```

The configuration file doesn't even need to be named ```aws-lambda-tools-defaults.json```, let's say we want it as ```lambda-config.json```, in case we have several configuration files, in this case the command would be:

```shell
$ dotnet lambda deploy-function --config-file lambda-config.json
```

### Cleaning up 

As seen before, we should clean up resources provisioned when not used, with ```dotnet lambda delete-function``` we pass the function name and region or we can still use the same configuration file as above.

```shell
$ dotnet lambda delete-function \
--function-name HelloLambda \
--region eu-west-1

Amazon Lambda Tools for .NET Core applications (4.0.0)
Project Home: https://github.com/aws/aws-extensions-for-dotnet-cli, https://github.com/aws/aws-lambda-dotnet
	
Lambda function HelloLambda deleted
```

As before, detach the policy and delete the role.
```shell
$ aws iam detach-role-policy \
--role-name HelloLambdaRole \
--policy-arn arn:aws:iam::aws:policy/AWSLambdaExecute

$ aws iam delete-role --role-name HelloLambdaRole
```

## Deploying a serverless application

If we are relying more on our template file (most likely as our application grows) it makes more sense to use ```dotnet lambda deploy-serverless``` command instead. This command uses the template provided to get information about the Lambda function and other resources.

###  Running deploy-serverless

Here is an example of this command. In this case, I have set the --configuration parameter to match what is in the template (CodeUri: bin/Debug/netcoreapp3.1/publish/) for simplicity.

```shell
$ dotnet lambda deploy-serverless \
--s3-bucket abelperez-temp \
--s3-prefix project-lambda/ \
--configuration Debug \
--stack-name project-lambda \
--template template.yaml \
--region eu-west-1

Amazon Lambda Tools for .NET Core applications (4.0.0)
Project Home: https://github.com/aws/aws-extensions-for-dotnet-cli, https://github.com/aws/aws-lambda-dotnet
	
Processing CloudFormation resource HelloLambda
Initiate packaging of . for resource HelloLambda
Executing publish command
... invoking 'dotnet publish', working folder '/home/abel/serverless-project/src/project.lambda/./bin/Debug/netcoreapp3.1/publish'
... publish: Microsoft (R) Build Engine version 16.5.0+d4cbfca49 for .NET Core
... publish: Copyright (C) Microsoft Corporation. All rights reserved.
... publish:   Restore completed in 202.08 ms for /home/abel/serverless-project/src/project.lambda/project.lambda.csproj.
... publish:   project.lambda -> /home/abel/serverless-project/src/project.lambda/bin/Debug/netcoreapp3.1/linux-x64/project.lambda.dll
... publish:   project.lambda -> /home/abel/serverless-project/src/project.lambda/bin/Debug/netcoreapp3.1/publish/
Changed permissions on published file (chmod +rx Amazon.Lambda.Core.dll).
Changed permissions on published file (chmod +rx project.lambda.pdb).
Changed permissions on published file (chmod +rx project.lambda.runtimeconfig.json).
Changed permissions on published file (chmod +rx project.lambda.dll).
Changed permissions on published file (chmod +rx project.lambda.deps.json).
Zipping publish folder /home/abel/serverless-project/src/project.lambda/./bin/Debug/netcoreapp3.1/publish to /tmp/HelloLambda-CodeUri-637236664102876867.zip
... zipping:   adding: Amazon.Lambda.Core.dll (deflated 46%)
... zipping:   adding: project.lambda.pdb (deflated 21%)
... zipping:   adding: project.lambda.runtimeconfig.json (deflated 23%)
... zipping:   adding: project.lambda.dll (deflated 58%)
... zipping:   adding: project.lambda.deps.json (deflated 59%)
Created publish archive (/tmp/HelloLambda-CodeUri-637236664102876867.zip).
Lambda project successfully packaged: /tmp/HelloLambda-CodeUri-637236664102876867.zip
Uploading to S3. (Bucket: abelperez-temp Key: project-lambda/HelloLambda-CodeUri-637236664102876867-637236664127816752.zip)
... Progress: 100%
Uploading to S3. (Bucket: abelperez-temp Key: project-lambda/project-lambda-template-637236664130369826.yaml)
... Progress: 100%
Found existing stack: False
CloudFormation change set created
... Waiting for change set to be reviewed
Created CloudFormation stack project-lambda
   
Timestamp            Logical Resource Id                      Status                                   
-------------------- ---------------------------------------- ---------------------------------------- 
28/04/2020 10:27     project-lambda                           CREATE_IN_PROGRESS                      
28/04/2020 10:27     HelloLambdaRole                          CREATE_IN_PROGRESS                      
28/04/2020 10:27     HelloLambdaRole                          CREATE_IN_PROGRESS                      
28/04/2020 10:27     HelloLambdaRole                          CREATE_COMPLETE                         
28/04/2020 10:27     HelloLambda                              CREATE_IN_PROGRESS                      
28/04/2020 10:27     HelloLambda                              CREATE_IN_PROGRESS                      
28/04/2020 10:27     HelloLambda                              CREATE_COMPLETE                         
28/04/2020 10:27     project-lambda                           CREATE_COMPLETE                         
Stack finished updating with status: CREATE_COMPLETE
```

It can be compared to the combination of commands ```sam package``` and ```sam deploy``` seen in the previous part. In this case it does both operations in one. After running it we can inspect what resources it has created on our behalf by invoking CloudFormation CLI.

```shell
$ aws cloudformation describe-stack-resources --stack-name project-lambda

{
    "StackResources": [
        {
            "StackName": "project-lambda",
            "StackId": "arn:aws:cloudformation:eu-west-1:12123123123:stack/project-lambda/65bb5f50-8932-11ea-8793-0297e5fc50ee",
            "LogicalResourceId": "HelloLambda",
            "PhysicalResourceId": "project-lambda-HelloLambda-QA46SRTJ3S57",
            "ResourceType": "AWS::Lambda::Function",
            "Timestamp": "2020-04-28T09:27:22.627000+00:00",
            "ResourceStatus": "CREATE_COMPLETE",
            "DriftInformation": {
                "StackResourceDriftStatus": "NOT_CHECKED"
            }
        },
        {
            "StackName": "project-lambda",
            "StackId": "arn:aws:cloudformation:eu-west-1:12123123123:stack/project-lambda/65bb5f50-8932-11ea-8793-0297e5fc50ee",
            "LogicalResourceId": "HelloLambdaRole",
            "PhysicalResourceId": "project-lambda-HelloLambdaRole-12BAEC1QKOU5Y",
            "ResourceType": "AWS::IAM::Role",
            "Timestamp": "2020-04-28T09:27:19.479000+00:00",
            "ResourceStatus": "CREATE_COMPLETE",
            "DriftInformation": {
                "StackResourceDriftStatus": "NOT_CHECKED"
            }
        }
    ]
}

```

We can see it has indeed created both the role and the function as it uses CloudFormation behind the scenes to create the stack based on our template. Essentially, it performs the following steps for us:

* Invokes ```dotnet publish``` command using the specified configuration i.e. Release.
* Creates the zip file containing all binaries and dependencies.
* Uploads the package to S3 using the bucket and prefix set.
* Creates the CloudFormation stack using the specified template and waits for the stack to complete.

### Invoking the function

Once it's done, we can continue to use ```dotnet lambda invoke``` to invoke the function as we did before to test it's working. One detail to remember is that the function name has been changed by CloudFormation.

```shell
$ dotnet lambda invoke-function \
--function-name project-lambda-HelloLambda-QA46SRTJ3S57 \
--region eu-west-1

Amazon Lambda Tools for .NET Core applications (4.0.0)
Project Home: https://github.com/aws/aws-extensions-for-dotnet-cli, https://github.com/aws/aws-lambda-dotnet
	
Payload:
Hello Lambda!

Log Tail:
START RequestId: 283791b6-ac0d-4950-b26c-592a59fb5a19 Version: $LATEST
END RequestId: 283791b6-ac0d-4950-b26c-592a59fb5a19
REPORT RequestId: 283791b6-ac0d-4950-b26c-592a59fb5a19	Duration: 76.56 ms	Billed Duration: 100 ms	Memory Size: 128 MB	Max Memory Used: 59 MB	Init Duration: 156.44 ms
```
### Reusing configuration parameters

Similarly, if we run the previous ```dotnet lambda deploy-serverless``` command with the option ```--persist-config-file true``` it generates a file ```aws-lambda-tools-defaults.json``` with all the parameters we set in the command line.

```json
{
    "region" : "eu-west-1",
    "configuration" : "Debug",
    "s3-bucket"     : "abelperez-temp",
    "s3-prefix"     : "project-lambda/",
    "template"      : "template.yaml",
    "stack-name"    : "project-lambda"
}
```

```shell
$ dotnet lambda deploy-serverless
```

Same rule applies if we use a different file name i.e ```serverless-config.json```

```shell
$ dotnet lambda deploy-serverless --config-file serverless-config.json 
```

### Cleaning up 

In this case, it's a much simpler process ```dotnet lambda delete-serverless``` will take care of everything since it relies on CloudFormation to orchestrate all resources.

```shell
$ dotnet lambda delete-serverless --stack-name project-lambda --region eu-west-1
```
Or, if using configuration file.
```shell
$ dotnet lambda delete-serverless --config-file serverless-config.json
```
Or, if using the default configuration file.
```shell
$ dotnet lambda delete-serverless
```