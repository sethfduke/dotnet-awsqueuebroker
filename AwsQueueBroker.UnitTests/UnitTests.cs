using Amazon.SQS;
using Amazon.SQS.Model;
using AwsQueueBroker.UnitTests.TestLib;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using Serilog;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.InMemory.Assertions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace AwsQueueBroker.UnitTests
{
    public class UnitTest1
    {
        [Theory, AutoMoqData]
        public void ShouldSendMessage(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            string queueUrl)
        {
            var response = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                MessageId = Guid.NewGuid().ToString("D")
            };

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);
            
            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = true;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>("test-message-name");

            var message =  new QMessage(
                "test-message-name",
                JsonSerializer.Serialize(model),
                new KeyValuePair<string, MessageAttributeValue>(
                    "testAttribute", 
                    new MessageAttributeValue()
                    {
                        DataType = "String",
                        StringValue = "test value"
                    }));
            
            sut.Invoking(s => s.SendAsync(message, queueUrl)).Should().NotThrow();

            InMemorySink.Instance.Should()
                .HaveMessage("Sent message {MessageId} to queue {queueUrl}.").Appearing().Once();
        }
        
        [Theory, AutoMoqData]
        public void ShouldThrowIfSendAsyncFails(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            string queueUrl)
        {
            var response = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.BadRequest
            };

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = true;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                });

            var message =  new QMessage(
                "test-message-name",
                JsonSerializer.Serialize(model),
                new KeyValuePair<string, MessageAttributeValue>(
                    "testAttribute", 
                    new MessageAttributeValue()
                    {
                        DataType = "String",
                        StringValue = "test value"
                    }));
            
            sut.Invoking(s => s.SendAsync(message, queueUrl)).Should().Throw<Exception>();
        }

        [Theory, AutoMoqData]
        public void ShouldThrowIfReceiveFails(
            Mock<IAmazonSQS> sqsClient)
        {
            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.BadRequest
            };

            sqsClient.Setup(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = true;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                });

            sut.Invoking(s => s.FetchAsync()).Should().Throw<Exception>();
        }
        
        [Theory, AutoMoqData]
        public void ShouldFetchAndProcessMessage(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            string testMessageName)
        {
            model.Name = "valid";
            
            var qmessage = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));

            var qmessage2 = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));
            
            var messages = new List<Message> { qmessage.AwsSqsMessage, qmessage2.AwsSqsMessage };

            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = messages
            };
            
            var empty = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = new List<Message>()
            };
            
            var sentResponse = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sentResponse);
            
            sqsClient.SetupSequence(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .ReturnsAsync(empty);

            var deleteResponse = new DeleteMessageResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);
            
            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = true;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>(testMessageName);

            sut.Invoking(s => s.FetchAsync()).Should().NotThrow();

            InMemorySink.Instance.Should().HaveMessage("Fetching queue messages.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processing {Count} messages.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processing message with id {Id}.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message received.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Received it.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Deserializing message body.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message validation.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Validated it").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message processing.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processed it.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Sent message {MessageId} to queue {queueUrl}.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Message id {id} deleted from queue.").Appearing().Times(2);
        }
        
        [Theory, AutoMoqData]
        public void ShouldNotDeleteOnSuccess(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            string testMessageName)
        {
            model.Name = "valid";
            
            var qmessage = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));

            var qmessage2 = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));
            
            var messages = new List<Message> { qmessage.AwsSqsMessage, qmessage2.AwsSqsMessage };

            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = messages
            };
            
            var empty = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = new List<Message>()
            };
            
            var sentResponse = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sentResponse);
            
            sqsClient.SetupSequence(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .ReturnsAsync(empty);

            var deleteResponse = new DeleteMessageResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);
            
            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = true;
                    settings.DeleteIfSuccess = false;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>(testMessageName);

            sut.Invoking(s => s.FetchAsync()).Should().NotThrow();

            InMemorySink.Instance.Should().HaveMessage("Fetching queue messages.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processing {Count} messages.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processing message with id {Id}.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message received.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Received it.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Deserializing message body.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message validation.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Validated it").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message processing.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processed it.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Sent message {MessageId} to queue {queueUrl}.").Appearing().Times(2);
            InMemorySink.Instance.Should().NotHaveMessage("Message id {id} deleted from queue.");
        }
        
        [Theory, AutoMoqData]
        public void ShouldLogIfDeleteError(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            string testMessageName)
        {
            model.Name = "valid";
            
            var qmessage = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));

            var qmessage2 = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));
            
            var messages = new List<Message> { qmessage.AwsSqsMessage, qmessage2.AwsSqsMessage };

            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = messages
            };
            
            var empty = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = new List<Message>()
            };
            
            var sentResponse = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sentResponse);
            
            sqsClient.SetupSequence(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .ReturnsAsync(empty);

            var deleteResponse = new DeleteMessageResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest
            };

            sqsClient.Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);
            
            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = true;
                    settings.DeleteIfError = false;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>(testMessageName);

            sut.Invoking(s => s.FetchAsync()).Should().NotThrow();

            InMemorySink.Instance.Should().HaveMessage("Fetching queue messages.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processing {Count} messages.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processing message with id {Id}.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message received.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Received it.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Deserializing message body.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message validation.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Validated it").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message processing.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processed it.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Sent message {MessageId} to queue {queueUrl}.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message error for message id {id}.").Appearing().Times(2);
        }
        
        [Theory, AutoMoqData]
        public void ShouldThrowIfDeleteOnErrorFails(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            string testMessageName)
        {
            model.Name = "valid";
            
            var qmessage = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));

            var qmessage2 = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));
            
            var messages = new List<Message> { qmessage.AwsSqsMessage, qmessage2.AwsSqsMessage };

            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = messages
            };
            
            var empty = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = new List<Message>()
            };
            
            var sentResponse = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sentResponse);
            
            sqsClient.SetupSequence(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .ReturnsAsync(empty);

            var deleteResponse = new DeleteMessageResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest
            };

            sqsClient.Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);
            
            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = true;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>(testMessageName);

            sut.Invoking(s => s.FetchAsync()).Should().Throw<Exception>();
        }
        
        [Theory, AutoMoqData]
        public void ShouldNotDeleteOnInvalid(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            string testMessageName)
        {
            model.Name = "invalid";
            
            var qmessage = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));

            var qmessage2 = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));
            
            var messages = new List<Message> { qmessage.AwsSqsMessage, qmessage2.AwsSqsMessage };

            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = messages
            };
            
            var empty = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = new List<Message>()
            };
            
            var sentResponse = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sentResponse);
            
            sqsClient.SetupSequence(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .ReturnsAsync(empty);

            var deleteResponse = new DeleteMessageResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);
            
            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.DeleteIfInvalid = false;
                    settings.FetchUntilEmpty = true;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>(testMessageName);

            sut.Invoking(s => s.FetchAsync()).Should().NotThrow();

            InMemorySink.Instance.Should().HaveMessage("Fetching queue messages.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processing {Count} messages.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processing message with id {Id}.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message received.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Received it.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Deserializing message body.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message validation.").Appearing().Times(2);
            InMemorySink.Instance.Should().NotHaveMessage("Message id {id} deleted from queue.");
        }
        
        [Theory, AutoMoqData]
        public void ShouldFetchAndWarnIfVersionMismatch(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            string testMessageName)
        {
            model.Name = "valid";
            
            var qmessage = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}),
                new KeyValuePair<string, MessageAttributeValue>(Constants.MessageAttributes.MessageVersion,
                    new MessageAttributeValue{DataType = "String", StringValue = "0.9.0"}));

            var qmessage2 = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}),
                new KeyValuePair<string, MessageAttributeValue>(Constants.MessageAttributes.MessageVersion,
                    new MessageAttributeValue{DataType = "String", StringValue = "1.0.1"}));
            
            var messages = new List<Message> { qmessage.AwsSqsMessage, qmessage2.AwsSqsMessage };

            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = messages
            };
            
            var empty = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = new List<Message>()
            };
            
            var sentResponse = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK
            };
            
            var deleteResponse = new DeleteMessageResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sentResponse);
            
            sqsClient.SetupSequence(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .ReturnsAsync(empty);
            
            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = true;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>(testMessageName);

            sut.Invoking(s => s.FetchAsync()).Should().NotThrow();

            InMemorySink.Instance.Should().HaveMessage("Fetching queue messages.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processing {Count} messages.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processing message with id {Id}.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message received.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Received it.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Deserializing message body.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message validation.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Validated it").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message processing.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processed it.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Sent message {MessageId} to queue {queueUrl}.").Appearing().Times(2);

            InMemorySink.Instance.Should()
                .HaveMessage(
                    "Message id {id} was created using version {messageVersion} which is older than this version {currentVersion}")
                .Appearing().Once();
            
            InMemorySink.Instance.Should()
                .HaveMessage(
                    "Message id {id} was created using version {messageVersion} which is newer than this version {currentVersion}")
                .Appearing().Once();
        }
        
        [Theory, AutoMoqData]
        public void ShouldFetchAndProcessMessageOnlyOnce(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            string testMessageName)
        {
            model.Name = "valid";
            
            var qmessage = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));
            
            var messages = new List<Message> { qmessage.AwsSqsMessage };

            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = messages
            };
            
            var empty = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = new List<Message>()
            };
            
            var sentResponse = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK
            };
            
            var deleteResponse = new DeleteMessageResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sentResponse);
            
            sqsClient.SetupSequence(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .ReturnsAsync(response)
                .ReturnsAsync(empty);
            
            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = false;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>(testMessageName);

            sut.Invoking(s => s.FetchAsync()).Should().NotThrow();

            InMemorySink.Instance.Should().HaveMessage("Fetching queue messages.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processing {Count} messages.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processing message with id {Id}.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message received.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Received it.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Deserializing message body.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message validation.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Validated it").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message processing.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processed it.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Sent message {MessageId} to queue {queueUrl}.").Appearing().Times(1);
        }
        
        [Theory, AutoMoqData]
        public void ShouldFetchAndLogErrorsAndContinue(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            TestMessageModel model2,
            string testMessageName)
        {
            model.Name = "valid";
            model2.Name = "throw";
            
            var qmessage = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));
            
            var qmessage2 = new QMessage(testMessageName, JsonConvert.SerializeObject(model2),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));
            
            var messages = new List<Message> { qmessage.AwsSqsMessage, qmessage2.AwsSqsMessage };

            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = messages
            };
            
            var deleteResponse = new DeleteMessageResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);
            
            var empty = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = new List<Message>()
            };
            
            var sentResponse = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sentResponse);
            
            sqsClient.SetupSequence(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .ReturnsAsync(empty);
            
            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = true;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>(testMessageName);

            sut.Invoking(s => s.FetchAsync()).Should().NotThrow();

            InMemorySink.Instance.Should().HaveMessage("Fetching queue messages.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processing {Count} messages.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processing message with id {Id}.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message received.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Received it.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Deserializing message body.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message validation.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Validated it").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message processing.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processed it.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Sent message {MessageId} to queue {queueUrl}.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message error for message id {id}.")
                .Appearing().Once();
            InMemorySink.Instance.Should().HaveMessage("Errored it.").Appearing().Once();
        }
        
        [Theory, AutoMoqData]
        public void ShouldNotDeleteOnError(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            TestMessageModel model2,
            string testMessageName)
        {
            model.Name = "valid";
            model2.Name = "throw";
            
            var qmessage = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));
            
            var qmessage2 = new QMessage(testMessageName, JsonConvert.SerializeObject(model2),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));
            
            var messages = new List<Message> { qmessage.AwsSqsMessage, qmessage2.AwsSqsMessage };

            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = messages
            };
            
            var deleteResponse = new DeleteMessageResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);
            
            var empty = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = new List<Message>()
            };
            
            var sentResponse = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sentResponse);
            
            sqsClient.SetupSequence(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .ReturnsAsync(empty);
            
            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.DeleteIfError = false;
                    settings.FetchUntilEmpty = true;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>(testMessageName);

            sut.Invoking(s => s.FetchAsync()).Should().NotThrow();

            InMemorySink.Instance.Should().HaveMessage("Fetching queue messages.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processing {Count} messages.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processing message with id {Id}.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message received.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Received it.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Deserializing message body.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message validation.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Validated it").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message processing.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processed it.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Sent message {MessageId} to queue {queueUrl}.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message error for message id {id}.")
                .Appearing().Once();
            InMemorySink.Instance.Should().HaveMessage("Errored it.").Appearing().Once();
            InMemorySink.Instance.Should().HaveMessage("Message id {id} deleted from queue.").Appearing().Once();
        }

        [Theory, AutoMoqData]
        public void ShouldFetchAndSkipIfMissingName(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            string testMessageName)
        {
            model.Name = "valid";
            
            var qmessage = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));

            var qmessage2 = new Message();
            
            var messages = new List<Message> { qmessage.AwsSqsMessage, qmessage2 };

            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = messages
            };
            
            var empty = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = new List<Message>()
            };
            
            var sentResponse = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sentResponse);
            
            sqsClient.SetupSequence(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .ReturnsAsync(empty);
            
            var deleteResponse = new DeleteMessageResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);
            
            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = true;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>(testMessageName);

            sut.Invoking(s => s.FetchAsync()).Should().NotThrow();

            InMemorySink.Instance.Should().HaveMessage("Fetching queue messages.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processing {Count} messages.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processing message with id {Id}.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message received.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Received it.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Deserializing message body.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message validation.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Validated it").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message processing.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processed it.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Sent message {MessageId} to queue {queueUrl}.").Appearing().Times(1);
            InMemorySink.Instance.Should()
                .HaveMessage("Message id {Id} does not appear to be a valid QBroker message and will be skipped.")
                .Appearing().Once();
        }
        
        [Theory, AutoMoqData]
        public void ShouldFetchAndSkipIfMissingVersion(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            string testMessageName)
        {
            model.Name = "valid";
            
            var qmessage = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));

            var qmessage2 = new Message();
            qmessage2.MessageAttributes.Add(Constants.MessageAttributes.MessageName, new MessageAttributeValue{DataType = "String", StringValue = "value"});
            
            var messages = new List<Message> { qmessage.AwsSqsMessage, qmessage2 };

            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = messages
            };
            
            var empty = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = new List<Message>()
            };
            
            var sentResponse = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sentResponse);
            
            sqsClient.SetupSequence(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .ReturnsAsync(empty);
            
            var deleteResponse = new DeleteMessageResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);
            
            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = true;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>(testMessageName);

            sut.Invoking(s => s.FetchAsync()).Should().NotThrow();

            InMemorySink.Instance.Should().HaveMessage("Fetching queue messages.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processing {Count} messages.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processing message with id {Id}.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message received.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Received it.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Deserializing message body.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message validation.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Validated it").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message processing.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processed it.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Sent message {MessageId} to queue {queueUrl}.").Appearing().Times(1);
            InMemorySink.Instance.Should()
                .HaveMessage("Message id {Id} does not appear to be a valid QBroker message and will be skipped.")
                .Appearing().Once();
        }
        
        [Theory, AutoMoqData]
        public void ShouldFetchAndSkipUfUnknownProcessor(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            string testMessageName)
        {
            model.Name = "valid";
            
            var qmessage = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));

            var qmessage2 = new QMessage("not-registered-name", JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));
            
            var messages = new List<Message> { qmessage.AwsSqsMessage, qmessage2.AwsSqsMessage };

            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = messages
            };
            
            var empty = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = new List<Message>()
            };
            
            var sentResponse = new SendMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sentResponse);
            
            sqsClient.SetupSequence(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .ReturnsAsync(empty);
            
            var deleteResponse = new DeleteMessageResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);

            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = true;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>(testMessageName);

            sut.Invoking(s => s.FetchAsync()).Should().NotThrow();

            InMemorySink.Instance.Should().HaveMessage("Fetching queue messages.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processing {Count} messages.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processing message with id {Id}.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message received.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Received it.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Deserializing message body.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message validation.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Validated it").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Executing processor message processing.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Processed it.").Appearing().Times(1);
            InMemorySink.Instance.Should().HaveMessage("Sent message {MessageId} to queue {queueUrl}.").Appearing().Times(1);
            InMemorySink.Instance.Should()
                .HaveMessage("Skipping message id {Id}. No processor found for message name.")
                .Appearing().Once();
        }
        
        [Theory, AutoMoqData]
        public void ShouldFetchAndHaltIfValidateReturnsNull(
            Mock<IAmazonSQS> sqsClient,
            TestMessageModel model,
            string testMessageName)
        {
            model.Name = "invalid";
            
            var qmessage = new QMessage(testMessageName, JsonConvert.SerializeObject(model),
                new KeyValuePair<string, MessageAttributeValue>("test-attribute", 
                    new MessageAttributeValue {DataType = "String", StringValue = "test-value"}));

            var messages = new List<Message> { qmessage.AwsSqsMessage };

            var response = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = messages
            };
            
            var empty = new ReceiveMessageResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                Messages = new List<Message>()
            };

            var deleteResponse = new DeleteMessageResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            };

            sqsClient.Setup(s => s.DeleteMessageAsync(It.IsAny<DeleteMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(deleteResponse);
            
            sqsClient.SetupSequence(
                    s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response)
                .ReturnsAsync(empty);
            
            var sut = new QBroker(sqsClient.Object)
                .Setup(settings =>
                {
                    settings.FetchUntilEmpty = true;
                    settings.MaxNumberOfMessages = 10;
                    settings.WaitTimeSeconds = 0;
                    settings.QueueUrl = "test-queue";
                    settings.Logger = _createLogger();
                })
                .AddMessageType<TestMessageModel, TestQProcessor>(testMessageName);

            sut.Invoking(s => s.FetchAsync()).Should().NotThrow();

            InMemorySink.Instance.Should().HaveMessage("Fetching queue messages.").Appearing().Times(2);
            InMemorySink.Instance.Should().HaveMessage("Processing {Count} messages.").Appearing().Once();
            InMemorySink.Instance.Should().HaveMessage("Processing message with id {Id}.").Appearing().Once();
            InMemorySink.Instance.Should().HaveMessage("Executing processor message received.").Appearing().Once();
            InMemorySink.Instance.Should().HaveMessage("Received it.").Appearing().Once();
            InMemorySink.Instance.Should().HaveMessage("Deserializing message body.").Appearing().Once();
            InMemorySink.Instance.Should().HaveMessage("Executing processor message validation.").Appearing().Once();
            InMemorySink.Instance.Should().HaveMessage("Message with id {Id} was marked invalid by processor and execution has stopped.").Appearing().Once();
            InMemorySink.Instance.Should().HaveMessage("Invalidated it");
            InMemorySink.Instance.Should().NotHaveMessage("Executing processor message processing.");
            InMemorySink.Instance.Should().NotHaveMessage("Processed it.");
            InMemorySink.Instance.Should().NotHaveMessage("Sent message {MessageId} to queue {queueUrl}.");
        }

        private ILogger _createLogger() =>
            new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.InMemory()
                .CreateLogger();
    }
}