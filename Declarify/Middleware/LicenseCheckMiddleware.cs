using Declarify.Services.API;

namespace Declarify.Middleware
{
    public class LicenseCheckMiddleware
    {
        private readonly RequestDelegate _next;
        private LicenseCheckResponse? _cachedLicense;
        private DateTime _lastCheck = DateTime.MinValue;
        private readonly TimeSpan _cacheTime = TimeSpan.FromMinutes(5);

        public LicenseCheckMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, CentralHubApiService centralHub)
        {
            // Skip for static files, setup, login page, etc.
            if (context.Request.Path.StartsWithSegments("/css") ||
                context.Request.Path.StartsWithSegments("/js") ||
                context.Request.Path.StartsWithSegments("/Home/TestPing") ||
                context.Request.Path.StartsWithSegments("/") ||
                context.Request.Path == "/Home/Activate" || context.Request.Path.StartsWithSegments("/Home/Activate") ||
                context.Request.Path.Value?.Contains("/Home/Login") == true)
            {
                await _next(context);
                return;
            }

            LicenseCheckResponse license;




            if (_cachedLicense != null && DateTime.UtcNow - _lastCheck < _cacheTime)
            {
                license = _cachedLicense;
            }
            else
            {
                try
                {
                    license = await centralHub.CheckLicenseAsync();
                    _cachedLicense = license;
                    _lastCheck = DateTime.UtcNow;
                }
                catch (HttpRequestException ex)
                {
                    // Network error or central is down
                    license = new LicenseCheckResponse
                    {
                        IsValid = false,
                        Message = "Cannot reach license server. Please check your internet connection."
                    };
                }
                catch (TaskCanceledException)
                {
                    license = new LicenseCheckResponse
                    {
                        IsValid = false,
                        Message = "License server timeout. Please try again later."
                    };
                }
                catch (Exception)
                {
                    license = new LicenseCheckResponse
                    {
                        IsValid = false,
                        Message = "Unexpected error checking license."
                    };
                }

                // Use cache as fallback if available
                if (_cachedLicense != null && license.Message.Contains("Cannot reach"))
                {
                    license.Message += " Using cached status.";
                    license.IsValid = _cachedLicense.IsValid;
                }

            }

            if (!license.IsValid)
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync($@"
                <html>
                    <head><title>License Invalid</title></head>
                    <body style='font-family: Arial; text-align: center; margin-top: 100px;'>
                        <h1>Access Denied</h1>
                        <h3>License Status: Invalid</h3>
                        <p>{System.Net.WebUtility.HtmlEncode(license.Message)}</p>
                        <p>Please contact support.</p>
                    </body>
                </html>");
                return;
            }

            // Optional: pass license info to views
            context.Items["LicenseInfo"] = license;

            await _next(context);
        }

    }
}
