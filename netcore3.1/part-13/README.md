# Part 13 - DynamoDB - Lambda - CloudFormation

Following the same trend as in previous parts, now that we've seen how to deal with DynamoDB using the CLI, it's time for CloudFormation.

We can reuse the same code code used in part 12, in a CloudFormation template, we are going to describe all the resources that we created manually earlier.

## Prepare the code

Before starting to write the CloudFormation template, let's prepare the code package by running ```dotnet lambda package``` command and then upload the zip package to S3.

```shell
$ dotnet lambda package -c Release -o project-lambda.zip
$ aws s3 cp project-lambda.zip s3://abelperez-temp/project-lambda/
```

## DynamoDB table

Let's start by defining the table which will be exactly the same used in the previous two parts. Compare this definition to the command line version, it's clearly more readable. 

```yaml
  # DynamoDb table
  ItemsTable:
    Type: AWS::DynamoDB::Table
    Properties: 
      KeySchema: 
        - AttributeName: PK
          KeyType: HASH
        - AttributeName: SK
          KeyType: RANGE
      AttributeDefinitions:
        - AttributeName: PK
          AttributeType: S
        - AttributeName: SK
          AttributeType: S
        - AttributeName: GSI1
          AttributeType: S
      ProvisionedThroughput:
        ReadCapacityUnits: 1
        WriteCapacityUnits: 1
      GlobalSecondaryIndexes:
        - IndexName: GSI1
          KeySchema: 
            - AttributeName: GSI1
              KeyType: HASH
            - AttributeName: SK
              KeyType: RANGE
          Projection: 
            ProjectionType: ALL
          ProvisionedThroughput: 
            ReadCapacityUnits: 1
            WriteCapacityUnits: 1
```

## Get Items resources

We can combine the policy definition with the role in the same resource definition. The first part is the ```assume role policy``` which grants the Lambda service permissions to assume this role. 

The second part contains the basic execution role we used before as well as the new policy where grant permissions to perform read operations on the table. 

**Notice** that the table name is expressed as a variable ```${ItemsTable}``` and not hard coded in the policy. The same applies to the region ```${AWS::Region}``` and account id ```${AWS::AccountId}```.

```yaml
  # Get Items Execution Role
  GetItemsRole:
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
      ManagedPolicyArns:
        - arn:aws:iam::aws:policy/AWSLambdaExecute
      Policies:
        - PolicyName: GetItemsRolePolicy
          PolicyDocument:
            Version: 2012-10-17
            Statement:
            - Effect: Allow
              Action:
              - dynamodb:GetItem
              - dynamodb:Scan
              - dynamodb:Query
              - dynamodb:BatchGetItem
              - dynamodb:DescribeTable
              Resource: 
                - !Sub arn:aws:dynamodb:${AWS::Region}:${AWS::AccountId}:table/${ItemsTable}
                - !Sub arn:aws:dynamodb:${AWS::Region}:${AWS::AccountId}:table/${ItemsTable}/index/*

```

With the role in place, we can now define the function. We use the Code definition from above where it was uploaded to S3. Role now refers to the ```GetItemsRole``` defined above. Also notice the environment variable ```TableName``` is using the value from the actual table name variable by using ```!Ref ItemsTable``` and not hard coded in the function.

```yaml
  # GetItems Lambda function
  GetItems:
    Type: AWS::Lambda::Function
    Properties:
      Handler: project.lambda::project.lambda.Function::GetHandler
      Role: !GetAtt GetItemsRole.Arn
      Code:
        S3Bucket: abelperez-temp
        S3Key: project-lambda/project-lambda.zip
      Runtime: dotnetcore3.1
      Timeout: 30
      Environment:
        Variables:
          TableName: !Ref ItemsTable
```

## Put Item resources

The same pattern is used to define Put item, updating accordingly the actions in the policy to only write actions.

```yaml
  # Put Item Execution Role
  PutItemRole:
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
      ManagedPolicyArns:
        - arn:aws:iam::aws:policy/AWSLambdaExecute
      Policies:
        - PolicyName: PutItemRolePolicy
          PolicyDocument:
            Version: 2012-10-17
            Statement:
            - Effect: Allow
              Action:
              - dynamodb:PutItem
              - dynamodb:UpdateItem
              - dynamodb:BatchWriteItem
              Resource: 
                - !Sub arn:aws:dynamodb:${AWS::Region}:${AWS::AccountId}:table/${ItemsTable}
                - !Sub arn:aws:dynamodb:${AWS::Region}:${AWS::AccountId}:table/${ItemsTable}/index/*

```

Also the references in the Lambda function to use the role ```PutItemRole``` and the handler method ```PutHandler```.

```yaml
  # PutItem Lambda function
  PutItem:
    Type: AWS::Lambda::Function
    Properties:
      Handler: project.lambda::project.lambda.Function::PutHandler
      Role: !GetAtt PutItemRole.Arn
      Code:
        S3Bucket: abelperez-temp
        S3Key: project-lambda/project-lambda.zip
      Runtime: dotnetcore3.1
      Timeout: 30
      Environment:
        Variables:
          TableName: !Ref ItemsTable
```

## Delete Item resources

The same pattern is used to define Delete item, updating accordingly the actions in the policy to only DeleteItem.

