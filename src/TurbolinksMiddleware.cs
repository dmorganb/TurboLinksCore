using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace TurbolinksCore
{
    public static class TurboLinksBuilderExtension
    {
        public static IApplicationBuilder UseTurboLinks(this IApplicationBuilder app)
            => app.UseMiddleware<TurbolinksMiddleware>();

        private class TurbolinksMiddleware
        {
            private const string turbolinksLocationKey = "Turbolinks-Location";
            private static readonly int[] _redirectCodes = new int[] { 301, 302 };
            private readonly RequestDelegate _next;
            private readonly ITempDataDictionaryFactory _tempDataDictionaryFactory;

            public TurbolinksMiddleware(
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
                    state => AddTurbolinksHeader((HttpContext)state), context);
                await _next(context);
            }

            private Task AddTurbolinksHeader(HttpContext context)
            {
                var tempData = _tempDataDictionaryFactory.GetTempData(context);
                var response = context.Response;

                if (IsRedirect(response))
                {
                    tempData[turbolinksLocationKey] = (string)response.Headers["Location"];
                }
                else if (tempData.TryGetValue(turbolinksLocationKey, out var location))
                {
                    response.Headers.Add(turbolinksLocationKey, (string)location);
                }

                tempData.Save();

                return Task.CompletedTask;
            }

            private static bool IsRedirect(HttpResponse response) 
                => _redirectCodes.Contains(response.StatusCode);
        }
    }
}