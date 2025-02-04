using System;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using Npgsql;
using StackExchange.Redis;

namespace Worker
{
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                // Récupération des variables d'environnement pour PostgreSQL
                var postgresHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
                var postgresUser = Environment.GetEnvironmentVariable("POSTGRES_USER");
                var postgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
                var postgresDb = Environment.GetEnvironmentVariable("POSTGRES_DB");
                var postgresPort = Environment.GetEnvironmentVariable("POSTGRES_PORT");

                var postgresConnectionString = $"Host={postgresHost};Port={postgresPort};Username={postgresUser};Password={postgresPassword};Database={postgresDb}";

                // Récupération des variables d'environnement pour Redis
                var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST");
                var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT");
                var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
                var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");

                Console.WriteLine($"Connecting to Postgres: Host={postgresHost}");
                Console.WriteLine($"Connecting to Redis: {redisHost}");

                // Connexion à Postgre et Redis
                var pgsql = OpenDbConnection(postgresConnectionString);
                var redisConn = OpenRedisConnection(redisHost, redisPort, redisPassword);
                var redis = redisConn.GetDatabase();

                var keepAliveCommand = pgsql.CreateCommand();
                keepAliveCommand.CommandText = "SELECT 1";

                var definition = new { vote = "", voter_id = "" };
                while (true)
                {
                    Thread.Sleep(100);

                    // Se reconnecter à Redis si la connexion est perdue
                    if (redisConn == null || !redisConn.IsConnected) {
                        Console.WriteLine("⚠️ Redis connection lost. Reconnecting...");
                        redisConn = OpenRedisConnection(redisHost, redisPort, redisPassword);
                        redis = redisConn.GetDatabase();
                    }

                    string json = redis.ListLeftPopAsync("votes").Result;
                    if (json != null)
                    {
                        var vote = JsonConvert.DeserializeAnonymousType(json, definition);
                        Console.WriteLine($"Processing vote for '{vote.vote}' by '{vote.voter_id}'");

                        // Se reconnecter à PostgreSQL si la connexion est perdue
                        if (!pgsql.State.Equals(System.Data.ConnectionState.Open))
                        {
                            Console.WriteLine("⚠️ PostgreSQL connection lost. Reconnecting...");
                            pgsql = OpenDbConnection(postgresConnectionString);
                        }
                        else
                        {
                            UpdateVote(pgsql, vote.voter_id, vote.vote);
                        }
                    }
                    else
                    {
                        keepAliveCommand.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ Error: {ex.Message}");
                return 1;
            }
        }

        private static NpgsqlConnection OpenDbConnection(string connectionString)
        {
            NpgsqlConnection connection;
            int retryCount = 5; // Tentatives de reconnexion

            while (true)
            {
                try
                {
                    connection = new NpgsqlConnection(connectionString);
                    connection.Open();
                    Console.WriteLine("✅ Connected to PostgreSQL");
                    break;
                }
                catch (SocketException)
                {
                    Console.Error.WriteLine("⏳ Waiting for PostgreSQL...");
                    retryCount--;
                    if (retryCount == 0) throw;
                    Thread.Sleep(2000);
                }
                catch (DbException)
                {
                    Console.Error.WriteLine("⏳ Waiting for PostgreSQL...");
                    retryCount--;
                    if (retryCount == 0) throw;
                    Thread.Sleep(2000);
                }
            }

            Console.Error.WriteLine("📌 PostgreSQL connection established");

            // Création de la table "votes" si elle n'existe pas
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"CREATE TABLE IF NOT EXISTS votes (
                                            id VARCHAR(255) PRIMARY KEY,
                                            vote VARCHAR(255) NOT NULL
                                        )";
                command.ExecuteNonQuery();
            }

            return connection;
        }

        private static ConnectionMultiplexer OpenRedisConnection(string hostname, string port, string password)
        {
            string redisConnectionString = password != null
                ? $"{hostname}:{port},password={password},abortConnect=false"
                : $"{hostname}:{port},abortConnect=false";

            Console.WriteLine($"🔗 Connecting to Redis");

            while (true)
            {
                try
                {
                    var redisConn = ConnectionMultiplexer.Connect(redisConnectionString);
                    if (redisConn.IsConnected)
                    {
                        Console.WriteLine("✅ Connected to Redis");
                        return redisConn;
                    }
                }
                catch (RedisConnectionException ex)
                {
                    Console.Error.WriteLine($"⏳ Waiting for Redis... {ex.Message}");
                    Thread.Sleep(1000);
                }
            }
        }


        private static string GetIp(string hostname)
            => Dns.GetHostEntryAsync(hostname)
                .Result
                .AddressList
                .First(a => a.AddressFamily == AddressFamily.InterNetwork)
                .ToString();

        private static void UpdateVote(NpgsqlConnection connection, string voterId, string vote)
        {
            var command = connection.CreateCommand();
            try
            {
                command.CommandText = "INSERT INTO votes (id, vote) VALUES (@id, @vote) ON CONFLICT (id) DO UPDATE SET vote = EXCLUDED.vote;";
                command.Parameters.AddWithValue("@id", voterId);
                command.Parameters.AddWithValue("@vote", vote);
                command.ExecuteNonQuery();
            }
            catch (DbException ex)
            {
                Console.Error.WriteLine($"❌ Database error: {ex.Message}");
            }
            finally
            {
                command.Dispose();
            }
        }

    }
}