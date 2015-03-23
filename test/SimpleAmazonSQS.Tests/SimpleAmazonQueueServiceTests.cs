﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SimpleAmazonSQS.Configuration;
using SimpleAmazonSQS.Exception;

namespace SimpleAmazonSQS.Tests
{
    [TestFixture]
    public class SimpleAmazonQueueServiceTests
    {
        private SimpleAmazonQueueService _simpleAmazonQueueService;
        private Mock<IAmazonSQS> _fakeAmazonSqs;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
        }

        [SetUp]
        public void SetUp()
        {
            _fakeAmazonSqs = new Mock<IAmazonSQS>();
            _simpleAmazonQueueService = new SimpleAmazonQueueService(
                configuration: new CustomConfiguration { SecretKey = "FakeSecretKey", AccessKey = "FakeAccessKey", QueueUrl = "http://queueurl.aws.com" },
                amazonSqsClient: _fakeAmazonSqs.Object
            );
        }

        [Test]
        public void QueueExists_WhenQueueDoExists_ShouldReturnTrue()
        {
            _fakeAmazonSqs.Setup(c => c.ListQueues(It.IsAny<ListQueuesRequest>()))
                          .Returns(new ListQueuesResponse
                          {
                              QueueUrls = new List<string>
                              {
                                  "http://queueurl.aws.com",
                                  "http://queueurl.aws1.com",
                                  "http://queueurl.aws2.com"
                              }
                          });

            Assert.IsTrue(_simpleAmazonQueueService.QueueExists());
        }

        [Test]
        public void QueueExists_WhenQueueDoNotExists_ShouldReturnFalse()
        {
            _fakeAmazonSqs.Setup(c => c.ListQueues(It.IsAny<ListQueuesRequest>()))
                      .Returns(new ListQueuesResponse
                      {
                          QueueUrls = new List<string>
                              {
                                  "http://queueurl.aws1.com",
                                  "http://queueurl.aws2.com"
                              }
                      });

            Assert.IsFalse(_simpleAmazonQueueService.QueueExists());
        }

        [Test]
        public void QueueExists_IfAmazonClientReturnNull_ShouldReturnFalse()
        {
            _fakeAmazonSqs.Setup(c => c.ListQueues(It.IsAny<ListQueuesRequest>()))
                          .Returns<ListQueuesResponse>(null);

            Assert.IsFalse(_simpleAmazonQueueService.QueueExists());
        }

        [Test]
        public void Enqueue_GivenAGuid_ShouldCallAmazonClientWithExpectedData()
        {
            _fakeAmazonSqs.Setup(c => c.ListQueues(It.IsAny<ListQueuesRequest>()))
              .Returns(new ListQueuesResponse
              {
                  QueueUrls = new List<string>
                              {
                                  "http://queueurl.aws.com"
                              }
              });

            var id = Guid.NewGuid();
            var idAsString = id.ToString();

            _simpleAmazonQueueService.Enqueue(id);

            _fakeAmazonSqs.Verify(mock => 
                mock.SendMessage(It.Is<SendMessageRequest>(parameters => parameters.QueueUrl == "http://queueurl.aws.com" && parameters.MessageBody == idAsString)
            ), Times.Once);
        }

        [Test]
        public void Enqueue_IfQueueDoNotExists_ShouldThrowsException()
        {
            var amazonQueueService = new Mock<SimpleAmazonQueueService>();
            amazonQueueService.Setup(c => c.QueueExists()).Returns(false);

            Action act = () => _simpleAmazonQueueService.Enqueue(Guid.NewGuid());

            act.ShouldThrow<SimpleAmazonSqsException>()
                .WithMessage("Queue is not available or could not be created.");
        }

        [Test]
        public void Dequeue_GivenMessaCountGreaterThan10_ShouldThrowsException()
        {
            Action act = () =>
            {
                var messages = _simpleAmazonQueueService.Dequeue(messageCount: 11).ToList();
            };

            act.ShouldThrow<ArgumentOutOfRangeException>()
               .WithMessage("messageCount must be between 1 and 10.\r\nParameter name: messageCount");
        }

        [Test]
        public void Dequeue_GivenMessaCountLessThan1_ShouldThrowsException()
        {
            Action act = () =>
            {
                var messages = _simpleAmazonQueueService.Dequeue(messageCount: 0).ToList();
            };

            act.ShouldThrow<ArgumentOutOfRangeException>()
               .WithMessage("messageCount must be between 1 and 10.\r\nParameter name: messageCount");
        }

        [Test]
        public void Dequeue_GivenMessageCountBetween1And10_ShouldCallAmazonClientWithExpectedData()
        {
            var messages = _simpleAmazonQueueService.Dequeue(messageCount: 10).ToList();

            _fakeAmazonSqs.Verify(
                mock => mock.ReceiveMessage(It.Is<ReceiveMessageRequest>(parameters => parameters.QueueUrl == "http://queueurl.aws.com" && parameters.MaxNumberOfMessages == 10)
            ), Times.Once);
        }

