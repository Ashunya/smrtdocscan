using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class LocationRepository
{
    private readonly string _connectionString;
    private readonly bool _autoEnsureSchema;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaChecked;

    public LocationRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
        _autoEnsureSchema = DatabaseSchemaOptions.AutoEnsureSchema(configuration);
    }

    public async Task<IReadOnlyList<LocationDto>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var locations = new List<LocationDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT location_id, comp_id, location_name, location_code, address, phone, inactive, created_on
            FROM company_location
            WHERE comp_id = @companyId
            ORDER BY location_name;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            locations.Add(MapLocation(reader));
        }

        return locations;
    }

    public async Task<LocationDto?> GetAsync(int locationId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT location_id, comp_id, location_name, location_code, address, phone, inactive, created_on
            FROM company_location
            WHERE location_id = @locationId;
            """;
        command.Parameters.AddWithValue("@locationId", locationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapLocation(reader) : null;
    }

    public async Task<LocationDto> UpsertAsync(LocationUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.LocationName))
        {
            throw new InvalidOperationException("Location name is required.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        if (request.LocationId.HasValue && request.LocationId.Value > 0)
        {
            command.CommandText = """
                UPDATE company_location
                SET location_name = @locationName,
                    location_code = @locationCode,
                    address = @address,
                    phone = @phone,
                    inactive = @inactive
                WHERE location_id = @locationId;
                SELECT @locationId;
                """;
            command.Parameters.AddWithValue("@locationId", request.LocationId.Value);
        }
        else
        {
            command.CommandText = """
                INSERT INTO company_location (comp_id, location_name, location_code, address, phone, inactive, created_on)
                OUTPUT INSERTED.location_id
                VALUES (@companyId, @locationName, @locationCode, @address, @phone, @inactive, SYSUTCDATETIME());
                """;
        }

        command.Parameters.AddWithValue("@companyId", request.CompanyId);
        command.Parameters.AddWithValue("@locationName", request.LocationName.Trim());
        command.Parameters.AddWithValue("@locationCode", string.IsNullOrWhiteSpace(request.LocationCode) ? DBNull.Value : request.LocationCode.Trim());
        command.Parameters.AddWithValue("@address", string.IsNullOrWhiteSpace(request.Address) ? DBNull.Value : request.Address.Trim());
        command.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(request.Phone) ? DBNull.Value : request.Phone.Trim());
        command.Parameters.AddWithValue("@inactive", request.Inactive ? 1 : 0);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return await GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Location was saved but could not be loaded.");
    }

    public async Task<bool> DeleteAsync(int locationId, int companyId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM company_location WHERE location_id = @locationId AND comp_id = @companyId;";
        command.Parameters.AddWithValue("@locationId", locationId);
        command.Parameters.AddWithValue("@companyId", companyId);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaChecked)
        {
            return;
        }

        if (!_autoEnsureSchema)
        {
            _schemaChecked = true;
            return;
        }

        await SchemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaChecked)
            {
                return;
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                IF OBJECT_ID('dbo.company_location', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.company_location (
                        location_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_company_location PRIMARY KEY,
                        comp_id INT NOT NULL,
                        location_name NVARCHAR(150) NOT NULL,
                        location_code NVARCHAR(50) NULL,
                        address NVARCHAR(250) NULL,
                        phone NVARCHAR(50) NULL,
                        inactive BIT NOT NULL CONSTRAINT DF_company_location_inactive DEFAULT 0,
                        created_on DATETIME2 NOT NULL CONSTRAINT DF_company_location_created DEFAULT SYSUTCDATETIME()
                    );
                    CREATE INDEX IX_company_location_comp ON dbo.company_location(comp_id);
                END
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _schemaChecked = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private static LocationDto MapLocation(SqlDataReader reader)
    {
        return new LocationDto
        {
            LocationId = reader.GetInt32(reader.GetOrdinal("location_id")),
            CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
            LocationName = reader.GetString(reader.GetOrdinal("location_name")),
            LocationCode = ReadString(reader, "location_code"),
            Address = ReadString(reader, "address"),
            Phone = ReadString(reader, "phone"),
            Inactive = reader.GetBoolean(reader.GetOrdinal("inactive")),
            CreatedOn = reader.GetDateTime(reader.GetOrdinal("created_on"))
        };
    }

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
