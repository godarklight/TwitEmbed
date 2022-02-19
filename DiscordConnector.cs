using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using TwitterSharp.Response;
using TwitterSharp.Response.RTweet;
using TwitterSharp.Response.RMedia;
using System.Diagnostics;

namespace TwitEmbed
{
    class DiscordConnector
    {
        DiscordSocketClient discord;
        TwitterConnector twitter;
        public DiscordConnector(TwitterConnector twitter)
        {
            DiscordSocketConfig dsc = new DiscordSocketConfig();
            dsc.GatewayIntents = GatewayIntents.AllUnprivileged;
            dsc.GatewayIntents |= GatewayIntents.GuildMessages;
            dsc.GatewayIntents -= GatewayIntents.GuildInvites;
            dsc.GatewayIntents -= GatewayIntents.GuildScheduledEvents;
            discord = new DiscordSocketClient(dsc);
            discord.Ready += Ready;
            discord.Log += Log;
            discord.MessageReceived += MessageReceived;
            this.twitter = twitter;
        }

        public async Task Login(string token)
        {
            await discord.LoginAsync(TokenType.Bot, token);
        }

        public async Task Start()
        {
            await discord.StartAsync();
        }

        async Task Ready()
        {
            await Log("Ready");
        }

        async Task MessageReceived(SocketMessage message)
        {
            if (message.Author.Id == discord.CurrentUser.Id)
            {
                return;
            }
            if (message.Author.IsBot)
            {
                return;
            }
            if (message.Content == null)
            {
                return;
            }

            //Detect is bot is an admin
            bool isAdmin = false;
            SocketTextChannel stc = message.Channel as SocketTextChannel;
            if (stc != null)
            {
                SocketGuildChannel sgc = stc as SocketGuildChannel;
                if (sgc != null)
                {
                    SocketGuildUser sgu = sgc.Guild.GetUser(discord.CurrentUser.Id);
                    ChannelPermissions permissions = sgu.GetPermissions(sgc);
                    isAdmin = permissions.ManageMessages;
                }
            }

            SocketUserMessage sum = message as SocketUserMessage;
            if (sum == null)
            {
                return;
            }
            ParseMessage(sum, isAdmin);
            await Task.CompletedTask;
        }

        async void ParseMessage(SocketUserMessage message, bool isAdmin)
        {
            TwitterData[] tweets = await twitter.ParseTweet(message.Content);
            if (tweets.Length == 0)
            {
                return;
            }

            await Log($"{message.Author.Id} {message.Author.Username}#{message.Author.Discriminator} posted a twitter link {message.Content}");

            bool suppress = false;
            foreach (TwitterData twitterData in tweets)
            {
                int currentIndex = 0;
                List<Embed> embeds = new List<Embed>();
                foreach (Media media in twitterData.tweet.Attachments.Media)
                {
                    //Twitter indexes start at 1
                    currentIndex++;
                    if (twitterData.mediaIndex != null && twitterData.mediaIndex != currentIndex)
                    {
                        continue;
                    }
                    if (media.Type != MediaType.Photo)
                    {
                        //Video gif, modify the preview URL to get the true URL.
                        ProcessStartInfo psi = new ProcessStartInfo("yt-dlp", $"--cookies cookies.txt {twitterData.url} --get-url");
                        psi.RedirectStandardOutput = true;
                        Process p = Process.Start(psi);
                        await p.WaitForExitAsync();
                        string realLink = p.StandardOutput.ReadToEnd();
                        if (!string.IsNullOrEmpty(realLink))
                        {
                            await message.ReplyAsync(realLink);
                        }
                    }
                    else
                    {
                        suppress = true;
                        EmbedBuilder eb = new EmbedBuilder();
                        if (currentIndex == 1)
                        {
                            eb.Description = twitterData.tweet.Text;
                        }
                        EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                        eab.WithUrl(twitterData.tweet.Author.Url);
                        eab.WithIconUrl(twitterData.tweet.Author.ProfileImageUrl);
                        eab.Name = $"{twitterData.tweet.Author.Name} ({twitterData.tweet.Author.Username})";
                        eb.WithAuthor(eab);
                        eb.ImageUrl = media.Url;
                        embeds.Add(eb.Build());
                    }
                }
                if (embeds.Count > 0)
                {
                    await message.ReplyAsync($"<@{message.Author.Id}> <{twitterData.url}>", embeds: embeds.ToArray());
                }
            }

            if (isAdmin && suppress)
            {
                await message.ModifyAsync(Suppress);
            }
        }

        void Suppress(MessageProperties properties)
        {
            MessageFlags flags = MessageFlags.None;
            if (properties.Flags.IsSpecified)
            {
                flags = properties.Flags.Value.Value;
            }
            flags |= MessageFlags.SuppressEmbeds;
            properties.Flags = flags;
        }

        async Task Log(LogMessage message)
        {
            Program.Log(message.ToString());
            await Task.CompletedTask;
        }

        async Task Log(string message, LogSeverity severity = LogSeverity.Info)
        {
            Program.Log(new LogMessage(severity, "DiscordConnector", message).ToString());
            await Task.CompletedTask;
        }
    }
}