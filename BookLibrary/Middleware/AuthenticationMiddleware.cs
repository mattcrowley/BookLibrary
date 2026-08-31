using BookLibrary.Api.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace BookLibrary.Api.Middleware
{
    public class ApiKeyAuthOptions : AuthenticationSchemeOptions { }
    public class AuthenticationMiddleware: AuthenticationHandler<ApiKeyAuthOptions>
    {
        private readonly string _appSecret;

        public AuthenticationMiddleware(
            IOptionsMonitor<ApiKeyAuthOptions> options, 
            ILoggerFactory logger, 
            UrlEncoder encoder, 
            IConfiguration config) 
            : base(options, logger, encoder)
        {
            _appSecret = config.GetValue<string>("AppAuthenticationSecret", "");
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(AppConstants.ApiKeyHeaderName, out var headerAPIKey))
            {
                return AuthenticateResult.NoResult();
            }

            if (!string.Equals(headerAPIKey,_appSecret))
            {
                return AuthenticateResult.Fail("Invalid API Key");
            }

            // Now, we are valid
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, ""),
                new Claim("AuthenticationType", "ApiKey")
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
    }
}
