using Discord;
using Discord.WebSocket;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord.Commands;


namespace discord_bot

{



    class Program
    {
        private static DiscordSocketClient _client;

        static async Task Main(string[] args)
        {


            // Discord botu için gerekli olan tokeni girin
            var discordToken = "";

            // MySQL veritabanı bağlantısı için gerekli bilgileri girin
            var connectionString = "SERVER=localhost;DATABASE=discord;UID=admin;PASSWORD=123456;";
            var selectQuery = "SELECT discord_id FROM discord";

            // Sunucu ve rol bilgilerini girin
            ulong guildId = 1111111;/// Server ID
            ulong roleId = 1111111111;/// Role ID

            _client = new DiscordSocketClient();
            _client.Log += Log;

            await _client.LoginAsync(TokenType.Bot, discordToken);
            await _client.StartAsync();

            var timer = new System.Timers.Timer();
            timer.Interval = 30000; // 30 saniye (milisaniye cinsinden)
            timer.Elapsed += async (sender, e) => await CheckUsersAndAssignRole(connectionString, selectQuery, guildId, roleId);

            timer.Start();

            await Task.Delay(-1);
           
        }

        private static Task Log(LogMessage message)
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        }


        private static async Task CheckUsersAndAssignRole(string connectionString, string selectQuery, ulong guildId, ulong roleId)
        {


            // 6 ay önceki tarih
            DateTime threeMonthsAgo = DateTime.Now.AddMonths(-6);

            // Veritabanı bağlantısını aç
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                // Sorgu oluştur
                string query = $"DELETE FROM discord WHERE date < '{threeMonthsAgo.ToString("yyyy-MM-dd HH:mm:ss")}'";

                // Sorguyu yürüt
                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    int rowsAffected = command.ExecuteNonQuery();

                    Console.WriteLine($"6 Aydan Eski {rowsAffected} rol silindi");
                }
            }

            var users = new List<string>();

            using (var connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var command = new MySqlCommand(selectQuery, connection);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        users.Add(reader.GetString(0));
                    }
                }
            }

            var guild = _client.GetGuild(guildId) as IGuild;

            if (guild == null)
            {
                Console.WriteLine($"Guild ID bulunamadı: {guildId}");
                return;
            }

            var role = guild.GetRole(roleId);

            if (role == null)
            {
                Console.WriteLine($"Role ID Bulunamadı: {roleId}");
                return;
            }

            var guildUsers = await guild.GetUsersAsync();

            // Veritabanındaki kayıtların olduğu kullanıcılara rol ver
            foreach (var userId in users)
            {
                var user = guildUsers.FirstOrDefault(u => u.Id == ulong.Parse(userId));
                if (user == null)
                {
                    continue;
                }

                try
                {
                    if (!user.RoleIds.Contains(role.Id))
                    {
                        await user.AddRoleAsync(role);
                        Console.WriteLine($"[{DateTime.Now}] {user.Username} isimli kullanıcıya {role.Name} rolü verildi");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Rol verilirken hata oluştu {user.Username}: {ex.Message}");
                }
            }

            // Veritabanındaki kayıtların olmadığı kullanıcılardan rolü kaldır
            foreach (var guildUser in guildUsers)
            {
                if (!users.Contains(guildUser.Id.ToString()) && guildUser.RoleIds.Contains(role.Id))
                {
                    try
                    {
                        await guildUser.RemoveRoleAsync(role);
                        Console.WriteLine($"[{DateTime.Now}] {guildUser.Username} isimli kullanıcının {role.Name} rolü silindi");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error removing role from user {guildUser.Username}: {ex.Message}");
                    }
                }
            }
        }









    }

}

