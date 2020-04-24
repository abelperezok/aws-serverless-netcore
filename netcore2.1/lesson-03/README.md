# Lesson 03 - Deploy to AWS environment - SAM CLI

In the previous lesson we experienced the process of deploying a Lambda function using purely AWS CLI, the full process consisted of a few steps. In this lessons we'll see how SAM can be helpful when doing the same process.

## Preparing the template

Since we've updated our project file, some of those changes impacted the template too. Basically the line with the CodeUri should be replaced with the new path to the publish directory, now it's **netcoreapp2.1** so it should look like this:

```
    CodeUri: bin/Debug/netcoreapp2.1/publish/
```

Also, as of this writing, there is a [bug in the package command](https://github.com/aws/aws-cli/issues/3376) (both sam and cloudformation) that ignores the Globals section when packaging, therefore, sadly we need to move this property to the function itself, which would make us duplicate the CodeUri property if we had several functions sharing the same package. We'll get around this in future lessons.

To clarify, the template file should look like this:

```yaml
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31
Description: >
    Sample SAM Template

Globals:
  Function:
    Timeout: 60
    Runtime: dotnetcore2.1

Resources:
    HelloLambda:
      Type: AWS::Serverless::Function
      Properties:
        CodeUri: bin/Debug/netcoreapp2.1/publish/
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

Uploading to project-lambda/93ae585917a8413ae50e85225296d543  23234 / 23234.0  (100.00%)
Successfully packaged artifacts and wrote output template to file packaged.yaml.
Execute the following command to deploy the packaged template
aws cloudformation deploy --template-file /home/abel/serverless-project/src/project.lambda/packaged.yaml --stack-name <YOUR STACK NAME>
```

If we inspect the generated template (packaged.yaml), there's a different CodeUri reference, you should see something like this.

```
CodeUri: s3://abelperez-temp/project-lambda/5a4443f1abb592cf86e966a622387eac
```

## Deploying using SAM Deploy command

Now, as suggested by the output of the previous command, we should run ```sam deploy``` providing the generated template.

```shell
$ sam deploy \
--template-file packaged.yaml \
--stack-name project-lambda \
--capabilities CAPABILITY_IAM

Waiting for changeset to be created..
Waiting for stack create/update to complete
Successfully created/updated stack - project-lambda
```

This process takes a little bit more time to complete, since it's communicating with CloudFormation to create a stack. The parameter ```--capabilities``` is required because as part of the stack resources, it will create a new IAM role, yes, the same role we manually created in the last lesson, if we don't provide this parameter, CloudFormation will fail the creation as we need to acknowledge we'll create some IAM resources.

Let's inspect what's in the CloudFormation stack SAM have just created for us, to do that, CloudFormation CLI provides a command describe-stack-resources.

```shell
$ aws cloudformation describe-stack-resources --stack-name project-lambda
{
    "StackResources": [
        {
            "StackId": "arn:aws:cloudformation:eu-west-1:123123123123:stack/project-lambda/f6706b50-944f-11e8-b4a5-50a686326036", 
            "ResourceStatus": "CREATE_COMPLETE", 
            "ResourceType": "AWS::Lambda::Function", 
            "Timestamp": "2018-07-30T23:26:48.166Z", 
            "StackName": "project-lambda", 
            "PhysicalResourceId": "project-lambda-HelloLambda-OBVQDSBNZJ7C", 
            "LogicalResourceId": "HelloLambda"
        }, 
        {
            "StackId": "arn:aws:cloudformation:eu-west-1:123123123123:stack/project-lambda/f6706b50-944f-11e8-b4a5-50a686326036", 
            "ResourceStatus": "CREATE_COMPLETE", 
            "ResourceType": "AWS::IAM::Role", 
            "Timestamp": "2018-07-30T23:26:45.624Z", 
            "StackName": "project-lambda", 
            "PhysicalResourceId": "project-lambda-HelloLambdaRole-1USWQQZT0JI07", 
            "LogicalResourceId": "HelloLambdaRole"
        }
    ]
}
```

As we can see, there is effectively an IAM role and a Lambda function. Essentially what happened is that SAM template engine has translated the resource type AWS::Serverless::Function into a AWS::Lambda::Function plus a AWS::IAM::Role for us.

## Invoking the function

After having deployed the function, we can invoke it the same way we did last time: using AWS CLI. One difference is the function and role names seem "distorted", that's a CloudFormation behaviour, it uses the name we give as "logical name", then it prepends the stack name and appends a random value to ensure uniqueness.

In this case, the function is renamed to project-lambda-HelloLambda-OBVQDSBNZJ7C which we can get from the previous command (a much better practice is to use Outputs section in the template, we'll get there in future lessons)

By running ```lambda invoke``` command, followed by cat the output file we can see the result is exactly the same.

```shell
$ aws lambda invoke \
--function-name project-lambda-HelloLambda-OBVQDSBNZJ7C \
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