using Microsoft.Data.SqlClient;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class CategoryRepository
{
    private readonly string _connectionString;

    public CategoryRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
    }

    public async Task<IReadOnlyList<CategoryDto>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var categories = new List<CategoryDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT cat_id, comp_id, cat_name, access
            FROM category
            WHERE comp_id = @companyId
            ORDER BY cat_name;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            categories.Add(new CategoryDto
            {
                CategoryId = reader.GetInt32(reader.GetOrdinal("cat_id")),
                CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
                CategoryName = ReadString(reader, "cat_name"),
                Access = ReadString(reader, "access")
            });
        }

        return categories;
    }

    public async Task<CategoryDto> CreateAsync(CategoryUpsertRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CategoryName))
        {
            throw new InvalidOperationException("Category name is required.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO category (cat_name, comp_id, access)
            OUTPUT INSERTED.cat_id
            VALUES (@categoryName, @companyId, @access);
            """;
        command.Parameters.AddWithValue("@categoryName", request.CategoryName.Trim());
        command.Parameters.AddWithValue("@companyId", request.CompanyId);
        command.Parameters.AddWithValue("@access", string.IsNullOrWhiteSpace(request.Access) ? DBNull.Value : request.Access.Trim());
        var categoryId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

        return new CategoryDto
        {
            CategoryId = categoryId,
            CompanyId = request.CompanyId,
            CategoryName = request.CategoryName.Trim(),
            Access = request.Access
        };
    }

    public async Task<bool> DeleteAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM category WHERE cat_id = @categoryId;";
        command.Parameters.AddWithValue("@categoryId", categoryId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
