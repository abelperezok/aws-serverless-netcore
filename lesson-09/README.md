# Lesson 09 - API Gateway - Lambda integration

Up to this point, we've seen how we can leverage API Gateway to create REST APIs, the example used is just enough to get the bare minimum on how it works by using a MOCK integration. API Gateway can be integrated with Lambda, in fact, this is one of the most common service combinations to create serverless applications. There are two ways to achieve this integration: Lambda integration (AWS) and Proxy integration (AWS_PROXY).

In this lesson we'll see how to integrate a Lambda function with API Gateway using Lambda integration (AWS). This approach defines a clear separation between the API responsibilities and the Lambda function, the function doesn't know anything about how is going to be invoked and therefore is totally independent from any HTTP handling. The API, on the other hand, will carry out all the mappings from the HTTP request to the Lambda function input and all the way back from the Lambda result to the end user HTTP response.

In the following sections we will create a Lambda integration using three different implementations in order to better understand the details. First, using purely AWS CLI, then from a Swagger file and finally with a CloudFormation template.

## Prepare the Lambda function

In this section we'll deploy a Lambda function which will be common to the first and second integration implementations. The code will be the same as in lesson 05 and can be copied from there to follow along. In a directory with files Function.cs and project.lambda.csproj we'll create and deploy the Lambda function by running the following commands (more details in lesson 05).

* abelperez-temp is the bucket of your choice to upload the code to S3.
* project-lambda/ is just a folder inside the bucket.
* the value for --role parameter is copied from the output of the aws iam create-role command.
* do take note of the function arn as we'll need it later for the integration.

```shell
$ dotnet lambda package -c Release -f netcoreapp2.1 -o project-lambda.zip
$ aws s3 cp project-lambda.zip s3://abelperez-temp/project-lambda/
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
$ aws lambda create-function --function-name HelloLambda \
--code S3Bucket=abelperez-temp,S3Key=project-lambda/project-lambda.zip \
--role arn:aws:iam::123123123123:role/HelloLambdaRole \
--handler project.lambda::project.lambda.Function::HelloHandler \
--runtime dotnetcore2.1 --timeout 30
```

## Test the function in isolation before integrating

Use dotnet lambda invoke-function command for simplicity, here are two examples to cover both scenarios.

```shell
$ dotnet lambda invoke-function -fn HelloLambda -p "{\"Name\":\"Abel\",\"Age\":33}" --region eu-west-1
$ dotnet lambda invoke-function -fn HelloLambda -p "{\"Name\":\"Abel\",\"Age\":88}" --region eu-west-1
```

## API Gateway Lambda integration using AWS CLI

First, we need an API and at least one resource with a method, let's create them.

### Create the REST API & resource

Following the same procedure as in lesson 60, we create a REST API with one resource listening on /hello and a GET Method. Each command's output returns ids used in the next command.

```shell
$ aws apigateway create-rest-api --name project-api-cli
$ aws apigateway get-resources --rest-api-id ryk941lawh
$ aws apigateway create-resource --rest-api-id ryk941lawh --parent-id cvzihh5l2d --path-part hello
$ aws apigateway put-method --rest-api-id ryk941lawh --resource-id bay12i --http-method GET --authorization-type "NONE"
$ aws apigateway put-method-response --rest-api-id ryk941lawh --resource-id bay12i --http-method GET --status-code 200
```

### Create Integration and Integration Response

First, we need to set up a request mapping template, the same way it's done with MOCK integration. Create a JSON file with the template mapping, in this case, we define that query string / header parameters **name** and **age** will be passed respectively to **Name** and **Age** input properties. This JSON is escaped because it's the value expected as a string for every key as content type. $input.params() retrieves the parameters from query string and headers (in that order).

```shell
$ cat > request-template.json <<EOM
{
    "application/json": "{\"Name\":\"\$input.params('name')\",\"Age\":\"\$input.params('age')\"}"
}
EOM
```

Assuming the function arn from the steps above is 'arn:aws:lambda:eu-west-1:123123123123:function:HelloLambda', let's create the integration by using the ```aws apigateway put-integration``` command.

