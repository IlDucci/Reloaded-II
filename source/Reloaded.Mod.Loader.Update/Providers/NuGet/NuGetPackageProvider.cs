namespace Reloaded.Mod.Loader.Update.Providers.NuGet;

/// <summary>
/// Provider which gives back a list of downloadable mods using NuGet.
/// </summary>
public class NuGetPackageProvider : IDownloadablePackageProvider
{
    private readonly INugetRepository _repository;
    private readonly string? _appId;

    private List<IPackageSearchMetadata>? _itemsForThisAppId;

    /// <summary/>
    public NuGetPackageProvider(INugetRepository repository, string? appId = null)
    {
        _repository = repository;
        _appId = appId;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<IDownloadablePackage>> SearchAsync(string text, int skip = 0, int take = 50, CancellationToken token = default)
    {
        var searchResults = await GetSearchResults(text, skip, take, token);
        var result = new List<IDownloadablePackage>();
        foreach (var res in searchResults)
            result.Add(await WebDownloadablePackage.FromNuGetAsync(res, _repository, true, true));

        return result;
    }

    private async Task<IEnumerable<IPackageSearchMetadata>> GetSearchResults(string text, int skip, int take, CancellationToken token)
    {
        // No App Filter
        if (string.IsNullOrEmpty(_appId))
            return await _repository.Search(text, false, skip, take, token);

        // Otherwise filter by app.
        _itemsForThisAppId ??= await GetItemsForThisAppIdAsync(token);

        // Filter app results manually.
        const StringComparison stringComparison = StringComparison.OrdinalIgnoreCase;
        var result = new List<IPackageSearchMetadata>();

        for (var x = skip; x < _itemsForThisAppId.Count; x++)
        {
            var package = _itemsForThisAppId[x];
            var name = !string.IsNullOrEmpty(package.Title) ? package.Title : package.Identity.Id;
            
            // Filter in name.
            if (name.Contains(text, stringComparison))
                result.Add(package);

            // Check if we have enough.
            if (result.Count >= take)
                return result;
        }

        return result;
    }

    private async Task<List<IPackageSearchMetadata>> GetItemsForThisAppIdAsync(CancellationToken token)
    {
        // We are making an assumption that the server can search by tag.
        // This is true for our BaGet server and NuGet Gallery, however is not a guarantee.
        // On unsupported servers, this will probably return empty, which is acceptable.

        const int maxTake  = 1000;
        var allResults     = new List<IPackageSearchMetadata>();
        
        int currentSkip = 0;
        bool hasMoreItems;
        do
        {
            var result = await _repository.Search(_appId!, false, currentSkip, maxTake, default);

            // Add items
            var itemsBefore = allResults.Count;
            allResults.AddRange(result.Where(x => x.Tags.Contains(_appId!, StringComparison.OrdinalIgnoreCase)));
            hasMoreItems = allResults.Count > itemsBefore;
            
            // Next Page
            currentSkip += maxTake;
        } 
        while (hasMoreItems);

        return allResults;
    }
}