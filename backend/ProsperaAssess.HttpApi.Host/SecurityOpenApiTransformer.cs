using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.RegularExpressions;
namespace ProsperaAssess.Api;

public sealed class SecurityOpenApiTransformer(IConfiguration configuration) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "Prospera Assess API";
        document.Info.Version = "v1";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "ASP.NET Core protected opaque token" };
        document.Components.SecuritySchemes["BrowserSession"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie, Name = "__Host-prospera-refresh", Description = "Secure HttpOnly browser session cookie in deployed environments." };
        document.Components.SecuritySchemes["MonitorKey"] = new OpenApiSecurityScheme {Type=SecuritySchemeType.ApiKey, In=ParameterLocation.Header, Name="X-Prospera-Monitor-Key", Description="Dedicated server-side monitoring credential; never use a browser token."};
        document.Info.Description = "Opaque bearer access tokens; deployed browser sessions also require their paired HttpOnly cookie. Login, refresh and logout validate the public Origin. No public registration.";
        foreach (var (path, item) in document.Paths)
        {
            foreach (var (method, operation) in item.Operations ?? [])
            {
                operation.OperationId = Regex.Replace(method + "_" + path, "[^a-zA-Z0-9]+", "_").Trim('_');
                var anonymous = path is "/login" or "/refresh" or "/api/health" or "/api/health/live" or "/api/sales/enquiries" or "/api/invitations/accept" or "/api/account/password/forgot" or "/api/account/password/reset" or "/api/account/logout";
                var requirement = new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] };
                if (configuration.GetValue("Authentication:UseRefreshCookies", false)) requirement[new OpenApiSecuritySchemeReference("BrowserSession", document)] = [];
                operation.Security = anonymous ? [] : [requirement];
                if (path == "/api/operations/metrics") operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("MonitorKey", document)] = [] }];
                if (path == "/refresh" && configuration.GetValue("Authentication:UseRefreshCookies", false)) operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("BrowserSession", document)] = [] }];
            }
        }
        return Task.CompletedTask;
    }
}
