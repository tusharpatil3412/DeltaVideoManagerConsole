using System;
using System.Data.SqlClient;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GeoTimeZone;

namespace AddTimeStampConsole
{
    public class CaseTrackerDbConnection
    {
        private readonly string _connectionString;

        // Constructor to initialize the connection string
        public CaseTrackerDbConnection()
        {
            _connectionString = Constants.DbConnection; 
        }

        // Method to get a record by code
        public async Task<DateTime?> GetRecordByCodeAsync(int code, DateTime utcDate)
        {
            string query = @"
             SELECT 
             A.Latitude, 
             A.Longitude 
             FROM [Case] C
             LEFT JOIN Claimant CM ON C.ClaimantId = CM.ClaimantId
             LEFT JOIN [Address] A ON CM.AddressId = A.AddressId
             WHERE C.CaseId = @Code";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    await connection.OpenAsync();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        // Add parameter to prevent SQL injection
                        command.Parameters.AddWithValue("@Code", code);

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (reader.HasRows)
                            {
                                while (await reader.ReadAsync())
                                {
                                    // Extract latitude and longitude
                                    double latitude = reader["Latitude"] != DBNull.Value ? Convert.ToDouble(reader["Latitude"]) : 0;
                                    double longitude = reader["Longitude"] != DBNull.Value ? Convert.ToDouble(reader["Longitude"]) : 0;


                                    Console.WriteLine($"Latitude: {latitude}, Longitude: {longitude}");

                                   if(latitude==0 || longitude == 0)
                                    {
                                        return null;
                                    }
                                    string timeZoneId = TimeZoneLookup.GetTimeZone(latitude, longitude).Result;

                                    // Convert UTC to local time zone
                                    DateTime? localTime = ConvertUtcToTimeZone(utcDate, timeZoneId);

                                    if (localTime != null)
                                    {
                                        Console.WriteLine($"Local Time for Code {code}: {localTime}");
                                        return localTime;
                                    }
                                    else
                                    {
                                        return  null ;
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("No record found for the given code.");
                                return null;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }

            // Return null if no valid record or conversion is found
            return null;
        }

        // Helper method to convert UTC to the specified time zone
        private static DateTime? ConvertUtcToTimeZone(DateTime utcDate, string timeZoneId)
        {
            try
            {
                // Check if the application is using IANA or Windows time zones
                bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

                // Convert IANA time zone ID to Windows if necessary
                string systemTimeZoneId = isWindows
                    ? TimeZoneConverter.TZConvert.IanaToWindows(timeZoneId)
                    : timeZoneId;

                // Get the TimeZoneInfo object for the system time zone
                TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(systemTimeZoneId);

                // Convert UTC to the target time zone
                DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(utcDate, timeZoneInfo);

                // Determine if the result is in Daylight Saving Time
                bool isDaylightSavingTime = timeZoneInfo.IsDaylightSavingTime(localTime);

                // Log or use the DST information if needed
                Console.WriteLine($"Is Daylight Saving Time: {isDaylightSavingTime}");

                return localTime;
            }
            catch (TimeZoneNotFoundException)
            {
                Console.WriteLine($"Time zone '{timeZoneId}' not found.");
            }
            catch (InvalidTimeZoneException)
            {
                Console.WriteLine($"Time zone '{timeZoneId}' is invalid.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            }

            // Return null as a fallback in case of error
            return null;
        }

    }
}
