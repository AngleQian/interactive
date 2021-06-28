// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;

namespace Microsoft.DotNet.Interactive
{
    public static class NugetPackageCompletion
    {
        public static readonly HttpClient client = new HttpClient();

        public static async Task<IEnumerable<string>> GetNugetPackageSuggestionAsync(string rawInput)
        {
            string pattern = @".*#r (?<startingQuote>""?)nuget:(?<spacesAfterColon>\s*)(?<searchText>[^,""]*)(?<comma>,?)(?<spacesAfterComma>\s*)[^,""\s]*""?";
            var matchResultGroups = Regex.Match(rawInput, pattern, RegexOptions.IgnoreCase).Groups;

            var startingQuote = matchResultGroups["startingQuote"].Value;
            var spacesAfterColon = matchResultGroups["spacesAfterColon"].Value;
            var searchText = matchResultGroups["searchText"].Value;
            var isSpaceBeforeComma = searchText != searchText.TrimEnd();
            var comma = matchResultGroups["comma"].Value;
            var spacesAfterComma = matchResultGroups["spacesAfterComma"].Value;


            HttpClient client = new HttpClient();
            var response = await client.GetAsync(getSearchQueryURL(searchText.Trim()));
            var responseContent = await response.Content.ReadAsStringAsync();
            var packages = JsonSerializer.Deserialize<NugetPackageSearchQueryResponse>(responseContent).data;

            NugetPackageSearchQueryResponsePackage packageWithIdExactlyMatchingSearchText = packages
                .FirstOrDefault(pkg => pkg.id.Equals(searchText.Trim()));

            if (packageWithIdExactlyMatchingSearchText is not null)
            {
                return GetPackageVersionSuggestions(
                    packageWithIdExactlyMatchingSearchText,
                    startingQuote,
                    spacesAfterColon,
                    isSpaceBeforeComma,
                    comma,
                    spacesAfterComma);
            }
            else
            {
                return GetPackageIdAndVersionSuggestions(
                    packages,
                    startingQuote,
                    spacesAfterColon,
                    searchText.Contains(" "));
            }

        }

        private static IEnumerable<string> GetPackageIdAndVersionSuggestions(
            NugetPackageSearchQueryResponsePackage[] packages,
            string startingQuote,
            string spacesAfterColon,
            bool didSearchTextContainSpace)
        {
            var prefix = spacesAfterColon == "" && !didSearchTextContainSpace
                ? $"{startingQuote}nuget:{spacesAfterColon}"
                : "";
            var spaceAfterComma = spacesAfterColon == "" ? "" : " ";

            var idAndVersionSuggestions = new List<string>();
            foreach (var pkg in packages)
            {
                var version = GetLatestStableVersionOrLatestVersion(pkg.versions);
                idAndVersionSuggestions.Add($"{prefix}{pkg.id},{spaceAfterComma}{version}");
            }
            return idAndVersionSuggestions;
        }

        private static IEnumerable<string> GetPackageVersionSuggestions(
            NugetPackageSearchQueryResponsePackage package,
            string startingQuote,
            string spacesAfterColon,
            bool isSpaceBeforeComma,
            string comma,
            string spacesAfterComma)
        {
            string prefix = "";
            if (spacesAfterComma == "")
            {
                prefix = $",";
                if (!isSpaceBeforeComma)
                {
                    prefix = $"{package.id}{prefix}";
                    if (spacesAfterColon == "")
                    {
                        prefix = $"{startingQuote}nuget:{prefix}";
                    }
                }
            }

            var versionSuggestions = new List<string>();
            foreach (var ver in package.versions)
            {
                versionSuggestions.Add($"{prefix}{ver.version}");
            }
            return versionSuggestions;
        }

        private static string GetLatestStableVersionOrLatestVersion(
            NugetPackageSearchQueryResponseVersions[] versions)
        {
            return versions.LastOrDefault(ver => !ver.version.Contains("-"))?.version
                ?? versions.LastOrDefault()?.version;
        }

        private static string getSearchQueryURL(string searchText)
        {
            return $"https://azuresearch-usnc.nuget.org/query?q={searchText}&prerelease=true&semVerLevel=2.0.0";
        }

        private class NugetPackageSearchQueryResponse
        {
            public int totalHits { get; set; }
            public NugetPackageSearchQueryResponsePackage[] data { get; set; }
        }

        private class NugetPackageSearchQueryResponsePackage
        {
            public string id { get; set; }
            public string version { get; set; }
            public string description { get; set; }
            public NugetPackageSearchQueryResponseVersions[] versions { get; set; }
        }
        private class NugetPackageSearchQueryResponseVersions
        {
            public string version { get; set; }
            public string id { get; set; }
        }
    }
}