```shell
$ aws apigateway put-integration --rest-api-id ryk941lawh \
--resource-id bay12i --http-method GET --type AWS \
--integration-http-method POST \
--uri arn:aws:apigateway:eu-west-1:lambda:path/2015-03-31/functions/arn:aws:lambda:eu-west-1:123123123123:function:HelloLambda/invocations \
--request-templates file://request-template.json
```

* --rest-api-id and --resource-id from the commands above.
* --http-method has to match one of the methods for the chosen resource, GET in this case is the only one.
* --integration-http-method is always POST.
* --uri is in the form of arn:aws:apigateway:{aws-region}:lambda:path/2015-03-31/functions/{function-arn}/invocations .
* --request-templates we use the file previously created.

Second, we need to set up a response mapping template, just like in the MOCK example. Similarly we create a JSON file which follows the same format. 

```shell
$ cat > response-template.json <<EOM
{
    "application/json": "#set (\$root=\$input.path('$'))\n{\n\"Message\": \"Dear \$root.Name, you are#if(!\$root.Old) not#end old.\"\n}"
}
EOM
```

This example is also escaped, it reads from the Lambda output and creates a new JSON with a Message property and a simple logic to adapt the content, this is the non-escaped version:

```json
#set ($root=$input.path('$'))
{
"Message": "Dear $root.Name, you are#if(!$root.Old) not#end old."
}
```

Notice the directives #if and #end. Now, add the integration response.

```shell
$ aws apigateway put-integration-response --rest-api-id ryk941lawh \
--resource-id bay12i --http-method GET \
--status-code 200 --selection-pattern "" \
--response-templates file://response-template.json
```

* --rest-api-id and --resource-id from the commands above.
* --http-method has to match one of the methods for the chosen resource, GET in this case is the only one.
* --status-code has to match one of the method responses created above, 200 is this case is the only one.
* --selection-pattern indicates the regular expression to identify the status code from the Lambda output, empty means it's the default one.

After this, we have closed the circuit from client API request to server API response. There is however, a missing piece in this puzzle.

### Adding Lambda permission

So far we've given permission to Lambda function to access CloudWatch logs so it can log to a log stream. However, we haven't given permission to API Gateway to invoke our Lambda function, this is done by creating a Lambda permission.

```shell
$ aws lambda add-permission --function-name HelloLambda \
--statement-id project-api-cli-inovke-lambda \
--action lambda:InvokeFunction \
--principal apigateway.amazonaws.com \
--source-arn arn:aws:execute-api:eu-west-1:123123123123:ryk941lawh/*/GET/hello
```

* --statement-id is an id for the permission, api name + invoke lambda would make it more meaningful. 
* --action should be lambda:InvokeFunction, that's all we need from the Lambda function, being able to invoke it.
* --principal should be apigateway.amazonaws.com , indicates that API Gateway service will be granted the permission.
* --source-arn should be in the form of arn:aws:execute-api:{aws-region}:{accountNumber}:{restApiId}/{stage}/{method}/{path}

### Deploy & test the API

Just like previously, we need a deployment to make all these resources available through an endpoint.

```shell
$ aws apigateway create-deployment \
--rest-api-id ryk941lawh \
--stage-name dev \
--stage-description "Development Stage" \
--description "First deployment to the dev stage"
```

A Rest API endpoint url is the format https://{rest-api-id}.execute-api.{AWS-region}.amazonaws.com/{stage-name}{path-and-query}

In this example, Rest API Id is ryk941lawh and we've created a stage named dev in the region of Ireland (eu-west-1), to test the resource with path /hello we use the following curl command.

```shell
$ curl -X GET "https://ryk941lawh.execute-api.eu-west-1.amazonaws.com/dev/hello?name=Abel&age=33"
{
"Message": "Dear Abel, you are not old."
}
```

In the example above, both parameters have been supplied in order to get a good response from the Lambda function. Now we can test the other alternative using a value for age higher than 50.

```shell
$ curl -X GET "https://ryk941lawh.execute-api.eu-west-1.amazonaws.com/dev/hello?name=Abel&age=88"
{
"Message": "Dear Abel, you are old."
}
```

## API Gateway Lambda integration using Open API (Swagger)

