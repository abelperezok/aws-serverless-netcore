# Part 6 - API Gateway - Introduction

Amazon API Gateway is a managed service that helps us creating REST APIs which can act as a front end to any existing back end. There are some important concepts in this service that I'll clarify before proceeding, since they can be sometimes confusing.

## Rest API

A Rest API is the root resource of every API created, it's the parent container of every resource and receives a name. Once created it's given an ID that will be used to access the API endpoints.

```shell
$ aws apigateway create-rest-api --name 'project-api-test'
{
    "apiKeySource": "HEADER",
    "name": "project-api-test",
     ... ,
    "id": "oqcuns04we"
}
```

## Resource / Child Resource

A Rest API contain many resources. A resource is associated with a path endpoint. When a new API is created, a default resource is created with the path "/". This default resource will be the parent of any other resources that are added to the API.

To get this parent resource ID, we can use the command ```aws apigateway get-resources --rest-api-id oqcuns04we``` optionally querying by path == "/" ```--query 'items[?path==`/`][id]'```.

```shell
$ aws apigateway get-resources --rest-api-id oqcuns04we
{
    "items": [
        {
            "path": "/",
            "id": "0589d1mne3"
        }
    ]
}
```

To create a new resource we need to provide both the Rest API ID and the parent resource ID as well as the path i.e to create a resource under "/hello"

```shell
$ aws apigateway create-resource --rest-api-id oqcuns04we \
--parent-id 0589d1mne3 \
--path-part hello
{
    "path": "/hello",
    "pathPart": "hello",
    "id": "oav4gj",
    "parentId": "0589d1mne3"
}
```

If we want to create another resource under "/hello" with a path parameter for example "/hello/{id}" the "{id}" path will be another resource as a child of "/hello".

```shell
$ aws apigateway create-resource --rest-api-id oqcuns04we \
--parent-id oav4gj \
--path-part {id}
{
    "path": "/hello/{id}",
    "pathPart": "{id}",
    "id": "abcw5u",
    "parentId": "oav4gj"
}
```

## Method (Request / HTTP Verb)

A method (this refers to request method) corresponds to a HTTP verb that will be associated with a given resource. A resource can contain one or more methods as we'd expect from a Rest API.

To associate the GET method with the two resources created above, we use the following commands:

```shell
$ aws apigateway put-method --rest-api-id oqcuns04we \
--resource-id oav4gj \
--http-method GET \
--authorization-type "NONE"
{
    "apiKeyRequired": false,
    "httpMethod": "GET",
    "authorizationType": "NONE"
}
```

```shell
$ aws apigateway put-method --rest-api-id oqcuns04we \
--resource-id abcw5u \
--http-method GET \
--authorization-type "NONE" \
--request-parameters method.request.path.id=true
{
    "apiKeyRequired": false,
    "httpMethod": "GET",
    "authorizationType": "NONE",
    "requestParameters": {
        "method.request.path.id": true
    }
}
```

## Method Response (Response / HTTP Verb)

A method response corresponds to one or more possible HTTP Response code for a given resource on a given request method (verb). Some examples: 
* Resource with path "/hello" on method GET can respond with a status code 200.
* Resource with path "/hello/{id}" on method GET can respond with either status code 200 or 404 if the provided id is not found.

The following commands will satisfy the above criteria.
```shell
$ aws apigateway put-method-response --rest-api-id oqcuns04we \
--resource-id oav4gj --http-method GET \
--status-code 200
{
    "statusCode": "200"
}
```

```shell
$ aws apigateway put-method-response --rest-api-id oqcuns04we \
--resource-id abcw5u --http-method GET \
--status-code 200
{
    "statusCode": "200"
}
```

```shell
$ aws apigateway put-method-response --rest-api-id oqcuns04we \
--resource-id abcw5u --http-method GET \
--status-code 404
{
    "statusCode": "404"
}
```

## Integration (Request)

An integration refers to how the request will be mapped to a back end for a given resource on a given method and for a given content type. The request template can be use to modify the input to be sent to the back end.

In this example we'll use the MOCK integration which means no real back end will be used and essentially every GET request to "/hello" resource will result in a HTTP 200 response with a content type "application/json".

```shell
$ aws apigateway put-integration \
--rest-api-id oqcuns04we \
--resource-id oav4gj \
--http-method GET \
--type MOCK \
--request-templates '{ "application/json": "{\"statusCode\": 200}" }'
{
    "passthroughBehavior": "WHEN_NO_MATCH",
    "requestTemplates": {
        "application/json": "{\"statusCode\": 200}"
    },
    "cacheKeyParameters": [],
    "type": "MOCK",
    "timeoutInMillis": 29000,
    "cacheNamespace": "oav4gj"
}
```

