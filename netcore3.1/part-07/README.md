# Part 7 - API Gateway - Open API Specification

In the previous part we've seen the fundamental components of Rest API using API Gateway. By using the CLI we created our first API step by step. There is another way to create an API without the complexity of the CLI, using OpenAPI specification.

## OpenAPI Specification and API Gateway

The [OpenAPI Specification](https://github.com/OAI/OpenAPI-Specification/blob/master/versions/3.0.3.md) is an API description format or API definition language. Basically, an OpenAPI Specification file allow you to describe an API including (amongst other things):

* General information about the API
* Available paths (/resources)
* Available operations on each path (get /resources)
* Input/Output for each operation

API Gateway allows us to import an API described in a formerly named "swagger file" which simplifies the development of an API. Details of the syntax is out of the scope of this guide, there is a good tutorial [here](https://app.swaggerhub.com/help/tutorials/openapi-3-tutorial) which you can follow to learn more. 

The Rest API created in the previous part can be rewritten using Open API. API Gateway also provides extensions to Open API specification to describe integration, authentication, validation, amongst other things. More info on [AWS docs](https://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-swagger-extensions.html).

## Writing the Open API file

Let's break this example down into sections for a better understanding.

### Section 1 - Introduction

```yaml
openapi: "3.0.1"
info:
  title: Hello API
  version: "1.0"
  description: A simple Rest API written using OpenAPI Specification

paths:
```

In this section we write some metadata and general description about the API and start the paths block.

### Section 2 - Resource /hello

```yaml
  /hello:
    get:
      summary: Sample endpoint
      description: Displays a hello message.
      responses:
        200:
          description: A hello message
          content:
            application/json:
              schema:
                type: object
                properties:
                  message:
                    type: string
```

In this section we declare /hello resource path, add a GET method and a method response 200. It also includes a schema of the possible response containing a property message.

### Section 3 - Integration and Integration Response

```yaml
      x-amazon-apigateway-integration:
        type: "mock"
        requestTemplates:
          application/json: |
            {
              "statusCode": 200
            }
        responses:
          default:
            statusCode: "200"
            responseTemplates:
              application/json: |
                {
                  "message": "hello 123"
                }
        passthroughBehavior: "when_no_match"
```

In this section we use one of the API Gateway extensions to declare the integration which is a mock indicating only 200 as a possible response and detailing the integration response to a fixed JSON result.

### Section 4 - Resource /hello/{id}

```yaml
  /hello/{id}:
    get:
      summary: Sample endpoint with path parameter id
      description: Displays a hello message.
      parameters:
        - name: id
          in: path
          required: true
          description: specific id
          schema:
            type: integer
      responses:
        200:
          description: A sample id and name
          content:
            application/json:
              schema:
                type: object
                properties:
                  id:
                    type: integer
                  name:
                    type: string
        404:
          description: The provided id was not found
```

In this section we declare /hello/{id} resource path, add a GET method and two method responses: 200 and 404. It also includes a schema for 200 response containing id and name properties.

### Section 5 - Integration and Integration Response

```yaml
      x-amazon-apigateway-integration:
        type: mock
        requestTemplates:
          application/json: |
            {
            #if( $input.params('id') == 123 )
              "statusCode": 200
            #else
              "statusCode": 404
            #end
            }
        responses:
          "default":
            statusCode: "200"
            responseTemplates:
              application/json: |
                {
                  "id": "123",
                  "name": "name123"
                }
          "404":
            statusCode: "404"
            responseTemplates:
              application/json: |
                {
                  "message": "ID Not Found"
                }
```

In this section we use again one of the API Gateway extensions to declare the integration which is a mock indicating two possible response codes: 200 or 404. Request template contains the logic to decide when to send either of the response codes based on the id path parameter. The integration response declare the two possible JSON results.

## Importing the API

Once we have the yaml file ready, let's assume it's named ```openapi.yaml``` and it's in current directory. We can use the ```import-rest-api``` command to let API Gateway know about our import process.

```shell
$ aws apigateway import-rest-api --body fileb://openapi.yaml
{
    "id": "rnwimto165",
    "name": "Hello API",
    "description": "A simple Rest API written using OpenAPI Specification",
    "createdDate": "2020-04-30T19:15:10+01:00",
    "version": "1.0",
    "apiKeySource": "HEADER",
    "endpointConfiguration": {
        "types": [
            "EDGE"
        ]
    }
}
```

At this point our API already exists and we've got its ID. Next is to create a Stage and a Deployment. Like in the previous part, let's create a dev Stage and deploy the first version.

```shell
$ aws apigateway create-deployment \
--rest-api-id rnwimto165 \
--stage-name dev \
--stage-description 'Development Stage' \
--description 'Testing dev stage'
{
    "description": "Testing dev stage",
    "id": "6dqsjp",
    "createdDate": 1535378123
}
```
## Testing the endpoint

Now the API has been deployed to dev Stage, let's remind the API endpoint format https://{rest-api-id}.execute-api.{AWS-region}.amazonaws.com/{stage-name}{path-and-query}

Let's test the three possible scenarios

```shell
$ curl -X GET https://rnwimto165.execute-api.eu-west-1.amazonaws.com/dev/hello
{
  "message": "hello 123"
}
```
```shell
$ curl -X GET https://rnwimto165.execute-api.eu-west-1.amazonaws.com/dev/hello/123
{
  "id": "123",
  "name": "name123"
}
```
```shell
$ curl -X GET https://rnwimto165.execute-api.eu-west-1.amazonaws.com/dev/hello/111
{
  "message": "ID Not Found"
}
```

As we can see, using an Open API definition is a much clearer approach as it allows us to keep it under source control as part of the application code base. Therefore it can be included in any CI/CD pipeline as part of daily code commits.

## Cleaning up

Same as in the previous part, we can delete the API once we no longer need it.

```shell
$ aws apigateway delete-rest-api --rest-api-id rnwimto165
```