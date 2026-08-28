using Microsoft.AspNetCore.WebUtilities;
using System.Buffers.Text;

namespace BookLibrary.Infrastructure.Services.External
{
    public class OpenLibraryService
    {
        private HttpClient _httpClient;

        // External endpoints, note lack of / at beginning!
        private const string _worksSearchAPIPath = "search.json";

        // API Query parameters names
        private const string _queryParameterSearch = "q";

        public OpenLibraryService(HttpClient httpClient) 
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Queries the Open Library Search API directly for a specific search term, returning the results
        /// or empty if not found.
        /// </summary>
        /// <param name="searchStr"></param>
        /// <returns></returns>
        public async Task<string> SearchWorks(string searchStr)
        {
            var searchQueryParams = new Dictionary<string, string?>
            {
                { _queryParameterSearch, searchStr }
            };

            var uri = BuildBaseUrlFromBaseAddress(_worksSearchAPIPath);
            var queryStrWithParams = QueryHelpers.AddQueryString(uri.ToString(), searchQueryParams);

            try
            {
                var response = await _httpClient.GetAsync(queryStrWithParams);

                if (response != null && response.IsSuccessStatusCode)
                {
                    var responseStr = await response.Content.ReadAsStringAsync();

                    return responseStr;
                }
                else
                {
                    return "";
                }
            }
            catch (Exception)
            {
                // Todo later when logging is added, log this
                return "";
            }
        }

        #region Private Methods

        private Uri BuildBaseUrlFromBaseAddress(string additionalPath)
        {
            // Url should always be set, just in case
            if (_httpClient.BaseAddress == null)
                throw new InvalidDataException("Missing Base URL!");

            // Ensure we have a / at the end of base address
            Uri baseUri = new Uri(_httpClient.BaseAddress.ToString().TrimEnd('/') + "/");
            return new Uri(baseUri, additionalPath);
        }

        #endregion
    }
}
