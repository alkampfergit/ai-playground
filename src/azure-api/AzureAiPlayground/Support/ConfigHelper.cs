using AzureAiLibrary.Configuration;
using AzureAiLibrary.Helpers;
using AzureAiPlayground.Pages.ViewModels;
using AzureAiPlayground.SemanticKernel.plugins.AudioVideoPlugin;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.SemanticKernel;

namespace AzureAiPlayground.Support;

public static class ConfigHelper
{
    public static T ConfigureSetting<T>(this IServiceCollection services, IConfiguration configuration, string section)
        where T : class, new()
    {
        services.Configure<T>(configuration.GetSection(section));
        var setting = new T();
        configuration.Bind(section, setting);
        return setting;
    }

    public static void ConfigureDocumentsSection(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ExploreDocumentViewModel>();
        services.AddSingleton<DocumentsViewModel>();
    }

    public static void ConfigureOverrideFile(this IConfigurationBuilder configuration)
    {
        var overrideFile = FoundOverrideFile();
        if (overrideFile != null)
        {
            configuration.AddJsonFile(overrideFile, optional: true, reloadOnChange: true);
        }
    }

    public static string? FoundOverrideFile()
    {
        var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        while (currentDirectory != null)
        {
            var overrideFile = Path.Combine(currentDirectory, "azure-ai.json");
            if (File.Exists(overrideFile))
            {
                return overrideFile;
            }

            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }

        return null;
    }
    
    public static void ConfigureSemanticKernel(
        this IServiceCollection services,
        AzureOpenAiConfiguration configuration,
        DumpLoggingProvider loggingProvider)
    {
        var kernelBuilder = services.AddKernel();
        kernelBuilder.Services.AddLogging(l => l
            .SetMinimumLevel(LogLevel.Trace)
            .AddConsole()
            .AddDebug()
            .AddProvider(loggingProvider)
        );

        kernelBuilder.Services.ConfigureHttpClientDefaults(c => c
            .AddLogger(s =>
                loggingProvider.CreateHttpRequestBodyLogger(
                    s.GetRequiredService<ILogger<DumpLoggingProvider>>())));

        var skConfig = configuration.GetSemanticKernelConfiguration();
        //need to grab information about what deployment we want to use for semantic kernel
        kernelBuilder.Services.AddAzureOpenAIChatCompletion(
            skConfig.DeploymentName,
            skConfig.BaseAddress,
            skConfig.GetApiKey());
        
        var pluginsDirectory = Path.Combine(
            System.IO.Directory.GetCurrentDirectory(), "SemanticKernel", "plugins", "PublishingPlugin");

        //now scan plugin directory and well known plugin
        kernelBuilder
            .Plugins
            .AddFromType<AudioVideoPlugin>("AudioVideoPlugin")
            .AddFromPromptDirectory(pluginsDirectory);
    }
}