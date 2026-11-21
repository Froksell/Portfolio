
using ClimateRpiGateway.Configuration;
using ClimateRpiGateway.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("config.json", optional: false);

var options = builder.Configuration.Get<GatewayOptions>()
    ?? throw new InvalidOperationException("config.json saknas eller är tom.");

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<AzureService>();
builder.Services.AddHostedService<MqttService>();

await builder.Build().RunAsync();