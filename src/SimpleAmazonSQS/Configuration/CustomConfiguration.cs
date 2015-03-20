﻿namespace SimpleAmazonSQS.Configuration
{
    public class CustomConfiguration : IConfiguration
    {
        public string AccessKey { get; set; }
        public string SecretKey { get; set; }
        public string QueueUrl { get; set; }
    }
}