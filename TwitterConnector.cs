using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using TwitterSharp.Client;
using TwitterSharp.Request.AdvancedSearch;

namespace TwitEmbed
{
    class TwitterConnector
    {
        TwitterClient tc;
        HttpClient hc;
        TweetOption[] to;
        MediaOption[] mo;
        UserOption[] uo;

        public TwitterConnector(string token)
        {
            tc = new TwitterClient(token);
            hc = new HttpClient();
            to = new TweetOption[] { TweetOption.Possibly_Sensitive, TweetOption.Attachments, TweetOption.Attachments_Ids };
            mo = new MediaOption[] { MediaOption.Url };
        }

        public async Task<string[]> ParseTweet(string url)
        {
            string[] split = url.Split(' ');
            List<string> returnValue = new List<string>();
            foreach (string str in split)
            {
                if (str.StartsWith("http://twitter.com/"))
                {
                    await ProcessFullLink(str, returnValue);
                }
                if (str.StartsWith("https://twitter.com/"))
                {
                    await ProcessFullLink(str, returnValue);
                }
                if (str.StartsWith("http://t.co/"))
                {
                    await ProcessShortLink(str, returnValue);
                }
                if (str.StartsWith("https://t.co/"))
                {
                    await ProcessShortLink(str, returnValue);
                }
            }
            return returnValue.ToArray();
        }

        private async Task ProcessFullLink(string url, List<string> returnValue)
        {
            //Decode URL
            string origUrl = url;
            int statusIndex = url.IndexOf("status/");
            if (statusIndex != -1)
            {
                url = url.Substring(statusIndex + 7);
            }
            int questionIndex = url.IndexOf("?");
            if (questionIndex != -1)
            {
                url = url.Substring(0, questionIndex);
            }
            int photoParse = -1;
            int photoIndex = url.IndexOf("/photo/");
            if (photoIndex != -1)
            {
                string photoString = url.Substring(url.LastIndexOf("/") + 1);
                Int32.TryParse(photoString, out photoParse);
                url = url.Substring(0, photoIndex);
            }
            if (Int64.TryParse(url, out long tweetID))
            {
                var tweet = await tc.GetTweetAsync(url, to, null, mo);
                if (tweet.Attachments != null && tweet.Attachments.Media != null)
                {
                    if (photoParse == -1)
                    {
                        Console.WriteLine($"Parsed {url}, {tweet.Attachments.Media.Length} attachments");
                        foreach (var attachment in tweet.Attachments.Media)
                        {
                            returnValue.Add(attachment.Url);
                        }
                    }
                    else
                    {
                        if (tweet.Attachments.Media.Length >= photoIndex)
                        {
                            Console.WriteLine($"Parsed {url}, selected attachment");
                            returnValue.Add(tweet.Attachments.Media[photoIndex].Url);
                        }
                        else
                        {
                            Console.WriteLine($"Parsed {url}, selected attachment not found");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Parsed {url}, no attachments");
                }
            }
            else
            {
                Console.WriteLine($"Failed to parse: {origUrl}, current: {url}");
            }
        }

        private async Task ProcessShortLink(string url, List<string> returnValue)
        {
            HttpResponseMessage hrm = await hc.GetAsync(url);
            await ProcessFullLink(hrm.RequestMessage?.RequestUri.ToString(), returnValue);
        }
    }
}
