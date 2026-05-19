using Microsoft.Data.SqlClient;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class CompanyRepository
{
    private readonly string _connectionString;

    public CompanyRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
    }

    public async Task<IReadOnlyList<CompanyDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var companies = new List<CompanyDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.comp_id, c.comp_name, c.owner, c.address, c.location, c.phone, c.barcode, c.inactive,
                   t.tenant_id AS microsoft_tenant_id,
                   t.tenant_name AS microsoft_tenant_name,
                   t.enabled AS microsoft_tenant_enabled
            FROM company c
            OUTER APPLY (
                SELECT TOP 1 tenant_id, tenant_name, enabled
                FROM company_identity_tenant
                WHERE comp_id = c.comp_id AND provider = 'microsoft'
                ORDER BY enabled DESC, tenant_name
            ) t
            ORDER BY c.comp_name;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            companies.Add(MapCompany(reader));
        }

        return companies;
    }

    public async Task<CompanyDto> UpsertAsync(CompanyUpsertRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName))
        {
            throw new InvalidOperationException("Company name is required.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        if (request.CompanyId.HasValue && request.CompanyId.Value > 0)
        {
            command.CommandText = """
                UPDATE company
                SET comp_name = @companyName,
                    owner = @owner,
                    address = @address,
                    location = @location,
                    phone = @phone,
                    barcode = @barcode,
                    inactive = @inactive
                WHERE comp_id = @companyId;
                SELECT @companyId;
                """;
            command.Parameters.AddWithValue("@companyId", request.CompanyId.Value);
        }
        else
        {
            command.CommandText = """
                INSERT INTO company (comp_name, owner, address, location, phone, barcode, inactive)
                OUTPUT INSERTED.comp_id
                VALUES (@companyName, @owner, @address, @location, @phone, @barcode, @inactive);
                """;
        }

        command.Parameters.AddWithValue("@companyName", request.CompanyName.Trim());
        command.Parameters.AddWithValue("@owner", DbValue(request.Owner));
        command.Parameters.AddWithValue("@address", DbValue(request.Address));
        command.Parameters.AddWithValue("@location", DbValue(request.Location));
        command.Parameters.AddWithValue("@phone", DbValue(request.Phone));
        command.Parameters.AddWithValue("@barcode", request.Barcode ? (byte)1 : (byte)0);
        command.Parameters.AddWithValue("@inactive", request.Inactive ? (byte)1 : (byte)0);

        var companyId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        await SaveMicrosoftTenantAsync(connection, companyId, request, cancellationToken);
        return await GetAsync(companyId, cancellationToken) ?? throw new InvalidOperationException("Company was saved but could not be loaded.");
    }

    public async Task<bool> DeleteAsync(int companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM company WHERE comp_id = @companyId;";
        command.Parameters.AddWithValue("@companyId", companyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private async Task<CompanyDto?> GetAsync(int companyId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.comp_id, c.comp_name, c.owner, c.address, c.location, c.phone, c.barcode, c.inactive,
                   t.tenant_id AS microsoft_tenant_id,
                   t.tenant_name AS microsoft_tenant_name,
                   t.enabled AS microsoft_tenant_enabled
            FROM company c
            OUTER APPLY (
                SELECT TOP 1 tenant_id, tenant_name, enabled
                FROM company_identity_tenant
                WHERE comp_id = c.comp_id AND provider = 'microsoft'
                ORDER BY enabled DESC, tenant_name
            ) t
            WHERE c.comp_id = @companyId;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapCompany(reader);
    }

    private static async Task SaveMicrosoftTenantAsync(SqlConnection connection, int companyId, CompanyUpsertRequest request, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(request.MicrosoftTenantId))
        {
            command.CommandText = """
                DELETE FROM company_identity_tenant
                WHERE comp_id = @companyId AND provider = 'microsoft';
                """;
            command.Parameters.AddWithValue("@companyId", companyId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        command.CommandText = """
            MERGE company_identity_tenant AS target
            USING (SELECT @companyId AS comp_id, 'microsoft' AS provider, @tenantId AS tenant_id) AS source
              ON target.comp_id = source.comp_id
             AND target.provider = source.provider
             AND target.tenant_id = source.tenant_id
            WHEN MATCHED THEN
                UPDATE SET tenant_name = @tenantName,
                           enabled = @enabled
            WHEN NOT MATCHED THEN
                INSERT (comp_id, provider, tenant_id, tenant_name, enabled)
                VALUES (@companyId, 'microsoft', @tenantId, @tenantName, @enabled);

            DELETE FROM company_identity_tenant
            WHERE comp_id = @companyId
              AND provider = 'microsoft'
              AND tenant_id <> @tenantId;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@tenantId", request.MicrosoftTenantId.Trim());
        command.Parameters.AddWithValue("@tenantName", DbValue(request.MicrosoftTenantName));
        command.Parameters.AddWithValue("@enabled", request.MicrosoftTenantEnabled);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static CompanyDto MapCompany(SqlDataReader reader)
    {
        return new CompanyDto
        {
            CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
            CompanyName = ReadString(reader, "comp_name"),
            Owner = ReadString(reader, "owner"),
            Address = ReadString(reader, "address"),
            Location = ReadString(reader, "location"),
            Phone = ReadString(reader, "phone"),
            Barcode = reader.GetByte(reader.GetOrdinal("barcode")) != 0,
            Inactive = reader.GetByte(reader.GetOrdinal("inactive")) != 0,
            MicrosoftTenantId = ReadString(reader, "microsoft_tenant_id"),
            MicrosoftTenantName = ReadString(reader, "microsoft_tenant_name"),
            MicrosoftTenantEnabled = ReadBool(reader, "microsoft_tenant_enabled")
        };
    }

    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static bool ReadBool(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
    }
}
