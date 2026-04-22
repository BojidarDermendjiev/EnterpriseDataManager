using EnterpriseDataManager.WorkstationAgent;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkstationAgentOptions>(
    builder.Configuration.GetSection(WorkstationAgentOptions.SectionName));

builder.Services.AddHttpClient("EDMApi", (sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WorkstationAgentOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(opts.ApiKey))
        client.DefaultRequestHeaders.Add("X-Api-Key", opts.ApiKey);
});

builder.Services.AddHostedService<Worker>();

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "EDM Workstation Agent";
});

var host = builder.Build();
host.Run();