        [Test]
        public void Dequeue_IfAmazonClientReturnNull_ShouldReturnEmptyGuidList()
        {
            _fakeAmazonSqs.Setup(c => c.ReceiveMessage(It.IsAny<ReceiveMessageRequest>()))
                          .Returns<ReceiveMessageResponse>(null);

            var messages = _simpleAmazonQueueService.Dequeue();

            messages.ToList().Should().BeEmpty();
        }

        [Test]
        public void Dequeue_IfMessagesReturnedAreEmpty_ShouldReturnEmptyGuidList()
        {
            _fakeAmazonSqs.Setup(c => c.ReceiveMessage(It.IsAny<ReceiveMessageRequest>()))
                          .Returns(new ReceiveMessageResponse { Messages = new List<Message>() });

            var messages = _simpleAmazonQueueService.Dequeue();

            messages.ToList().Should().BeEmpty();
        }

        [Test]
        public void Dequeue_GivenMessagesReturnedFromAamazonSqs_ShouldBeParsedToGuidList()
        {
            _fakeAmazonSqs.Setup(c => c.ReceiveMessage(It.IsAny<ReceiveMessageRequest>()))
                          .Returns(new ReceiveMessageResponse
                          {
                              Messages = new List<Message>
                              {
                                  new Message { Body = "9e3098ce-47c0-4610-9dbc-802a64ebd757" },
                                  new Message { Body = "9e3098ce-47c0-4610-9dbc-802a64ebd757" },
                                  new Message { Body = "9e3098ce-47c0-4610-9dbc-802a64ebd757" }
                              }
                          });

            var messages = _simpleAmazonQueueService.Dequeue(messageCount: 3);

            messages.ToList().Should().BeEquivalentTo(new[]
            {
                new Guid("9e3098ce-47c0-4610-9dbc-802a64ebd757"),
                new Guid("9e3098ce-47c0-4610-9dbc-802a64ebd757"),
                new Guid("9e3098ce-47c0-4610-9dbc-802a64ebd757"),
            });
        }

        [Test]
        public void Dequeue_GivenMessageOnQueueThatIsNotAGuid_ShouldBeIgnored()
        {
            _fakeAmazonSqs.Setup(c => c.ReceiveMessage(It.IsAny<ReceiveMessageRequest>()))
                          .Returns(new ReceiveMessageResponse
                          {
                              Messages = new List<Message>
                              {
                                  new Message { Body = "9e3098ce-47c0-4610-9dbc-802a64ebd757" },
                                  new Message { Body = "im not a guid :-) what is going to happen?" },
                                  new Message { Body = "9e3098ce-47c0-4610-9dbc-802a64ebd757" }
                              }
                          });

            var messages = _simpleAmazonQueueService.Dequeue(messageCount: 3);

            messages.ToList().Should().BeEquivalentTo(new[]
            {
                new Guid("9e3098ce-47c0-4610-9dbc-802a64ebd757"),
                new Guid("9e3098ce-47c0-4610-9dbc-802a64ebd757"),
            });
        }

        [Test]
        public void Dequeue_WhenDequeing_DeleteMessageShouldBeCalledForEachMessage()
        {
            _fakeAmazonSqs.Setup(c => c.ReceiveMessage(It.IsAny<ReceiveMessageRequest>()))
              .Returns(new ReceiveMessageResponse
              {
                  Messages = new List<Message>
                              {
                                  new Message { Body = "9e3098ce-47c0-4610-9dbc-802a64ebd757" },
                                  new Message { Body = "im not a guid :-) what is going to happen?" },
                                  new Message { Body = "9e3098ce-47c0-4610-9dbc-802a64ebd757" }
                              }
              });

            var messages = _simpleAmazonQueueService.Dequeue(messageCount: 3).ToList();

            _fakeAmazonSqs.Verify(c => c.DeleteMessage(It.IsAny<DeleteMessageRequest>()), Times.Exactly(3));
        }

        [Test]
        public void DeleteMessage_ShouldCallDeleteMessageOnAmazonClient()
        {
            _simpleAmazonQueueService.DeleteMessage("receiptHandle");

            _fakeAmazonSqs.Verify(
                c => c.DeleteMessage(It.Is<DeleteMessageRequest>(parameters => parameters.QueueUrl == "http://queueurl.aws.com" && parameters.ReceiptHandle == "receiptHandle")), Times.Once);
        }

        [Test]
        public void Count_ShouldCallGetQueueAttributesOnAmazonClient()
        {
            var messageCount = _simpleAmazonQueueService.Count();

            _fakeAmazonSqs.Verify(mock=>
                mock.GetQueueAttributes(It.Is<GetQueueAttributesRequest>(parameters =>
                    parameters.QueueUrl == "http://queueurl.aws.com" &&
                    parameters.AttributeNames[0] == "ApproximateNumberOfMessages")), 
                    
                Times.Once()
            );
        }
    }
}