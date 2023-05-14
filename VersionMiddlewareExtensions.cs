using System;
using CAL.CD.Listings.API.Web.Infrastructure.Versioning;
using Microsoft.AspNetCore.Builder;

namespace CAL.CD.Listings.API.Web.Startup.Middleware
{
    public static class VersionMiddlewareExtensions
    {
        public static IApplicationBuilder UseVersioning(this IApplicationBuilder builder,
            Action<VersionMiddlewareConfigurationOptions> options = null)
        {
            var opt = new VersionMiddlewareConfigurationOptions();
            options?.Invoke(opt);

            return builder.UseMiddleware<VersionMiddleware>(opt);
        }
    }
}