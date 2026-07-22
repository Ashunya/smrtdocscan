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
            SELECT comp_id, comp_name, owner, address, location, phone, barcode, inactive
            FROM company
            ORDER BY comp_name;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            companies.Add(MapCompany(reader));
        }
        await reader.CloseAsync();

        if (companies.Count > 0)
        {
            await using var tenantCmd = connection.CreateCommand();
            tenantCmd.CommandText = """
                SELECT comp_id, tenant_id, tenant_name, enabled
                FROM company_identity_tenant
                WHERE provider = 'microsoft'
                ORDER BY enabled DESC, tenant_name;
                """;

            var tenantsByCompany = new Dictionary<int, List<CompanyTenantDto>>();
            await using var tenantReader = await tenantCmd.ExecuteReaderAsync(cancellationToken);
            while (await tenantReader.ReadAsync(cancellationToken))
            {
                var compId = tenantReader.GetInt32(tenantReader.GetOrdinal("comp_id"));
                var tenant = new CompanyTenantDto
                {
                    TenantId = tenantReader.GetString(tenantReader.GetOrdinal("tenant_id")),
                    TenantName = tenantReader.IsDBNull(tenantReader.GetOrdinal("tenant_name")) ? null : tenantReader.GetString(tenantReader.GetOrdinal("tenant_name")),
                    Enabled = tenantReader.GetBoolean(tenantReader.GetOrdinal("enabled"))
                };

                if (!tenantsByCompany.TryGetValue(compId, out var list))
                {
                    list = new List<CompanyTenantDto>();
                    tenantsByCompany[compId] = list;
                }
                list.Add(tenant);
            }

            foreach (var company in companies)
            {
                if (tenantsByCompany.TryGetValue(company.CompanyId, out var tenants))
                {
                    company.Tenants = tenants;
                    var primary = tenants.FirstOrDefault();
                    if (primary != null)
                    {
                        company.MicrosoftTenantId = primary.TenantId;
                        company.MicrosoftTenantName = primary.TenantName;
                        company.MicrosoftTenantEnabled = primary.Enabled;
                    }
                }
            }
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
        await SaveCompanyTenantsAsync(connection, companyId, request, cancellationToken);
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
            SELECT comp_id, comp_name, owner, address, location, phone, barcode, inactive
            FROM company
            WHERE comp_id = @companyId;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);

        CompanyDto? company = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                company = MapCompany(reader);
            }
        }

        if (company != null)
        {
            await using var tenantCmd = connection.CreateCommand();
            tenantCmd.CommandText = """
                SELECT tenant_id, tenant_name, enabled
                FROM company_identity_tenant
                WHERE comp_id = @companyId AND provider = 'microsoft'
                ORDER BY enabled DESC, tenant_name;
                """;
            tenantCmd.Parameters.AddWithValue("@companyId", companyId);

            await using var tenantReader = await tenantCmd.ExecuteReaderAsync(cancellationToken);
            while (await tenantReader.ReadAsync(cancellationToken))
            {
                var tenant = new CompanyTenantDto
                {
                    TenantId = tenantReader.GetString(tenantReader.GetOrdinal("tenant_id")),
                    TenantName = tenantReader.IsDBNull(tenantReader.GetOrdinal("tenant_name")) ? null : tenantReader.GetString(tenantReader.GetOrdinal("tenant_name")),
                    Enabled = tenantReader.GetBoolean(tenantReader.GetOrdinal("enabled"))
                };
                company.Tenants.Add(tenant);
            }

            var primary = company.Tenants.FirstOrDefault();
            if (primary != null)
            {
                company.MicrosoftTenantId = primary.TenantId;
                company.MicrosoftTenantName = primary.TenantName;
                company.MicrosoftTenantEnabled = primary.Enabled;
            }
        }

        return company;
    }

    private static async Task SaveCompanyTenantsAsync(SqlConnection connection, int companyId, CompanyUpsertRequest request, CancellationToken cancellationToken)
    {
        var newTenants = request.Tenants;
        if ((newTenants == null || newTenants.Count == 0) && !string.IsNullOrWhiteSpace(request.MicrosoftTenantId))
        {
            newTenants = new List<CompanyTenantDto>
            {
                new()
                {
                    TenantId = request.MicrosoftTenantId.Trim(),
                    TenantName = request.MicrosoftTenantName?.Trim(),
                    Enabled = request.MicrosoftTenantEnabled
                }
            };
        }

        if (newTenants == null || newTenants.Count == 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM company_identity_tenant
                WHERE comp_id = @companyId AND provider = 'microsoft';
                """;
            command.Parameters.AddWithValue("@companyId", companyId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        foreach (var tenant in newTenants)
        {
            if (string.IsNullOrWhiteSpace(tenant.TenantId)) continue;

            await using var command = connection.CreateCommand();
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
                """;
            command.Parameters.AddWithValue("@companyId", companyId);
            command.Parameters.AddWithValue("@tenantId", tenant.TenantId.Trim());
            command.Parameters.AddWithValue("@tenantName", DbValue(tenant.TenantName));
            command.Parameters.AddWithValue("@enabled", tenant.Enabled);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var validTenantIds = newTenants
            .Where(t => !string.IsNullOrWhiteSpace(t.TenantId))
            .Select(t => t.TenantId.Trim())
            .ToList();

        if (validTenantIds.Count > 0)
        {
            await using var deleteCmd = connection.CreateCommand();
            var paramNames = new List<string>();
            for (int i = 0; i < validTenantIds.Count; i++)
            {
                var paramName = $"@t{i}";
                paramNames.Add(paramName);
                deleteCmd.Parameters.AddWithValue(paramName, validTenantIds[i]);
            }
            deleteCmd.CommandText = $"""
                DELETE FROM company_identity_tenant
                WHERE comp_id = @companyId
                  AND provider = 'microsoft'
                  AND tenant_id NOT IN ({string.Join(", ", paramNames)});
                """;
            deleteCmd.Parameters.AddWithValue("@companyId", companyId);
            await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await using var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = """
                DELETE FROM company_identity_tenant
                WHERE comp_id = @companyId
                  AND provider = 'microsoft';
                """;
            deleteCmd.Parameters.AddWithValue("@companyId", companyId);
            await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
        }
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
            Inactive = reader.GetByte(reader.GetOrdinal("inactive")) != 0
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
