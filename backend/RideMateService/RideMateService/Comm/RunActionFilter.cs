using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using RideMateService.Comm.Logger;

namespace RideMateService.Comm;

public class RunActionFilter : IActionFilter
{
    private readonly ILogService _logService;
    private readonly ILogger<RunActionFilter> _logger;
    private const string stopWatchKey = "_RunAction_stopWatch_";

    public RunActionFilter(ILogger<RunActionFilter> logger, ILogService logService)
    {
        this._logger = logger;
        this._logService = logService;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var sw = Stopwatch.StartNew();
        context.HttpContext.Items[stopWatchKey] = sw;

        _logger.LogInformation("Start : {Action}", context.ActionDescriptor.DisplayName);
        _logService.ConsoleMessage($"Start for action {context.ActionDescriptor.DisplayName}");
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.HttpContext.Items.TryGetValue(stopWatchKey, out var obj) && obj is Stopwatch sw)
        {
            sw.Stop();
            _logger.LogInformation("End : {Action} executed in {ElapsedMilliseconds} ms",
                context.ActionDescriptor.DisplayName, sw.ElapsedMilliseconds);
            _logService.ConsoleMessage($"End : {context.ActionDescriptor.DisplayName} executed in {sw.ElapsedMilliseconds} ms");
        }
        else
        {
            _logger.LogWarning("Stopwatch not found for action {Action}", context.ActionDescriptor.DisplayName);
            _logService.ConsoleWarning($"Stopwatch not found for action {context.ActionDescriptor.DisplayName}");
        }
    }
}
