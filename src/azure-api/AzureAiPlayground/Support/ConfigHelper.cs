using AzureAiPlayground.Pages.ViewModels;
using Microsoft.Extensions.Configuration.Json;

namespace AzureAiPlayground.Support
{
    public static class ConfigHelper
    {
        public static T ConfigureSetting<T>(this IServiceCollection services, IConfiguration configuration, string section) where T : class, new()
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
    }
}
