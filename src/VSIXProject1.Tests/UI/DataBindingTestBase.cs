#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using ContinueVS.Tests.Infrastructure;

namespace ContinueVS.Tests.UI
{
    /// <summary>
    /// Base class for data binding tests.
    /// Provides helpers to verify PropertyChanged and collection change notifications
    /// without requiring full WPF rendering or UI thread.
    /// </summary>
    public abstract class DataBindingTestBase : TestFixtureBase
    {
        /// <summary>
        /// Tracks PropertyChanged events on an INotifyPropertyChanged instance.
        /// </summary>
        protected class PropertyChangedTracker : IDisposable
        {
            private readonly INotifyPropertyChanged _source;
            private bool _disposed;

            public PropertyChangedTracker(INotifyPropertyChanged source)
            {
                _source = source ?? throw new ArgumentNullException(nameof(source));
                ChangedProperties = new List<string>();
                _source.PropertyChanged += OnPropertyChanged;
            }

            public List<string> ChangedProperties { get; }

            private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e?.PropertyName != null)
                {
                    ChangedProperties.Add(e.PropertyName);
                }
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _source.PropertyChanged -= OnPropertyChanged;
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// Tracks collection changes on an INotifyCollectionChanged collection.
        /// </summary>
        protected class CollectionChangeTracker : IDisposable
        {
            private readonly INotifyCollectionChanged _source;
            private bool _disposed;

            public CollectionChangeTracker(INotifyCollectionChanged source)
            {
                _source = source ?? throw new ArgumentNullException(nameof(source));
                Changes = new List<NotifyCollectionChangedEventArgs>();
                _source.CollectionChanged += OnCollectionChanged;
            }

            public List<NotifyCollectionChangedEventArgs> Changes { get; }

            private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                Changes.Add(e);
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _source.CollectionChanged -= OnCollectionChanged;
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// Asserts that a property changed event was fired with the given property name.
        /// </summary>
        protected void AssertPropertyChanged(PropertyChangedTracker tracker, string propertyName)
        {
            if (!tracker.ChangedProperties.Contains(propertyName))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Expected PropertyChanged for '{propertyName}', but it was not fired. " +
                    $"Fired properties: {string.Join(", ", tracker.ChangedProperties)}");
            }
        }

        /// <summary>
        /// Asserts that a property changed event was NOT fired with the given property name.
        /// </summary>
        protected void AssertPropertyNotChanged(PropertyChangedTracker tracker, string propertyName)
        {
            if (tracker.ChangedProperties.Contains(propertyName))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Expected PropertyChanged NOT to fire for '{propertyName}', but it did.");
            }
        }

        /// <summary>
        /// Asserts that a collection change was fired with the expected action.
        /// </summary>
        protected void AssertCollectionChanged(
            CollectionChangeTracker tracker,
            NotifyCollectionChangedAction expectedAction)
        {
            var matches = tracker.Changes.Where(c => c.Action == expectedAction).ToList();
            if (matches.Count == 0)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Expected CollectionChanged with action '{expectedAction}', but no such change was fired. " +
                    $"Fired actions: {string.Join(", ", tracker.Changes.Select(c => c.Action))}");
            }
        }

        /// <summary>
        /// Asserts that a collection Add change was fired.
        /// </summary>
        protected void AssertCollectionAdded(CollectionChangeTracker tracker, int count = 1)
        {
            var addChanges = tracker.Changes.Where(c => c.Action == NotifyCollectionChangedAction.Add).ToList();
            if (addChanges.Count != count)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Expected {count} Add change(s), but got {addChanges.Count}. " +
                    $"All changes: {string.Join(", ", tracker.Changes.Select(c => c.Action))}");
            }
        }

        /// <summary>
        /// Asserts that a collection Remove change was fired.
        /// </summary>
        protected void AssertCollectionRemoved(CollectionChangeTracker tracker, int count = 1)
        {
            var removeChanges = tracker.Changes.Where(c => c.Action == NotifyCollectionChangedAction.Remove).ToList();
            if (removeChanges.Count != count)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Expected {count} Remove change(s), but got {removeChanges.Count}. " +
                    $"All changes: {string.Join(", ", tracker.Changes.Select(c => c.Action))}");
            }
        }
    }
}
