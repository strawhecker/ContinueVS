using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of ILocalStorageService that manages persistent local storage.
    /// Stores key-value pairs in ~/.continueVS/localStorageCache.json with type safety and event notifications.
    /// </summary>
    public class LocalStorageService : ILocalStorageService
    {
        private readonly Dictionary<string, object?> _cache = new Dictionary<string, object?>();
        private readonly object _lock = new object();

        private static readonly string LocalStorageCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".continueVS");

        private static readonly string LocalStorageCachePath = Path.Combine(
            LocalStorageCacheDir,
            "localStorageCache.json");

        public event EventHandler<LocalStorageChangedEventArgs>? LocalStorageChanged;

        /// <summary>
        /// Initializes a new instance of LocalStorageService.
        /// </summary>
        public LocalStorageService()
        {
            LoadCacheFromDisk();
        }

        /// <summary>
        /// Stores a value in localStorage for the given key.
        /// </summary>
        public void SetItem<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            try
            {
                object? oldValue = null;
                lock (_lock)
                {
                    if (_cache.ContainsKey(key))
                    {
                        oldValue = _cache[key];
                    }

                    _cache[key] = value;
                    SaveCacheToDisk();
                }

                FireLocalStorageChanged(key, oldValue, value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalStorageService.SetItem error for key '{key}': {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a value from localStorage.
        /// </summary>
        public T? GetItem<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return default;
            }

            try
            {
                lock (_lock)
                {
                    if (!_cache.ContainsKey(key))
                    {
                        return default;
                    }

                    var cachedValue = _cache[key];
                    if (cachedValue == null)
                    {
                        return default;
                    }

                    // If the cached value is already of type T, return it directly
                    if (cachedValue is T typedValue)
                    {
                        return typedValue;
                    }

                    // Otherwise, deserialize from JSON
                    if (cachedValue is JToken jtoken)
                    {
                        return jtoken.ToObject<T>();
                    }

                    // Try to convert directly
                    return (T?)Convert.ChangeType(cachedValue, typeof(T));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalStorageService.GetItem error for key '{key}': {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Removes a value from localStorage.
        /// </summary>
        public void RemoveItem(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            try
            {
                object? oldValue = null;
                bool removed = false;

                lock (_lock)
                {
                    if (_cache.ContainsKey(key))
                    {
                        oldValue = _cache[key];
                        _cache.Remove(key);
                        removed = true;
                        SaveCacheToDisk();
                    }
                }

                if (removed)
                {
                    FireLocalStorageChanged(key, oldValue, null);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalStorageService.RemoveItem error for key '{key}': {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the cache from disk (localStorageCache.json) into memory.
        /// </summary>
        private void LoadCacheFromDisk()
        {
            try
            {
                lock (_lock)
                {
                    _cache.Clear();

                    if (!File.Exists(LocalStorageCachePath))
                    {
                        EnsureDirectoryExists();
                        return;
                    }

                    var json = File.ReadAllText(LocalStorageCachePath);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return;
                    }

                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    if (data != null)
                    {
                        foreach (var kvp in data)
                        {
                            _cache[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalStorageService.LoadCacheFromDisk error: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the in-memory cache to disk (localStorageCache.json).
        /// Must be called within the _lock.
        /// </summary>
        private void SaveCacheToDisk()
        {
            try
            {
                EnsureDirectoryExists();

                // Serialize cache to JSON
                var json = JsonConvert.SerializeObject(_cache, Formatting.Indented);
                File.WriteAllText(LocalStorageCachePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalStorageService.SaveCacheToDisk error: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensures the localStorage cache directory exists.
        /// </summary>
        private void EnsureDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(LocalStorageCacheDir))
                {
                    Directory.CreateDirectory(LocalStorageCacheDir);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalStorageService.EnsureDirectoryExists error: {ex.Message}");
            }
        }

        /// <summary>
        /// Fires the LocalStorageChanged event.
        /// </summary>
        private void FireLocalStorageChanged(string key, object? oldValue, object? newValue)
        {
            try
            {
                LocalStorageChanged?.Invoke(this, new LocalStorageChangedEventArgs
                {
                    Key = key,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalStorageService.FireLocalStorageChanged error: {ex.Message}");
            }
        }
    }
}
