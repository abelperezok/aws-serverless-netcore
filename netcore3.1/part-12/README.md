#  Part 12 - DynamoDB - Lambda - CLI

In the previous part we created a table following a specific pattern of data modelling. We also quickly added and removed some data in the table. 

In this part we'll continue with the same table structure and will see how to list, add and remove items from our ToDo list this time from Lambda function code.

## Creating the table

For easy access, here is the command line to create the DynamoDB table if you don't have it already from previous part.

```shell
$ TABLE_NAME=dynamodb_test_lambda

$ aws dynamodb create-table \
--table-name $TABLE_NAME \
--attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
    AttributeName=GSI1,AttributeType=S \
--key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
--provisioned-throughput \
    ReadCapacityUnits=1,WriteCapacityUnits=1 \
--global-secondary-indexes \
IndexName=GSI1,KeySchema=["{AttributeName=GSI1,KeyType=HASH},{AttributeName=SK,KeyType=RANGE}"],\
Projection="{ProjectionType=ALL}",\
ProvisionedThroughput="{ReadCapacityUnits=1,WriteCapacityUnits=1}"

$ aws dynamodb wait table-exists --table-name $TABLE_NAME 
```

## Creating the correct Roles

Before creating the Lambda function, we need a role, as seen before, up to now, a basic role was sufficient. But these functions will be accessing the DynamoDB table and therefore they need permissions to do so.

As before, the trust relationship policy document that grants Lambda service the AssumeRole action on our role.

```shell
$ read -r -d '' ROLE_POLICY_DOCUMENT << EOM
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
```

### Read only access role

Create the role using the variable previously created with the name GetItemsRole.

```shell
$ aws iam create-role \
--role-name GetItemsRole \
--assume-role-policy-document "$ROLE_POLICY_DOCUMENT"
```

