using System;
using Raveshmand.Localization.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Raveshmand.Localization.Xml.Extentions
{
    public static class LocalizationExtentions
    {
        public static void AddXmlLocalization(this IServiceCollection services,
            Action<Core.LocalizationOptions> options)
        {
            services.Configure(options);
            services.Add(ServiceDescriptor.Singleton<IStringLocalizerFactory, XmlStringLocalizerFactory>());
            services.Add(ServiceDescriptor.Singleton<ILocalizerCrud, XmlLocalizationCrud>());
        }
    }
}
