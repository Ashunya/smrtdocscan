using Microsoft.Data.SqlClient;
using SmartDocScan.Api.Models;

namespace SmartDocScan.Api.Data;

public sealed class PatientRepository
{
    private readonly string _connectionString;

    public PatientRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SmartDocScan")
            ?? throw new InvalidOperationException("Connection string 'SmartDocScan' is missing.");
    }

    public async Task<IReadOnlyList<PatientDto>> SearchAsync(int companyId, string? search, int take = 100, CancellationToken cancellationToken = default)
    {
        var patients = new List<PatientDto>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildSearchSql(search);
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@take", Math.Clamp(take, 1, 250));

        var terms = SplitSearch(search);
        for (var i = 0; i < terms.Count; i++)
        {
            command.Parameters.AddWithValue("@term" + i, "%" + terms[i] + "%");
            if (int.TryParse(terms[i], out var patientId))
            {
                command.Parameters.AddWithValue("@patientId" + i, patientId);
            }
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            patients.Add(MapPatient(reader));
        }

        return patients;
    }

    public async Task<PatientDto?> GetAsync(int patientId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = PatientSelectSql + " WHERE p.patient_id = @patientId";
        command.Parameters.AddWithValue("@patientId", patientId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapPatient(reader) : null;
    }

    public async Task<PatientDto> CreateAsync(PatientUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureExternalPatientIdIsUniqueAsync(request.CompanyId, request.ExternalPatientId, null, cancellationToken);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO patient (comp_id, pext_id, first_name, last_name, dob, gender, physician, box, ssn)
            OUTPUT INSERTED.patient_id
            VALUES (@companyId, @externalPatientId, @firstName, @lastName, @dateOfBirth, @gender, @physician, @box, @ssn);
            """;
        AddUpsertParameters(command, request);
        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return await GetAsync(id, cancellationToken) ?? throw new InvalidOperationException("Patient was created but could not be loaded.");
    }

    public async Task<PatientDto?> UpdateAsync(int patientId, PatientUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureExternalPatientIdIsUniqueAsync(request.CompanyId, request.ExternalPatientId, patientId, cancellationToken);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE patient
            SET comp_id = @companyId,
                pext_id = @externalPatientId,
                first_name = @firstName,
                last_name = @lastName,
                dob = @dateOfBirth,
                gender = @gender,
                physician = @physician,
                box = @box,
                ssn = @ssn
            WHERE patient_id = @patientId;
            """;
        command.Parameters.AddWithValue("@patientId", patientId);
        AddUpsertParameters(command, request);
        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        return rows == 0 ? null : await GetAsync(patientId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int patientId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM patient WHERE patient_id = @patientId;";
        command.Parameters.AddWithValue("@patientId", patientId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private async Task EnsureExternalPatientIdIsUniqueAsync(int companyId, string? externalPatientId, int? currentPatientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalPatientId))
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM patient
            WHERE comp_id = @companyId
              AND pext_id = @externalPatientId
              AND (@currentPatientId IS NULL OR patient_id <> @currentPatientId);
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@externalPatientId", externalPatientId.Trim());
        command.Parameters.AddWithValue("@currentPatientId", currentPatientId.HasValue ? currentPatientId.Value : DBNull.Value);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (count > 0)
        {
            throw new InvalidOperationException("Patient ID already exists.");
        }
    }

    private static string BuildSearchSql(string? search)
    {
        var terms = SplitSearch(search);
        var where = " WHERE p.comp_id = @companyId";
        for (var i = 0; i < terms.Count; i++)
        {
            var hasNumeric = int.TryParse(terms[i], out _);
            where += hasNumeric
                ? $" AND (p.patient_id = @patientId{i} OR p.pext_id LIKE @term{i} OR p.first_name LIKE @term{i} OR p.last_name LIKE @term{i})"
                : $" AND (p.pext_id LIKE @term{i} OR p.first_name LIKE @term{i} OR p.last_name LIKE @term{i})";
        }

        return PatientSearchSelectSql + where + " ORDER BY last_document_date DESC, patient_id DESC;";
    }

    private static List<string> SplitSearch(string? search)
    {
        return string.IsNullOrWhiteSpace(search)
            ? new List<string>()
            : search.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static void AddUpsertParameters(SqlCommand command, PatientUpsertRequest request)
    {
        command.Parameters.AddWithValue("@companyId", request.CompanyId);
        command.Parameters.AddWithValue("@externalPatientId", DbValue(request.ExternalPatientId));
        command.Parameters.AddWithValue("@firstName", DbValue(request.FirstName));
        command.Parameters.AddWithValue("@lastName", DbValue(request.LastName));
        command.Parameters.AddWithValue("@dateOfBirth", request.DateOfBirth.HasValue ? request.DateOfBirth.Value : DBNull.Value);
        command.Parameters.AddWithValue("@gender", DbValue(request.Gender));
        command.Parameters.AddWithValue("@physician", DbValue(request.Physician));
        command.Parameters.AddWithValue("@box", DbValue(request.Box));
        command.Parameters.AddWithValue("@ssn", DbValue(request.Ssn));
    }

    private static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static PatientDto MapPatient(SqlDataReader reader)
    {
        return new PatientDto
        {
            PatientId = reader.GetInt32(reader.GetOrdinal("patient_id")),
            CompanyId = reader.GetInt32(reader.GetOrdinal("comp_id")),
            ExternalPatientId = ReadString(reader, "pext_id"),
            FirstName = ReadString(reader, "first_name"),
            LastName = ReadString(reader, "last_name"),
            DateOfBirth = ReadDateTime(reader, "dob"),
            Gender = ReadString(reader, "gender"),
            Physician = ReadString(reader, "physician"),
            Box = ReadString(reader, "box"),
            Ssn = ReadString(reader, "ssn"),
            LastDocumentDate = ReadDateTime(reader, "last_document_date")
        };
    }

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime? ReadDateTime(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private const string PatientSelectSql = """
        SELECT p.patient_id, p.comp_id, p.pext_id, p.first_name, p.last_name, p.dob, p.gender, p.physician, p.box, p.ssn,
               latest.last_document_date
        FROM patient p
        OUTER APPLY (
            SELECT MAX(d.date) AS last_document_date
            FROM documents d
            WHERE d.patient_id = p.patient_id
              AND d.comp_id = p.comp_id
              AND ISNULL(d.deleted, 0) = 0
        ) latest
        """;

    private const string PatientSearchSelectSql = """
        SELECT TOP (@take) p.patient_id, p.comp_id, p.pext_id, p.first_name, p.last_name, p.dob, p.gender, p.physician, p.box, p.ssn,
               latest.last_document_date
        FROM patient p
        LEFT JOIN (
            SELECT d.patient_id, d.comp_id, MAX(d.date) AS last_document_date
            FROM documents d
            WHERE d.comp_id = @companyId
              AND ISNULL(d.deleted, 0) = 0
            GROUP BY d.patient_id, d.comp_id
        ) latest
          ON latest.patient_id = p.patient_id
         AND latest.comp_id = p.comp_id
        """;
}