```yaml
  # Delete Item Execution Role
  DeleteItemRole:
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
      ManagedPolicyArns:
        - arn:aws:iam::aws:policy/AWSLambdaExecute
      Policies:
        - PolicyName: DeleteItemRolePolicy
          PolicyDocument:
            Version: 2012-10-17
            Statement:
            - Effect: Allow
              Action:
              - dynamodb:DeleteItem
              Resource: 
                - !Sub arn:aws:dynamodb:${AWS::Region}:${AWS::AccountId}:table/${ItemsTable}
                - !Sub arn:aws:dynamodb:${AWS::Region}:${AWS::AccountId}:table/${ItemsTable}/index/*

```

Also the references in the Lambda function to use the role ```DeleteItemRole``` and the handler method ```DeleteHandler```.

```yaml
  # DeleteItem Lambda function
  DeleteItem:
    Type: AWS::Lambda::Function
    Properties:
      Handler: project.lambda::project.lambda.Function::DeleteHandler
      Role: !GetAtt DeleteItemRole.Arn
      Code:
        S3Bucket: abelperez-temp
        S3Key: project-lambda/project-lambda.zip
      Runtime: dotnetcore3.1
      Timeout: 30
      Environment:
        Variables:
          TableName: !Ref ItemsTable
```

## Template Outputs

Finally, outputs will be helpful in getting the actual resource names so we can for instance, invoke the functions.

```yaml
Outputs:
  TableName:
    Description: "DynamoDb table for Items"
    Value: !Ref ItemsTable
  GetItems:
    Description: "GetItems Lambda function"
    Value: !Ref GetItems
  PutItem:
    Description: "PutItem Lambda function"
    Value: !Ref PutItem
  DeleteItem:
    Description: "DeleteItem Lambda function"
    Value: !Ref DeleteItem  
```

## Deploying the template

To deploy a CloudFormation template, we use the ```aws cloudformation deploy``` command.

```shell
$ aws cloudformation deploy \
--template-file cloudformation.yaml \
--stack-name project-lambda-dynamodb-cfn \
--capabilities CAPABILITY_IAM

Waiting for changeset to be created..
Waiting for stack create/update to complete
Successfully created/updated stack - project-lambda-dynamodb-cfn
```

After successfully deploying the template, we can grab the function names so we can invoke them.

```shell
$ aws cloudformation describe-stacks --stack-name project-lambda-dynamodb-cfn --query Stacks[0].Outputs

[
    {
        "OutputKey": "TableName",
        "OutputValue": "project-lambda-dynamodb-cfn-ItemsTable-12X1FLE7QGVBP",
        "Description": "DynamoDb table for Items"
    },
    {
        "OutputKey": "DeleteItem",
        "OutputValue": "project-lambda-dynamodb-cfn-DeleteItem-T9QN8FEV7641",
        "Description": "DeleteItem Lambda function"
    },
    {
        "OutputKey": "PutItem",
        "OutputValue": "project-lambda-dynamodb-cfn-PutItem-1BP9T5ACT8AIK",
        "Description": "PutItem Lambda function"
    },
    {
        "OutputKey": "GetItems",
        "OutputValue": "project-lambda-dynamodb-cfn-GetItems-15KE21F9NPOM1",
        "Description": "GetItems Lambda function"
    }
]
```

## Invoking the functions

With everything in place, we can now follow the same CRUD-like sequence as before.

Initially, GetItems will return an empty list, as obviously, there are no items in the table.

```shell
$ dotnet lambda invoke-function -fn project-lambda-dynamodb-cfn-GetItems-15KE21F9NPOM1 --region eu-west-1

Payload:
[]

```

Invoke PutItem a couple of times with items 1 and 2. 

```shell
$ dotnet lambda invoke-function -fn project-lambda-dynamodb-cfn-PutItem-1BP9T5ACT8AIK -p '{"Id":"1", "Name":"Item 1"}' --region eu-west-1

Payload:
{"Success":true}

```

```shell
$ dotnet lambda invoke-function -fn project-lambda-dynamodb-cfn-PutItem-1BP9T5ACT8AIK -p '{"Id":"2", "Name":"Item 2"}' --region eu-west-1

Payload:
{"Success":true}

```

Verify that if we invoke GetItems again, we effectively get the two items previously inserted.

```shell
$ dotnet lambda invoke-function -fn project-lambda-dynamodb-cfn-GetItems-15KE21F9NPOM1 --region eu-west-1

Payload:
[{"Id":"1","Name":"Item 1"},{"Id":"2","Name":"Item 2"}]

```

Finally, delete both items by invoking DeleteItem.


```shell
$ dotnet lambda invoke-function -fn project-lambda-dynamodb-cfn-DeleteItem-T9QN8FEV7641 -p '1' --region eu-west-1

Payload:
{"Success":true}

```

```shell
$ dotnet lambda invoke-function -fn project-lambda-dynamodb-cfn-DeleteItem-T9QN8FEV7641 -p '2' --region eu-west-1

Payload:
{"Success":true}

```

After deleting the two items from the table, if we invoke GetItems again, it will return an empty list, which brings us back to the initial state.

```shell
$ dotnet lambda invoke-function -fn project-lambda-dynamodb-cfn-GetItems-15KE21F9NPOM1 --region eu-west-1

Payload:
[]

```

### Cleaning up

When using CloudFormation, cleaning up is much simpler, the only thing we need to delete is the stack and it will take care of the rest.

```shell
$ aws cloudformation delete-stack --stack-name project-lambda-dynamodb-cfn
```

Optionally, we can wait until the stack is completely delete which is when all the resources were successfully deleted.

```shell
$ aws cloudformation wait stack-delete-complete --stack-name project-lambda-dynamodb-cfn
```