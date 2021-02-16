using Amazon.SQS;
using Amazon.SQS.Model;
using Semver;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace AwsQueueBroker
{
    /// <summary>
    /// QBroker Class
    /// </summary>
    public sealed class QBroker
    {
        private IAmazonSQS _sqsClient;
        private QBrokerSettings _settings;
        private readonly Dictionary<string, Type> _messageTypes = new Dictionary<string, Type>();
        private readonly Dictionary<string, Type> _modelTypes = new Dictionary<string, Type>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="sqsClient">IAmazonSQS instance to use for send and receive.</param>
        public QBroker(IAmazonSQS sqsClient)
        {
            _sqsClient = sqsClient;
        }
        
        /// <summary>
        /// Add a model and its associated message processor class and associate with the specified message name.
        /// </summary>
        /// <param name="name">The unique name of the message</param>
        /// <typeparam name="TModel">A model representing the expected body of the message</typeparam>
        /// <typeparam name="TProcessor">Implementation of QProcessor for this type.</typeparam>
        /// <returns>QBroker</returns>
        public QBroker AddMessageType<TModel, TProcessor>(string name) 
            where TProcessor : QProcessor<TModel>
        {
            _messageTypes.Add(name, typeof(TProcessor));
            _modelTypes.Add(name, typeof(TModel));
            return this;
        }

        /// <summary>
        /// Allows configuration of the brokers settings.
        /// </summary>
        /// <param name="settings">settings => {...}</param>
        /// <returns>QBroker</returns>
        public QBroker Setup(
            Action<QBrokerSettings> settings)
        {
            _settings = new QBrokerSettings();
            settings(_settings);
            return this;
        }

        /// <summary>
        /// Delete a message from a queue.
        /// </summary>
        /// <param name="message">The QMessage to delete.</param>
        /// <returns>Task</returns>
        /// <exception cref="Exception">Throws if delete fails.</exception>
        public async Task DeleteAsync(
            QMessage message)
        {
            var result = await
                _sqsClient.DeleteMessageAsync(new DeleteMessageRequest(_settings.QueueUrl,
                    message.AwsSqsMessage.ReceiptHandle));

            if (result.HttpStatusCode == HttpStatusCode.OK)
            {
                _settings.Logger?.Debug("Message id {id} deleted from queue.", message.Id);
            }
            else
            {
                throw new Exception($"Error deleting message id {message.Id} to queue {_settings.QueueUrl}.");
            }
        }
        
        /// <summary>
        /// Send an SQS message to the specified queue url.
        /// </summary>
        /// <param name="message">QMessage to send.</param>
        /// <param name="queueUrl">Queue url to send to.</param>
        /// <returns>Task</returns>
        /// <exception cref="Exception">Throws if not successful.</exception>
        public async Task SendAsync(
            QMessage message,
            string queueUrl)
        {
            var result = await _sqsClient.SendMessageAsync(new SendMessageRequest()
            {
                MessageBody = message.Body,
                QueueUrl = queueUrl,
                MessageAttributes = message.MessageAttributes
            });

            if (result.HttpStatusCode == HttpStatusCode.OK)
            {
                _settings.Logger?.Debug("Sent message {MessageId} to queue {queueUrl}.", result.MessageId, queueUrl);
            }
            else
            {
                throw new Exception($"Error sending message to queue {queueUrl}.");
            }
        }

        /// <summary>
        /// Polls the queue url specified in the QBrokerSettings and processes according to the added processors.
        /// </summary>
        /// <returns>Task</returns>
        /// <exception cref="Exception"></exception>
        public async Task FetchAsync()
        {
            while (true)
            {
                _settings.Logger?.Information("Fetching queue messages.");

                var received = await _readMessages();
                
                if (received.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Unable to read messages from queue {_settings.QueueUrl}. AWS responded with code {received.HttpStatusCode}.");
                }
                
                if (received.Messages.Count <= 0) return;

                _settings.Logger?.Information("Processing {Count} messages.", received.Messages.Count);

                foreach (var receivedMessage in received.Messages)
                {
                    QMessage qMessage;

                    try
                    {
                        qMessage = new QMessage(receivedMessage);
                    }
                    catch (ArgumentException)
                    {
                        _settings.Logger?
                            .Debug("Message id {Id} does not appear to be a valid QBroker message and will be skipped.",
                                receivedMessage.MessageId);
                        continue;
                    }

                    _settings.Logger?.Debug("Processing message with id {Id}.", qMessage.Id);

                    var messageVersion = Semver.SemVersion.Parse(qMessage.Version);
                    var compare = SemVersion.Compare(messageVersion, Constants.Library.SemVersion);
                    switch (compare)
                    {
                        case -1:
                            _settings.Logger?.Warning(
                                "Message id {id} was created using version {messageVersion} which is older than this version {currentVersion}",
                                qMessage.Id, messageVersion, Constants.Library.Version);
                            break;
                        case 1:
                            _settings.Logger?.Warning(
                                "Message id {id} was created using version {messageVersion} which is newer than this version {currentVersion}",
                                qMessage.Id, messageVersion, Constants.Library.Version);
                            break;
                    }

                    if (!_messageTypes.ContainsKey(qMessage.Name))
                    {
                        _settings.Logger?.Debug("Skipping message id {Id}. No processor found for message name.", qMessage.Id);
                        continue;
                    }

                    var messageType = _messageTypes[qMessage.Name];
                    var modelType = _modelTypes[qMessage.Name];

                    var processor = Activator.CreateInstance(messageType);

                    try
                    {
                        await _processMessage(qMessage, messageType, modelType, processor, receivedMessage);
                    }
                    catch (Exception e)
                    {
                        _settings.Logger?.Debug("Executing processor message error for message id {id}.", qMessage.Id);

                        await _executeAsyncMethod(Constants.MethodNames.Error, messageType, processor,
                            _obj(receivedMessage, e, _settings.Logger));
                        
                        if (_settings.DeleteIfError)
                        {
                            await DeleteAsync(qMessage);
                        }
                    }
                }

                if (_settings.FetchUntilEmpty) continue;
                break;
            }
        }

        private async Task _processMessage(
            QMessage qMessage,
            Type messageType,
            Type modelType,
            object processor,
            Message receivedMessage)
        {
            _settings.Logger?.Debug("Executing processor message received.", qMessage.Id);
                    
            await _executeAsyncMethod(Constants.MethodNames.Received,
                messageType, processor, _obj(receivedMessage, _settings.Logger));

            _settings.Logger?.Debug("Deserializing message body.", qMessage.Id);

            var body = Newtonsoft.Json.JsonConvert.DeserializeObject(receivedMessage.Body, modelType);

            _settings.Logger?.Debug("Executing processor message validation.", qMessage.Id);
                    
            var validatedModel = await _executeAsyncMethod<object>(Constants.MethodNames.Validate,
                messageType, processor, _obj(receivedMessage, body, _settings.Logger));

            if (validatedModel != null)
            {
                _settings.Logger?.Debug("Executing processor message processing.", qMessage.Id);
                        
                var reply = await _executeAsyncMethod<QReply>(Constants.MethodNames.Process,
                    messageType, processor, _obj(receivedMessage, validatedModel, _settings.Logger));

                if (reply != null && !string.IsNullOrEmpty(reply.ReplyToQueueUrl))
                {
                    await SendAsync(reply.Message, reply.ReplyToQueueUrl);
                }
                
                if (_settings.DeleteIfSuccess)
                {
                    await DeleteAsync(qMessage);
                }
            }
            else
            {
                _settings.Logger?.Debug("Message with id {Id} was marked invalid by processor and execution has stopped.",
                    qMessage.Id);

                if (_settings.DeleteIfInvalid)
                {
                    await DeleteAsync(qMessage);
                }
            }
        }

        private static object[] _obj(params object[] o) => o;

        private static async Task _executeAsyncMethod(
            string method,
            Type type,
            object processor,
            object[] parameters)
        {
            var receivedMethod = type.GetMethod(method);
            if (receivedMethod != null)
            {
                if (receivedMethod.Invoke(processor, parameters) is Task task)
                {
                    await task;
                } 
            }
        }
        
        private static async Task<TResult> _executeAsyncMethod<TResult>(
            string method,
            Type type,
            object processor,
            object[] parameters)
        {
            var receivedMethod = type.GetMethod(method);
            var task = receivedMethod?.Invoke(processor, parameters) as Task<TResult>;
            return await task;
        }
        
        private async Task<ReceiveMessageResponse> _readMessages()
        {
            return await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest{
                QueueUrl = _settings.QueueUrl,
                MaxNumberOfMessages = _settings.MaxNumberOfMessages,
                WaitTimeSeconds = _settings.WaitTimeSeconds,
                MessageAttributeNames = new List<string> { "All" }
            });
        }
    }
}