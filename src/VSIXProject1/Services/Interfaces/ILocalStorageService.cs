using System;
using ContinueVS.Services.Events;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Interface for managing persistent local storage with type safety and custom events.
    /// Stores key-value pairs in ~/.continueVS/localStorageCache.json.
    /// </summary>
    public interface ILocalStorageService
    {
        /// <summary>
        /// Stores a value in localStorage for the given key.
        /// Fires LocalStorageChanged event when called.
        /// </summary>
        /// <typeparam name="T">Type of value to store.</typeparam>
        /// <param name="key">Storage key.</param>
        /// <param name="value">Value to store.</param>
        void SetItem<T>(string key, T value);

        /// <summary>
        /// Retrieves a value from localStorage.
        /// </summary>
        /// <typeparam name="T">Type of value to retrieve.</typeparam>
        /// <param name="key">Storage key.</param>
        /// <returns>The stored value, or null if key does not exist or deserialization fails.</returns>
        T? GetItem<T>(string key);

        /// <summary>
        /// Removes a value from localStorage.
        /// Fires LocalStorageChanged event when called.
        /// </summary>
        /// <param name="key">Storage key to remove.</param>
        void RemoveItem(string key);

        /// <summary>
        /// Event fired when localStorage values are changed via SetItem or RemoveItem.
        /// </summary>
        event EventHandler<LocalStorageChangedEventArgs>? LocalStorageChanged;
    }
}
