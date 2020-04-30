#!/bin/bash
echo Creating REST API

APIID=$(aws apigateway create-rest-api --name 'project-api-test' \
--query id --output text)

echo API ID = $APIID

ROOT=$(aws apigateway get-resources --rest-api-id $APIID \
--query 'items[?path==`/`][id]' --output text)

echo ROOT = $ROOT

HELLO=$(aws apigateway create-resource --rest-api-id $APIID \
--parent-id $ROOT \
--path-part hello \
--query id --output text)

echo HELLO = $HELLO

HELLO_ID=$(aws apigateway create-resource --rest-api-id $APIID \
--parent-id $HELLO \
--path-part {id} \
--query id --output text)

echo HELLO_ID = $HELLO_ID

echo put-method 

aws apigateway put-method --rest-api-id $APIID \
--resource-id $HELLO \
--http-method GET \
--authorization-type "NONE" 1> /dev/null

aws apigateway put-method --rest-api-id $APIID \
--resource-id $HELLO_ID \
--http-method GET \
--authorization-type "NONE" \
--request-parameters method.request.path.id=true 1> /dev/null

echo put-method-response

aws apigateway put-method-response --rest-api-id $APIID \
--resource-id $HELLO --http-method GET \
--status-code 200 1> /dev/null

aws apigateway put-method-response --rest-api-id $APIID \
--resource-id $HELLO_ID --http-method GET \
--status-code 200 1> /dev/null

aws apigateway put-method-response --rest-api-id $APIID \
--resource-id $HELLO_ID --http-method GET \
--status-code 404 1> /dev/null

echo put-integration

aws apigateway put-integration \
--rest-api-id $APIID \
--resource-id $HELLO \
--http-method GET \
--type MOCK \
--request-templates '{ "application/json": "{\"statusCode\": 200}" }' \
1> /dev/null

aws apigateway put-integration \
--rest-api-id $APIID \
--resource-id $HELLO_ID \
--http-method GET \
--type MOCK \
--request-templates '{"application/json": "{\n#if( $input.params(\"id\") == \"123\" )\n    \"statusCode\": 200\n#else\n    \"statusCode\": 404\n#end\n}"}' \
1> /dev/null

echo put-integration-response

aws apigateway put-integration-response \
--rest-api-id $APIID  \
--resource-id $HELLO \
--http-method GET \
--status-code 200 \
--selection-pattern "" \
--response-templates '{"application/json": "{\"message\": \"hello 123\"}"}' \
1> /dev/null

aws apigateway put-integration-response \
--rest-api-id $APIID \
--resource-id $HELLO_ID \
--http-method GET \
--status-code 200 \
--selection-pattern "" \
--response-templates '{"application/json": "{\"id\": \"123\",\"name\": \"name123\"}"}' \
1> /dev/null


aws apigateway put-integration-response \
--rest-api-id $APIID \
--resource-id $HELLO_ID \
--http-method GET \
--status-code 404 \
--selection-pattern "404" \
--response-templates '{"application/json": "{\"message\": \"ID Not Found\"}"}' \
1> /dev/null

echo Deploying to Dev stage

aws apigateway create-deployment \
--rest-api-id $APIID \
--stage-name dev \
--stage-description 'Development Stage' \
--description 'Testing dev stage' \
1> /dev/null

REGION=$(aws configure get region)

echo test the endpoint https://$APIID.execute-api.$REGION.amazonaws.com/dev/hello

read -n 1 -s -r -p "Press any key to continue"
echo 

echo Deleting REST API $APIID

aws apigateway delete-rest-api --rest-api $APIID


