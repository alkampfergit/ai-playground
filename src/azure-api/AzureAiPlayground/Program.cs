using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Documents;
using AzureAiLibrary.Documents.Jobs;
using AzureAiLibrary.Documents.Support;
using AzureAiLibrary.Helpers;
using AzureAiPlayground.Agents;
using AzureAiPlayground.Pages.ViewModels;
using AzureAiPlayground.Support;
using MudBlazor;
using MudBlazor.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var logConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("service", "AzureAiLibrary");

Log.Logger = logConfiguration.CreateLogger();

builder.Configuration.ConfigureOverrideFile();

var chatConfig = builder.Services.ConfigureSetting<ChatConfig>(builder.Configuration, "chatConfig");
var azureOpenAiConfiguration = builder.Services.ConfigureSetting<AzureOpenAiConfiguration>(builder.Configuration, "AzureOpenAiConfiguration");
var documentsConfiguration = builder.Services.ConfigureSetting<DocumentsConfig>(builder.Configuration, "Documents");
var jarvisConfiguration = builder.Services.ConfigureSetting<JarvisConfig>(builder.Configuration, "Jarvis");

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddMudMarkdownServices();

builder.Services.AddSingleton<FolderDatabaseFactory>();
builder.Services.AddSingleton<TemplateHelper>();
builder.Services.AddSingleton<PythonTokenizer>();
var esservice = new ElasticSearchService(new Uri(documentsConfiguration.ElasticUrl));
builder.Services.AddSingleton(esservice);
builder.Services.AddSingleton<ITemplateManager, DefaultTemplateManager>();
builder.Services.AddTransient<ChatViewModel>();
builder.Services.AddTransient<JarvisApiCaller>();

builder.Services.AddLogging(cfg => cfg.AddSerilog());

//Configure everything related to documents.
ConfigHelper.ConfigureDocumentsSection(builder.Services, builder.Configuration);

//loggerFactory.AddSerilog(); //TODO: Do not remmeber where to put this with new initialization
foreach (var config in azureOpenAiConfiguration.Endpoints)
{
    builder.Services.AddHttpClient(config.Name, client =>
    {
        client.BaseAddress = new Uri(config.BaseAddress);
        client.DefaultRequestHeaders.Add("api-key", Environment.GetEnvironmentVariable("AI_KEY"));
    });
}

builder.Services.AddHttpClient("python_embeddings", client =>
{
    client.BaseAddress = new Uri(documentsConfiguration.PythonTokenizerFlaskUrl);
});

builder.Services.AddHttpClient("jarvis", client =>
{
    client.BaseAddress = new Uri(jarvisConfiguration.BaseUrl);
    client.DefaultRequestHeaders.Add("jarvis-auth-token", jarvisConfiguration.AccessToken);
});

//Register all agents
builder.Services.AddSingleton<IAgent, ApplyRuleAgent>();

builder.Services.AddTransient<ChatClient>();
//builder.Services.AddTransient(provider =>
//{
//    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
//    return new ChatClient(httpClientFactory);
//});

//builder.Services.AddSwaggerGen(c =>
//{
//    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
//});

builder.Services.AddMvcCore();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

//app.UseSwagger();
//app.UseSwaggerUI(c =>
//{
//    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
//});

app.Run();
