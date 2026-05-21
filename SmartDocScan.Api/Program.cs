using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ImageMagick;
using SmartDocScan.Api.Data;
using SmartDocScan.Api.Models;
using SmartDocScan.Api.Services;

var builder = WebApplication.CreateBuilder(args);
const string MicrosoftSsoNotConfiguredMessage = "Microsoft SSO is not configured. Save Microsoft Client ID and Client Secret in Settings.";
SettingsRepository.LoadIntoConfiguration(builder.Configuration);

var maxDocumentUploadBytes = builder.Configuration.GetValue<long?>("Uploads:MaxDocumentBytes") ?? 200L * 1024L * 1024L;
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxDocumentUploadBytes;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxDocumentUploadBytes;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = 64 * 1024;
});

builder.Services.AddSingleton<PatientRepository>();
builder.Services.AddSingleton<BoxRepository>();
builder.Services.AddSingleton<CategoryRepository>();
builder.Services.AddSingleton<DocumentRepository>();
builder.Services.AddSingleton<CompanyRepository>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<AuthRepository>();
builder.Services.AddSingleton<SettingsRepository>();
builder.Services.AddSingleton<IEmailSender, EmailSender>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
var authBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "smartdocscan.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        var cookieDomain = builder.Configuration["Authentication:CookieDomain"];
        if (!string.IsNullOrWhiteSpace(cookieDomain))
        {
            options.Cookie.Domain = cookieDomain.Trim();
        }
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(10);
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });

authBuilder.AddOpenIdConnect(options =>
{
    options.Authority = "https://login.microsoftonline.com/organizations/v2.0";
    options.ClientId = string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Microsoft:ClientId"])
        ? "smartdocscan-db-configured"
        : builder.Configuration["Authentication:Microsoft:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"] ?? "smartdocscan-db-configured";
    options.CallbackPath = builder.Configuration["Authentication:Microsoft:CallbackPath"] ?? "/api/auth/microsoft/callback";
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.SaveTokens = false;
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.TokenValidationParameters.ValidateIssuer = false;
    options.Events.OnRedirectToIdentityProvider = async context =>
    {
        var settings = await LoadMicrosoftSsoRuntimeSettingsAsync(context.HttpContext);
        if (!ApplyMicrosoftSsoSettings(context.Options, settings))
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = MicrosoftSsoNotConfiguredMessage });
            return;
        }

        context.ProtocolMessage.ClientId = settings.ClientId;
    };
    options.Events.OnAuthorizationCodeReceived = async context =>
    {
        var settings = await LoadMicrosoftSsoRuntimeSettingsAsync(context.HttpContext);
        if (!ApplyMicrosoftSsoSettings(context.Options, settings))
        {
            context.Fail(MicrosoftSsoNotConfiguredMessage);
            return;
        }

        if (context.TokenEndpointRequest is not null)
        {
            context.TokenEndpointRequest.ClientId = settings.ClientId;
            context.TokenEndpointRequest.ClientSecret = settings.ClientSecret;
        }
    };
    options.Events.OnTokenValidated = async context =>
    {
        var authRepository = context.HttpContext.RequestServices.GetRequiredService<AuthRepository>();
        var cancellationToken = context.HttpContext.RequestAborted;
        var principal = context.Principal;
        var tenantId = FindClaimValue(principal, "tid", "http://schemas.microsoft.com/identity/claims/tenantid");
        var objectId = FindClaimValue(principal, "oid", "http://schemas.microsoft.com/identity/claims/objectidentifier", ClaimTypes.NameIdentifier);
        var email = FindClaimValue(principal, "preferred_username", ClaimTypes.Email, "email", "upn");

        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(objectId))
        {
            context.Fail("Microsoft sign-in did not return tenant and object identifiers.");
            return;
        }

        var user = await authRepository.FindExternalUserAsync("microsoft", tenantId, objectId, email, cancellationToken);
        if (user is null)
        {
            context.Fail("This Microsoft account is not linked to an active SmartDocScan user.");
            return;
        }

        context.Principal = CreatePrincipal(user, "microsoft");
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
app.Use(async (context, next) =>
{
    AddSecurityHeaders(context.Response);

    if (IsUnsafeMethod(context.Request.Method)
        && !IsAllowedBrowserOrigin(context.Request, allowedOrigins))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { message = "Request origin is not allowed." });
        return;
    }

    await next();
});

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/patients", async (int companyId, string? search, int? take, ClaimsPrincipal principal, PatientRepository repository, CancellationToken cancellationToken) =>
{
    if (!CanAccessCompany(principal, companyId))
    {
        return Results.Forbid();
    }

    var patients = await repository.SearchAsync(companyId, search, take ?? 100, cancellationToken);
    return Results.Ok(patients);
}).RequireAuthorization();

app.MapGet("/api/patients/{patientId:int}", async (int patientId, ClaimsPrincipal principal, PatientRepository repository, CancellationToken cancellationToken) =>
{
    var patient = await repository.GetAsync(patientId, cancellationToken);
    if (patient is null)
    {
        return Results.NotFound();
    }

    return CanAccessCompany(principal, patient.CompanyId) ? Results.Ok(patient) : Results.Forbid();
}).RequireAuthorization();

