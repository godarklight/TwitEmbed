using System;
using Discord;
using Discord.WebSocket;
using TwitterSharp.Client;
using System.Threading.Tasks;

namespace TwitEmbed
{
    class Program
    {
        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        static async Task MainAsync(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: Program.exe TwitterKey DiscordKey");
                return;
            }
            Database db = new Database();
            TwitterConnector twitter = new TwitterConnector(args[0]);
            DiscordConnector discord = new DiscordConnector(twitter, db);
            await discord.Login(args[1]);
            await discord.Start();
            await Task.Delay(-1);

        }
        public static void Log(string text)
        {
            Console.WriteLine(text);
        }
    }
}
