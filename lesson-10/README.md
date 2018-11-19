# Lesson 10 - API Gateway - Proxy integration

In the previous lesson we've seen how to integrate a Lambda function with API Gateway using Lambda integration (AWS) following three different implementations. Now we are in a position to see how this integration can be done using Lambda Proxy integration (AWS_PROXY).

This approach relieves API Gateway from the burden of dealing with HTTP request / response details and mapping from / to Lambda functions, thus shifting all this work to the Lambda function itself. Lambda functions on the other hand, are now responsible for receiving, parsing and validating the raw input from the user agent, as well as generating HTTP response in a way that API Gateway is able to wrap the response end ship it back to the user agent.

In the following sections we will create a Lambda integration using three different implementations in order to better understand the details. First, using purely AWS CLI, then from a Swagger file and finally with a CloudFormation template.

* [AWS CLI](#api-gateway-lambda-integration-using-aws-cli)
* [Open API (Swagger)](#api-gateway-lambda-integration-using-open-api-swagger)
* [CloudFormation](#api-gateway-lambda-integration-using-cloudformation)

## Prepare the Lambda function

In this section we'll deploy a Lambda function which will be common to the first and second integration implementations. The code will be modified from what we've seen before to accommodate to the new proxy structure. From the previous example, classes LambdaInput and LambdaOutput have not been modified.

```cs
    public class Function
    {
        public APIGatewayProxyResponse HelloHandler(APIGatewayProxyRequest apigProxyEvent, ILambdaContext context)
        {
            var input = JsonConvert.DeserializeObject<LambdaInput>(apigProxyEvent.Body);

            context.Logger.Log($"Hello {input.Name}, you are now {input.Age}");

            var output = new LambdaOutput { Name = input.Name, Old = input.Age > 50 };
            
            return new APIGatewayProxyResponse
            {
                Body = JsonConvert.SerializeObject(output),
                StatusCode = 200,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
    }
```

In this implementation, the Lambda function is no longer receiving or returning custom objects, in order to use proxy integration input and output must be objects of type APIGatewayProxyRequest and APIGatewayProxyResponse respectively. APIGatewayProxyRequest contains the whole raw request that has been wrapped by API Gateway. Similarly, APIGatewayProxyResponse wraps the response in a format that API Gateway expects it to be sent back to the user agent.

In .csproj file, two references have been added: Amazon.Lambda.APIGatewayEvents and Newtonsoft.Json, the former contains APIGatewayProxyRequest and APIGatewayProxyResponse classes and the latter is used to deserialize the input and serialize back the output.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netcoreapp2.1</TargetFramework>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.Core" Version="1.0.0" />
    <PackageReference Include="Amazon.Lambda.Serialization.Json" Version="1.3.0" />
    <PackageReference Include="Amazon.Lambda.APIGatewayEvents" Version="1.0.2" />
    <PackageReference Include="Newtonsoft.Json" Version="11.0.2" />
  </ItemGroup>

  <ItemGroup>
    <DotNetCliToolReference Include="Amazon.Lambda.Tools" Version="2.2.0" />
  </ItemGroup>

</Project>
```

In a directory with files Function.cs and project.lambda.csproj we'll create and deploy the Lambda function by running the following commands (similar procedure as in lesson 05).

* abelperez-temp is the bucket of your choice to upload the code to S3.
* project-lambda/ is just a folder inside the bucket.
* the value for --role parameter is copied from the output of the aws iam create-role command.
* do take note of the function arn as we'll need it later for the integration.

```shell
dotnet lambda package -c Release -f netcoreapp2.1 -o project-lambda-proxy.zip
aws s3 cp project-lambda-proxy.zip s3://abelperez-temp/project-lambda/
$ cat > role-policy.json <<EOM
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
$ aws iam create-role --role-name HelloLambdaRole --assume-role-policy-document file://role-policy.json
$ aws iam attach-role-policy --policy-arn arn:aws:iam::aws:policy/AWSLambdaExecute --role-name HelloLambdaRole
$ aws lambda create-function --function-name HelloLambdaProxy \
--code S3Bucket=abelperez-temp,S3Key=project-lambda/project-lambda-proxy.zip \
--role arn:aws:iam::123123123123:role/HelloLambdaRole \
--handler project.lambda::project.lambda.Function::HelloHandler \
--runtime dotnetcore2.1 --timeout 30
```

## Test the function in isolation before integrating

Use dotnet lambda invoke-function command for simplicity, here are two examples to cover both scenarios. 

```shell
$ dotnet lambda invoke-function -fn HelloLambdaProxy -p '{"body": "{\"Name\":\"Abel\",\"Age\":33}"}' --region eu-west-1
$ dotnet lambda invoke-function -fn HelloLambdaProxy -p '{"body": "{\"Name\":\"Abel\",\"Age\":88}"}' --region eu-west-1
```
Notice the payload in this case is wrapped in a 'body' object, this is to be compliant with the proxy integration. Also, the response payload contains more information: status code, headers and body.

```
...
Payload:
{"statusCode":200,"headers":{"Content-Type":"application/json"},"body":"{\"Name\":\"Abel\",\"Old\":false}"}
...
Payload:
{"statusCode":200,"headers":{"Content-Type":"application/json"},"body":"{\"Name\":\"Abel\",\"Old\":true}"}
...
```

## API Gateway Lambda integration using AWS CLI

We need an API and at least one resource with a method, let's create them.

### Create the REST API & resource

Following the same procedure as in lesson 06, we create a REST API with one resource listening on /hello and a GET Method. Each command's output returns ids used in the next command.

```shell
$ aws apigateway create-rest-api --name project-api-proxy-cli
$ aws apigateway get-resources --rest-api-id poe2oi69ya
$ aws apigateway create-resource --rest-api-id poe2oi69ya --parent-id t72rhyoya1 --path-part hello
$ aws apigateway put-method --rest-api-id poe2oi69ya --resource-id csg1ls --http-method POST --authorization-type "NONE"
```

Notice in this case we don't set any method response (nothing about the response will be set) since Lambda itself is responsible for generating the final HTTP response to the user agent. As described before it's a much simpler integration process, it requires less configurations. 

### Create Proxy Integration

Having created (and tested) the Lambda function and the REST API, it's time to put the integration for the resource previously created. Since there's no need to add any request template, we just issue the ```aws apigateway put-integration``` command assuming the function arn from the steps above is 'arn:aws:lambda:eu-west-1:12123123123:function:HelloLambdaProxy'.

```shell
aws apigateway put-integration \
--rest-api-id poe2oi69ya \
--resource-id csg1ls \
--http-method POST \
--type AWS_PROXY \
--integration-http-method POST \
--uri arn:aws:apigateway:eu-west-1:lambda:path/2015-03-31/functions/arn:aws:lambda:eu-west-1:12123123123:function:HelloLambdaProxy/invocations
```

* --rest-api-id and --resource-id from the commands above.
* --http-method has to match one of the methods for the chosen resource, POST in this case is the only one.
* --type AWS_PROXY indicates a proxy integration with an AWS service, Lambda in this case.
* --integration-http-method is always POST for Lambda invocations.
* --uri is in the form of arn:aws:apigateway:{aws-region}:lambda:path/2015-03-31/functions/{function-arn}/invocations .

After this, we have closed the circuit from client API request to server API response. There is however, a missing piece in this puzzle.

### Adding Lambda permission

So far we've given permission to Lambda function to access CloudWatch logs so it can log to a log stream. However, we haven't given permission to API Gateway to invoke our Lambda function, this is done by creating a Lambda permission.

```shell
$ aws lambda add-permission --function-name HelloLambdaProxy \
--statement-id project-api-proxy-cli-inovke-lambda \
--action lambda:InvokeFunction \
--principal apigateway.amazonaws.com \
--source-arn arn:aws:execute-api:eu-west-1:123123123123:poe2oi69ya/*/POST/hello
```

* --statement-id is an id for the permission, api name + invoke lambda would make it more meaningful. 
* --action should be lambda:InvokeFunction, that's all we need from the Lambda function, being able to invoke it.
* --principal should be apigateway.amazonaws.com , indicates that API Gateway service will be granted the permission.
* --source-arn should be in the form of arn:aws:execute-api:{aws-region}:{accountNumber}:{restApiId}/{stage}/{method}/{path}

### Deploy & test the API

Just like previously, we need a deployment to make all these resources available through an endpoint.

```shell
$ aws apigateway create-deployment \
--rest-api-id poe2oi69ya \
--stage-name dev \
--stage-description "Development Stage" \
--description "First deployment to the dev stage"
```

A Rest API endpoint url is the format https://{rest-api-id}.execute-api.{AWS-region}.amazonaws.com/{stage-name}{path-and-query}

In this example, Rest API Id is poe2oi69ya and we've created a stage named dev in the region of Ireland (eu-west-1), to test the resource with path /hello we use the following curl command.

```shell
$ curl -X POST "https://poe2oi69ya.execute-api.eu-west-1.amazonaws.com/dev/hello" \
-d '{"Name":"Abel","Age":33}'

{"Name":"Abel","Old":false}
```

In the example above, both parameters have been supplied in order to get a good response from the Lambda function. Now we can test the other alternative using a value for age higher than 50.

```shell
$ curl -X POST "https://poe2oi69ya.execute-api.eu-west-1.amazonaws.com/dev/hello" \
-d '{"Name":"Abel","Age":88}'

{"Name":"Abel","Old":true}
```

If we were to produce the same output as in the Lambda integration example, then all the changes must be done in the Lambda function code, instead of returning a LambdaOutput object, we'd create a new class with the desired structure and we'd need to add the logic to create the output message.

Here is the code change, a new line between producing the output and sending the result back to the user agent. Also, we serialize the modified output instead of the raw output.

```c#
        public APIGatewayProxyResponse HelloHandler(APIGatewayProxyRequest apigProxyEvent, ILambdaContext context)
        {
            var input = JsonConvert.DeserializeObject<LambdaInput>(apigProxyEvent.Body);

            context.Logger.Log($"Hello {input.Name}, you are now {input.Age}");

            var output = new LambdaOutput { Name = input.Name, Old = input.Age > 50 };

            var apiOutput = new { Message = $"Dear {output.Name}, you are {(output.Old ? "": "not ")}old." };

            return new APIGatewayProxyResponse
            {
                Body = JsonConvert.SerializeObject(apiOutput),
                StatusCode = 200,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
```

This code needs to updated in Lambda in order to be accessed, here is the procedure to update it:

* Rebuild and upload the zip to S3
```shell
$ dotnet lambda package -c Release -f netcoreapp2.1 -o project-lambda-proxy.zip
$ aws s3 cp project-lambda-proxy.zip s3://abelperez-temp/project-lambda/
```
* Use the update-function-code command
```shell
$ aws lambda update-function-code \
--function-name HelloLambdaProxy \
--s3-bucket abelperez-temp \
--s3-key project-lambda/project-lambda-proxy.zip
```

* Invoke the function and watch the updated result
```shell
$ dotnet lambda invoke-function -fn HelloLambdaProxy -p '{"body": "{\"Name\":\"Abel\",\"Age\":88}"}' --region eu-west-1
...
Payload:
{"statusCode":200,"headers":{"Content-Type":"application/json"},"body":"{\"Message\":\"Dear Abel, you are old.\"}"}
...
```

With the Lambda function being updated, we can just retry invoking the REST API.

```shell
$ curl -X POST "https://poe2oi69ya.execute-api.eu-west-1.amazonaws.com/dev/hello" \
-d '{"Name":"Abel","Age":88}'

{"Message":"Dear Abel, you are old."}
```

## API Gateway Lambda integration using Open API (Swagger)

In this implementation we'll declare a swagger file where apart from the API definition, there will be the API Gateway integration extensions that are not part of the original specification. They are identified by **x-amazon-apigateway** prefix.

### Create the Swagger file

```yaml
swagger: "2.0"

info:
  version: 1.0.0
  title: Project API Swagger Proxy
  description: A simple API written using OpenAPI Specification with Lambda Proxy integration

paths:
```

Just like the previous example, the first section contains general metadata about the API.

```yaml
  /hello:
    post:
      summary: Simple Lambda integration
      description: Simple Lambda integration with a function previously created.
      parameters:
        - name: name
          in: body
          required: true
          description: The person's name
          type: string
        - name: age
          in: body
          required: true
          description: The person's age
          type: integer
      responses:
        200:
          description: A normal output name / old
          schema:
            properties:
              Message:
                type: string
```

In this section, we declare the resource /hello and a POST method accepting parameters **name** and **age** as part of the body payload. There's also a response with status code 200.

```yaml
      x-amazon-apigateway-integration:
        type: aws_proxy
        uri: arn:aws:apigateway:eu-west-1:lambda:path/2015-03-31/functions/arn:aws:lambda:eu-west-1:123123123123:function:HelloLambdaProxy/invocations
        httpMethod: POST
        responses:
          "default":
            statusCode: 200
```

Here we use the swagger extension **x-amazon-apigateway-integration** where we define what Lambda function will be invoked along with a response with status code 200.

### Import the REST API

With the Swagger ready, and assuming we still have the Lambda function deployed, we can now import the REST API.

```shell
$ aws apigateway import-rest-api --body file://swagger.yaml
{
    "apiKeySource": "HEADER",
    ...
    "version": "1.0.0",
    "createdDate": 1542473263,
    "id": "8qfsdj0eb3",
    "name": "Project API Swagger Proxy"
}
```

### Adding Lambda permission

Just like the example using CLI, we need to grant the REST API permission to invoke Lambda function.

```shell
$ aws lambda add-permission --function-name HelloLambda \
--statement-id project-api-swagger-inovke-lambda \
--action lambda:InvokeFunction \
--principal apigateway.amazonaws.com \
--source-arn arn:aws:execute-api:eu-west-1:123123123123:8qfsdj0eb3/*/GET/hello
```

### Deploy & test the API

Following the same procedure, create a deployment to get an endpoint.

```shell
$ aws apigateway create-deployment \
--rest-api-id 8qfsdj0eb3 \
--stage-name dev \
--stage-description "Development Stage" \
--description "First deployment to the dev stage"
```

Now we can issue the same **curl** commands as before.

```shell
$ curl -X POST "https://8qfsdj0eb3.execute-api.eu-west-1.amazonaws.com/dev/hello" \
-d '{"Name":"Abel","Age":33}'
$ curl -X POST "https://8qfsdj0eb3.execute-api.eu-west-1.amazonaws.com/dev/hello" \
-d '{"Name":"Abel","Age":88}'
```

## API Gateway Lambda integration using CloudFormation

In this implementation we'll create a CloudFormation template that contains all the resources we need, it will follow similar structure to the one explained in lesson 08.

### Create the CloudFormation template

```yaml
Description: Template to create a serverless web api 

Resources:
  AWSLambdaExecutionRole:
    Type: AWS::IAM::Role
    Properties:
      AssumeRolePolicyDocument:
        Version: 2012-10-17
        Statement:
          - Effect: Allow
            Principal:
              Service:
                - lambda.amazonaws.com
            Action: sts:AssumeRole
      Path: /
      Policies:
        - PolicyName: PermitLambda
          PolicyDocument:
            Version: 2012-10-17
            Statement:
            - Effect: Allow
              Action:
              - logs:CreateLogGroup
              - logs:CreateLogStream
              - logs:PutLogEvents
              Resource: arn:aws:logs:*:*:*

  HelloLambdaProxy:
    Type: AWS::Lambda::Function
    Properties:
      Handler: project.lambda::project.lambda.Function::HelloHandler
      Role: !GetAtt AWSLambdaExecutionRole.Arn
      Code:
        S3Bucket: abelperez-temp
        S3Key: project-lambda/project-lambda-proxy.zip
      Runtime: dotnetcore2.1
      Timeout: 30
```

In this section we declare the Lambda function and the IAM role associated with an inline policy that allows access to CloudWatch logs. Tha Lambda function uses the same compiled and zipped code in the above steps. 

```yaml
  ApiGatewayRestApi:
    Type: AWS::ApiGateway::RestApi
    Properties:
      Name: Serverless API Proxy
      Description: Serverless API - Using CloudFormation and Swagger - Proxy Integration

  ApiGatewayResourceHello:
    Type: AWS::ApiGateway::Resource
    Properties:
      ParentId: !GetAtt ApiGatewayRestApi.RootResourceId
      PathPart: hello
      RestApiId: !Ref ApiGatewayRestApi
```

Here we declare the REST API and the /hello resource associated with the root resource of the API.

```yaml
  ApiGatewayMethodHelloPost:
    Type: AWS::ApiGateway::Method
    Properties:
      HttpMethod: POST
      RequestParameters: {}
      ResourceId: !Ref ApiGatewayResourceHello
      RestApiId: !Ref ApiGatewayRestApi
      ApiKeyRequired: false
      AuthorizationType: NONE
      Integration:
        IntegrationHttpMethod: POST
        Type: AWS_PROXY
        Uri: !Sub
          - arn:aws:apigateway:${AWS::Region}:lambda:path/2015-03-31/functions/${RefFunc}/invocations
          - { RefFunc: !GetAtt HelloLambdaProxy.Arn }
        IntegrationResponses:
          - StatusCode: 200
            SelectionPattern: ''
      MethodResponses:
        - ResponseParameters: {}
          ResponseModels: {}
          StatusCode: 200
```

In this section we declare a POST method for the /hello resource. As part of this method definition, it's the integration, in this case, the Lambda function is also defined in the template and therefore we can use its reference as opposed to the hard-coded function ARN and AWS region.

```yaml
  ApiGatewayLambdaPermission:
    Type: AWS::Lambda::Permission
    Properties:
      FunctionName: !GetAtt HelloLambdaProxy.Arn
      Action: lambda:InvokeFunction
      Principal: apigateway.amazonaws.com
      SourceArn: !Sub 
        - arn:aws:execute-api:${AWS::Region}:${AWS::AccountId}:${RefApi}/*/*/*
        - { RefApi: !Ref ApiGatewayRestApi }
```

Once again, Lambda permission referencing both the Lambda function and the REST API.

```yaml
  ApiGatewayDeployment201811172102:
    Type: AWS::ApiGateway::Deployment
    Properties:
      RestApiId: !Ref ApiGatewayRestApi
      StageName: dev
    DependsOn:
      - ApiGatewayMethodHelloPost
```

A Deployment resource which contains a timestamp in the name, just like in lesson 08, so every time we deploy, it forces the creation of a new resource (and the deletion of the previous one). It's important not to forget to include the explicit dependency on the methods that should be included in the deployment.

```yaml
Outputs:
  ApiEndpoint:
    Description: "Endpoint to communicate with the API"
    Value: !Sub 
      - https://${RefApi}.execute-api.${AWS::Region}.amazonaws.com/dev/hello
      - RefApi: !Ref ApiGatewayRestApi
```
Finally, some output to get the endpoint to test the API.

### Deploy the stack & test the endpoint

```shell
$ aws cloudformation deploy \
--template-file cloudformation.yaml \
--stack-name project-lambda-cfn-api-proxy \
--capabilities CAPABILITY_IAM
```

Now get the outputs.

```shell
$ aws cloudformation describe-stacks --stack-name project-lambda-cfn-api-proxy --query Stacks[*].Outputs
[
    [
        {
            "Description": "Endpoint to communicate with the API",
            "OutputKey": "ApiEndpoint",
            "OutputValue": "https://1153cq3qnh.execute-api.eu-west-1.amazonaws.com/dev/hello"
        }
    ]
]
```

Issue **curl** command to test the urls with the above test cases.

```shell
$ curl -X GET "https://1153cq3qnh.execute-api.eu-west-1.amazonaws.com/dev/hello?name=Abel&age=33"
$ curl -X GET "https://1153cq3qnh.execute-api.eu-west-1.amazonaws.com/dev/hello?name=Abel&age=88"
```

## Conclusion

We have seen a basic example of Lambda Proxy integration with API Gateway using three different implementations: AWS CLI, Swagger and CloudFormation. API Gateway allows the whole HTTP request to be wrapped and passed to Lambda functions which gives a lot of control over the HTTP request / response handling. API Gateway integration extensions can be used from a Swagger file. CloudFormation allows to build a template containing all required resources for a fully functional REST API and Lambda back end.