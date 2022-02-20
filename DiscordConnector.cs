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
        Database db;
        DiscordSocketClient discord;
        TwitterConnector twitter;
        public DiscordConnector(TwitterConnector twitter, Database db)
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
            discord.MessageDeleted += MessageDeleted;
            this.twitter = twitter;
            this.db = db;
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
                    suppress = true;
                    if (media.Type != MediaType.Photo)
                    {
                        //Video gif, modify the preview URL to get the true URL.
                        int tIndex = twitterData.url.IndexOf("twitter");
                        string twitterLHS = twitterData.url.Substring(0, tIndex);
                        string twitterRHS = twitterData.url.Substring(tIndex);
                        string fxTwitter = $"{twitterLHS}fx{twitterRHS}";
                        IMessage sentMessage = await message.ReplyAsync(fxTwitter);
                        db.AddReference(message.Id, sentMessage.Id);
                        /*
                        ProcessStartInfo psi = new ProcessStartInfo("yt-dlp", $"--cookies cookies.txt {twitterData.url} --get-url");
                        psi.RedirectStandardOutput = true;
                        Process p = Process.Start(psi);
                        await p.WaitForExitAsync();
                        string realLink = p.StandardOutput.ReadToEnd();
                        if (!string.IsNullOrEmpty(realLink))
                        {
                            IMessage sentMessage = await message.ReplyAsync(realLink);
                            db.AddReference(message.Id, sentMessage.Id);
                        }
                        */
                    }
                    else
                    {
                        EmbedBuilder eb = new EmbedBuilder();
                        if (currentIndex == 1)
                        {
                            eb.Description = twitterData.tweet.Text;
                        }
                        EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                        eab.WithUrl(twitterData.tweet.Author.Url);
                        eab.WithIconUrl(twitterData.tweet.Author.ProfileImageUrl);
                        eab.Name = $"{twitterData.tweet.Author.Name} (@{twitterData.tweet.Author.Username})";
                        eb.WithAuthor(eab);
                        eb.ImageUrl = media.Url;
                        embeds.Add(eb.Build());
                    }
                }
                if (embeds.Count > 0)
                {
                    IMessage sentMessage = await message.ReplyAsync("", embeds: embeds.ToArray());
                    db.AddReference(message.Id, sentMessage.Id);
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

        async Task MessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {
            DeleteMessage(message, channel);
            await Task.CompletedTask;
        }

        async void DeleteMessage(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {
            ulong[] botPosts = db.GetBotPosts(message.Id);
            if (botPosts.Length > 0)
            {
                db.DeleteSource(message.Id);
                IMessageChannel imc = await channel.GetOrDownloadAsync();
                foreach (ulong botPost in botPosts)
                {
                    await imc.DeleteMessageAsync(botPost);
                }
                await Log($"Deleted twitter message {message.Id}");
            }
        }

        async Task Log(LogMessage message)
        {
            Program.Log(message.ToString());
            await Task.CompletedTask;
        }

        async Task Log(string message, LogSeverity severity = LogSeverity.Info)
        {
            Program.Log(new LogMessage(severity, "Discord", message).ToString());
            await Task.CompletedTask;
        }
    }
}