using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;
using SeniorManagement.Models;

namespace SeniorManagement.Helpers
{
    public class NotificationHelper
    {
        private readonly DatabaseHelper _dbHelper;

        public NotificationHelper(DatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        // Create announcement when event is created
        public async Task CreateEventAnnouncementAsync(Event eventItem, string createdBy)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    string query = @"INSERT INTO announcements 
                                    (Title, Message, Type, RelatedEventId, CreatedBy, CreatedAt) 
                                    VALUES 
                                    (@Title, @Message, @Type, @RelatedEventId, @CreatedBy, @CreatedAt)";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Title", $"New Event: {eventItem.EventTitle}");
                        cmd.Parameters.AddWithValue("@Message", $"A new {eventItem.EventType.ToLower()} '{eventItem.EventTitle}' has been scheduled for {eventItem.EventDate:MMM dd, yyyy} at {eventItem.EventLocation}. Organized by {eventItem.OrganizedBy}.");
                        cmd.Parameters.AddWithValue("@Type", "Event");
                        cmd.Parameters.AddWithValue("@RelatedEventId", eventItem.Id);
                        cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating event announcement: {ex.Message}");
            }
        }

        // Create announcement when event is updated
        public async Task CreateEventUpdateAnnouncementAsync(Event eventItem, string createdBy)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    string query = @"INSERT INTO announcements 
                                    (Title, Message, Type, RelatedEventId, CreatedBy, CreatedAt) 
                                    VALUES 
                                    (@Title, @Message, @Type, @RelatedEventId, @CreatedBy, @CreatedAt)";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Title", $"Event Updated: {eventItem.EventTitle}");
                        cmd.Parameters.AddWithValue("@Message", $"The event '{eventItem.EventTitle}' has been updated. It's scheduled for {eventItem.EventDate:MMM dd, yyyy} at {eventItem.EventLocation}. Organized by {eventItem.OrganizedBy}.");
                        cmd.Parameters.AddWithValue("@Type", "Event");
                        cmd.Parameters.AddWithValue("@RelatedEventId", eventItem.Id);
                        cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating event update announcement: {ex.Message}");
            }
        }

        // Create announcement when event is deleted
        public async Task CreateEventDeleteAnnouncementAsync(Event eventItem, string createdBy)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    string query = @"INSERT INTO announcements 
                                    (Title, Message, Type, CreatedBy, CreatedAt) 
                                    VALUES 
                                    (@Title, @Message, @Type, @CreatedBy, @CreatedAt)";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Title", $"Event Cancelled: {eventItem.EventTitle}");
                        cmd.Parameters.AddWithValue("@Message", $"The event '{eventItem.EventTitle}' scheduled for {eventItem.EventDate:MMM dd, yyyy} has been cancelled.");
                        cmd.Parameters.AddWithValue("@Type", "Alert");
                        cmd.Parameters.AddWithValue("@CreatedBy", createdBy);
                        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating event delete announcement: {ex.Message}");
            }
        }

        // Get unread announcements count
        public async Task<int> GetUnreadCountAsync(string userName)
        {
            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    string query = @"SELECT COUNT(*) FROM announcements";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        var result = await cmd.ExecuteScalarAsync();
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting unread count: {ex.Message}");
                return 0;
            }
        }

        // Get recent announcements
        public async Task<List<Announcement>> GetRecentAnnouncementsAsync(string userName, int limit = 10)
        {
            var announcements = new List<Announcement>();

            try
            {
                using (var connection = _dbHelper.GetConnection())
                {
                    await connection.OpenAsync();

                    string query = @"SELECT a.*, e.EventTitle, e.EventDate, e.EventLocation, e.OrganizedBy
                                   FROM announcements a
                                   LEFT JOIN events e ON a.RelatedEventId = e.Id
                                   ORDER BY a.CreatedAt DESC 
                                   LIMIT @Limit";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Limit", limit);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var announcement = new Announcement
                                {
                                    Id = reader.GetInt32("Id"),
                                    Title = reader.GetString("Title"),
                                    Message = reader.GetString("Message"),
                                    Type = reader.GetString("Type"),
                                    RelatedEventId = reader.IsDBNull(reader.GetOrdinal("RelatedEventId")) ? null : (int?)reader.GetInt32("RelatedEventId"),
                                    CreatedBy = reader.GetString("CreatedBy"),
                                    CreatedAt = reader.GetDateTime("CreatedAt")
                                };

                                // Add related event data if available
                                if (!reader.IsDBNull(reader.GetOrdinal("EventTitle")))
                                {
                                    announcement.RelatedEvent = new Event
                                    {
                                        EventTitle = reader.GetString("EventTitle"),
                                        EventDate = reader.GetDateTime("EventDate"),
                                        EventLocation = reader.GetString("EventLocation"),
                                        OrganizedBy = reader.GetString("OrganizedBy")
                                    };
                                }

                                announcements.Add(announcement);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting announcements: {ex.Message}");
            }

            return announcements;
        }
    }
}