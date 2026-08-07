using Microsoft.AspNetCore.Http;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers.Http;
using TCFModSync.Server.Sync;
using TCFModSync.Shared.Json;
using TCFModSync.Shared.Logging;
using TCFModSync.Shared.Models;

namespace TCFModSync.Server.Http;

[Injectable(InjectionType = InjectionType.Singleton, TypePriority = OnLoadOrder.PreSptModLoader)]
public class SptRouteListener(ISptLogger<SptRouteListener> logger) : IHttpListener
{
    public const string RoutePrefix = "/tcfmodsync";
    public const string RouteManifest = "/manifest";
    public const string RouteFile = "/file";

    public static SyncRequestHandler? Handler { get; set; }

    public bool CanHandle(MongoId sessionId, HttpContext context)
    {
        return context.Request.Path.StartsWithSegments(RoutePrefix, StringComparison.OrdinalIgnoreCase);
    }

    public async Task Handle(MongoId sessionId, HttpContext context)
    {
        if (Handler == null)
        {
            const string message = "[TCF-ModSync] Request received but the mod did not start successfully; " +
                                    "check the server log for a startup error.";
            logger.Warning(message);
            FileLog.Current?.Write($"WARN: {message}");
            context.Response.StatusCode = 503;
            return;
        }

        try
        {
            var path = context.Request.Path.Value ?? string.Empty;

            if (path.Equals($"{RoutePrefix}{RouteManifest}", StringComparison.OrdinalIgnoreCase))
            {
                await HandleManifestAsync(context);
            }
            else if (path.StartsWith($"{RoutePrefix}{RouteFile}/", StringComparison.OrdinalIgnoreCase))
            {
                await HandleFileAsync(context, path);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        }
        catch (Exception ex)
        {
            var message = $"[TCF-ModSync] Error handling {context.Request.Path}: {ex}";
            logger.Error(message);
            FileLog.Current?.Write($"ERROR: {message}");
            context.Response.StatusCode = 500;
        }
    }

    private async Task HandleManifestAsync(HttpContext context)
    {
        var headless = string.Equals(context.Request.Query["headless"], "true", StringComparison.OrdinalIgnoreCase);
        var manifest = Handler!.BuildManifest(headless);

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(manifest, JsonDefaults.Options),
            context.RequestAborted);
    }

    private async Task HandleFileAsync(HttpContext context, string path)
    {
        var encoded = path.Substring($"{RoutePrefix}{RouteFile}/".Length);
        var relativePath = Uri.UnescapeDataString(encoded);

        if (!Handler!.TryResolveSafePath(relativePath, out var absolutePath))
        {
            var message = $"[TCF-ModSync] Refused request for '{relativePath}' - outside the served root.";
            logger.Warning(message);
            FileLog.Current?.Write($"WARN: {message}");
            context.Response.StatusCode = 403;
            return;
        }

        var info = new FileInfo(absolutePath);
        if (!info.Exists)
        {
            context.Response.StatusCode = 404;
            return;
        }

        context.Response.ContentType = "application/octet-stream";
        context.Response.ContentLength = info.Length;
        context.Response.StatusCode = 200;

        await context.Response.SendFileAsync(info.FullName, 0, null, context.RequestAborted);
    }
}
