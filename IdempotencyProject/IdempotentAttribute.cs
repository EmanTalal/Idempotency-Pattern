using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
using System.Net.NetworkInformation;

namespace IdempotencyProject
{
    public class IdempotentAttribute : ActionFilterAttribute
    {
        private const string IdempotencyKeyHeder = "X-Idempotency-Key";
        private const string InProgressStatus = "in-progress";

        private class ChachedResponse
        {
            public int StatusCode {  get; set; }
            public Object? Value { get; set; } 
        }
        //Will be exected with every action
        public override  async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.Request.Headers.TryGetValue(IdempotencyKeyHeder, out var dempotencyKey) || string.IsNullOrEmpty(dempotencyKey))
			{
                context.Result = new BadRequestObjectResult($"Missing or empty {IdempotencyKeyHeder} header");
                return;
			}
            var cachekey = $"idempotency: {dempotencyKey}";
            var cache = context.HttpContext.RequestServices.GetService<IDistributedCache>();
            var cacheResponseJson = await cache.GetStringAsync(cachekey);
            if (!string.IsNullOrEmpty(cacheResponseJson))
            {
                if (cacheResponseJson == InProgressStatus)
                {
                    context.Result = new StatusCodeResult(StatusCodes.Status409Conflict);
                    return;
                }
                var cachedResponse = System.Text.Json.JsonSerializer.Deserialize<ChachedResponse>(cacheResponseJson);
                if (cachedResponse != null) 
                {
                    context.Result = new ObjectResult(cachedResponse.Value)
                    {
                        StatusCode=cachedResponse.StatusCode,
                    };
                    return;
                }
            }
            else
            {
                //Make the request In-progress To Prevent duplicated processing 
                await cache.SetStringAsync(cachekey, InProgressStatus, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
                });
                var executedContext = await next();
                if (executedContext.Result is ObjectResult objectResult)
                    if(objectResult.StatusCode >= 200 && objectResult.StatusCode < 300)
                    {


                        {
                            var responseToCache = new ChachedResponse
                            {
                                StatusCode = objectResult.StatusCode ?? 200,
                                Value = objectResult.Value
                            };
                            var responseJson = System.Text.Json.JsonSerializer.Serialize(responseToCache);
                            await cache.SetStringAsync(cachekey, responseJson, new DistributedCacheEntryOptions
                            {
                                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                            });
                        }
                    }
                    else
                    {
                        await cache.RemoveAsync(cachekey);
                    }
            }
        }
            
    }
}
