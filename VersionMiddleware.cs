using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CAL.CD.Listings.API.Web.Infrastructure.Versioning;
using Microsoft.AspNetCore.Http;

namespace CAL.CD.Listings.API.Web.Startup.Middleware
{
    public class VersionMiddleware
    {
        private readonly RequestDelegate _next;

        private readonly VersionMiddlewareConfigurationOptions _options;

        private static IEnumerable<IVersionMigration> _migrations;

        public VersionMiddleware(
            RequestDelegate next,
            VersionMiddlewareConfigurationOptions options
        )
        {
            _next = next;
            _options = options;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var migrationHelper = new VersionMigrationHelper(_options, context);
            var existingBody = context.Response.Body;

            await using var newBody = new MemoryStream();
            context.Response.Body = newBody;

            migrationHelper.ApplyUpMigrations();

            await _next(context).ConfigureAwait(false);

            context.Response.Body = existingBody;

            migrationHelper.ApplyDownMigrations(newBody);
        }
    }
}