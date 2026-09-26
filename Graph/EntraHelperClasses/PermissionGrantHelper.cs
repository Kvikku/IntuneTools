using Microsoft.Graph;

namespace IntuneTools.Graph.EntraHelperClasses
{
    /// <summary>
    /// Reads the delegated permission grant actually recorded for this app's own service
    /// principal in a tenant, so it can be diffed against the scopes the app currently requests
    /// — surfacing "unnecessary" permissions left over from a prior version of the tool, or
    /// consented more broadly than the app needs.
    /// </summary>
    public static class PermissionGrantHelper
    {
        private static readonly string[] NonPermissionScopes = { "openid", "offline_access", "profile", "email" };

        /// <summary>
        /// Returns every delegated Graph scope this tenant has actually consented to for the
        /// given app (client) registration, or null if that couldn't be determined (e.g. the
        /// app's service principal wasn't found, or the caller lacks Application.Read.All /
        /// DelegatedPermissionGrant.Read.All).
        /// </summary>
        public static async Task<HashSet<string>?> GetConsentedAppScopesAsync(GraphServiceClient graphServiceClient, string appClientId)
        {
            try
            {
                var servicePrincipals = await graphServiceClient.ServicePrincipals.GetAsync(rc =>
                {
                    rc.QueryParameters.Filter = $"appId eq '{appClientId}'";
                    rc.QueryParameters.Select = new[] { "id" };
                });

                var servicePrincipalId = servicePrincipals?.Value?.FirstOrDefault()?.Id;
                if (string.IsNullOrEmpty(servicePrincipalId))
                    return null;

                var grants = await graphServiceClient.Oauth2PermissionGrants.GetAsync(rc =>
                {
                    rc.QueryParameters.Filter = $"clientId eq '{servicePrincipalId}'";
                });

                var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (grants?.Value != null)
                {
                    foreach (var grant in grants.Value)
                    {
                        if (string.IsNullOrWhiteSpace(grant.Scope)) continue;
                        foreach (var scope in grant.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                            scopes.Add(scope);
                    }
                }

                return scopes;
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Could not read consented app permissions: {ex.Message}", appFunction.Main);
                return null;
            }
        }

        /// <summary>
        /// Filters consented scopes down to ones not present in the app's currently
        /// required set — permissions the tenant has granted that this version of the
        /// tool doesn't actually use.
        /// </summary>
        public static List<string> FindUnnecessaryScopes(IEnumerable<string> consentedScopes, IEnumerable<string> requiredScopes)
        {
            var required = new HashSet<string>(requiredScopes, StringComparer.OrdinalIgnoreCase);
            return consentedScopes
                .Where(s => !required.Contains(s) && !NonPermissionScopes.Contains(s, StringComparer.OrdinalIgnoreCase))
                .OrderBy(s => s)
                .ToList();
        }
    }
}
