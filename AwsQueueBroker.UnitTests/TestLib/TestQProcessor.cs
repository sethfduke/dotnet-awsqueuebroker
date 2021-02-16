using Amazon.SQS.Model;
using Serilog;
using System;
using System.Threading.Tasks;

namespace AwsQueueBroker.UnitTests.TestLib
{
    public class TestQProcessor : QProcessor<TestMessageModel>
    {
        public override async Task Received(Message message, ILogger logger = null)
        {
            logger?.Debug("Received it.");
        }

        public override async Task<object> Validate(Message message, TestMessageModel body, ILogger logger = null)
        {
            if (body.Name == "valid")
            {
                logger?.Debug("Validated it");
                return body;
            }

            if (body.Name == "throw")
            {
                throw new Exception("We blew it.");
            }

            logger?.Debug("Invalidated it");
            return null;
        }

        public override async Task<QReply> Process(Message message, TestMessageModel model, ILogger logger = null)
        {
            logger?.Debug("Processed it.");

            return new QReply()
            {
                Message = new QMessage("reply-message", "Some content"),
                ReplyToQueueUrl = "http://localhost:4566/000000000000/qbroker-test-reply-queue"
            };
        }

        public override async Task Error(Message message, Exception exception, ILogger logger = null)
        {
            logger?.Debug("Errored it.");
        }
    }
}