Instead of attaching an existing policy provided by AWS, we are going to create a new policy. The below is taken from the [Policy Template List](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/serverless-policy-template-list.html#dynamo-db-read-policy) and removed the intrinsic functions from CloudFormation. The policy is called **DynamoDBReadPolicy**.

Creating policies in AWS is a whole universe on its own, but in this context let's say it's only giving permission to the lambda function to execute the actions listed and they're all only to get information, nothing to write or delete. All these actions are only going to be allowed on the specified table and its indexes.

AWS recommend that we should always follow the **principle of least privilege** which basically states that we give access only to the required actions on the required resources. In this particular case, we could narrow it down to only ```"dynamodb:Query"```. However, I believe there has to be a balance between tight security and code maintainability.

```json
{
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "dynamodb:GetItem",
                "dynamodb:Scan",
                "dynamodb:Query",
                "dynamodb:BatchGetItem",
                "dynamodb:DescribeTable"
            ],
            "Resource": [
                "arn:aws:dynamodb:eu-west-1:123123123123:table/dynamodb_test_lambda",
                "arn:aws:dynamodb:eu-west-1:123123123123:table/dynamodb_test_lambda/index/*"
            ]
        }
    ]
}
```

Assuming we have the above content in a file named ```dynamodb-readonly-policy.json```, let's run the command to attach this policy to the previously created role. 

```shell
$ aws iam put-role-policy \
--role-name GetItemsRole \
--policy-name GetItemsRolePolicy \
--policy-document file://dynamodb-readonly-policy.json
```

And we have a **read only** role with a policy that is tied to the table we are working with.

### Write access role

Create the role using the same variable previously created. This time its name is PutItemRole.

```shell
$ aws iam create-role \
--role-name PutItemRole \
--assume-role-policy-document "$ROLE_POLICY_DOCUMENT"
```

Following the previous policy example, let's take this time **DynamoDBWritePolicy**. Observe they're very similar, only changed the actions.

```json
{
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
              "dynamodb:PutItem",
              "dynamodb:UpdateItem",
              "dynamodb:BatchWriteItem"
            ],
            "Resource": [
                "arn:aws:dynamodb:eu-west-1:123123123123:table/dynamodb_test_lambda",
                "arn:aws:dynamodb:eu-west-1:123123123123:table/dynamodb_test_lambda/index/*"
            ]
        }
    ]
}
```

Assuming we have the above content in a file named ```dynamodb-writeonly-policy.json```, let's run the command to attach this policy to the previously created role. 

```shell
$ aws iam put-role-policy \
--role-name PutItemRole \
--policy-name PutItemRolePolicy \
--policy-document file://dynamodb-writeonly-policy.json
```

And we have a **write only** role with a policy that is tied to the table we are working with.

### Delete access role

Create the role using the same variable previously created. This time its name is DeleteItemRole.

```shell
$ aws iam create-role \
--role-name DeleteItemRole \
--assume-role-policy-document "$ROLE_POLICY_DOCUMENT"
```

Following the previous policy example, let's take this time **DynamoDBCrudPolicy**. Observe they're very similar, only changed the actions.

```json
{
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
              "dynamodb:DeleteItem"
            ],
            "Resource": [
                "arn:aws:dynamodb:eu-west-1:123123123123:table/dynamodb_test_lambda",
                "arn:aws:dynamodb:eu-west-1:123123123123:table/dynamodb_test_lambda/index/*"
            ]
        }
    ]
}
```

Assuming we have the above content in a file named ```dynamodb-deleteonly-policy.json```, let's run the command to attach this policy to the previously created role. 

```shell
$ aws iam put-role-policy \
--role-name DeleteItemRole \
--policy-name DeleteItemRolePolicy \
--policy-document file://dynamodb-deleteonly-policy.json
```

And we have a **delete only** role with a policy that is tied to the table we are working with.

## Connecting to DynamoDB 

AWS provides a very good [SDK for .NET Core](https://aws.amazon.com/sdk-for-net/) that gives access to all services, in case of DynamoDB, there is a nuget package ```AWSSDK.DynamoDBv2``` that connects to DynamoDB and allows to invoke operations.

First, add nuget package to the lambda project.

```shell
$ dotnet add package AWSSDK.DynamoDBv2
```

I have encapsulated all DynamoDB operations in a class called DynamoDbRepository which gives more clarity in the Lambda function itself. This is a stripped down version of a more complete solution. 

A more detailed explanation can be found in another github repository [DynamoDB-BaseRepository](https://github.com/abelperezok/DynamoDB-BaseRepository).

This is the code for the function handlers. They obtain the table name from the environment variable ```TableName``` to avoid hard coding values. The flow is simple: get the input, call DynamoDB repository and return the appropriated value.

```cs
    private const string TableName = "TableName";

    public async Task<List<Item>> GetHandler(ILambdaContext context)
    {
        var tableName = Environment.GetEnvironmentVariable(TableName);
        context.Logger.LogLine($"Using table {tableName}");

        var repository = new DynamoDbRepository(tableName);
        return await repository.GetItems();
    }

    public async Task<Result> PutHandler(Item item, ILambdaContext context)
    {
        var tableName = Environment.GetEnvironmentVariable(TableName);
        context.Logger.LogLine($"Using table {tableName}");

        var repository = new DynamoDbRepository(tableName);

        var result = await repository.AddItem(item);
        context.Logger.LogLine($"Result = {result}");
        
        return new Result { Success = result };
    }

    public async Task<Result> DeleteHandler(string id, ILambdaContext context)
    {
        var tableName = Environment.GetEnvironmentVariable(TableName);
        context.Logger.LogLine($"Using table {tableName}");

        var repository = new DynamoDbRepository(tableName);

        var result = await repository.RemoveItem(id);
        context.Logger.LogLine($"Result = {result}");
                    
        return new Result { Success = result };
    }
```

And these are the DTOs:

```cs
    public class Item
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class Result
    {
        public bool Success { get; set; }
    }
```

## Deploying the functions

With the function code ready, we can proceed to deploy the functions. But before that, we should prepare the configuration files for convenience, three different configurations, one per function following the procedure applied in part 4, here is the content.

Note that there is a new property for the environment variable. 

lambda-get-config.json
```json
{
    "region" : "eu-west-1",
    "function-name" : "GetItems",
    "function-handler" : "project.lambda::project.lambda.Function::GetHandler",
    "function-memory-size" : 128,
    "function-role"        : "GetItemsRole",
    "function-timeout"     : 30,
    "function-runtime"     : "dotnetcore3.1",
    "s3-bucket"            : "abelperez-temp",
    "s3-prefix"            : "project-lambda/",
    "environment-variables": "TableName=dynamodb_test_lambda"
}
```

lambda-put-config.json
```json
{
    "region" : "eu-west-1",
    "function-name" : "PutItem",
    "function-handler" : "project.lambda::project.lambda.Function::PutHandler",
    "function-memory-size" : 128,
    "function-role"        : "PutItemRole",
    "function-timeout"     : 30,
    "function-runtime"     : "dotnetcore3.1",
    "s3-bucket"            : "abelperez-temp",
    "s3-prefix"            : "project-lambda/",
    "environment-variables": "TableName=dynamodb_test_lambda"
}
```

lambda-delete-config.json
```json
{
    "region" : "eu-west-1",
    "function-name" : "DeleteItem",
    "function-handler" : "project.lambda::project.lambda.Function::DeleteHandler",
    "function-memory-size" : 128,
    "function-role"        : "DeleteItemRole",
    "function-timeout"     : 30,
    "function-runtime"     : "dotnetcore3.1",
    "s3-bucket"            : "abelperez-temp",
    "s3-prefix"            : "project-lambda/",
    "environment-variables": "TableName=dynamodb_test_lambda"
}
```

Using ```dotnet lambda deploy-function``` command with the above configuration files, we can deploy the three functions.

```shell
$ dotnet lambda deploy-function -cfg lambda-get-config.json 

$ dotnet lambda deploy-function -cfg lambda-put-config.json 

$ dotnet lambda deploy-function -cfg lambda-delete-config.json
```

## Invoking the functions

It's time to see result of all this effort, with the functions being deployed, we can now invoke them in a sequence.

Initially, GetItems will return an empty list, as obviously, there are no items in the table.

```shell
$ dotnet lambda invoke-function -fn GetItems --region eu-west-1

Payload:
[]

```

Invoke PutItem a couple of times with items 1 and 2. 

```shell
$ dotnet lambda invoke-function -fn PutItem -p '{"Id":"1", "Name":"Item 1"}' --region eu-west-1

Payload:
{"Success":true}

```

```shell
$ dotnet lambda invoke-function -fn PutItem -p '{"Id":"2", "Name":"Item 2"}' --region eu-west-1

Payload:
{"Success":true}

```

Verify that if we invoke GetItems again, we effectively get the two items previously inserted.

```shell
$ dotnet lambda invoke-function -fn GetItems --region eu-west-1

Payload:
[{"Id":"1","Name":"Item 1"},{"Id":"2","Name":"Item 2"}]

```

Finally, let's delete both items by invoking DeleteItem.


```shell
$ dotnet lambda invoke-function -fn DeleteItem -p '1' --region eu-west-1

Payload:
{"Success":true}

```

```shell
$ dotnet lambda invoke-function -fn DeleteItem -p '2' --region eu-west-1

Payload:
{"Success":true}

```

After deleting the two items from the table, if we invoke GetItems again, it will return an empty list, which brings us back to the initial state.

```shell
$ dotnet lambda invoke-function -fn GetItems --region eu-west-1

Payload:
[]

```

## Cleaning up

Delete the DynamoDB table.

```shell
$ aws dynamodb delete-table --table-name $TABLE_NAME

{
    "TableDescription": {
        "TableName": "dynamodb_test_lambda",
        "TableStatus": "DELETING",
        "ProvisionedThroughput": {
            "NumberOfDecreasesToday": 0,
            "ReadCapacityUnits": 1,
            "WriteCapacityUnits": 1
        },
        "TableSizeBytes": 0,
        "ItemCount": 0,
        "TableArn": "arn:aws:dynamodb:eu-west-1:123123123123:table/dynamodb_test_lambda",
        "TableId": "2923e954-47e5-480c-9e63-10983ff8fc39"
    }
}

$ aws dynamodb wait table-not-exists --table-name $TABLE_NAME
```

Delete the Lambda functions

```shell
$ dotnet lambda delete-function -fn GetItems --region eu-west-1

$ dotnet lambda delete-function -fn PutItem --region eu-west-1

$ dotnet lambda delete-function -fn DeleteItem --region eu-west-1
```

This step is required before deleting the roles, similarly when attaching policies they need to be detached first. Failure to do so, will result in an error like this one:

```
An error occurred (DeleteConflict) when calling the DeleteRole operation: Cannot delete entity, must delete policies first.
```

Delete the policies from the roles. 

```shell
$ aws iam delete-role-policy --role-name GetItemsRole --policy-name GetItemsRolePolicy

$ aws iam delete-role-policy --role-name PutItemRole --policy-name PutItemRolePolicy

$ aws iam delete-role-policy --role-name DeleteItemRole --policy-name DeleteItemRolePolicy
```

Delete the Roles

```shell
$ aws iam delete-role --role-name GetItemsRole

$ aws iam delete-role --role-name PutItemRole

$ aws iam delete-role --role-name DeleteItemRole
```
