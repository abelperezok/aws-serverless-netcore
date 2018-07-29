# Creating AWS serverless applications in .NET Core from scratch

Why from scratch? most of the time when starting a new project using a new technology, we are provided with some templates/boilerplate to get started quickly without worrying too much about how to set everything up.

Some examples:

[.NET Core Lambda CLI](https://docs.aws.amazon.com/lambda/latest/dg/lambda-dotnet-coreclr-deployment-package.html)

```shell
$ dotnet new lambda.EmptyFunction --name HelloLambda
```

[AWS SAM CLI](https://github.com/awslabs/aws-sam-cli)

```shell
$ sam init -r dotnetcore2.0 -n HelloLambda
```

While this is great to just get started and quickly see "something working", it doesn't help us to understand the underlying technology plumbing logic that every time is more complex as we build on top of previous abstraction layers. 

In this tutorial, we are going to start with the bare minimum to get a Lambda function running and build from there. We'll progressively increase complexity in the examples.

## Prerequisites

* [AWS Account (free tier is enough)](https://aws.amazon.com/free/)
* [AWS CLI (at least one profile configured)](https://docs.aws.amazon.com/cli/latest/userguide/installing.html)
* [.NET Core 2 SDK](https://www.microsoft.com/net/download)
* [Docker (CE is enough)](https://www.docker.com/community-edition#/download)
* [AWS SAM CLI](https://github.com/awslabs/aws-sam-cli)

Verify everything is good to go.

```shell
$ aws --version
aws-cli/1.15.59 Python/2.7.15 Linux/4.9.0-4-amd64 botocore/1.10.58
$ dotnet --version
2.1.302
$ sam --version
SAM CLI, version 0.5.0
$ docker --version
Docker version 18.03.1-ce, build 9ee9f40
```

## Content

[Lesson 01 - Creating a Lambda Function from scratch](lesson-01/)
[Lesson 02 - Deploy to AWS environment - AWS CLI](lesson-02/)