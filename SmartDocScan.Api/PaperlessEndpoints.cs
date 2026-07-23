using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Threading;
using SmartDocScan.Api.Models;
using SmartDocScan.Api.Data;
using Microsoft.Data.SqlClient;
using System;
using System.Linq;

namespace SmartDocScan.Api;

public static class PaperlessEndpoints
{
    public static void MapPaperlessEndpoints(this WebApplication app)
    {
        // Correspondents
        app.MapGet("/api/correspondents", async (int companyId, ClaimsPrincipal principal, CorrespondentRepository repository, CancellationToken cancellationToken) =>
        {
            if (!CanAccessCompany(principal, companyId)) return Results.Forbid();
            return Results.Ok(await repository.GetByCompanyAsync(companyId, cancellationToken));
        }).RequireAuthorization();

        app.MapPost("/api/correspondents", async (CorrespondentUpsertRequest request, ClaimsPrincipal principal, CorrespondentRepository repository, CancellationToken cancellationToken) =>
        {
            if (!CanAccessCompany(principal, request.CompanyId) || !CanManageBusiness(principal)) return Results.Forbid();
            try
            {
                var created = await repository.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/correspondents/{created.CorrespondentId}", created);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization();

        app.MapDelete("/api/correspondents/{id:int}", async (int id, int companyId, ClaimsPrincipal principal, CorrespondentRepository repository, CancellationToken cancellationToken) =>
        {
            if (!CanAccessCompany(principal, companyId) || !CanManageBusiness(principal)) return Results.Forbid();
            try
            {
                return await repository.DeleteAsync(id, companyId, cancellationToken) ? Results.NoContent() : Results.NotFound();
            }
            catch (SqlException)
            {
                return Results.Conflict(new { message = "Cannot be deleted because it is in use." });
            }
        }).RequireAuthorization();

        // Document Types
        app.MapGet("/api/document-types", async (int companyId, ClaimsPrincipal principal, DocumentTypeRepository repository, CancellationToken cancellationToken) =>
        {
            if (!CanAccessCompany(principal, companyId)) return Results.Forbid();
            return Results.Ok(await repository.GetByCompanyAsync(companyId, cancellationToken));
        }).RequireAuthorization();

        app.MapPost("/api/document-types", async (DocumentTypeUpsertRequest request, ClaimsPrincipal principal, DocumentTypeRepository repository, CancellationToken cancellationToken) =>
        {
            if (!CanAccessCompany(principal, request.CompanyId) || !CanManageBusiness(principal)) return Results.Forbid();
            try
            {
                var created = await repository.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/document-types/{created.DocumentTypeId}", created);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization();

        app.MapDelete("/api/document-types/{id:int}", async (int id, int companyId, ClaimsPrincipal principal, DocumentTypeRepository repository, CancellationToken cancellationToken) =>
        {
            if (!CanAccessCompany(principal, companyId) || !CanManageBusiness(principal)) return Results.Forbid();
            try
            {
                return await repository.DeleteAsync(id, companyId, cancellationToken) ? Results.NoContent() : Results.NotFound();
            }
            catch (SqlException)
            {
                return Results.Conflict(new { message = "Cannot be deleted because it is in use." });
            }
        }).RequireAuthorization();

        // Tags
        app.MapGet("/api/tags", async (int companyId, ClaimsPrincipal principal, TagRepository repository, CancellationToken cancellationToken) =>
        {
            if (!CanAccessCompany(principal, companyId)) return Results.Forbid();
            return Results.Ok(await repository.GetByCompanyAsync(companyId, cancellationToken));
        }).RequireAuthorization();

        app.MapPost("/api/tags", async (TagUpsertRequest request, ClaimsPrincipal principal, TagRepository repository, CancellationToken cancellationToken) =>
        {
            if (!CanAccessCompany(principal, request.CompanyId) || !CanManageBusiness(principal)) return Results.Forbid();
            try
            {
                var created = await repository.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/tags/{created.TagId}", created);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization();

        app.MapDelete("/api/tags/{id:int}", async (int id, int companyId, ClaimsPrincipal principal, TagRepository repository, CancellationToken cancellationToken) =>
        {
            if (!CanAccessCompany(principal, companyId) || !CanManageBusiness(principal)) return Results.Forbid();
            try
            {
                return await repository.DeleteAsync(id, companyId, cancellationToken) ? Results.NoContent() : Results.NotFound();
            }
            catch (SqlException)
            {
                return Results.Conflict(new { message = "Cannot be deleted because it is in use." });
            }
        }).RequireAuthorization();
    }

    private static bool CanAccessCompany(ClaimsPrincipal principal, int companyId)
    {
        if (principal.IsInRole("Admin")) return true;
        var accessible = principal.FindAll("CompanyId").Select(c => int.Parse(c.Value)).ToList();
        return accessible.Contains(companyId);
    }

    private static bool CanManageBusiness(ClaimsPrincipal principal)
    {
        return principal.IsInRole("Admin") || principal.IsInRole("BusinessAdmin");
    }
}
