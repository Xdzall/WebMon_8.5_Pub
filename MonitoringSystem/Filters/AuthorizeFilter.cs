using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MonitoringSystem.Filters
{
    /// <summary>
    /// Global authorization filter that enforces authentication for all pages except those explicitly allowed
    /// </summary>
    public class AuthorizeFilter : IAuthorizationFilter
    {   
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Check if the action or controller has AllowAnonymous attribute
            var endpoint = context.HttpContext.GetEndpoint();

            var allowAnonymous = endpoint?.Metadata?.GetMetadata<AllowAnonymousAttribute>() != null;

            if (allowAnonymous)
                return;

            // Check if user is authenticated
            if (!context.HttpContext.User.Identity.IsAuthenticated)
            {
                // Redirect to login page
                context.Result = new RedirectToPageResult("/Account/Login", new { area = "Identity" });
            }
        }
    }
}