﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Raveshmand.Localization.Core;
using Raveshmand.Localization.EntityFramework.Extentions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Raveshmand.Localization.EntityFramework
{
    public class EFLocalizationCrud<TContext> : ILocalizerCrud
        where TContext : DbContext
    {
        private readonly CultureInfo _culture;
        private readonly IServiceProvider _resolver;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _distributedCache;
        private readonly LocalizationOptions _options;

        public EFLocalizationCrud(IServiceProvider resolver,
            IMemoryCache memoryCache,
            IDistributedCache distributedCache,
            IOptions<LocalizationOptions> options)
        {
            _culture = CultureInfo.CurrentUICulture;
            _resolver = resolver ?? throw new ArgumentException(nameof(resolver));
            _options = options.Value;

            if (_options.CacheDependency == CacheOption.IMemoryCache)
            {
                _memoryCache = memoryCache ?? throw new ArgumentException(nameof(memoryCache));
            }
            else
            {
                _distributedCache = distributedCache ?? throw new ArgumentException(nameof(distributedCache));
            }
        }

        public void Delete(string name, string cultureName, string resourceName)
        {
            var resource = string.IsNullOrEmpty(resourceName) ? nameof(LocalizationResourceNames.SharedResource) : resourceName;

            string computedKey = string.Format(DefaultConfiguration.LocalizationCacheKeyTemplate, CultureInfo.CurrentUICulture.Name, resource, name);

            bool isSuccess = _resolver.RunScopedService<bool, TContext>(context =>
            {
                LocalizationRecord entity = context.Set<LocalizationRecord>()
                .FirstOrDefault(a => a.Name == name && a.CultureName == cultureName
                    && a.ResourceName == computedKey);

                if (entity != null)
                {
                    context.Set<LocalizationRecord>().Remove(entity);
                    context.SaveChanges();
                    return true;
                }

                return false;
            });

            if (isSuccess)
            {
                if (_options.CacheDependency == CacheOption.IMemoryCache)
                {
                    _memoryCache.Remove(computedKey);
                }
                else
                {
                    _distributedCache.Remove(computedKey);
                }
            }

        }

        public void Delete(IEnumerable<string> names, string cultureName, string resourceName)
        {
            var resource = string.IsNullOrEmpty(resourceName) ? nameof(LocalizationResourceNames.SharedResource) : resourceName;

            List<string> isSuccess = new List<string>();
            _resolver.RunScopedService<TContext>(context =>
            {
                foreach (string item in names)
                {
                    string computedKey = string.Format(DefaultConfiguration.LocalizationCacheKeyTemplate, CultureInfo.CurrentUICulture.Name, resource, item);

                    LocalizationRecord entity = context.Set<LocalizationRecord>()
                        .FirstOrDefault(a => a.Name == item && a.CultureName == cultureName
                            && a.ResourceName == computedKey);

                    if (entity != null)
                    {
                        context.Set<LocalizationRecord>().Remove(entity);
                        isSuccess.Add(computedKey);
                    }
                }

                context.SaveChanges();
            });

            if (_options.CacheDependency == CacheOption.IMemoryCache)
            {
                foreach (string item in isSuccess)
                {
                    _memoryCache.Remove(item);
                }
            }
            else
            {
                foreach (string item in isSuccess)
                {
                    _distributedCache.Remove(item);
                }
            }
        }

        public string ExportJson(string cultureName, string resourceName)
        {
            var resource = string.IsNullOrEmpty(resourceName) ? nameof(LocalizationResourceNames.SharedResource) : resourceName;
            string computedKey = string.Format(DefaultConfiguration.LocalizationPathTemplate, CultureInfo.CurrentUICulture.Name, resource);

            List<LocalizationRecord> records = new List<LocalizationRecord>();
            _resolver.RunScopedService<TContext>(context =>
            {
                records = context.Set<LocalizationRecord>().Where(e => e.ResourceName.Contains(computedKey)).ToList();
            });

            return Newtonsoft.Json.JsonConvert.SerializeObject(records);
        }

        public string ExportXml(string cultureName, string resourceName)
        {
            var resource = string.IsNullOrEmpty(resourceName) ? nameof(LocalizationResourceNames.SharedResource) : resourceName;
            string computedKey = string.Format(DefaultConfiguration.LocalizationPathTemplate, CultureInfo.CurrentUICulture.Name, resource);

            List<LocalizationRecord> records = new List<LocalizationRecord>();
            _resolver.RunScopedService<TContext>(context =>
            {
                records = context.Set<LocalizationRecord>().Where(e => e.ResourceName.Contains(computedKey)).ToList();
            });
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<LocalizationRecord>));
            MemoryStream memoryStream = new MemoryStream();
            XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8)
            {
                Formatting = Formatting.Indented
            };

            xmlSerializer.Serialize(xmlTextWriter, records);

            string output = Encoding.UTF8.GetString(memoryStream.ToArray());
            string _byteOrderMarkUtf8 = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());
            if (output.StartsWith(_byteOrderMarkUtf8, StringComparison.Ordinal))
            {
                output = output.Remove(0, _byteOrderMarkUtf8.Length);
            }

            return output;
        }

        public void Insert(string name, string value, string cultureName, string resourceName)
        {
            var resource = string.IsNullOrEmpty(resourceName) ? nameof(LocalizationResourceNames.SharedResource) : resourceName;

            string computedKey = string.Format(DefaultConfiguration.LocalizationCacheKeyTemplate, CultureInfo.CurrentUICulture.Name, resource, name);

            bool isSuccess = _resolver.RunScopedService<bool, TContext>(context =>
            {
                LocalizationRecord entity = context.Set<LocalizationRecord>()
                .FirstOrDefault(a => a.Name == name && a.CultureName == cultureName
                    && a.ResourceName == computedKey);

                if (entity == null)
                {
                    entity = new LocalizationRecord
                    {
                        Name = name,
                        Value = value,
                        CultureName = cultureName,
                        ResourceName = computedKey,
                    };
                    context.Set<LocalizationRecord>().Add(entity);
                    context.SaveChanges();
                    return true;
                }
                else
                {
                    Update(name, value, cultureName, resourceName);
                }

                return false;
            });

            if (isSuccess)
            {
                if (_options.CacheDependency == CacheOption.IMemoryCache)
                {
                    _memoryCache.Set(computedKey, value);
                }
                else
                {
                    _distributedCache.SetString(computedKey, value);
                }
            }
        }


        public void Insert(IEnumerable<KeyValuePair<string, string>> keyValue, string cultureName, string resourceName)
        {
            var resource = string.IsNullOrEmpty(resourceName) ? nameof(LocalizationResourceNames.SharedResource) : resourceName;

            List<KeyValuePair<string, string>> isSuccess = new List<KeyValuePair<string, string>>();
            _resolver.RunScopedService<TContext>(context =>
            {
                foreach (KeyValuePair<string, string> item in keyValue)
                {
                    string computedKey = string.Format(DefaultConfiguration.LocalizationCacheKeyTemplate, CultureInfo.CurrentUICulture.Name, resource, item.Key);

                    LocalizationRecord entity = context.Set<LocalizationRecord>()
                        .FirstOrDefault(a => a.Name == item.Key && a.CultureName == cultureName
                            && a.ResourceName == computedKey);

                    if (entity == null)
                    {
                        entity = new LocalizationRecord
                        {
                            Name = item.Key,
                            Value = item.Value,
                            CultureName = cultureName,
                            ResourceName = computedKey,
                        };
                        context.Set<LocalizationRecord>().Add(entity);
                        isSuccess.Add(new KeyValuePair<string, string>(computedKey, item.Value));
                    }
                }

                context.SaveChanges();
            });

            if (_options.CacheDependency == CacheOption.IMemoryCache)
            {
                foreach (KeyValuePair<string, string> item in isSuccess)
                {
                    _memoryCache.Set(item.Key, item.Value);
                }
            }
            else
            {
                foreach (KeyValuePair<string, string> item in isSuccess)
                {
                    _distributedCache.SetString(item.Key, item.Value);
                }
            }
        }

        public void Update(string name, string value, string cultureName, string resourceName)
        {
            var resource = string.IsNullOrEmpty(resourceName) ? nameof(LocalizationResourceNames.SharedResource) : resourceName;

            string computedKey = string.Format(DefaultConfiguration.LocalizationCacheKeyTemplate, CultureInfo.CurrentUICulture.Name, resource, name);

            var isSuccess = _resolver.RunScopedService<bool, TContext>(context =>
            {
                LocalizationRecord entity = context.Set<LocalizationRecord>()
                    .FirstOrDefault(a => a.Name == name && a.CultureName == cultureName
                        && a.ResourceName == computedKey);

                if (entity != null)
                {
                    entity.Value = value;

                    context.Set<LocalizationRecord>().Update(entity);
                    context.SaveChanges();
                    return true;
                }

                return false;
            });

            if (isSuccess)
            {
                if (_options.CacheDependency == CacheOption.IMemoryCache)
                {
                    _memoryCache.Set(computedKey, value);
                }
                else
                {
                    _distributedCache.SetString(computedKey, value);
                }
            }
        }

        public void Update(IEnumerable<KeyValuePair<string, string>> keyValue, string cultureName, string resourceName)
        {
            var resource = string.IsNullOrEmpty(resourceName) ? nameof(LocalizationResourceNames.SharedResource) : resourceName;

            List<KeyValuePair<string, string>> isSuccess = new List<KeyValuePair<string, string>>();
            _resolver.RunScopedService<TContext>(context =>
            {
                foreach (KeyValuePair<string, string> item in keyValue)
                {
                    string computedKey = string.Format(DefaultConfiguration.LocalizationCacheKeyTemplate, CultureInfo.CurrentUICulture.Name, resource, item.Key);

                    LocalizationRecord entity = context.Set<LocalizationRecord>()
                        .FirstOrDefault(a => a.Name == item.Key && a.CultureName == cultureName
                            && a.ResourceName == computedKey);

                    if (entity == null)
                    {
                        entity.Value = item.Value;

                        context.Set<LocalizationRecord>().Update(entity);
                        isSuccess.Add(new KeyValuePair<string, string>(computedKey, item.Value));
                    }
                }

                context.SaveChanges();
            });

            if (_options.CacheDependency == CacheOption.IMemoryCache)
            {
                foreach (KeyValuePair<string, string> item in isSuccess)
                {
                    _memoryCache.Set(item.Key, item.Value);
                }
            }
            else
            {
                foreach (KeyValuePair<string, string> item in isSuccess)
                {
                    _distributedCache.SetString(item.Key, item.Value);
                }
            }
        }
    }
}