In this implementation we'll declare a swagger file where apart from the API definition, there will be the API Gateway integration extensions that are not part of the original specification. They are identified by **x-amazon-apigateway prefix**.

### Create the Swagger file

```yaml
swagger: "2.0"

info:
  version: 1.0.0
  title: Project API Swagger
  description: A simple API written using OpenAPI Specification with Lambda integration

paths:
```

Just like the previous example, the first section contains general metadata about the API.

```yaml
  /hello:
    get:
      summary: Simple Lambda integration
      description: Simple Lambda integration with a function previously created.
      parameters:
        - name: name
          in: query
          required: true
          description: The person's name
          type: string
        - name: age
          in: query
          required: true
          description: The person's age
          type: integer
      responses:
        200:
          description: A normal output name / old
          schema:
            properties:
              Name:
                type: string
              Old:
                type: boolean
```

In this section, we declare the resource /hello and a GET method with two parameters: **name** and **age** as well as a response with status code 200.

```yaml
      x-amazon-apigateway-integration:
        type: aws
        uri: arn:aws:apigateway:eu-west-1:lambda:path/2015-03-31/functions/arn:aws:lambda:eu-west-1:123123123123:function:HelloLambda/invocations
        httpMethod: POST
        requestTemplates:
          application/json: |
            {
              "Name": "$input.params('name')",
              "Age": $input.params('age')
            }
        responses:
          "default":
            statusCode: 200
            responseTemplates:
              application/json: |
                #set ($root=$input.path('$')) 
                {
                  "Message": "Dear $root.Name, you are#if(!$root.Old) not#end old."
                }
```

Here we use the swagger extension x-amazon=apigateway-integration where we define what Lambda function will be invoked. The request template and response template are the same as above, just now taking advantage of YAML syntax that allows multi-line text. 

### Import the REST API

With the Swagger ready, and assuming we still have the Lambda function deployed, we can now import the REST API.

```shell
$ aws apigateway import-rest-api --body file://swagger.yaml
{
    "apiKeySource": "HEADER",
    "description": "A simple API written using OpenAPI Specification with Lambda integration",
    "endpointConfiguration": {
        "types": [
            "EDGE"
        ]
    },
    "version": "1.0.0",
    "createdDate": 1541353528,
    "id": "fjw1w4q0q8",
    "name": "Project API Swagger"
}
```

### Adding Lambda permission

Just like the example using CLI, we need to grant the REST API permission to invoke Lambda function.

```shell
$ aws lambda add-permission --function-name HelloLambda \
--statement-id project-api-swagger-inovke-lambda \
--action lambda:InvokeFunction \
--principal apigateway.amazonaws.com \
--source-arn arn:aws:execute-api:eu-west-1:976153948458:fjw1w4q0q8/*/GET/hello
```

### Deploy & test the API

Following the same procedure, create a deployment to get an endpoint.

```shell
$ aws apigateway create-deployment \
--rest-api-id fjw1w4q0q8 \
--stage-name dev \
--stage-description "Development Stage" \
--description "First deployment to the dev stage"
```

Now we can issue the same **curl** commands as before.

```shell
$ curl -X GET "https://fjw1w4q0q8.execute-api.eu-west-1.amazonaws.com/dev/hello?name=Abel&age=33"
$ curl -X GET "https://fjw1w4q0q8.execute-api.eu-west-1.amazonaws.com/dev/hello?name=Abel&age=88"
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

  HelloLambda:
    Type: AWS::Lambda::Function
    Properties:
      Handler: project.lambda::project.lambda.Function::HelloHandler
      Role: !GetAtt AWSLambdaExecutionRole.Arn
      Code:
        S3Bucket: abelperez-temp
        S3Key: project-lambda/deploy-package.zip
      Runtime: dotnetcore2.1
      Timeout: 30
```

In this section we declare the Lambda function and the IAM role associated with an inline policy that allows access to CloudWatch logs. Tha Lambda function uses the same compiled and zipped code in the above steps. 

