using System;
using System.Data.SqlClient;

var connStr = Environment.GetEnvironmentVariable("SMARTDOCSCAN_CONNECTION_STRING") ?? "Server=localhost;Database=smartdocscan;User Id=sa;Password=Your_password123;TrustServerCertificate=True";
using var conn = new SqlConnection(connStr);
conn.Open();
using var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'usersinfo' AND COLUMN_NAME = 'username'";
using var reader = cmd.ExecuteReader();
if (reader.Read())
{
    Console.WriteLine($"{reader["DATA_TYPE"]}({reader["CHARACTER_MAXIMUM_LENGTH"]})");
}
else
{
    Console.WriteLine("Not found");
}
