# Part 3 - Deploy to AWS environment - SAM CLI

In the previous part we experienced the process of deploying a Lambda function using purely AWS CLI, the full process consisted of a few steps. In this part we'll see how SAM can be helpful when doing the same process.

## Preparing the template

With the project file updated, let's double check the template is inline with the output directory and all the other settings. At this point, the template file should look like this:

```yaml
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
```

Once that's done, let's run a ```dotnet publish``` to get a freshly compiled code.

## Using SAM Package command

We saw that we can provide our code package in two different ways: a zip file with the function at the time of creation or a reference to a zip file in a S3 bucket. SAM uses the second approach and it will zip the file for us when using ```sam package```. 

As a result we get a "translated" template containing the line with the CodeUri pointing to the zip file in the S3 bucket we specify.

```shell
$ sam package \
--template-file template.yaml \
--s3-bucket abelperez-temp \
--s3-prefix project-lambda \
--output-template-file packaged.yaml

Uploading to project-lambda/455f500d6551a7de2a6ce223d8927ffd  10986 / 10986.0  (100.00%)

Successfully packaged artifacts and wrote output template to file packaged.yaml.
Execute the following command to deploy the packaged template
sam deploy --template-file /home/abel/serverless-project/src/project.lambda/packaged.yaml --stack-name <YOUR STACK NAME>
```

If we inspect the generated template (packaged.yaml), there's a different CodeUri reference, you should see something like this.

```
CodeUri: s3://abelperez-temp/project-lambda/455f500d6551a7de2a6ce223d8927ffd
```

## Deploying using SAM Deploy command

Now, as suggested by the output of the previous command, we should run ```sam deploy``` providing the generated template.

```shell
$ sam deploy \
--template-file packaged.yaml \
--stack-name project-lambda \
--capabilities CAPABILITY_IAM


	Deploying with following values
	===============================
	Stack name                 : project-lambda
	Region                     : None
	Confirm changeset          : False
	Deployment s3 bucket       : None
	Capabilities               : ["CAPABILITY_IAM"]
	Parameter overrides        : {}

Initiating deployment
=====================

Waiting for changeset to be created..

CloudFormation stack changeset
---------------------------------------------------------------------------------------------------------------------
Operation                               LogicalResourceId                       ResourceType                          
---------------------------------------------------------------------------------------------------------------------
+ Add                                   HelloLambdaRole                         AWS::IAM::Role                        
+ Add                                   HelloLambda                             AWS::Lambda::Function                 
---------------------------------------------------------------------------------------------------------------------

Changeset created successfully. arn:aws:cloudformation:eu-west-1:123123123123:changeSet/samcli-deploy1587980002/3df4a9cb-f81c-45dd-8684-355a2d607102


2020-04-27 10:33:27 - Waiting for stack create/update to complete

CloudFormation events from changeset
---------------------------------------------------------------------------------------------------------------------
ResourceStatus                ResourceType                  LogicalResourceId             ResourceStatusReason        
---------------------------------------------------------------------------------------------------------------------
CREATE_IN_PROGRESS            AWS::IAM::Role                HelloLambdaRole               -                           
CREATE_IN_PROGRESS            AWS::IAM::Role                HelloLambdaRole               Resource creation Initiated 
CREATE_COMPLETE               AWS::IAM::Role                HelloLambdaRole               -                           
CREATE_IN_PROGRESS            AWS::Lambda::Function         HelloLambda                   Resource creation Initiated 
CREATE_IN_PROGRESS            AWS::Lambda::Function         HelloLambda                   -                           
CREATE_COMPLETE               AWS::Lambda::Function         HelloLambda                   -                           
CREATE_COMPLETE               AWS::CloudFormation::Stack    project-lambda                -                           
---------------------------------------------------------------------------------------------------------------------

Successfully created/updated stack - project-lambda in None
```

This process takes a little bit more time to complete, since it's communicating with CloudFormation to create a stack. 

Newer versions of SAM are considerably more verbose than earlier ones, this is a good thing as it's showing what it's doing at any point in time.

The parameter ```--capabilities``` is required because as part of the stack resources, it will create a new IAM role, yes, the same role we manually created in the last part, if we don't provide this parameter, CloudFormation will fail the creation as we need to acknowledge we'll create some IAM resources.

Let's inspect what's in the CloudFormation stack SAM have just created for us, to do that, CloudFormation CLI provides a command describe-stack-resources.

```shell
$ aws cloudformation describe-stack-resources --stack-name project-lambda
{
    "StackResources": [
        {
            "StackName": "project-lambda",
            "StackId": "arn:aws:cloudformation:eu-west-1:123123123123:stack/project-lambda/22ecb2f0-886a-11ea-bb03-0a9b71aae734",
            "LogicalResourceId": "HelloLambda",
            "PhysicalResourceId": "project-lambda-HelloLambda-GNCSEV3U04G9",
            "ResourceType": "AWS::Lambda::Function",
            "Timestamp": "2020-04-27T09:33:49.525000+00:00",
            "ResourceStatus": "CREATE_COMPLETE",
            "DriftInformation": {
                "StackResourceDriftStatus": "NOT_CHECKED"
            }
        },
        {
            "StackName": "project-lambda",
            "StackId": "arn:aws:cloudformation:eu-west-1:123123123123:stack/project-lambda/22ecb2f0-886a-11ea-bb03-0a9b71aae734",
            "LogicalResourceId": "HelloLambdaRole",
            "PhysicalResourceId": "project-lambda-HelloLambdaRole-1UXN0SKYJW8LW",
            "ResourceType": "AWS::IAM::Role",
            "Timestamp": "2020-04-27T09:33:46.440000+00:00",
            "ResourceStatus": "CREATE_COMPLETE",
            "DriftInformation": {
                "StackResourceDriftStatus": "NOT_CHECKED"
            }
        }
    ]
}
```

As we can see, there is effectively an IAM role and a Lambda function. Essentially what happened is that SAM template engine has translated the resource type AWS::Serverless::Function into a AWS::Lambda::Function plus a AWS::IAM::Role for us.

## Invoking the function

After having deployed the function, we can invoke it the same way we did last time: using AWS CLI. One difference is the function and role names seem "distorted", that's a CloudFormation behaviour, it uses the name we give as "logical name", then it prepends the stack name and appends a random value to ensure uniqueness.

In this case, the function is renamed to project-lambda-HelloLambda-GNCSEV3U04G9 which we can get from the previous command (a much better practice is to use Outputs section in the template, we'll get there in future parts)

By running ```lambda invoke``` command, followed by cat the output file we can see the result is exactly the same.

```shell
$ aws lambda invoke \
--function-name project-lambda-HelloLambda-GNCSEV3U04G9 \
output.txt && cat output.txt
{
    "ExecutedVersion": "$LATEST", 
    "StatusCode": 200
}
Hello Lambda!
```

## Cleaning up

This time the clean up process is much easier, since we are using CloudFormation behind the scenes, we only need to delete the stack and it will take care of deleting all the resources attached to it.

```shell
$ aws cloudformation delete-stack --stack-name project-lambda
```

Although this process does not block the UI until it's done, it's useful enough to safely delete the stack. However if you want to make sure it's actually deleted, there is a ```wait``` command.

```shell
$ aws cloudformation wait stack-delete-complete \
--stack-name project-lambda
```