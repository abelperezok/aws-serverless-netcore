# Part 11 - Persisting data - DynamoDB - Introduction

After all previous parts, we've made it to the point where we can execute server logic in response to an HTTP request through API Gateway via Lambda / Proxy integration. In many cases, we need to store the state but serverless is inherently stateless, so we need to store it somewhere else. 

One popular destination is DynamoDB because of its fast response time and the serverless nature of the service offering by AWS. In this part, we'll see how to use the most common operations provided by DynamoDB using the AWS CLI. 

A common example tends to be the classical ToDo list, where at the very least, we add and remove items from a list. This is the perfect excuse to introduce the need for some persistency. 

## Quick overview on data modeling

It's out of scope of this material to go in depth about DynamoDB data modeling. We'll however, stick to some general guidelines that can solve the majority of common cases. 

The table structure will follow the general idea about hierarchical data overloading the partition and sort keys as well as the GSI.

* Generic partition key "PK" string.
* Generic sort key "SK" string.
* Generic attribute "GSI1" string.
* GSI partition key is "GSI1".
* GSI sort key is also "SK".
* GSI projects all attributes.
* Other attributes such as ID, Name.

Table with sample data

PK (S) | SK (S) | GSI1 | Id | Name 
-------|--------|------|----|------
ITEM#0001 | METADATA#0001 | ITEM | 0001 | Item 1	
ITEM#0002 | METADATA#0002 | ITEM | 0002 | Item 2
ITEM#0003 | METADATA#0003 | ITEM | 0003 | Item 3	


GSI with sample data

PK (S) GSI1 | SK (S) SK | Id | Name 
------------|-----------|----|------
ITEM | METADATA#0001 | 0001 | Item 1
ITEM | METADATA#0002 | 0002 | Item 2
ITEM | METADATA#0003 | 0003 | Item 3

This model allows us to add more entities into the same table provided their partition keys are prefixed differently.

### How to query this table ?

* Single item given the ID: Table PK = "ENTITY#ID", SK = "METADATA#ID"

  - Get Item 0001: Table PK = ITEM#0001, SK = METADATA#0001
  - Get Item 0002: Table PK = ITEM#0002, SK = METADATA#0002

* Multiple items per type: GSI PK = "ENTITY"

  - Get all items: GSI PK = "ITEM"
 

## Creating the table

Following the trend from previous parts we'll see how to create the DynamoDB table using AWS CLI. This is the information that needs to be supplied to AWS in order to create a table.

* Table name: simply the table name.
* Attribute definition: A collection describing all known attributes along with their data type.
* Key schema: A collection of attributes that compose the primary key, these attributes need to be defined in attribute definition.
* Provisioned throughput: read and write capacity values for the table.
* Global secondary indexes (GSIs): A collection of GSIs, for each one, we need to specify its name, key schema, projection type and provisioned throughput. 

In this case, the line has been broken into several parts for readability given the length of this command. Pay special attention to the quotation of ```KeySchema``` property in ```--global-secondary-indexes``` parameter. It doesn't seem to follow the same pattern as other commands and also it's not very clear in the documentation because it lacks examples like this.

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

# Table immediately goes to CREATING status.
...
"TableStatus": "CREATING",
"CreationDateTime": "2020-05-09T12:46:10.747000+01:00",
...


$ aws dynamodb wait table-exists --table-name $TABLE_NAME 
```

After the wait command finishes the table status is ACTIVE, this can be verified with the command ```describe-table```.

```shell
$ aws dynamodb describe-table --table-name $TABLE_NAME

