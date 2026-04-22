namespace EnterpriseDataManager.WorkstationAgent;

using System.Net.Http.Json;
using System.Text.Json;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class Worker : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<WorkstationAgentOptions> _options;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IHttpClientFactory httpClientFactory,
        IOptions<WorkstationAgentOptions> options,
        ILogger<Worker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;

        if (string.IsNullOrWhiteSpace(opts.Schedule))
        {
            _logger.LogWarning("No cron schedule configured. WorkstationAgent will not run.");
            return;
        }

        var cron = CronExpression.Parse(opts.Schedule, CronFormat.Standard);

        _logger.LogInformation("WorkstationAgent started. Schedule: {Schedule}", opts.Schedule);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var next = cron.GetNextOccurrence(now.UtcDateTime, TimeZoneInfo.Utc);

            if (next == null)
            {
                _logger.LogWarning("Cron expression produced no next occurrence. Exiting.");
                break;
            }

            var delay = next.Value - now.UtcDateTime;
            if (delay > TimeSpan.Zero)
            {
                _logger.LogInformation("Next archive job scheduled at {NextRun} (in {Delay})", next.Value, delay);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            await TriggerArchiveJobAsync(opts, stoppingToken);
        }

        _logger.LogInformation("WorkstationAgent stopped.");
    }

    private async Task TriggerArchiveJobAsync(WorkstationAgentOptions opts, CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(opts.ApiBaseUrl) || string.IsNullOrWhiteSpace(opts.SourcePath))
        {
            _logger.LogWarning("ApiBaseUrl or SourcePath not configured. Skipping job trigger.");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("EDMApi");
            var payload = new
            {
                sourcePath = opts.SourcePath,
                label = opts.Label,
                description = opts.Description
            };

            var response = await client.PostAsJsonAsync(
                $"{opts.ApiBaseUrl.TrimEnd('/')}/api/archive/jobs",
                payload,
                stoppingToken);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(stoppingToken);
                _logger.LogInformation("Archive job created successfully. Response: {Body}", body);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(stoppingToken);
                _logger.LogWarning("Archive job creation failed. Status: {Status}, Body: {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to trigger archive job against {ApiBaseUrl}", opts.ApiBaseUrl);
        }
    }
}
