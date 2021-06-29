# AwsQueueBroker

The AwsQueueBroker library allows for SQS queue message definitions to be constructed by assigning unique name attributes on each message and attaching messages by that name to an implementation of a message processor class and it's associated .net object model. This allows the developer to remove all the logic of receiving, sending, and deleting SQS queue messages from their code-base so that the focus can be placed on the actual processing of messages in a structured and repeatable manner.

## Usage

The library works by associating an instance of the abstract QProcessor class with a unique message name and a .Net class that represents the body of that particular message. The QBroker can be setup with any number of named message types and proecssors.

## Documentation

Generated documentation can be viewed at https://sethfduke.github.io/dotnet-awsqueuebroker/index.html

**Example**

```
public class TestMessageModel
{
    // This model should match the expected format of the message body.
    public string Name { get; set; }
}

public class TestQProcessor : QProcessor<TestMessageModel>
{
    public override async Task Received(Message message, ILogger logger = null)
    {
        // do something here
    }

    public override async Task<object> Validate(Message message, TestMessageModel body, ILogger logger = null)
    {
        if (body.Name == "valid")
        {
            // returning the body (or some modfiied version of it) tells the process it is valid and to continue.
            return body;
        }

        if (body.Name == "throw")
        {
            // throwing an exception in any method will forward the exception to the Error() method.
            throw new Exception("We blew it.");
        }

        // returning null tells the processor the model was not valid and to stop processing this message.
        return null;
    }

    public override async Task<QReply> Process(Message message, TestMessageModel model, ILogger logger = null)
    {
        // optionally returning a QReply instance will send a reply QMessage to the specified queue.
        // return null if no reply is needed.
        
        return new QReply()
        {
            Message = new QMessage("reply-message", "Some content"),
            ReplyToQueueUrl = "http://localhost:4566/000000000000/qbroker-test-reply-queue"
        };
    }

    public override async Task Error(Message message, Exception exception, ILogger logger = null)
    {
        // do something here if an exception was thrown in any of the other methods.
    }
}
```

The QBroker can now be setup to process messages that match an assigned name for the above process implementation:

```
var sqsClient = new AmazonSQSClient(RegionEndpoint.USEast1);

var broker = new QBroker(sqsClient)
    .Setup(settings =>
    {
        // keep fetching messages until there are no more.
        settings.FetchUntilEmpty = true;
        
        // delete succesfully processed messages
        settings.DeleteIfSuccess = true;

        // delete messages marked invalid
        settings.DeleteIfInvalid = true;
        
        // delete messages that resulted in an error
        settings.DeleteIfError = true;
        
        // number of messages to fetch at a time (1 to 10)
        settings.MaxNumberOfMessages = 10;
        
        // polling wait time in seconds
        settings.WaitTimeSeconds = 0;
        
        // queue url to poll for messages
        settings.QueueUrl = "https://sqs.us-east-2.amazonaws.com/123456789012/MyQueue";
        
        // optional Serilog ILogger
        settings.Logger = _createLogger();
    })
    // add the test processor with a message name attribute of
    // test-message-name with TestMessageModel as the expected body.
    .AddMessageType<TestMessageModel, TestQProcessor>("test-message-name");

// poll for messages and process
await broker.FetchAsync()
```

The broker can also be utilized to send messages outside of the scope of a processor if needed using

```
var model = new TestMessageModel() {
    Name = "test"
};

// QMessage constructor can also include additional message attributes to include.
var message = new QMessage(
    "test-message-name",
    JsonSerializer.Serialize(model));

broker.SendAsync(message, queueUrl));
```

## Dependency Injection

```
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IQBroker, QBroker>();
}
```
```
public class TestQProcessor : QProcessor<TestMessageModel>
{
    private IMyDependency _myDependency;

    public TestQProcessor(IMyDependency myDependency){
        _myDependency = myDependency;
    }

    public override async Task Received(Message message, ILogger logger = null){
        ...
    }

    public override async Task<object> Validate(Message message, TestMessageModel body, ILogger logger = null)
    {
        ...
    }

    public override async Task<QReply> Process(Message message, TestMessageModel model, ILogger logger = null)
    {
        ...
    }

    public override async Task Error(Message message, Exception exception, ILogger logger = null)
    {
        ...
    }
}
```
