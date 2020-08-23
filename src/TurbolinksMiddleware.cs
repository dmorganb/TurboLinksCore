using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace TurboLinksCore
{
    public static class TurboLinksBuilderExtension
    {
        public static IApplicationBuilder UseTurboLinks(this IApplicationBuilder app)
            => app.UseMiddleware<TurboLinksMiddleware>();

        private class TurboLinksMiddleware
        {
            private const string turboLinksLocationKey = "Turbolinks-Location";
            private static readonly int[] _redirectCodes = new int[] { 301, 302 };
            private readonly RequestDelegate _next;
            private readonly ITempDataDictionaryFactory _tempDataDictionaryFactory;

            public TurboLinksMiddleware(
                RequestDelegate next,
                ITempDataDictionaryFactory tempDataDictionaryFactory)
            {
                _next = next;
                _tempDataDictionaryFactory = tempDataDictionaryFactory ?? 
                    throw new ArgumentNullException(nameof(tempDataDictionaryFactory));
            }

            public async Task Invoke(HttpContext context)
            {
                context.Response.OnStarting(
                    state => AddTurboLinksHeader((HttpContext)state), context);
                await _next(context);
            }

            private Task AddTurboLinksHeader(HttpContext context)
            {
                var tempData = _tempDataDictionaryFactory.GetTempData(context);
                var response = context.Response;

                if (IsRedirect(response))
                {
                    tempData[turboLinksLocationKey] = (string)response.Headers["Location"];
                }
                else if (tempData.TryGetValue(turboLinksLocationKey, out var location))
                {
                    response.Headers.Add(turboLinksLocationKey, (string)location);
                }

                tempData.Save();

                return Task.CompletedTask;
            }

            private static bool IsRedirect(HttpResponse response) 
                => _redirectCodes.Contains(response.StatusCode);
        }
    }
}