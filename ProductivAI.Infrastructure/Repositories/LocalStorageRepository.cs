using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProductivAI.Infrastructure.Repositories
{
    public abstract class LocalStorageRepository<T> where T : class
    {
        protected readonly IJSRuntime _jsRuntime;
        protected readonly string _storageKey;

        public LocalStorageRepository(IJSRuntime jsRuntime, string storageKey)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _storageKey = storageKey ?? throw new ArgumentNullException(nameof(storageKey));
        }

        protected async Task<List<T>> GetItemsFromStorageAsync()
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", _storageKey);
            if (string.IsNullOrEmpty(json))
                return new List<T>();

            return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
        }

        protected async Task SaveItemsToStorageAsync(List<T> items)
        {
            var json = JsonSerializer.Serialize(items);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", _storageKey, json);
        }
    }
}