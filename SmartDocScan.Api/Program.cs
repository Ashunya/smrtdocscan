using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
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
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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

app.UseCors();
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

app.MapGet("/api/patients/{patientId:int}", async (int patientId, PatientRepository repository, CancellationToken cancellationToken) =>
{
    var patient = await repository.GetAsync(patientId, cancellationToken);
    return patient is null ? Results.NotFound() : Results.Ok(patient);
}).RequireAuthorization();

app.MapPost("/api/patients", async (PatientUpsertRequest request, ClaimsPrincipal principal, PatientRepository repository, CancellationToken cancellationToken) =>
{
    if (!CanAccessCompany(principal, request.CompanyId))
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
    if (!CanAccessCompany(principal, request.CompanyId))
    {
        return Results.Forbid();
    }

    try
    {
        var patient = await repository.UpdateAsync(patientId, request, cancellationToken);
        return patient is null ? Results.NotFound() : Results.Ok(patient);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapDelete("/api/patients/{patientId:int}", async (int patientId, PatientRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        return await repository.DeleteAsync(patientId, cancellationToken) ? Results.NoContent() : Results.NotFound(new { message = "Patient not found." });
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
    if (!CanAccessCompany(principal, request.CompanyId))
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

app.MapDelete("/api/boxes/{boxId:int}", async (int boxId, BoxRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        return await repository.DeleteAsync(boxId, cancellationToken) ? Results.NoContent() : Results.NotFound(new { message = "Box not found." });
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
    if (!CanAccessCompany(principal, request.CompanyId))
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

app.MapDelete("/api/categories/{categoryId:int}", async (int categoryId, CategoryRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        return await repository.DeleteAsync(categoryId, cancellationToken) ? Results.NoContent() : Results.NotFound(new { message = "Category not found." });
    }
    catch (SqlException)
    {
        return Results.Conflict(new { message = "Category cannot be deleted because related documents exist." });
    }
}).RequireAuthorization();

app.MapGet("/api/documents", async (int companyId, int patientId, ClaimsPrincipal principal, DocumentRepository repository, CancellationToken cancellationToken) =>
{
    if (!CanAccessCompany(principal, companyId))
    {
        return Results.Forbid();
    }

    var documents = await repository.GetByPatientAsync(companyId, patientId, cancellationToken);
    return Results.Ok(documents);
}).RequireAuthorization();

app.MapPost("/api/documents", async (HttpRequest httpRequest, ClaimsPrincipal principal, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
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

    var safeName = BuildStoredDocumentName(form["documentName"], file.FileName);
    var relativeUrl = Path.Combine(companyId.ToString(), patientId.ToString(), categoryId + "_" + safeName).Replace('\\', '/');
    var storeRoot = configuration["Store:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "Store");
    var targetDirectory = Path.Combine(storeRoot, companyId.ToString(), patientId.ToString());
    Directory.CreateDirectory(targetDirectory);
    var targetPath = Path.Combine(targetDirectory, categoryId + "_" + safeName);

    await using (var stream = File.Create(targetPath))
    {
        await file.CopyToAsync(stream, cancellationToken);
    }

    var pages = int.TryParse(form["pages"], out var parsedPages) ? Math.Max(parsedPages, 1) : 1;
    var dateOfService = ParseDateOnly(form["dateOfService"]);
    var document = await repository.CreateAsync(companyId, patientId, categoryId, safeName, relativeUrl, pages, form["uploadedBy"], dateOfService, cancellationToken);
    return Results.Created($"/api/documents/{document.DocumentId}", document);
}).RequireAuthorization();

app.MapPost("/api/documents/scan", async (HttpRequest httpRequest, ClaimsPrincipal principal, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
{
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

    var safeName = BuildStoredDocumentName(httpRequest.Query["documentName"], file.FileName);
    var relativeUrl = Path.Combine(companyId.ToString(), patientId.ToString(), categoryId + "_" + safeName).Replace('\\', '/');
    var storeRoot = configuration["Store:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "Store");
    var targetDirectory = Path.Combine(storeRoot, companyId.ToString(), patientId.ToString());
    Directory.CreateDirectory(targetDirectory);
    var targetPath = Path.Combine(targetDirectory, categoryId + "_" + safeName);

    await using (var stream = File.Create(targetPath))
    {
        await file.CopyToAsync(stream, cancellationToken);
    }

    var pages = int.TryParse(httpRequest.Query["pages"], out var parsedPages) ? Math.Max(parsedPages, 1) : 1;
    var dateOfService = ParseDateOnly(httpRequest.Query["dateOfService"]);
    var document = await repository.CreateAsync(companyId, patientId, categoryId, safeName, relativeUrl, pages, "Scanner", dateOfService, cancellationToken);
    return Results.Ok(document);
}).RequireAuthorization();

app.MapGet("/api/documents/{documentId:int}/download", async (int documentId, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var document = await repository.GetDocumentAsync(documentId, cancellationToken);
    if (document?.Url is null)
    {
        return Results.NotFound(new { message = "Document not found." });
    }

    var storeRoot = configuration["Store:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "Store");
    var fullPath = Path.GetFullPath(Path.Combine(storeRoot, document.Url.Replace('/', Path.DirectorySeparatorChar)));
    var fullStoreRoot = Path.GetFullPath(storeRoot);
    if (!fullPath.StartsWith(fullStoreRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
    {
        return Results.NotFound(new { message = "Document file not found." });
    }

    return Results.File(fullPath, "application/octet-stream", BuildDownloadFileName(document.DocumentName, document.Url));
}).RequireAuthorization();

app.MapGet("/api/documents/{documentId:int}/preview", (int documentId, HttpContext httpContext, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
    PreviewDocumentAsync(documentId, null, httpContext, repository, configuration, cancellationToken)).RequireAuthorization();
app.MapGet("/api/documents/{documentId:int}/preview/{fileName}", (int documentId, string fileName, HttpContext httpContext, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
    PreviewDocumentAsync(documentId, fileName, httpContext, repository, configuration, cancellationToken)).RequireAuthorization();

app.MapDelete("/api/documents/{documentId:int}", async (int documentId, DocumentRepository repository, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var document = await repository.GetDocumentAsync(documentId, cancellationToken);
    if (document is null)
    {
        return Results.NotFound(new { message = "Document not found." });
    }

    var deleted = await repository.DeleteAsync(documentId, "Miranda", cancellationToken);
    if (!deleted)
    {
        return Results.NotFound(new { message = "Document not found." });
    }

    if (!string.IsNullOrWhiteSpace(document.Url))
    {
        var storeRoot = configuration["Store:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "Store");
        var fullPath = Path.GetFullPath(Path.Combine(storeRoot, document.Url.Replace('/', Path.DirectorySeparatorChar)));
        var fullStoreRoot = Path.GetFullPath(storeRoot);
        if (fullPath.StartsWith(fullStoreRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
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
    if (!CanAccessCompany(principal, companyId))
    {
        return Results.Forbid();
    }

    var users = await repository.GetByCompanyAsync(companyId, cancellationToken);
    return Results.Ok(users);
}).RequireAuthorization();

app.MapPost("/api/users", async (UserUpsertRequest request, ClaimsPrincipal principal, UserRepository repository, CancellationToken cancellationToken) =>
{
    if (!CanAccessCompany(principal, request.CompanyId))
    {
        return Results.Forbid();
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

app.MapDelete("/api/users/{username}", async (string username, UserRepository repository, CancellationToken cancellationToken) =>
{
    return await repository.DeleteAsync(Uri.UnescapeDataString(username), cancellationToken) ? Results.NoContent() : Results.NotFound(new { message = "User not found." });
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
});

app.MapPost("/api/auth/verify-email-otp", async (VerifyOtpRequest request, AuthRepository authRepository, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    var user = await authRepository.VerifyEmailOtpChallengeAsync(request.ChallengeId, request.Code, cancellationToken);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, CreatePrincipal(user, "local"));
    return Results.Ok(new LoginResponse { User = user });
});

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
});

app.MapPost("/api/auth/change-password", async (ChangePasswordRequest request, ClaimsPrincipal principal, UserRepository repository, CancellationToken cancellationToken) =>
{
    if (principal.FindFirst("auth_provider")?.Value != "local")
    {
        return Results.BadRequest(new { message = "Password changes are only available for local SmartDocScan users." });
    }

    var username = principal.FindFirst("username")?.Value;
    var changed = await repository.ChangePasswordAsync(username ?? "", request.CurrentPassword, request.NewPassword, cancellationToken);
    return changed ? Results.NoContent() : Results.BadRequest(new { message = "Current password is incorrect." });
}).RequireAuthorization();

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

    var storeRoot = configuration["Store:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "Store");
    var fullPath = Path.GetFullPath(Path.Combine(storeRoot, document.Url.Replace('/', Path.DirectorySeparatorChar)));
    var fullStoreRoot = Path.GetFullPath(storeRoot);
    if (!fullPath.StartsWith(fullStoreRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
    {
        return Results.NotFound(new { message = "Document file not found." });
    }

    var storedFileName = Path.GetFileName(document.Url);
    var displayFileName = string.IsNullOrWhiteSpace(routeFileName) ? storedFileName : routeFileName;
    if (IsTiffFile(storedFileName))
    {
        return PreviewTiffAsPdf(fullPath, displayFileName, httpContext);
    }

    var contentType = GetContentType(storedFileName);
    httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{SanitizeHeaderFileName(displayFileName)}\"";
    httpContext.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(fullPath, contentType, enableRangeProcessing: true);
}

static IResult PreviewTiffAsPdf(string fullPath, string displayFileName, HttpContext httpContext)
{
    using var image = Image.Load(fullPath);
    var frames = new List<PdfImagePage>();
    for (var index = 0; index < image.Frames.Count; index++)
    {
        using var frame = image.Frames.CloneFrame(index);
        using var stream = new MemoryStream();
        frame.SaveAsJpeg(stream, new JpegEncoder { Quality = 85 });
        frames.Add(new PdfImagePage(stream.ToArray(), frame.Width, frame.Height));
    }

    var pdf = BuildImagePdf(frames);
    var previewName = Path.ChangeExtension(displayFileName, ".pdf") ?? "preview.pdf";
    httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{SanitizeHeaderFileName(previewName)}\"";
    httpContext.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(pdf, "application/pdf", enableRangeProcessing: false);
}

static byte[] BuildImagePdf(IReadOnlyList<PdfImagePage> images)
{
    using var output = new MemoryStream();
    var offsets = new List<long> { 0 };
    WriteAscii(output, "%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n");

    var pageCount = images.Count;
    var pagesObjectId = 2;
    var firstPageObjectId = 3;
    var pageObjectIds = Enumerable.Range(0, pageCount).Select(i => firstPageObjectId + (i * 3)).ToArray();
    var imageObjectIds = Enumerable.Range(0, pageCount).Select(i => firstPageObjectId + (i * 3) + 1).ToArray();
    var contentObjectIds = Enumerable.Range(0, pageCount).Select(i => firstPageObjectId + (i * 3) + 2).ToArray();

    WriteObject(output, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
    WriteObject(output, offsets, pagesObjectId, $"<< /Type /Pages /Count {pageCount} /Kids [{string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"))}] >>");

    for (var i = 0; i < pageCount; i++)
    {
        var layout = FitImageToPage(images[i]);
        var content = FormattableString.Invariant($"q {layout.Width:0.###} 0 0 {layout.Height:0.###} {layout.X:0.###} {layout.Y:0.###} cm /Im{i + 1} Do Q");
        var contentBytes = Encoding.ASCII.GetBytes(content);

        WriteAscii(output, $"{pageObjectIds[i]} 0 obj\n");
        offsets.Add(output.Position);
        WriteAscii(output, FormattableString.Invariant(
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {layout.PageWidth:0.###} {layout.PageHeight:0.###}] /Resources << /XObject << /Im{i + 1} {imageObjectIds[i]} 0 R >> >> /Contents {contentObjectIds[i]} 0 R >>\nendobj\n"));

        WriteAscii(output, $"{imageObjectIds[i]} 0 obj\n");
        offsets.Add(output.Position);
        WriteAscii(output, $"<< /Type /XObject /Subtype /Image /Width {images[i].Width} /Height {images[i].Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {images[i].JpegBytes.Length} >>\nstream\n");
        output.Write(images[i].JpegBytes);
        WriteAscii(output, "\nendstream\nendobj\n");

        WriteAscii(output, $"{contentObjectIds[i]} 0 obj\n");
        offsets.Add(output.Position);
        WriteAscii(output, $"<< /Length {contentBytes.Length} >>\nstream\n");
        output.Write(contentBytes);
        WriteAscii(output, "\nendstream\nendobj\n");
    }

    var xrefOffset = output.Position;
    var highestObjectId = contentObjectIds.Last();
    WriteAscii(output, $"xref\n0 {highestObjectId + 1}\n0000000000 65535 f \n");
    for (var objectId = 1; objectId <= highestObjectId; objectId++)
    {
        WriteAscii(output, $"{offsets[objectId]:0000000000} 00000 n \n");
    }
    WriteAscii(output, $"trailer\n<< /Size {highestObjectId + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
    return output.ToArray();
}

static void WriteObject(Stream stream, IList<long> offsets, int objectId, string body)
{
    while (offsets.Count <= objectId)
    {
        offsets.Add(0);
    }
    offsets[objectId] = stream.Position;
    WriteAscii(stream, $"{objectId} 0 obj\n{body}\nendobj\n");
}

static PdfPageLayout FitImageToPage(PdfImagePage image)
{
    const double portraitWidth = 612;
    const double portraitHeight = 792;
    const double margin = 18;
    var landscape = image.Width > image.Height;
    var pageWidth = landscape ? portraitHeight : portraitWidth;
    var pageHeight = landscape ? portraitWidth : portraitHeight;
    var maxWidth = pageWidth - (margin * 2);
    var maxHeight = pageHeight - (margin * 2);
    var scale = Math.Min(maxWidth / image.Width, maxHeight / image.Height);
    var width = image.Width * scale;
    var height = image.Height * scale;

    return new PdfPageLayout(
        pageWidth,
        pageHeight,
        (pageWidth - width) / 2,
        (pageHeight - height) / 2,
        width,
        height);
}

static void WriteAscii(Stream stream, string value)
{
    var bytes = Encoding.ASCII.GetBytes(value);
    stream.Write(bytes);
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

sealed record PdfImagePage(byte[] JpegBytes, int Width, int Height);

sealed record PdfPageLayout(double PageWidth, double PageHeight, double X, double Y, double Width, double Height);
