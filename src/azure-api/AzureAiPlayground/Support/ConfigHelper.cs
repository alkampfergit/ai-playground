using AzureAiPlayground.Pages.ViewModels;

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
            services.AddSingleton<DocumentsViewModel>();
        }
    }
}
