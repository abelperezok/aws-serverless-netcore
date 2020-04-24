# Creating AWS serverless applications in .NET Core from scratch

Why from scratch? most of the time when starting a new project using a new technology, we are provided with some templates/boilerplate to get started quickly without worrying too much about how to set everything up.

Some examples:

[.NET Core Lambda CLI](https://docs.aws.amazon.com/lambda/latest/dg/lambda-dotnet-coreclr-deployment-package.html)

```shell
$ dotnet new lambda.EmptyFunction --name HelloLambda
```

[AWS SAM CLI](https://github.com/awslabs/aws-sam-cli)

```shell
$ sam init -r dotnetcore2.1 -n HelloLambda
```

[Serverless Framework](https://serverless.com/framework/docs/getting-started/)

```shell
$ serverless create --template aws-csharp --path HelloLambda
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

* [Lesson 01 - Creating a Lambda Function from scratch](lesson-01/)
* [Lesson 02 - Deploy to AWS environment - AWS CLI](lesson-02/)
* [Lesson 03 - Deploy to AWS environment - SAM CLI](lesson-03/)
* [Lesson 04 - Deploy to AWS environment - dotnet lambda](lesson-04/)
* [Lesson 05 - Complex input / output and logging](lesson-05/)
* [Lesson 06 - API Gateway - Introduction](lesson-06/)
* [Lesson 07 - API Gateway - OpenAPI (Swagger) Specification](lesson-07/)
* [Lesson 08 - API Gateway - CloudFormation](lesson-08/)
* [Lesson 09 - API Gateway - Lambda integration](lesson-09/)
* [Lesson 10 - API Gateway - Proxy integration](lesson-10/)

## Progress

- [x] Lesson 01 - Creating a Lambda Function from scratch
- [x] Lesson 02 - Deploy to AWS environment - AWS CLI
- [x] Lesson 03 - Deploy to AWS environment - SAM CLI
- [x] Lesson 04 - Deploy to AWS environment - dotnet lambda
- [x] Lesson 05 - Complex input / output and logging
- [x] Lesson 06 - API Gateway - Introduction
- [x] Lesson 07 - API Gateway - OpenAPI (Swagger) Specification
- [x] Lesson 08 - API Gateway - CloudFormation
- [x] Lesson 09 - API Gateway - Lambda integration
- [x] Lesson 10 - API Gateway - Proxy integration
- [ ] Lesson 11 - Some back end code (DynamoDb)
- [ ] Lesson 12 - TBD
