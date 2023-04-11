using AzureAiLibrary;
using AzureAiLibrary.Configuration;
using AzureAiLibrary.Helpers;
using AzureAiPlayground.Pages.ViewModels;
using AzureAiPlayground.Support;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureSetting<ChatConfig>(builder.Configuration, "chatConfig");

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddMudMarkdownServices();

builder.Services.AddSingleton<FolderDatabaseFactory>();
builder.Services.AddSingleton<TemplateHelper>();
builder.Services.AddSingleton<ITemplateManager, DefaultTemplateManager>();
builder.Services.AddTransient<ChatViewModel>();

builder.Services.AddHttpClient("ChatClient", client =>
{
    client.BaseAddress = new Uri("https://alkopenai.openai.azure.com/");
    client.DefaultRequestHeaders.Add("api-key", Environment.GetEnvironmentVariable("AI_KEY"));
});

builder.Services.AddTransient(provider =>
{
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    return new ChatClient(httpClientFactory);
});

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