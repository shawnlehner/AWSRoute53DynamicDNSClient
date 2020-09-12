# Dynamic DNS Client for AWS Route53
## Background
I created this simple project because I had a basic need. I had a location with a dynamic IP address (The WAN IP could change upon modem/router reboots) and I wanted to have a hostname that I could use to always access the location.

Now don't get me wrong, there are a lot of these dynamic IP services out there (no-ip, dydns, etc ...) but all of them have quite a few limitations, especially when you don't want to take on yet another monthly subscription. This was particularly frustrating to me considering I was already paying for multiple domain names. Thus, I was inspired to create a very simple utility which could connect to my existing domains and update DNS records that I could use to reference each location.

## Getting Started
To make this project as portable as possible, and because I love C#, this project was built using [.NET Core 3.1](https://docs.microsoft.com/en-us/dotnet/fundamentals/).

### Accessing the AWS Route53 API

To connect to the AWS Route53 API you will need API credentials. These consist of an **AWS Access ID** and **AWS Access Secret**. You can generate these credentials for your Root AWS account but it is highly recommended that you create an [IAM User](https://docs.aws.amazon.com/IAM/latest/UserGuide/introduction.html) with limited permissions instead.

Once you have created your IAM user you should assign them a custom policy like the one below. This policy will give them access to only the permissions that are needed to update the DNS records for the specified domain.

```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Sid": "VisualEditor0",
            "Effect": "Allow",
            "Action": [
                "route53:ChangeResourceRecordSets",
                "route53:ListResourceRecordSets"
            ],
            "Resource": "arn:aws:route53:::hostedzone/{yourZoneID}"
        },
        {
            "Sid": "VisualEditor1",
            "Effect": "Allow",
            "Action": "route53:ListHostedZonesByName",
            "Resource": "*"
        }
    ]
}
```

### Running the App

Once you have setup your access credentials and built the app for distribution you and run it from the command line. See a sample execution below.

```
DynamicDnsRoute53.exe --id {yourAccessID} --secret {yourAccessSecret} --domain example.com --subdomain loc1
```
 
| Parameter | Description                                                                                                                              |
|-----------|------------------------------------------------------------------------------------------------------------------------------------------|
| id        | Your AWS Access ID for the IAM user you created.                                                                                         |
| seecret   | Your AWS Access secret for the IAM user you created.                                                                                     |
| domain    | The domain name you would like to update DNS records for that is hosted on Route53.                                                      |
| subdomain | The subdomain, or record name, you wish to update. For example, if you wanted to update loc1.example.com this parameter would be "loc1". |

## Thank you!
I always love working on these little experiments and learning along the way. I want to thank you for taking the time to check out my project and I always love feedback (Good and bad!). Check out my website at [shawnlehner.com](https://www.shawnlehner.com) for even more projects!