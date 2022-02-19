using TwitterSharp.Response.RTweet;
public class TwitterData
{
    public readonly Tweet tweet;
    public readonly string url;
    public readonly int? mediaIndex;

    public TwitterData(Tweet tweet, string url, int? mediaIndex)
    {
        this.tweet = tweet;
        this.url = url;
        this.mediaIndex = mediaIndex;
    }
}