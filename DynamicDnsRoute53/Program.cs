using Amazon;
using Amazon.Route53;
using Amazon.Route53.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace DynamicDnsRoute53
{
    class Program
    {
        private static readonly string[] _requiredParameterNames = new string[] { "id", "secret", "domain", "subdomain" };

        static void Main(string[] args)
        {
            // This will store our command line parameters
            Dictionary<string, string> lookup = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            // Very basic logic to parse our command line parameters
            // 1) If the param starts with a "-" then it is a parameter name
            // 2) If the param does not start with a "-" then it is a value and we add it to our lookup
            // NOTE: This does not support multiple params with the same name. It will only use the last pair.
            {
                string flag = null;
                foreach (string s in args)
                {
                    if (s.StartsWith('-')) flag = s.TrimStart('-');
                    else lookup[flag] = s;
                }
            }          

            // Verify our input parameters and update Route53 DNS records if possible
            if (VerifyRequiredParameters(lookup))
                PerformDnsUpdate(lookup["id"], lookup["secret"], lookup["domain"], lookup["subdomain"]).Wait();
        }

        private static bool VerifyRequiredParameters(Dictionary<string, string> paramLookup)
        {
            bool missingRequiredParams = false;
            foreach(string name in _requiredParameterNames)
            {
                // TODO: We should update this to roughly validate the input format for the variables as well

                if (paramLookup.ContainsKey(name) == false)
                {
                    Console.WriteLine($"Missing required input parameter: {name}");
                    missingRequiredParams = true;
                }
            }

            return !missingRequiredParams;
        }

        /// <summary>
        /// This method does all of the work to update the DNS records with our current WAN IP. This is separated from the main
        /// method to make it easier in the future to perform batch updates (if needed).
        /// </summary>
        /// <param name="id">The AWS Access key ID</param>
        /// <param name="secret">The AWS Access key secret</param>
        /// <param name="domain">The domain name we are going to be updating records for (e.g. shawnlehner.com)</param>
        /// <param name="subdomain">The subdomain we would like to update (e.g. "house" if we were updating house.shawnlehner.com)</param>
        private static async Task PerformDnsUpdate(string id, string secret, string domain, string subdomain)
        {
            #region Lookup Current WAN IP

            // We use ipify.org to quickly/easily get our external WAN IP address which we can later use
            // for updating our DNS records.

            string ip = null;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.ipify.org");
            using (Stream s = (await request.GetResponseAsync()).GetResponseStream())
            using (StreamReader r = new StreamReader(s))
            {
                ip = r.ReadToEnd();
            }

            #endregion

            // Combine our domain and subdomain to get the full record name
            string recordName = subdomain.Trim('.') + "." + domain.Trim('.');

            // Create our AWS API client for sending requests to the Route53 API
            AmazonRoute53Client client = new AmazonRoute53Client(id, secret, RegionEndpoint.USEast1);

            #region Lookup current state of the domain on Route53

            // Lookup the zone for our domain
            HostedZone zone = (await client.ListHostedZonesByNameAsync(new ListHostedZonesByNameRequest
            {
                DNSName = domain
            })).HostedZones.First();

            // Lookup our current records to see if we need to make an update
            ListResourceRecordSetsResponse recordSet = await client.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
            {
                HostedZoneId = zone.Id,
                MaxItems = "1",
                StartRecordName = recordName
            });

            #endregion

            // Check to see if our IP is already up to date. No sense making a change if we don't need to.
            if (recordSet.ResourceRecordSets.Count > 0 && 
                recordSet.ResourceRecordSets[0].Name.Trim('.') == recordName &&
                recordSet.ResourceRecordSets[0].ResourceRecords[0].Value == ip)
                return;

            #region Request DNS record update with new IP

            // Our IP address is not up-to-date so we need to make a change request. We use UPSERT action which
            // will work whether or not a record already exists.
            ChangeResourceRecordSetsRequest changeRequest = new ChangeResourceRecordSetsRequest
            {
                HostedZoneId = zone.Id,
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change>
                    {
                        new Change
                        {
                            ResourceRecordSet = new ResourceRecordSet
                            {
                                Name = recordName,
                                TTL = 60,
                                Type = RRType.A,
                                ResourceRecords = new List<ResourceRecord> { new ResourceRecord { Value = ip } }
                            },
                            Action = ChangeAction.UPSERT
                        }
                    }
                }
            };

            // Send our change request to the API
            ChangeResourceRecordSetsResponse response = 
                await client.ChangeResourceRecordSetsAsync(changeRequest);

            // Check our response code to verify everything worked
            if (response.HttpStatusCode != HttpStatusCode.OK) 
                throw new Exception("API request to update DNS record has failed.");

            #endregion
        }
    }
}
