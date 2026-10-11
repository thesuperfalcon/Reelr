using System.Net.Http.Json;
using backend.Features.Paging;

namespace backend.Tests.Infrastructure;

// List endpoints answer one page at a time. Most tests only need the items of the first page.
public static class PageExtensions
{
    public static async Task<List<T>> GetItemsAsync<T>(this HttpClient client, string url) =>
        (await client.GetFromJsonAsync<PageDto<T>>(url))!.Items;

    public static async Task<List<T>> ReadItemsAsync<T>(this HttpContent content) =>
        (await content.ReadFromJsonAsync<PageDto<T>>())!.Items;
}