app.MapPost("/api/patients", async (PatientUpsertRequest request, ClaimsPrincipal principal, PatientRepository repository, CancellationToken cancellationToken) =>
{
    if (!CanAccessCompany(principal, request.CompanyId) || !CanManagePatients(principal))
    {
        return Results.Forbid();
    }

    try
    {
        var patient = await repository.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/patients/{patient.PatientId}", patient);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapPut("/api/patients/{patientId:int}", async (int patientId, PatientUpsertRequest request, ClaimsPrincipal principal, PatientRepository repository, CancellationToken cancellationToken) =>
{
    var existingPatient = await repository.GetAsync(patientId, cancellationToken);
    if (existingPatient is null)
    {
        return Results.NotFound();
    }

    if (!CanAccessCompany(principal, existingPatient.CompanyId) || !CanManagePatients(principal))
    {
        return Results.Forbid();
    }

    if (request.CompanyId != existingPatient.CompanyId && !IsElevated(principal))
    {
        return Results.Forbid();
    }

    if (!CanAccessCompany(principal, request.CompanyId))
    {
        return Results.Forbid();
    }

    try
    {
        var patient = await repository.UpdateAsync(patientId, request, existingPatient.CompanyId, cancellationToken);
        return patient is null ? Results.NotFound() : Results.Ok(patient);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapDelete("/api/patients/{patientId:int}", async (int patientId, ClaimsPrincipal principal, PatientRepository repository, CancellationToken cancellationToken) =>
{
    var existingPatient = await repository.GetAsync(patientId, cancellationToken);
    if (existingPatient is null)
    {
        return Results.NotFound(new { message = "Patient not found." });
    }

    if (!CanAccessCompany(principal, existingPatient.CompanyId) || !IsElevated(principal))
    {
        return Results.Forbid();
    }

    try
    {
        return await repository.DeleteAsync(patientId, existingPatient.CompanyId, cancellationToken) ? Results.NoContent() : Results.NotFound(new { message = "Patient not found." });
    }
    catch (SqlException)
    {
        return Results.Conflict(new { message = "Patient cannot be deleted because related records exist." });
    }
}).RequireAuthorization();

app.MapGet("/api/boxes", async (int companyId, ClaimsPrincipal principal, BoxRepository repository, CancellationToken cancellationToken) =>
{
    if (!CanAccessCompany(principal, companyId))
    {
        return Results.Forbid();
    }

    var boxes = await repository.GetByCompanyAsync(companyId, cancellationToken);
    return Results.Ok(boxes);
}).RequireAuthorization();

app.MapPost("/api/boxes", async (BoxUpsertRequest request, ClaimsPrincipal principal, BoxRepository repository, CancellationToken cancellationToken) =>
{
    if (!CanAccessCompany(principal, request.CompanyId) || !CanManageBoxes(principal))
    {
        return Results.Forbid();
    }

    try
    {
        var box = await repository.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/boxes/{box.BoxId}", box);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapDelete("/api/boxes/{boxId:int}", async (int boxId, ClaimsPrincipal principal, BoxRepository repository, CancellationToken cancellationToken) =>
{
    var box = await repository.GetAsync(boxId, cancellationToken);
    if (box is null)
    {
        return Results.NotFound(new { message = "Box not found." });
    }

    if (!CanAccessCompany(principal, box.CompanyId) || !CanManageBoxes(principal))
    {
        return Results.Forbid();
    }

    try
    {
        return await repository.DeleteAsync(boxId, box.CompanyId, cancellationToken) ? Results.NoContent() : Results.NotFound(new { message = "Box not found." });
    }
    catch (SqlException)
    {
        return Results.Conflict(new { message = "Box cannot be deleted because related records exist." });
    }
}).RequireAuthorization();

app.MapGet("/api/categories", async (int companyId, ClaimsPrincipal principal, CategoryRepository repository, CancellationToken cancellationToken) =>
{
    if (!CanAccessCompany(principal, companyId))
    {
        return Results.Forbid();
    }

    var categories = await repository.GetByCompanyAsync(companyId, cancellationToken);
    return Results.Ok(categories);
}).RequireAuthorization();

app.MapPost("/api/categories", async (CategoryUpsertRequest request, ClaimsPrincipal principal, CategoryRepository repository, CancellationToken cancellationToken) =>
{
    if (!CanAccessCompany(principal, request.CompanyId) || !CanManageCategories(principal))
    {
        return Results.Forbid();
    }

    try
    {
        var category = await repository.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/categories/{category.CategoryId}", category);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapDelete("/api/categories/{categoryId:int}", async (int categoryId, ClaimsPrincipal principal, CategoryRepository repository, CancellationToken cancellationToken) =>
{
    var category = await repository.GetAsync(categoryId, cancellationToken);
    if (category is null)
    {
        return Results.NotFound(new { message = "Category not found." });
    }

    if (!CanAccessCompany(principal, category.CompanyId) || !CanManageCategories(principal))
    {
        return Results.Forbid();
    }

    try
    {
        return await repository.DeleteAsync(categoryId, category.CompanyId, cancellationToken) ? Results.NoContent() : Results.NotFound(new { message = "Category not found." });
    }
    catch (SqlException)
    {
        return Results.Conflict(new { message = "Category cannot be deleted because related documents exist." });
    }
}).RequireAuthorization();

app.MapGet("/api/documents", async (int companyId, int patientId, ClaimsPrincipal principal, DocumentRepository repository, PatientRepository patientRepository, CancellationToken cancellationToken) =>
{
    if (!CanAccessCompany(principal, companyId))
    {
        return Results.Forbid();
    }

    var patient = await patientRepository.GetAsync(patientId, cancellationToken);
    if (patient is null || patient.CompanyId != companyId)
    {
        return Results.NotFound(new { message = "Patient not found." });
    }

    var documents = await repository.GetByPatientAsync(companyId, patientId, cancellationToken);
    return Results.Ok(documents);
}).RequireAuthorization();

app.MapPost("/api/documents", async (HttpRequest httpRequest, ClaimsPrincipal principal, DocumentRepository repository, PatientRepository patientRepository, CategoryRepository categoryRepository, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    if (!httpRequest.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Multipart form data is required." });
    }

    var form = await httpRequest.ReadFormAsync(cancellationToken);
    var file = form.Files["file"];
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "Please select a document to upload." });
    }

    if (!int.TryParse(form["companyId"], out var companyId) ||
        !int.TryParse(form["patientId"], out var patientId) ||
        !int.TryParse(form["categoryId"], out var categoryId))
    {
        return Results.BadRequest(new { message = "Company, patient, and category are required." });
    }
    if (!CanAccessCompany(principal, companyId))
    {
        return Results.Forbid();
    }

    if (!ReadBoolClaim(principal, "upload_document") && !IsElevated(principal))
    {
        return Results.Forbid();
    }

    var ownershipValidation = await ValidateDocumentOwnershipAsync(companyId, patientId, categoryId, patientRepository, categoryRepository, cancellationToken);
    if (ownershipValidation is not null)
    {
        return ownershipValidation;
    }

    var safeName = BuildStoredDocumentName(form["documentName"], file.FileName);
    var validationResult = await ValidateUploadedDocumentAsync(file, safeName, maxDocumentUploadBytes, cancellationToken);
    if (validationResult is not null)
    {
        return validationResult;
    }

    var storeRoot = configuration["Store:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "Store");
    var savedDocument = await SaveUploadedDocumentAsync(file, storeRoot, companyId, patientId, categoryId, safeName, cancellationToken);

    var pages = int.TryParse(form["pages"], out var parsedPages) ? Math.Max(parsedPages, 1) : 1;
    var dateOfService = ParseDateOnly(form["dateOfService"]);
    var document = await repository.CreateAsync(companyId, patientId, categoryId, savedDocument.SafeName, savedDocument.RelativeUrl, pages, form["uploadedBy"], dateOfService, cancellationToken);
    return Results.Created($"/api/documents/{document.DocumentId}", document);
}).RequireAuthorization();

app.MapPost("/api/documents/scan", async (HttpRequest httpRequest, ClaimsPrincipal principal, DocumentRepository repository, PatientRepository patientRepository, CategoryRepository categoryRepository, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    if (!httpRequest.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Multipart form data is required." });
    }

    var file = httpRequest.Form.Files["RemoteFile"] ?? httpRequest.Form.Files["file"];
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "No scanned document was received." });
    }

    if (!int.TryParse(httpRequest.Query["Id"], out var companyId) ||
        !int.TryParse(httpRequest.Query["pid"], out var patientId) ||
        !int.TryParse(httpRequest.Query["Cat_id"], out var categoryId))
    {
        return Results.BadRequest(new { message = "Company, patient, and category are required." });
    }
    if (!CanAccessCompany(principal, companyId))
    {
        return Results.Forbid();
    }

    if (!ReadBoolClaim(principal, "scan_document") && !IsElevated(principal))
    {
        return Results.Forbid();
    }

    var ownershipValidation = await ValidateDocumentOwnershipAsync(companyId, patientId, categoryId, patientRepository, categoryRepository, cancellationToken);
    if (ownershipValidation is not null)
    {
        return ownershipValidation;
    }

    var safeName = BuildStoredDocumentName(httpRequest.Query["documentName"], file.FileName);
    var validationResult = await ValidateUploadedDocumentAsync(file, safeName, maxDocumentUploadBytes, cancellationToken);
    if (validationResult is not null)
    {
        return validationResult;
    }

    var storeRoot = configuration["Store:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "Store");
    var savedDocument = await SaveUploadedDocumentAsync(file, storeRoot, companyId, patientId, categoryId, safeName, cancellationToken);

    var pages = int.TryParse(httpRequest.Query["pages"], out var parsedPages) ? Math.Max(parsedPages, 1) : 1;
    var dateOfService = ParseDateOnly(httpRequest.Query["dateOfService"]);
    var document = await repository.CreateAsync(companyId, patientId, categoryId, savedDocument.SafeName, savedDocument.RelativeUrl, pages, "Scanner", dateOfService, cancellationToken);
    return Results.Ok(document);
}).RequireAuthorization();

app.MapGet("/api/documents/{documentId:int}/download", async (int documentId, ClaimsPrincipal principal, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var document = await repository.GetDocumentAsync(documentId, cancellationToken);
    if (document?.Url is null)
    {
        return Results.NotFound(new { message = "Document not found." });
    }

    if (!CanAccessCompany(principal, document.CompanyId) || (!ReadBoolClaim(principal, "download_document") && !IsElevated(principal)))
    {
        return Results.Forbid();
    }

    var storeRoot = configuration["Store:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "Store");
    var fullPath = Path.GetFullPath(Path.Combine(storeRoot, document.Url.Replace('/', Path.DirectorySeparatorChar)));
    var fullStoreRoot = Path.GetFullPath(storeRoot);
    if (!IsPathUnderRoot(fullPath, fullStoreRoot) || !File.Exists(fullPath))
    {
        return Results.NotFound(new { message = "Document file not found." });
    }

    return Results.File(fullPath, "application/octet-stream", BuildDownloadFileName(document.DocumentName, document.Url));
}).RequireAuthorization();

app.MapGet("/api/documents/{documentId:int}/preview", (int documentId, HttpContext httpContext, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
    PreviewDocumentAsync(documentId, null, httpContext, repository, configuration, cancellationToken)).RequireAuthorization();
app.MapGet("/api/documents/{documentId:int}/preview/{fileName}", (int documentId, string fileName, HttpContext httpContext, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
    PreviewDocumentAsync(documentId, fileName, httpContext, repository, configuration, cancellationToken)).RequireAuthorization();
app.MapGet("/api/documents/{documentId:int}/page/{page:int}", (int documentId, int page, HttpContext httpContext, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
    PreviewTiffPageAsync(documentId, page, httpContext, repository, configuration, cancellationToken)).RequireAuthorization();
app.MapGet("/api/documents/{documentId:int}/thumbnail", (int documentId, HttpContext httpContext, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
    PreviewTiffPageAsync(documentId, 1, httpContext, repository, configuration, cancellationToken)).RequireAuthorization();

app.MapDelete("/api/documents/{documentId:int}", async (int documentId, ClaimsPrincipal principal, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var document = await repository.GetDocumentAsync(documentId, cancellationToken);
    if (document is null)
    {
        return Results.NotFound(new { message = "Document not found." });
    }

    if (!CanAccessCompany(principal, document.CompanyId) || (!ReadBoolClaim(principal, "delete_document") && !IsElevated(principal)))
    {
        return Results.Forbid();
    }

    var deleted = await repository.DeleteAsync(documentId, document.CompanyId, principal.FindFirst("username")?.Value, cancellationToken);
    if (!deleted)
    {
        return Results.NotFound(new { message = "Document not found." });
    }

    if (!string.IsNullOrWhiteSpace(document.Url))
    {
        var storeRoot = configuration["Store:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "Store");
        var fullPath = Path.GetFullPath(Path.Combine(storeRoot, document.Url.Replace('/', Path.DirectorySeparatorChar)));
        var fullStoreRoot = Path.GetFullPath(storeRoot);
        if (IsPathUnderRoot(fullPath, fullStoreRoot) && File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    return Results.NoContent();
}).RequireAuthorization();

app.MapGet("/api/companies", async (ClaimsPrincipal principal, CompanyRepository repository, CancellationToken cancellationToken) =>
{
    var companies = await repository.GetAllAsync(cancellationToken);
    if (!IsElevated(principal))
    {
        var ownCompanyId = ReadCompanyId(principal);
        companies = companies.Where(company => company.CompanyId == ownCompanyId).ToList();
    }
    return Results.Ok(companies);
}).RequireAuthorization();

app.MapPost("/api/companies", async (CompanyUpsertRequest request, ClaimsPrincipal principal, CompanyRepository repository, CancellationToken cancellationToken) =>
{
    if (!IsElevated(principal))
    {
        return Results.Forbid();
    }

    try
    {
        var company = await repository.UpsertAsync(request, cancellationToken);
        return Results.Ok(company);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapDelete("/api/companies/{companyId:int}", async (int companyId, ClaimsPrincipal principal, CompanyRepository repository, CancellationToken cancellationToken) =>
{
    if (!IsElevated(principal))
    {
        return Results.Forbid();
    }

    try
    {
        return await repository.DeleteAsync(companyId, cancellationToken) ? Results.NoContent() : Results.NotFound(new { message = "Company not found." });
    }
    catch (SqlException)
    {
        return Results.Conflict(new { message = "Company cannot be deleted because related records exist." });
    }
}).RequireAuthorization();

app.MapGet("/api/reports/documents", async (int companyId, DateTime? fromDate, DateTime? toDate, int? take, ClaimsPrincipal principal, DocumentRepository repository, CancellationToken cancellationToken) =>
{
    if (!CanAccessCompany(principal, companyId))
    {
        return Results.Forbid();
    }

    var documents = await repository.GetReportAsync(companyId, fromDate, toDate, take ?? 500, cancellationToken);
    return Results.Ok(documents);
}).RequireAuthorization();

app.MapGet("/api/users", async (int companyId, ClaimsPrincipal principal, UserRepository repository, CancellationToken cancellationToken) =>
{
    if (!CanAccessCompany(principal, companyId) || !CanManageUsers(principal))
    {
        return Results.Forbid();
    }

    var users = await repository.GetByCompanyAsync(companyId, cancellationToken);
    return Results.Ok(users);
}).RequireAuthorization();

app.MapPost("/api/users", async (UserUpsertRequest request, ClaimsPrincipal principal, UserRepository repository, CancellationToken cancellationToken) =>
{
    if (!CanAccessCompany(principal, request.CompanyId) || !CanManageUsers(principal))
    {
        return Results.Forbid();
    }

    if (!ReadBoolClaim(principal, "super_user") && (request.SuperUser || request.IsAdmin))
    {
        return Results.Forbid();
    }

    var existingUser = string.IsNullOrWhiteSpace(request.Username)
        ? null
        : await repository.GetByUsernameAsync(request.Username.Trim(), cancellationToken);
    if (existingUser is not null)
    {
        if (string.Equals(existingUser.Username, principal.FindFirst("username")?.Value, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = "You cannot modify your own user account." });
        }

        if (!CanAccessCompany(principal, existingUser.CompanyId))
        {
            return Results.Forbid();
        }

        if (!ReadBoolClaim(principal, "super_user") && (existingUser.SuperUser || existingUser.IsAdmin))
        {
            return Results.Forbid();
        }
    }

    try
    {
        var user = await repository.UpsertAsync(request, cancellationToken);
        return Results.Ok(user);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapDelete("/api/users/{username}", async (string username, ClaimsPrincipal principal, UserRepository repository, CancellationToken cancellationToken) =>
{
    var decodedUsername = Uri.UnescapeDataString(username);
    var user = await repository.GetByUsernameAsync(decodedUsername, cancellationToken);
    if (user is null)
    {
        return Results.NotFound(new { message = "User not found." });
    }

    if (!CanAccessCompany(principal, user.CompanyId) || !CanManageUsers(principal))
    {
        return Results.Forbid();
    }

    if (!ReadBoolClaim(principal, "super_user") && (user.SuperUser || user.IsAdmin))
    {
        return Results.Forbid();
    }

    if (string.Equals(decodedUsername, principal.FindFirst("username")?.Value, StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { message = "You cannot delete your own user account." });
    }

    return await repository.DeleteAsync(decodedUsername, user.CompanyId, cancellationToken) ? Results.NoContent() : Results.NotFound(new { message = "User not found." });
}).RequireAuthorization();

app.MapGet("/api/settings/security", async (ClaimsPrincipal principal, SettingsRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    if (!ReadBoolClaim(principal, "super_user"))
    {
        return Results.Forbid();
    }

    return Results.Ok(await repository.GetSecuritySettingsAsync(configuration, cancellationToken));
}).RequireAuthorization();

app.MapPost("/api/settings/security", async (SecuritySettingsDto request, ClaimsPrincipal principal, SettingsRepository repository, CancellationToken cancellationToken) =>
{
    if (!ReadBoolClaim(principal, "super_user"))
    {
        return Results.Forbid();
    }

    await repository.SaveSecuritySettingsAsync(request, cancellationToken);
    return Results.Ok(new { message = "Settings saved." });
}).RequireAuthorization();

app.MapGet("/api/settings/branding", async (SettingsRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    return Results.Ok(await repository.GetBrandingSettingsAsync(configuration, cancellationToken));
});

app.MapGet("/api/auth/me", (ClaimsPrincipal principal) =>
{
    if (principal.Identity?.IsAuthenticated != true)
    {
        return Results.Ok(new CurrentUserDto { Authenticated = false });
    }

    return Results.Ok(new CurrentUserDto
    {
        Authenticated = true,
        AuthProvider = principal.FindFirst("auth_provider")?.Value,
        User = UserFromClaims(principal)
    });
});

app.MapPost("/api/auth/login", async (LoginRequest request, UserRepository repository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    var user = await repository.LoginAsync(request.Username, request.Password, cancellationToken);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, CreatePrincipal(user, "local"));
    return Results.Ok(new LoginResponse { User = user });
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/verify-email-otp", async (VerifyOtpRequest request, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    var user = await authRepository.VerifyEmailOtpChallengeAsync(request.ChallengeId, request.Code, cancellationToken);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, CreatePrincipal(user, "local"));
    return Results.Ok(new LoginResponse { User = user });
}).RequireRateLimiting("auth");

app.MapGet("/api/auth/microsoft", async (HttpContext httpContext) =>
{
    var settings = await LoadMicrosoftSsoRuntimeSettingsAsync(httpContext);
    if (!HasMicrosoftSsoSettings(settings))
    {
        return Results.BadRequest(new { message = MicrosoftSsoNotConfiguredMessage });
    }

    var redirectUri = BuildPostSignInRedirect(
        httpContext.Request.Query["returnUrl"].FirstOrDefault(),
        httpContext.RequestServices.GetRequiredService<IConfiguration>());

    return Results.Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, [OpenIdConnectDefaults.AuthenticationScheme]);
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/change-password", async (ChangePasswordRequest request, ClaimsPrincipal principal, UserRepository repository, CancellationToken cancellationToken) =>
{
    if (principal.FindFirst("auth_provider")?.Value != "local")
    {
        return Results.BadRequest(new { message = "Password changes are only available for local SmartDocScan users." });
    }

    if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
    {
        return Results.BadRequest(new { message = "New passwords do not match." });
    }

    var username = principal.FindFirst("username")?.Value;
    var changed = await repository.ChangePasswordAsync(username ?? "", request.CurrentPassword, request.NewPassword, cancellationToken);
    return changed ? Results.NoContent() : Results.BadRequest(new { message = "Current password is incorrect." });
}).RequireAuthorization().RequireRateLimiting("auth");

app.MapPost("/api/auth/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
});

app.Run();

static ClaimsPrincipal CreatePrincipal(UserDto user, string authProvider)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Username ?? ""),
        new(ClaimTypes.Name, user.Name ?? user.Username ?? ""),
        new("username", user.Username ?? ""),
        new("name", user.Name ?? ""),
        new("company_id", user.CompanyId.ToString()),
        new("auth_provider", authProvider),
        new("upload_document", user.UploadDocument.ToString()),
        new("scan_document", user.ScanDocument.ToString()),
        new("delete_document", user.DeleteDocument.ToString()),
        new("delete_manage", user.DeleteManage.ToString()),
        new("print_document", user.PrintDocument.ToString()),
        new("download_document", user.DownloadDocument.ToString()),
        new("add_category", user.AddCategory.ToString()),
        new("add_users", user.AddUsers.ToString()),
        new("add_patients", user.AddPatients.ToString()),
        new("box", user.Box.ToString()),
        new("report", user.Report.ToString()),
        new("super_user", user.SuperUser.ToString()),
        new("is_admin", user.IsAdmin.ToString())
    };
    return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
}

static UserDto UserFromClaims(ClaimsPrincipal principal)
{
    return new UserDto
    {
        Username = principal.FindFirst("username")?.Value,
        Name = principal.FindFirst("name")?.Value,
        CompanyId = int.TryParse(principal.FindFirst("company_id")?.Value, out var companyId) ? companyId : 0,
        UploadDocument = ReadBoolClaim(principal, "upload_document"),
        ScanDocument = ReadBoolClaim(principal, "scan_document"),
        DeleteDocument = ReadBoolClaim(principal, "delete_document"),
        DeleteManage = ReadBoolClaim(principal, "delete_manage"),
        PrintDocument = ReadBoolClaim(principal, "print_document"),
        DownloadDocument = ReadBoolClaim(principal, "download_document"),
        AddCategory = ReadBoolClaim(principal, "add_category"),
        AddUsers = ReadBoolClaim(principal, "add_users"),
        AddPatients = ReadBoolClaim(principal, "add_patients"),
        Box = ReadBoolClaim(principal, "box"),
        Report = ReadBoolClaim(principal, "report"),
        SuperUser = ReadBoolClaim(principal, "super_user"),
        IsAdmin = ReadBoolClaim(principal, "is_admin")
    };
}

static bool ReadBoolClaim(ClaimsPrincipal principal, string type)
{
    return bool.TryParse(principal.FindFirst(type)?.Value, out var value) && value;
}

static string? FindClaimValue(ClaimsPrincipal? principal, params string[] claimTypes)
{
    if (principal is null)
    {
        return null;
    }

    foreach (var claimType in claimTypes)
    {
        var value = principal.FindFirst(claimType)?.Value;
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }

    return null;
}

static async Task<MicrosoftSsoSettingsDto> LoadMicrosoftSsoRuntimeSettingsAsync(HttpContext httpContext)
{
    var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
    var repository = httpContext.RequestServices.GetRequiredService<SettingsRepository>();
    return await repository.GetMicrosoftSsoRuntimeSettingsAsync(configuration, httpContext.RequestAborted);
}

static bool ApplyMicrosoftSsoSettings(OpenIdConnectOptions options, MicrosoftSsoSettingsDto settings)
{
    if (!HasMicrosoftSsoSettings(settings))
    {
        return false;
    }

    options.ClientId = settings.ClientId!.Trim();
    options.ClientSecret = settings.ClientSecret!.Trim();
    if (!string.IsNullOrWhiteSpace(settings.CallbackPath))
    {
        options.CallbackPath = settings.CallbackPath.Trim();
    }
    return true;
}

static bool HasMicrosoftSsoSettings(MicrosoftSsoSettingsDto settings)
{
    return !string.IsNullOrWhiteSpace(settings.ClientId)
        && !string.IsNullOrWhiteSpace(settings.ClientSecret);
}

static string BuildPostSignInRedirect(string? returnUrl, IConfiguration configuration)
{
    if (string.IsNullOrWhiteSpace(returnUrl))
    {
        returnUrl = "/";
    }

    var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absoluteReturnUri)
        && allowedOrigins.Any(origin => Uri.TryCreate(origin, UriKind.Absolute, out var allowedOrigin)
            && Uri.Compare(absoluteReturnUri, allowedOrigin, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0))
    {
        return absoluteReturnUri.ToString();
    }

    if (!returnUrl.StartsWith("/", StringComparison.Ordinal) || returnUrl.StartsWith("//", StringComparison.Ordinal))
    {
        returnUrl = "/";
    }

    var webOrigin = allowedOrigins.FirstOrDefault(origin => Uri.TryCreate(origin, UriKind.Absolute, out _));
    return string.IsNullOrWhiteSpace(webOrigin) ? returnUrl : new Uri(new Uri(webOrigin.TrimEnd('/') + "/"), returnUrl.TrimStart('/')).ToString();
}

static bool CanAccessCompany(ClaimsPrincipal principal, int companyId)
{
    return principal.Identity?.IsAuthenticated == true
        && (IsElevated(principal) || ReadCompanyId(principal) == companyId);
}

static bool IsElevated(ClaimsPrincipal principal)
{
    return ReadBoolClaim(principal, "is_admin") || ReadBoolClaim(principal, "super_user");
}

static bool CanManageUsers(ClaimsPrincipal principal)
{
    return IsElevated(principal) || ReadBoolClaim(principal, "add_users");
}

static bool CanManagePatients(ClaimsPrincipal principal)
{
    return IsElevated(principal) || ReadBoolClaim(principal, "add_patients");
}

static void AddSecurityHeaders(HttpResponse response)
{
    response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    response.Headers.TryAdd("X-Frame-Options", "DENY");
    response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
}

static bool IsUnsafeMethod(string method)
{
    return !HttpMethods.IsGet(method)
        && !HttpMethods.IsHead(method)
        && !HttpMethods.IsOptions(method)
        && !HttpMethods.IsTrace(method);
}

static bool IsAllowedBrowserOrigin(HttpRequest request, string[] allowedOrigins)
{
    var origin = request.Headers.Origin.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(origin))
    {
        return true;
    }

    if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
    {
        return false;
    }

    return allowedOrigins.Any(allowed =>
        Uri.TryCreate(allowed, UriKind.Absolute, out var allowedUri)
        && Uri.Compare(originUri, allowedUri, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0);
}

static string GetRateLimitPartitionKey(HttpContext httpContext)
{
    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static async Task<IResult?> ValidateDocumentOwnershipAsync(
    int companyId,
    int patientId,
    int categoryId,
    PatientRepository patientRepository,
    CategoryRepository categoryRepository,
    CancellationToken cancellationToken)
{
    var patient = await patientRepository.GetAsync(patientId, cancellationToken);
    if (patient is null || patient.CompanyId != companyId)
    {
        return Results.NotFound(new { message = "Patient not found." });
    }

    var category = await categoryRepository.GetAsync(categoryId, cancellationToken);
    if (category is null || category.CompanyId != companyId)
    {
        return Results.NotFound(new { message = "Category not found." });
    }

    return null;
}

static bool CanManageBoxes(ClaimsPrincipal principal)
{
    return IsElevated(principal) || ReadBoolClaim(principal, "box");
}

static bool CanManageCategories(ClaimsPrincipal principal)
{
    return IsElevated(principal) || ReadBoolClaim(principal, "add_category");
}

static int ReadCompanyId(ClaimsPrincipal principal)
{
    return int.TryParse(principal.FindFirst("company_id")?.Value, out var companyId) ? companyId : 0;
}

static async Task<IResult> PreviewDocumentAsync(int documentId, string? routeFileName, HttpContext httpContext, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken)
{
    var document = await repository.GetDocumentAsync(documentId, cancellationToken);
    if (document?.Url is null)
    {
        return Results.NotFound(new { message = "Document not found." });
    }

    if (!CanAccessCompany(httpContext.User, document.CompanyId))
    {
        return Results.Forbid();
    }

    var storeRoot = configuration["Store:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "Store");
    var fullPath = Path.GetFullPath(Path.Combine(storeRoot, document.Url.Replace('/', Path.DirectorySeparatorChar)));
    var fullStoreRoot = Path.GetFullPath(storeRoot);
    if (!IsPathUnderRoot(fullPath, fullStoreRoot) || !File.Exists(fullPath))
    {
        return Results.NotFound(new { message = "Document file not found." });
    }

    var storedFileName = Path.GetFileName(document.Url);
    var displayFileName = string.IsNullOrWhiteSpace(routeFileName) ? storedFileName : routeFileName;
    if (IsTiffFile(storedFileName))
    {
        return PreviewTiffAsHtml(document, displayFileName, fullPath, httpContext);
    }

    var contentType = GetContentType(storedFileName);
    httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{SanitizeHeaderFileName(displayFileName)}\"";
    httpContext.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(fullPath, contentType, enableRangeProcessing: true);
}

static async Task<IResult> PreviewTiffPageAsync(int documentId, int page, HttpContext httpContext, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken)
{
    var document = await repository.GetDocumentAsync(documentId, cancellationToken);
    if (document?.Url is null)
    {
        return Results.NotFound(new { message = "Document not found." });
    }

    if (!CanAccessCompany(httpContext.User, document.CompanyId))
    {
        return Results.Forbid();
    }

    var resolved = ResolveStoredDocumentPath(configuration, document.Url);
    if (resolved is null)
    {
        return Results.NotFound(new { message = "Document file not found." });
    }

    var storedFileName = Path.GetFileName(document.Url);
    if (!IsTiffFile(storedFileName))
    {
        return Results.BadRequest(new { message = "Document is not a TIFF file." });
    }

    var width = ParsePositiveQueryInt(httpContext, "width", 1400);
    var height = ParsePositiveQueryInt(httpContext, "height", 1800);
    var image = RenderTiffPageAsJpeg(resolved, page, width, height);
    var pageFileName = $"{Path.GetFileNameWithoutExtension(storedFileName)}-page-{Math.Max(page, 1)}.jpg";
    httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{SanitizeHeaderFileName(pageFileName)}\"";
    httpContext.Response.Headers.XContentTypeOptions = "nosniff";
    httpContext.Response.Headers.CacheControl = "private, max-age=3600";
    return Results.File(image, "image/jpeg", enableRangeProcessing: false);
}

static IResult PreviewTiffAsHtml(DocumentDto document, string displayFileName, string fullPath, HttpContext httpContext)
{
    using var images = new MagickImageCollection(fullPath);
    var pageCount = Math.Max(document.NumberOfPages, images.Count);
    if (pageCount <= 0)
    {
        return Results.Problem("The TIFF file does not contain any readable pages.", statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    var title = HtmlEncode(displayFileName);
    var basePath = httpContext.Request.PathBase.ToString();
    var builder = new StringBuilder();
    builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
    builder.Append("<title>").Append(title).AppendLine("</title>");
    builder.AppendLine("<style>body{margin:0;background:#262626;color:#fff;font-family:Segoe UI,Arial,sans-serif}.toolbar{position:sticky;top:0;z-index:2;display:flex;gap:12px;align-items:center;padding:10px 16px;background:#1f1f1f;border-bottom:1px solid #3a3a3a}.toolbar strong{flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.toolbar button{border:1px solid #777;background:#fff;color:#111;border-radius:4px;padding:7px 12px;font-weight:600;cursor:pointer}.pages{padding:24px 12px}.page{display:block;max-width:min(96vw,1400px);width:auto;height:auto;margin:0 auto 24px;background:#fff;box-shadow:0 2px 12px rgba(0,0,0,.45)}@media print{.toolbar{display:none}.pages{padding:0;background:#fff}.page{width:100%;max-width:100%;margin:0;box-shadow:none;page-break-after:always}}</style>");
    builder.AppendLine("</head><body>");
    builder.Append("<div class=\"toolbar\"><strong>").Append(title).Append("</strong><span>").Append(pageCount).Append(" pages</span><button onclick=\"window.print()\">Print</button></div><main class=\"pages\">");
    for (var page = 1; page <= pageCount; page++)
    {
        builder.Append("<img class=\"page\" alt=\"Page ").Append(page).Append("\" src=\"")
            .Append(basePath).Append("/api/documents/").Append(document.DocumentId).Append("/page/").Append(page)
            .Append("?width=1400&height=1800\">");
    }
    builder.AppendLine("</main></body></html>");

    httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{SanitizeHeaderFileName(Path.ChangeExtension(displayFileName, ".html") ?? "preview.html")}\"";
    httpContext.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.Text(builder.ToString(), "text/html", Encoding.UTF8);
}

static byte[] RenderTiffPageAsJpeg(string fullPath, int page, int maxWidth, int maxHeight)
{
    using var images = new MagickImageCollection(fullPath);
    if (images.Count == 0)
    {
        throw new InvalidOperationException("The TIFF file does not contain any readable pages.");
    }

    var pageIndex = Math.Clamp(page, 1, images.Count) - 1;
    using var image = (MagickImage)images[pageIndex].Clone();
    image.AutoOrient();
    image.BackgroundColor = MagickColors.White;
    image.Alpha(AlphaOption.Remove);
    image.ResetPage();
    if (maxWidth > 0 || maxHeight > 0)
    {
        image.Resize(new MagickGeometry((uint)Math.Max(maxWidth, 1), (uint)Math.Max(maxHeight, 1))
        {
            IgnoreAspectRatio = false,
            Greater = true
        });
    }
    image.Format = MagickFormat.Jpeg;
    image.Quality = 92;
    return image.ToByteArray();
}

static string? ResolveStoredDocumentPath(IConfiguration configuration, string storedUrl)
{
    var storeRoot = configuration["Store:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "Store");
    var fullPath = Path.GetFullPath(Path.Combine(storeRoot, storedUrl.Replace('/', Path.DirectorySeparatorChar)));
    var fullStoreRoot = Path.GetFullPath(storeRoot);
    return IsPathUnderRoot(fullPath, fullStoreRoot) && File.Exists(fullPath)
        ? fullPath
        : null;
}

static bool IsPathUnderRoot(string fullPath, string fullRoot)
{
    var normalizedRoot = Path.TrimEndingDirectorySeparator(fullRoot) + Path.DirectorySeparatorChar;
    return fullPath.Equals(Path.TrimEndingDirectorySeparator(fullRoot), StringComparison.OrdinalIgnoreCase)
        || fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
}

static int ParsePositiveQueryInt(HttpContext httpContext, string name, int fallback)
{
    return int.TryParse(httpContext.Request.Query[name], out var parsed) && parsed > 0
        ? Math.Min(parsed, 3000)
        : fallback;
}

static string HtmlEncode(string? value)
{
    return System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}

static string GetContentType(string fileName)
{
    return Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".tif" or ".tiff" => "image/tiff",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };
}

static bool IsTiffFile(string fileName)
{
    var extension = Path.GetExtension(fileName).ToLowerInvariant();
    return extension is ".tif" or ".tiff";
}

static string SanitizeHeaderFileName(string? fileName)
{
    return string.IsNullOrWhiteSpace(fileName)
        ? "document"
        : fileName.Replace("\\", "_").Replace("/", "_").Replace("\"", "'");
}

static string BuildDownloadFileName(string? documentName, string? storedUrl)
{
    var storedFileName = Path.GetFileName((storedUrl ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
    var extension = Path.GetExtension(storedFileName);
    var displayName = string.IsNullOrWhiteSpace(documentName) ? storedFileName : documentName.Trim();
    var safeName = SanitizeHeaderFileName(Path.GetFileName(displayName));

    if (string.IsNullOrWhiteSpace(Path.GetExtension(safeName)) && !string.IsNullOrWhiteSpace(extension))
    {
        safeName += extension;
    }

    return safeName;
}

static DateTime? ParseDateOnly(string? value)
{
    return DateTime.TryParse(value, out var parsed) ? parsed.Date : null;
}

static string BuildStoredDocumentName(string? requestedName, string originalFileName)
{
    var originalSafeName = Path.GetFileName(originalFileName);
    var extension = Path.GetExtension(originalSafeName);
    var rawName = string.IsNullOrWhiteSpace(requestedName) ? originalSafeName : requestedName.Trim();
    var safeName = Path.GetFileName(rawName);

    foreach (var invalidChar in Path.GetInvalidFileNameChars())
    {
        safeName = safeName.Replace(invalidChar, '_');
    }

    safeName = safeName.Trim();
    if (string.IsNullOrWhiteSpace(safeName))
    {
        safeName = "document";
    }

    if (string.IsNullOrWhiteSpace(Path.GetExtension(safeName)) && !string.IsNullOrWhiteSpace(extension))
    {
        safeName += extension;
    }

    return safeName;
}

static async Task<(string SafeName, string RelativeUrl)> SaveUploadedDocumentAsync(
    IFormFile file,
    string storeRoot,
    int companyId,
    int patientId,
    int categoryId,
    string safeName,
    CancellationToken cancellationToken)
{
    var targetDirectory = Path.Combine(storeRoot, companyId.ToString(), patientId.ToString());
    Directory.CreateDirectory(targetDirectory);

    for (var attempt = 0; attempt < 10_000; attempt++)
    {
        var candidateName = AddFileNameSuffix(safeName, attempt);
        var storedFileName = categoryId + "_" + candidateName;
        var targetPath = Path.Combine(targetDirectory, storedFileName);

        try
        {
            await using var stream = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
            await file.CopyToAsync(stream, cancellationToken);

            var relativeUrl = Path.Combine(companyId.ToString(), patientId.ToString(), storedFileName).Replace('\\', '/');
            return (candidateName, relativeUrl);
        }
        catch (IOException) when (File.Exists(targetPath))
        {
            // Another document already uses this name. Try a numeric suffix without overwriting it.
        }
    }

    throw new IOException("Unable to allocate a unique stored document name.");
}

static string AddFileNameSuffix(string fileName, int suffix)
{
    if (suffix <= 0)
    {
        return fileName;
    }

    var extension = Path.GetExtension(fileName);
    var name = Path.GetFileNameWithoutExtension(fileName);
    return string.IsNullOrWhiteSpace(extension)
        ? $"{name}-{suffix}"
        : $"{name}-{suffix}{extension}";
}

static async Task<IResult?> ValidateUploadedDocumentAsync(IFormFile file, string storedFileName, long maxDocumentUploadBytes, CancellationToken cancellationToken)
{
    if (file.Length > maxDocumentUploadBytes)
    {
        return Results.BadRequest(new { message = $"Document exceeds the maximum upload size of {maxDocumentUploadBytes / 1024 / 1024} MB." });
    }

    var nameValidation = ValidateStoredDocumentName(storedFileName);
    if (nameValidation is not null)
    {
        return nameValidation;
    }

    var signatureValid = await HasAllowedFileSignatureAsync(file, storedFileName, cancellationToken);
    return signatureValid
        ? null
        : Results.BadRequest(new { message = "The uploaded file content does not match its file type." });
}

static IResult? ValidateStoredDocumentName(string fileName)
{
    var extension = Path.GetExtension(fileName).ToLowerInvariant();
    if (IsAllowedDocumentExtension(extension))
    {
        return null;
    }

    return Results.BadRequest(new { message = "Unsupported document type. Allowed file types are PDF, TIFF, JPG, PNG, BMP, GIF, WEBP, and TXT." });
}

static bool IsAllowedDocumentExtension(string extension)
{
    return extension is ".pdf" or ".tif" or ".tiff" or ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp" or ".txt";
}

static async Task<bool> HasAllowedFileSignatureAsync(IFormFile file, string storedFileName, CancellationToken cancellationToken)
{
    var extension = Path.GetExtension(storedFileName).ToLowerInvariant();
    if (extension == ".txt")
    {
        return true;
    }

    var header = new byte[12];
    await using var stream = file.OpenReadStream();
    var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
    return HasAllowedFileSignature(extension, header, read);
}

static bool HasAllowedFileSignature(string extension, byte[] header, int read)
{
    var span = header.AsSpan(0, read);

    return extension switch
    {
        ".pdf" => StartsWithAscii(span, "%PDF"),
        ".tif" or ".tiff" => StartsWith(span, [0x49, 0x49, 0x2A, 0x00]) || StartsWith(span, [0x4D, 0x4D, 0x00, 0x2A]),
        ".jpg" or ".jpeg" => StartsWith(span, [0xFF, 0xD8, 0xFF]),
        ".png" => StartsWith(span, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        ".gif" => StartsWithAscii(span, "GIF87a") || StartsWithAscii(span, "GIF89a"),
        ".bmp" => StartsWithAscii(span, "BM"),
        ".webp" => span.Length >= 12 && StartsWithAscii(span, "RIFF") && StartsWithAscii(span[8..], "WEBP"),
        _ => false
    };
}

static bool StartsWithAscii(ReadOnlySpan<byte> value, string expected)
{
    return value.Length >= expected.Length && Encoding.ASCII.GetString(value[..expected.Length]) == expected;
}

static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> expected)
{
    return value.Length >= expected.Length && value[..expected.Length].SequenceEqual(expected);
}
