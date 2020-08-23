using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Net;
using System;

namespace TurboLinksCore.Tests
{
    public class TurboLinksMiddlewareTests
    {
        /// <summary>
        /// Sets up a test server (Microsoft.AspNetCore.TestHost) with the 
        /// TurboLinks middleware.
        /// Does a request that returns a redirect with Location header = "/" 
        /// and a cookie to keep the temp data. The next request, following 
        /// the redirect and with the cookie set, returns a 200 with the 
        /// "Turbolinks-Location" header as "/" and the temp data cookie expired
        /// </summary>
        [Fact]
        public async Task Redirect()
        {
            using var host = await TestHost(endpoints =>
            {
                endpoints.MapGet("/", Get);
                endpoints.MapGet("/redirect", RedirectTo("/"));
            });
            using var client = host.GetTestServer().CreateClient();

            // 1) Request to "/redirect" should redirect to "/" ...
            var redirect = await client.GetAsync("/redirect");
            Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
            var location = redirect.Headers.GetValues("Location").First();
            Assert.Equal("/", location);

            // ... and set a cookie for the tempData
            var cookie = redirect.Headers.GetValues("Set-Cookie").First();
            Assert.Contains(CookieTempDataProvider.CookieName, cookie);

            // 2) Request to "/" (Following the redirect) should return OK ...
            client.DefaultRequestHeaders.Add("cookie", cookie); // sets the cookie
            var response = await client.GetAsync(location);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // ... and include the turbolinks header
            var turboLinksHeader = response.Headers.GetValues("Turbolinks-Location").First();
            Assert.Equal("/", turboLinksHeader);

            // ... also, temp data cookie should be expired.
            var expiredCookie = response.Headers.GetValues("Set-Cookie").First();
            Assert.Contains(CookieTempDataProvider.CookieName, expiredCookie);
            Assert.Contains("expires=", expiredCookie);
        }

        // Creates a Host with TurboLinks middleware setup
        private static async Task<IHost> TestHost(Action<IEndpointRouteBuilder> endpoints)
        {
            return await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            // adds the ITempDataDictionaryFactory needed 
                            // by the turbolinks middleware
                            services.AddMvc();
                        })
                        .Configure(app =>
                        {
                            app.UseTurboLinks();
                            app.UseRouting();
                            app.UseEndpoints(endpoints);
                        });
                })
                .StartAsync();
        }

        private static async Task Get(HttpContext context)
            => await context.Response.WriteAsync("OK");

        private static RequestDelegate RedirectTo(string location)
            => context =>
            {
                context.Response.Redirect(location);
                return Task.CompletedTask;
            };
    }
}
