using Microsoft.Extensions.Configuration;

namespace SmartDocScan.Api.Data;

internal static class DatabaseSchemaOptions
{
    public static bool AutoEnsureSchema(IConfiguration configuration)
    {
        return configuration.GetValue<bool>("Database:AutoEnsureSchema");
    }
}
