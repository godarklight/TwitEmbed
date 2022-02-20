using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using TwitterSharp.Client;
using TwitterSharp.Request.AdvancedSearch;
using TwitterSharp.Response.RTweet;
using Discord;

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
            mo = new MediaOption[] { MediaOption.Url, MediaOption.Preview_Image_Url, MediaOption.Duration_Ms, MediaOption.Height, MediaOption.Width };
            uo = new UserOption[] { UserOption.Profile_Image_Url, UserOption.Url };
        }

        public async Task<TwitterData[]> ParseTweet(string url)
        {
            string[] split = url.Split(' ');
            List<TwitterData> returnValue = new List<TwitterData>();
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

        private async Task ProcessFullLink(string url, List<TwitterData> returnValue)
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
            int photoIndex = url.IndexOf("/photo/");
            int? mediaIndex = null;
            if (photoIndex != -1)
            {
                string photoString = url.Substring(url.LastIndexOf("/") + 1);
                if (Int32.TryParse(photoString, out int mediaIndexInt))
                {
                    mediaIndex = mediaIndexInt;
                }
                url = url.Substring(0, photoIndex);
            }
            if (Int64.TryParse(url, out long tweetID))
            {
                var tweet = await tc.GetTweetAsync(url, to, uo, mo);
                if (tweet.Attachments != null && tweet.Attachments.Media != null)
                {
                    TwitterData tg = new TwitterData(tweet, origUrl, mediaIndex);
                    returnValue.Add(tg);
                }
            }
            else
            {
                await Log($"Failed to parse: {origUrl}, current: {url}");
            }
        }

        private async Task ProcessShortLink(string url, List<TwitterData> returnValue)
        {
            HttpResponseMessage hrm = await hc.GetAsync(url);
            await ProcessFullLink(hrm.RequestMessage?.RequestUri.ToString(), returnValue);
        }

        async Task Log(string message, LogSeverity severity = LogSeverity.Info)
        {
            Program.Log(new LogMessage(severity, "Twitter", message).ToString());
            await Task.CompletedTask;
        }
    }
}