Adding a little bit of complexity now, we can express the "logic" to decide what response to send out depending on the input values, in this case depending on the path parameter id, if it's 123 we'll respond with 200 otherwise we'll respond with a 404.

This logic will be part of the ```--request-template``` parameter, the problem here is that it needs to be escaped inside the JSON, this is the original JSON code.
```
{
#if( $input.params('id') == "123" )
    "statusCode": 200
#else
    "statusCode": 404
#end
}
```
When escaped for the command line it looks like this.

```shell
$ aws apigateway put-integration \
--rest-api-id oqcuns04we \
--resource-id abcw5u \
--http-method GET \
--type MOCK \
--request-templates '{"application/json": "{\n#if( $input.params(\"id\") == \"123\" )\n    \"statusCode\": 200\n#else\n    \"statusCode\": 404\n#end\n}"}'
{
    "passthroughBehavior": "WHEN_NO_MATCH",
    "requestTemplates": {
        "application/json": "{\n#if( $input.params(\"id\") == \"123\" )\n    \"statusCode\": 200\n#else\n    \"statusCode\": 404\n#end\n}"
    },
    "cacheKeyParameters": [],
    "type": "MOCK",
    "timeoutInMillis": 29000,
    "cacheNamespace": "abcw5u"
}
```

## Integration Response

An integration response refers to how the response from the back end is mapped to the client. There should be an integration response for each method response. The response template can be use to modify the final output to the client.

Following the above example
* Resource with path "/hello" has one method response 200
* Resource with path "/hello/{id}" has two method responses: 200 and 404.

Therefore we need three integration responses in this case.

```shell
$ aws apigateway put-integration-response \
--rest-api-id oqcuns04we \
--resource-id oav4gj \
--http-method GET \
--status-code 200 \
--selection-pattern "" \
--response-templates '{"application/json": "{\"message\": \"hello 123\"}"}'
{
    "statusCode": "200",
    "selectionPattern": "",
    "responseTemplates": {
        "application/json": "{\"message\": \"hello 123\"}"
    }
}
```

```shell
$ aws apigateway put-integration-response \
--rest-api-id oqcuns04we \
--resource-id abcw5u \
--http-method GET \
--status-code 200 \
--selection-pattern "" \
--response-templates '{"application/json": "{\"id\": \"123\",\"name\": \"name123\"}"}'
{
    "statusCode": "200",
    "selectionPattern": "",
    "responseTemplates": {
        "application/json": "{\"id\": \"123\",\"name\": \"name123\"}"
    }
}
```

```shell
$ aws apigateway put-integration-response \
--rest-api-id oqcuns04we \
--resource-id abcw5u \
--http-method GET \
--status-code 404 \
--selection-pattern "404" \
--response-templates '{"application/json": "{\"message\": \"ID Not Found\"}"}'
{
    "statusCode": "404",
    "selectionPattern": "404",
    "responseTemplates": {
        "application/json": "{\"message\": \"ID Not Found\"}"
    }
}
```

## Stage

A Stage is an environment where the Rest API can be deployed, it's a scope that can contain variable specific to that Stage, typical stages are Test (Stage/Staging) and Production (Prod).

## Deployment

A Deployment is a snapshot of the Rest API with all the child resources that is made available on one specific Stage. At deployment time a new Stage can be created.

To deploy to a new Stage named ```dev``` we run this command.

```shell
$ aws apigateway create-deployment \
--rest-api-id oqcuns04we \
--stage-name dev \
--stage-description 'Development Stage' \
--description 'Testing dev stage'
{
    "description": "Testing dev stage",
    "id": "7u47dw",
    "createdDate": 1535330252
}
```

## Test endpoint

A Rest API endpoint url is the format https://{rest-api-id}.execute-api.{AWS-region}.amazonaws.com/{stage-name}{path-and-query}

In this example, Rest API Id is oqcuns04we and we've created a stage named dev in the region of Ireland (eu-west-1), to test the resource with path /hello we use the following curl command.

```shell
$ curl -X GET https://oqcuns04we.execute-api.eu-west-1.amazonaws.com/dev/hello
{"message": "hello 123"}
```

The next commands will test /hello/123 and /hello/111 to verify we're getting the expected outputs according to the mock set up in previous steps.

```shell
$ curl -X GET https://oqcuns04we.execute-api.eu-west-1.amazonaws.com/dev/hello/123
{"id": "123","name": "name123"}
```
```shell
$ curl -X GET https://oqcuns04we.execute-api.eu-west-1.amazonaws.com/dev/hello/111
{"message": "ID Not Found"}
```

## Cleaning up

Once we are done with this Rest API, we can delete it by using the ```delete-rest-api``` command.

```shell
$ aws apigateway delete-rest-api --rest-api-id oqcuns04we
```