```yaml
  ApiGatewayRestApi:
    Type: AWS::ApiGateway::RestApi
    Properties:
      Name: Serverless API
      Description: Serverless API - Using CloudFormation and Swagger

  ApiGatewayResourceHello:
    Type: AWS::ApiGateway::Resource
    Properties:
      ParentId: !GetAtt ApiGatewayRestApi.RootResourceId
      PathPart: hello
      RestApiId: !Ref ApiGatewayRestApi
```

Here we declare the REST API and the /hello resource associated with the root resource of the API.

```yaml
  ApiGatewayMethodHelloGet:
    Type: AWS::ApiGateway::Method
    Properties:
      HttpMethod: GET
      RequestParameters: {}
      ResourceId: !Ref ApiGatewayResourceHello
      RestApiId: !Ref ApiGatewayRestApi
      ApiKeyRequired: false
      AuthorizationType: NONE
      Integration:
        IntegrationHttpMethod: POST
        Type: AWS
        Uri: !Sub 
          - arn:aws:apigateway:${AWS::Region}:lambda:path/2015-03-31/functions/${RefFunc}/invocations
          - { RefFunc: !GetAtt HelloLambda.Arn }
        PassthroughBehavior: NEVER
        RequestTemplates:
          application/json: |
            {
              "Name": "$input.params('name')",
              "Age": "$input.params('age')"
            }
        IntegrationResponses:
          - StatusCode: 200
            SelectionPattern: ''
            ResponseParameters: {}
            ResponseTemplates: 
              application/json: |
                  #set ($root=$input.path('$')) 
                  {
                    "Message": "Dear $root.Name, you are#if(!$root.Old) not#end old."
                  }
      MethodResponses:
        - ResponseParameters: {}
          ResponseModels: {}
          StatusCode: 200
```

In this section we declare a GET method for the /hello resource. As part of this method definition, it's the integration, in this case, the Lambda function is also defined in the template and therefore we can use its reference as opposed to the hard-coded function ARN and AWS region.

```yaml
  ApiGatewayLambdaPermission:
    Type: AWS::Lambda::Permission
    Properties:
      FunctionName: !GetAtt HelloLambda.Arn
      Action: lambda:InvokeFunction
      Principal: apigateway.amazonaws.com
      SourceArn: !Sub 
        - arn:aws:execute-api:${AWS::Region}:${AWS::AccountId}:${RefApi}/*/*/*
        - { RefApi: !Ref ApiGatewayRestApi }
```

Once again, Lambda permission referencing both the Lambda function and the REST API.

```yaml
  ApiGatewayDeployment201811052246:
    Type: AWS::ApiGateway::Deployment
    Properties:
      RestApiId: !Ref ApiGatewayRestApi
      StageName: dev
    DependsOn:
      - ApiGatewayMethodHelloGet
```

A Deployment resource which contains a timestamp in the name, just like in the previous lesson, so every time we deploy, force the creation of a new resource (and the deletion of the previous one). It's important not to forget to include the explicit dependency on the methods that should be included in the deployment.

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
$ aws cloudformation deploy --template-file cloudformation.yaml --stack-name project-api-cfn --capabilities CAPABILITY_IAM
```

Now get the outputs.

```shell
$ aws cloudformation describe-stacks --stack-name project-api-cfn --query Stacks[*].Outputs
[
    [
        {
            "Description": "Endpoint to communicate with the API",
            "OutputKey": "ApiEndpoint",
            "OutputValue": "https://4592drbcvf.execute-api.eu-west-1.amazonaws.com/dev/hello"
        }
    ]
]
```

Issue **curl** command to test the urls with the above test cases.

```shell
$ curl -X GET "https://4592drbcvf.execute-api.eu-west-1.amazonaws.com/dev/hello?name=Abel&age=33"
$ curl -X GET "https://4592drbcvf.execute-api.eu-west-1.amazonaws.com/dev/hello?name=Abel&age=88"
```

## Conclusion

We have seen a basic example of Lambda integration with API Gateway using three different implementations: AWS CLI, Swagger and CloudFormation. API Gateway is very flexible when it comes to mapping requests / responses from / to Lambda functions. API Gateway integration extensions can be used from a Swagger file. CloudFormation allows to build a template containing all required resources for a fully functional REST API and Lambda back end.