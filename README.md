# Creating AWS serverless applications in .NET Core from scratch

> **Note** - This repository has been updated to use the latest stable version of .NET Core supported by AWS Lambda (.NET Core v3.1 LTS). For the previous version 2.1 see [netcore2.1 section](netcore2.1/README.md)


## Why from scratch? 
most of the time when starting a new project using a new technology, we are provided with some templates/boilerplate to get started quickly without worrying too much about how to set everything up.

Some examples:

[.NET Core Lambda CLI](https://docs.aws.amazon.com/lambda/latest/dg/lambda-dotnet-coreclr-deployment-package.html) ( [Github](https://github.com/aws/aws-extensions-for-dotnet-cli) )

```shell
$ dotnet new lambda.EmptyFunction --name HelloLambda
```
[AWS SAM CLI](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/serverless-getting-started-hello-world.html) ( [Github](https://github.com/awslabs/aws-sam-cli) )


```shell
$ sam init -r dotnetcore3.1 -n HelloLambda
```

[Serverless Framework](https://serverless.com/framework/docs/getting-started/) ( [Github](https://github.com/serverless/serverless) )
It doesn't seem to have been updated to netcore 3.1 just yet.


```shell
$ serverless create --template aws-csharp --path HelloLambda
```

While this is great to just get started and quickly see "something working", it doesn't help us to understand the underlying technology plumbing logic that every time is more complex as we build on top of previously built layers. 

In this tutorial, we are going to start with the bare minimum to get a Lambda function running and build from there. We'll progressively increase complexity in the examples.

## Prerequisites

* [AWS Account (free tier is enough)](https://aws.amazon.com/free/)
* [AWS CLI (at least one profile configured)](https://docs.aws.amazon.com/cli/latest/userguide/cli-chap-install.html)
* [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1)
* [Docker (CE is enough)](https://www.docker.com/get-started)
* [AWS SAM CLI](https://github.com/awslabs/aws-sam-cli)

Verify everything is good to go.

```shell
$ aws --version
aws-cli/2.0.7 Python/3.7.3 Linux/5.4.0-4-amd64 botocore/2.0.0dev11
$ dotnet --version
3.1.201
$ sam --version
SAM CLI, version 0.47.0
$ docker --version
Docker version 19.03.8, build afacb8b7f0
```

## Content

- [x] [Part 01 - Creating a Lambda Function from scratch](netcore3.1/part-01/)
- [x] [Part 02 - Deploy to AWS environment - AWS CLI](netcore3.1/part-02/)
- [ ] Part 03 - Deploy to AWS environment - SAM CLI
- [ ] Part 04 - Deploy to AWS environment - dotnet lambda
- [ ] Part 05 - Complex input / output and logging
- [ ] Part 06 - API Gateway - Introduction
- [ ] Part 07 - API Gateway - OpenAPI (Swagger) Specification
- [ ] Part 08 - API Gateway - CloudFormation
- [ ] Part 09 - API Gateway - Lambda integration
- [ ] Part 10 - API Gateway - Proxy integration
- [ ] Part 11 - Some back end code (DynamoDb)
- [ ] Part 12 - TBD
