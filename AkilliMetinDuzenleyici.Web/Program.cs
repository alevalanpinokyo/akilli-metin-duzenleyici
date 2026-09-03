using System;
using System.Net.Http;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using AkilliMetinDuzenleyici.Web;
using AkilliMetinDuzenleyici.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register Application Services
builder.Services.AddScoped<ITokenCounterService, TokenCounterService>();
builder.Services.AddScoped<ITextChunkerService, TextChunkerService>();
builder.Services.AddScoped<IGroqApiService, GroqApiService>();
builder.Services.AddScoped<IQuotaManagerService, WebQuotaManagerService>();
builder.Services.AddScoped<ISettingsService, WebSettingsService>();

await builder.Build().RunAsync();
