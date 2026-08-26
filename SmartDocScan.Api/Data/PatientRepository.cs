using Npgsql;
using NpgsqlTypes;
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

    public async Task<IReadOnlyList<PatientDto>> SearchAsync(int companyId, string? search, DateTime? dateOfBirth, int take = 100, CancellationToken cancellationToken = default)
    {
        var patients = new List<PatientDto>();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = BuildSearchSql(search, dateOfBirth);
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@take", Math.Clamp(take, 1, 250));
        if (dateOfBirth.HasValue)
        {
            command.Parameters.AddWithValue("@dateOfBirth", NpgsqlDbType.Date, DateOnly.FromDateTime(dateOfBirth.Value.Date));
        }

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
        await using var connection = new NpgsqlConnection(_connectionString);
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

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await PostgresSequenceRepair.EnsureSerialDefaultAsync(connection, "patient", "patient_id", "patient_patient_id_seq", cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO patient (comp_id, pext_id, first_name, last_name, dob, gender, physician, box, ssn)
            VALUES (@companyId, @externalPatientId, @firstName, @lastName, @dateOfBirth, @gender, @physician, @box, @ssn)
            RETURNING patient_id;
            """;
        AddUpsertParameters(command, request);
        var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return await GetAsync(id, cancellationToken) ?? throw new InvalidOperationException("Patient was created but could not be loaded.");
    }

    public async Task<PatientDto?> UpdateAsync(int patientId, PatientUpsertRequest request, int existingCompanyId, CancellationToken cancellationToken = default)
    {
        await EnsureExternalPatientIdIsUniqueAsync(request.CompanyId, request.ExternalPatientId, patientId, cancellationToken);

        await using var connection = new NpgsqlConnection(_connectionString);
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
            WHERE patient_id = @patientId
              AND comp_id = @existingCompanyId;
            """;
        command.Parameters.AddWithValue("@patientId", patientId);
        command.Parameters.AddWithValue("@existingCompanyId", existingCompanyId);
        AddUpsertParameters(command, request);
        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        return rows == 0 ? null : await GetAsync(patientId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int patientId, int companyId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM patient WHERE patient_id = @patientId AND comp_id = @companyId;";
        command.Parameters.AddWithValue("@patientId", patientId);
        command.Parameters.AddWithValue("@companyId", companyId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private async Task EnsureExternalPatientIdIsUniqueAsync(int companyId, string? externalPatientId, int? currentPatientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalPatientId))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT patient_id, pext_id, first_name, last_name, dob
            FROM patient
            WHERE comp_id = @companyId
              AND btrim(pext_id) = @externalPatientId
              AND (@currentPatientId IS NULL OR patient_id <> @currentPatientId)
            ORDER BY patient_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@externalPatientId", externalPatientId.Trim());
        command.Parameters.AddWithValue("@currentPatientId", NpgsqlDbType.Integer, currentPatientId.HasValue ? currentPatientId.Value : DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var matchingPatientId = reader.GetInt32(reader.GetOrdinal("patient_id"));
            var matchingExternalId = ReadString(reader, "pext_id")?.Trim() ?? externalPatientId.Trim();
            var matchingFirstName = ReadString(reader, "first_name")?.Trim();
            var matchingLastName = ReadString(reader, "last_name")?.Trim();
            var matchingDob = ReadDateTime(reader, "dob");
            var matchingName = string.Join(", ", new[] { matchingLastName, matchingFirstName }.Where(value => !string.IsNullOrWhiteSpace(value)));
            var dobText = matchingDob.HasValue ? $", DOB {matchingDob.Value:MM/dd/yyyy}" : string.Empty;
            throw new InvalidOperationException(
                $"Patient ID {matchingExternalId} already belongs to {matchingName} (record {matchingPatientId}{dobText}) in company {companyId}. Open that patient or verify the active company before creating another record.");
        }
    }

    private static string BuildSearchSql(string? search, DateTime? dateOfBirth)
    {
        var terms = SplitSearch(search);
        var where = " WHERE p.comp_id = @companyId";
        if (dateOfBirth.HasValue)
        {
            where += " AND p.dob::date = @dateOfBirth";
        }

        for (var i = 0; i < terms.Count; i++)
        {
            var hasNumeric = int.TryParse(terms[i], out _);
            where += hasNumeric
                ? $" AND (p.patient_id = @patientId{i} OR p.pext_id ILIKE @term{i} OR p.first_name ILIKE @term{i} OR p.last_name ILIKE @term{i})"
                : $" AND (p.pext_id ILIKE @term{i} OR p.first_name ILIKE @term{i} OR p.last_name ILIKE @term{i})";
        }

        var innerWhere = where.Replace("p.", string.Empty, StringComparison.Ordinal);
        return PatientSearchSelectSql
            .Replace("{where}", innerWhere, StringComparison.Ordinal)
            .Replace("{order}", string.IsNullOrWhiteSpace(search) ? "patient_id DESC" : "last_name, first_name, patient_id DESC", StringComparison.Ordinal);
    }

    private static List<string> SplitSearch(string? search)
    {
        return string.IsNullOrWhiteSpace(search)
            ? new List<string>()
            : search.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static void AddUpsertParameters(NpgsqlCommand command, PatientUpsertRequest request)
    {
        command.Parameters.AddWithValue("@companyId", request.CompanyId);
        command.Parameters.AddWithValue("@externalPatientId", DbValue(request.ExternalPatientId));
        command.Parameters.AddWithValue("@firstName", DbValue(request.FirstName));
        command.Parameters.AddWithValue("@lastName", DbValue(request.LastName));
        command.Parameters.AddWithValue("@dateOfBirth", NpgsqlDbType.Timestamp, request.DateOfBirth.HasValue ? request.DateOfBirth.Value : DBNull.Value);
        command.Parameters.AddWithValue("@gender", DbValue(request.Gender));
        command.Parameters.AddWithValue("@physician", DbValue(request.Physician));
        command.Parameters.AddWithValue("@box", DbValue(request.Box));
        command.Parameters.AddWithValue("@ssn", DbValue(request.Ssn));
    }

    private static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static PatientDto MapPatient(NpgsqlDataReader reader)
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

    private static string? ReadString(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime? ReadDateTime(NpgsqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private const string PatientSelectSql = """
        SELECT p.patient_id, p.comp_id, p.pext_id, p.first_name, p.last_name, p.dob, p.gender, p.physician, p.box, p.ssn,
               latest.last_document_date
        FROM patient p
        LEFT JOIN LATERAL (
            SELECT MAX(d.date) AS last_document_date
            FROM documents d
            WHERE d.patient_id = p.patient_id
              AND d.comp_id = p.comp_id
              AND COALESCE(d.deleted, false) = false
        ) latest ON true
        """;

    private const string PatientSearchSelectSql = """
        SELECT p.patient_id, p.comp_id, p.pext_id, p.first_name, p.last_name, p.dob, p.gender, p.physician, p.box, p.ssn,
               latest.last_document_date
        FROM (
            SELECT patient_id, comp_id, pext_id, first_name, last_name, dob, gender, physician, box, ssn
            FROM patient
            {where}
            ORDER BY {order}
            LIMIT @take
        ) p
        LEFT JOIN LATERAL (
            SELECT MAX(d.date) AS last_document_date
            FROM documents d
            WHERE d.comp_id = p.comp_id
              AND COALESCE(d.deleted, false) = false
              AND (
                  d.patient_id = p.patient_id
                  OR (
                      p.pext_id IS NOT NULL
                      AND btrim(p.pext_id) = d.patient_id::text
                  )
              )
        ) latest ON true
        ORDER BY latest.last_document_date DESC NULLS LAST, p.patient_id DESC;
        """;
}
