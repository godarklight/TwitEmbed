using System;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;

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
            await Log(new LogMessage(LogSeverity.Info, "DiscordConnector", "Ready"));
        }

        async Task MessageReceived(SocketMessage message)
        {
            if (message.Author.IsBot)
            {
                return;
            }
            if (message.Content == null)
            {
                return;
            }
            SocketTextChannel stc = message.Channel as SocketTextChannel;
            if (stc == null)
            {
                return;
            }
            ParseMessage(stc, message);
            await Task.CompletedTask;
        }

        async void ParseMessage(SocketTextChannel stc, SocketMessage message)
        {
            string[] urls = await twitter.ParseTweet(message.Content);
            if (urls.Length == 0)
            {
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<@{message.Author.Id}>");
            foreach (string url in urls)
            {
                sb.AppendLine(url);
            }
            string urlJoin = sb.ToString();
            if (urlJoin.Length > 1000)
            {
                urlJoin = urlJoin.Substring(0, 1000);
            }
            await stc.SendMessageAsync(urlJoin);
            SocketGuildChannel sgc = stc as SocketGuildChannel;
            SocketGuildUser sgu = sgc.Guild.GetUser(discord.CurrentUser.Id);
            ChannelPermissions permissions = sgu.GetPermissions(sgc);
            if (permissions.ManageMessages)
            {
                await message.DeleteAsync();
            }
        }

        async Task Log(LogMessage message)
        {
            Program.Log(message.ToString());
            await Task.CompletedTask;
        }
    }
}