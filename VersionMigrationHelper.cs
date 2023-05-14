using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CAL.CD.Listings.API.Web.Infrastructure.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyModel;

namespace CAL.CD.Listings.API.Web.Startup.Middleware
{
    public class VersionMigrationHelper: IVersionMigrationHelper
    {
        private readonly VersionMiddlewareConfigurationOptions _options;

        private static IEnumerable<IVersionMigration> _migrations;
        private static IHttpContextHelper _httpContextHelper;
        private static HttpContext _context;

        public VersionMigrationHelper(VersionMiddlewareConfigurationOptions options,
            HttpContext context)
        {
            _options = options;
            _httpContextHelper = new HttpContextHelper(context);
            _context = context;
        }

        public async Task ApplyUpMigrations()
        {
            await InitializeRequestBody(_context);

            ProcessMigrations(ApiMigrationDirection.Up);

            _context.Request.Body = await RewriteRequest();
        }

        public async Task ApplyDownMigrations(MemoryStream newBody)
        {
            await InitializeResponseBody(newBody);

            ProcessMigrations(ApiMigrationDirection.Down);
                
            var modifiedResponse = _httpContextHelper.GetResponseBody();
                
            await _context.Response.WriteAsync(modifiedResponse);
            _context.Response.ContentLength = _context.Response.Body.Length;
        }

        private static async Task InitializeRequestBody(HttpContext context)
        {
            var request = context.Request;
            var requestStream = request.Body;

            var content = await new StreamReader(requestStream).ReadToEndAsync();

            _httpContextHelper.SetRequestBody(content);
        }

        private void ProcessMigrations(ApiMigrationDirection direction)
        {
            Version.TryParse(_options.RequestedApiVersion(_httpContextHelper.GetHttpContext()), out var requestedVersion);

            var currentVersion = Version.Parse(_options.CurrentApiVersion);

            if (requestedVersion == null || currentVersion.Equals(requestedVersion)) return;

            var migrations = GetMigrations();

            ApplyMigrations(migrations, direction, requestedVersion);
        }

        private static async Task<Stream> RewriteRequest()
        {
            var json = _httpContextHelper.GetRequestBody();

            var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

            var stream = await requestContent.ReadAsStreamAsync();

            return stream;
        }

        private static async Task InitializeResponseBody(MemoryStream newBody)
        {
            newBody.Seek(0, SeekOrigin.Begin);
            var applicationResponseBody = await new StreamReader(newBody).ReadToEndAsync();
            newBody.Seek(0, SeekOrigin.Begin);

            _httpContextHelper.SetResponseBody(applicationResponseBody);
        }

        private static IEnumerable<IVersionMigration> GetMigrations()
        {
            if (_migrations != null) return _migrations;

            var typesFromAssemblies = GetAllTypesOf<IVersionMigration>();

            _migrations = typesFromAssemblies.Select(type => (IVersionMigration)Activator.CreateInstance(type))
                .ToList();

            _migrations.ToList().Sort((migration, versionMigration) =>
            {
                var v1 = migration.GetVersionTag();
                var v2 = versionMigration.GetVersionTag();

                return v1.CompareTo(v2);
            });

            return _migrations;
        }

        private static void ApplyMigrations(IEnumerable<IVersionMigration> migrations,
            ApiMigrationDirection direction,
            Version requestedVersion)
        {
            if (direction == ApiMigrationDirection.Up)
            {
                migrations = migrations.Reverse();
            }

            foreach (var migration in migrations)
            {
                if (!ShouldApply(requestedVersion, migration.GetVersionTag(), direction)) continue;

                if (direction == ApiMigrationDirection.Up)
                {
                    migration.Up(_httpContextHelper);
                }
                else
                {
                    migration.Down(_httpContextHelper);
                }
            }
        }

        private static IEnumerable<Type> GetAllTypesOf<T>()
        {
            var platform = Environment.OSVersion.Platform.ToString();
            var runtimeAssemblyNames = DependencyContext.Default.GetRuntimeAssemblyNames(platform);

            return runtimeAssemblyNames
                .Select(Assembly.Load)
                .SelectMany(a => a.ExportedTypes)
                .Where(t => t.IsClass && typeof(T).IsAssignableFrom(t));
        }

        private static bool ShouldApply(Version requestedVersion, Version migrationVersion,
            ApiMigrationDirection direction)
        {
            if (direction == ApiMigrationDirection.Up)
            {
                return migrationVersion.CompareTo(requestedVersion) >= 0;
            }

            return requestedVersion.CompareTo(migrationVersion) <= 0;
        }
    }
}