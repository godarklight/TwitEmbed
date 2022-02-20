using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using Discord;

namespace TwitEmbed
{
    public class Database
    {
        SQLiteConnection connection;
        public Database()
        {
            connection = new SQLiteConnection("Data Source=data.db");
            connection.Open();
            CreateTable();
        }

        void CreateTable()
        {
            string query = "CREATE TABLE IF NOT EXISTS post_source ( time INTEGER NOT NULL, source INTEGER NOT NULL, bot INTEGER NOT NULL );";
            using (SQLiteCommand command = new SQLiteCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        public void AddReference(ulong source, ulong bot)
        {
            string query = "INSERT INTO post_source ( time, source, bot ) VALUES ( @time, @source, @bot )";
            using (SQLiteCommand command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("time", DateTime.UtcNow.Ticks);
                command.Parameters.AddWithValue("source", source);
                command.Parameters.AddWithValue("bot", bot);
                int result = command.ExecuteNonQuery();
                if (result > 0)
                {
                    Log($"Linked bot post {bot} to {source}, {result} affected");
                }
            }
        }

        public void DeleteSource(ulong source)
        {
            string query = "DELETE FROM post_source WHERE source = @source";
            using (SQLiteCommand command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("source", source);
                int result = command.ExecuteNonQuery();
                if (result > 0)
                {
                    Log($"Removing source {source}, {result} affected");
                }
            }
        }

        public ulong[] GetBotPosts(ulong source)
        {
            List<ulong> retVal = new List<ulong>();
            string query = "SELECT bot FROM post_source WHERE source = @source";
            using (SQLiteCommand command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("source", source);
                SQLiteDataReader sdr = command.ExecuteReader();
                while (sdr.Read())
                {
                    retVal.Add((ulong)((long)sdr[0]));
                }
            }
            return retVal.ToArray();
        }

        void Log(string message, LogSeverity severity = LogSeverity.Info)
        {
            Program.Log(new LogMessage(severity, "Database", message).ToString());
        }
    }
}