# Table is now active and ready to use.
...
"TableStatus": "ACTIVE",
"CreationDateTime": "2020-05-09T12:46:10.747000+01:00",
...
```

We can list the tables in the current region to check it's actually there.

```shell
$ aws dynamodb list-tables 
{
    "TableNames": [
        "dynamodb_test_lambda"
    ]
}
```

## Working with data

Just after creating the table, there is no data, if we run the query intended to get all **Items** in the table, no **Items** are returned back. 

```shell
$ aws dynamodb query \
    --table-name $TABLE_NAME \
    --index-name GSI1 \
    --key-condition-expression "GSI1 = :item" \
    --expression-attribute-values '{ ":item": {"S": "ITEM"} }'
{
    "Items": [],
    "Count": 0,
    "ScannedCount": 0,
    "ConsumedCapacity": null
}
```

This query uses the GSI1 index to find in its partition key, records whose values are "ITEM", in case we add more entities in the future.

Let's add some data, to do that, we use the ```put-item``` command to add new records, the data provided is a JSON specifying each of the attributes defined above as well as its data type, in this particular scenario, all of them are string ("S").

```shell
$ aws dynamodb put-item \
    --table-name $TABLE_NAME \
    --item '{"PK": {"S": "ITEM#0001"}, "SK": {"S": "METADATA#0001"}, "GSI1": {"S": "ITEM"}, "Id": {"S": "0001"}, "Name": {"S": "Item 1"}}'

$ aws dynamodb put-item \
    --table-name $TABLE_NAME \
    --item '{"PK": {"S": "ITEM#0002"}, "SK": {"S": "METADATA#0002"}, "GSI1": {"S": "ITEM"}, "Id": {"S": "0002"}, "Name": {"S": "Item 2"}}'
```

Now there is some data, if we repeat the previous query to get all items, the output is effectively showing two items. 

```shell
aws dynamodb query \
    --table-name $TABLE_NAME \
    --index-name GSI1 \
    --key-condition-expression "GSI1 = :item" \
    --expression-attribute-values '{ ":item": {"S": "ITEM"} }'

{
    "Items": [
        {
            "SK": {
                "S": "METADATA#0001"
            },
            "GSI1": {
                "S": "ITEM"
            },
            "Id": {
                "S": "0001"
            },
            "PK": {
                "S": "ITEM#0001"
            },
            "Name": {
                "S": "Item 1"
            }
        },
        {
            "SK": {
                "S": "METADATA#0002"
            },
            "GSI1": {
                "S": "ITEM"
            },
            "Id": {
                "S": "0002"
            },
            "PK": {
                "S": "ITEM#0002"
            },
            "Name": {
                "S": "Item 2"
            }
        }
    ],
    "Count": 2,
    "ScannedCount": 2,
    "ConsumedCapacity": null
}
```
**One note** about the ```put-item``` command, it can act as both INSERT or UPDATE, DynamoDB checks the primary key, if there is a record with the same key, it will replace the non-key attributes with the supplied attributes and values.

if we want to only update some attributes while keeping the rest untouched, then ```update-item``` command should be used instead.

To complete the cycle, let's delete those two items. 

```shell
$ aws dynamodb delete-item \
    --table-name $TABLE_NAME \
    --key '{ "PK": {"S": "ITEM#0001"}, "SK": {"S": "METADATA#0001"}}'

$ aws dynamodb delete-item \
    --table-name $TABLE_NAME \
    --key '{ "PK": {"S": "ITEM#0002"}, "SK": {"S": "METADATA#0002"}}'
```

After deleting the items, there should be no data, if run the query again, no items are returned back. 

```shell
$ aws dynamodb query \
    --table-name $TABLE_NAME \
    --index-name GSI1 \
    --key-condition-expression "GSI1 = :item" \
    --expression-attribute-values '{ ":item": {"S": "ITEM"} }'
{
    "Items": [],
    "Count": 0,
    "ScannedCount": 0,
    "ConsumedCapacity": null
}
```

And we are back to the initial state with this table.

## Cleaning up

The only resource used is the DynamoDB table, so to clean up, the only step consists of deleting the table by using the ```delete-table``` command.

Optionally it can be followed by another wait command, this time until the table doesn't exist any more.

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
