# Lesson 08 - API Gateway - CloudFormation

In previous lessons we've seen how to build a REST API using API Gateway both from scratch purely AWS CLI and importing it from a Open API specification file. In this lesson, we'll implement the same REST API but with AWS CloudFormation.

## Writing the CloudFormation template

Let's break this example down into sections for a better understanding.

### Section 1 - Introduction 

```yaml
Description: Template to create a serverless web api 

Resources:
```

In this section we write some general description about the template and start the **Resources** block.

### Section 2 - Rest API resource

```yaml
  ApiGatewayRestApi:
    Type: AWS::ApiGateway::RestApi
    Properties:
      Name: Serverless API
      Description: Serverless API - Using CloudFormation and Swagger
```

In this section we declare the resource RestApi which represents the main container of all resources and paths.

### Section 3 - Resource /hello & GET method

```yaml
ApiGatewayResourceHello:
    Type: AWS::ApiGateway::Resource
    Properties:
      ParentId: !GetAtt ApiGatewayRestApi.RootResourceId
      PathPart: hello
      RestApiId: !Ref ApiGatewayRestApi

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
        Type: MOCK
        PassthroughBehavior: NEVER
        RequestTemplates:
          application/json: |
            {
              "statusCode" : 200
            }
        IntegrationResponses:
          - StatusCode: 200
            SelectionPattern: ''
            ResponseParameters: {}
            ResponseTemplates:
              application/json: |
                {
                  "message": "hello 123"
                }
      MethodResponses:
        - ResponseParameters: {}
          ResponseModels: {}
          StatusCode: 200
```

In this section we define two CloudFormation resources: ApiGatewayResourceHello and ApiGatewayMethodHelloGet. Just like in the example using AWS CLI, we need first to define an ApiGateway Resource, in this case, it will listen on /hello. Since it's a top level resource, its parent id is the RootResourceId from the RestApi.

Unlike when creating the API from AWS CLI, a Method resource contains all the information about the integration, which in this case is just a MOCK. RequestTemplates and ResponseTemplates are exactly the same used in the Swagger example.

### Section 4 - Resource /hello/{id} & GET method

```yaml
  ApiGatewayResourceHelloId:
    Type: AWS::ApiGateway::Resource
    Properties:
      ParentId: !Ref ApiGatewayResourceHello
      PathPart: "{id}"
      RestApiId: !Ref ApiGatewayRestApi

  ApiGatewayMethodHelloIdGet:
    Type: AWS::ApiGateway::Method
    Properties:
      HttpMethod: GET
      RequestParameters:
        method.request.path.id: true
      ResourceId: !Ref ApiGatewayResourceHelloId
      RestApiId: !Ref ApiGatewayRestApi
      ApiKeyRequired: false
      AuthorizationType: NONE
      Integration:
        Type: MOCK
        PassthroughBehavior: NEVER
        RequestTemplates:
          application/json: |
            {
            #if( $input.params('id') == 123 )
              "statusCode": 200
            #else
              "statusCode": 404
            #end
            }
        IntegrationResponses:
          - StatusCode: 200
            SelectionPattern: '200'
            ResponseParameters: {}
            ResponseTemplates:
              application/json: |
                {
                  "id": "123",
                  "name": "name123"
                }
          - StatusCode: 404
            SelectionPattern: '404'
            ResponseParameters: {}
            ResponseTemplates:
              application/json: |
                {
                  "message": "ID Not Found"
                }
      MethodResponses:
        - ResponseParameters: {}
          ResponseModels: {}
          StatusCode: 200
        - ResponseParameters: {}
          ResponseModels: {}
          StatusCode: 404
```

In this section we define another Api resource and method, this time to respond to /hello/{id}. Since ApiGatewayResourceHelloId is not a top level resource, its parent id is in fact, ApiGatewayResourceHello previously declared. Once again, RequestTemplates and ResponseTemplates are the same from the Swagger example. 

When configuring IntegrationResponses it's important to set correctly the SelectionPattern parameter, it's the only way it has to distinguish between one integration response and another.

### Section 5 - Deployment resource

```yaml
  ApiGatewayDeployment201810281321:
    Type: AWS::ApiGateway::Deployment
    Properties:
      RestApiId: !Ref ApiGatewayRestApi
      StageName: dev
    DependsOn:
      - ApiGatewayMethodHelloGet
      - ApiGatewayMethodHelloIdGet
```

Once we have defined all we need to have a fully functional REST API using API Gateway, it's time to create a deployment resource. This is a particular resource in this scenario, because of the way API Gateway works, two important points to observe here:

* We have to create a new deployment every time we want to make a change to be "deployed" to a specific Stage, hence the timestamp at the end of the name in the example, ideally this kind of tasks can be automated when being part of a Ci/CD pipeline.
* It requires an explicit dependency on each Api Gateway Method we'd like to be included in the deployment, this is achieved by using DependsOn property available on every resource in CloudFormation. 

### Section 6 - Template Outputs

```yaml
Outputs:
  ApiEndpoint:
    Description: "Endpoint to communicate with the API"
    Value: !Sub 
      - https://${RefApi}.execute-api.${AWS::Region}.amazonaws.com/dev/hello
      - RefApi: !Ref ApiGatewayRestApi
```

Finally, we declare the **Outputs**  block where in this case we are only interested in the REST API endpoint so we know where to point when testing.

## Deploy the CloudFormation stack

With the template completed, we can use AWS CloudFormation CLI to accomplish this task, ```aws cloudformation deploy``` command will be used to create / update the stack.

```shell
$ aws cloudformation deploy --template-file cloudformation.yaml --stack-name project-lambda-cfn-api

Waiting for changeset to be created..
Waiting for stack create/update to complete
Successfully created/updated stack - project-lambda-cfn-api
```

Now that the REST API has been deployed, we can query to get the Outputs by using ```aws cloudformation describe-stacks``` command.

```shell
$ aws cloudformation describe-stacks --stack-name project-lambda-cfn-api --query Stacks[*].Outputs

[
    [
        {
            "Description": "Endpoint to communicate with the API",
            "OutputKey": "ApiEndpoint",
            "OutputValue": "https://76xgf0jwc2.execute-api.eu-west-1.amazonaws.com/dev/hello"
        }
    ]
]
```

## Testing the endpoint

Let's test the three possible scenarios: /dev/hello, /dev/hello/123 expecting a HTTP 200 and /dev/hello/111 (or anything but 123) expecting a HTTP 404.

```shell
$ curl -X GET https://76xgf0jwc2.execute-api.eu-west-1.amazonaws.com/dev/hello
{
  "message": "hello 123"
}
```
```shell
$ curl -X GET https://76xgf0jwc2.execute-api.eu-west-1.amazonaws.com/dev/hello/123
{
  "id": "123",
  "name": "name123"
}
```
```shell
$ curl -X GET https://76xgf0jwc2.execute-api.eu-west-1.amazonaws.com/dev/hello/111
{
  "message": "ID Not Found"
}
```

As we can see, using a CloudFormation template is another approach that allows to keep the API resources under source control as part of the application code base. Therefore it can be included in any CI/CD pipeline as part of daily code commits.

## Cleaning up

When we are done with the REST API, we can delete the stack using the ```aws cloudformation delete-stack``` command.

```shell
$ aws cloudformation delete-stack --stack-name project-lambda-cfn-api
```

This commands produces no output, and the deletion might not be immediate, in which case we can use the ```wait``` command.

```shell
$ aws cloudformation wait stack-delete-complete --stack-name project-lambda-cfn-api
```