﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DCS.Core.Configuration;
using DCS.Core.DataCollectors;
using DCS.Core.DataReaders;
using Octokit;
using OSIsoft.AF;
using OSIsoft.AF.Asset;
using OSIsoft.AF.Time;



namespace GitHubDataPlugin
{
    /// <summary>
    /// This data collector can be used to generate random data when you need to test data collection.
    /// It requires a database that contains elements based on an ElementTemplate with the following Attributes: 
    /// value:     the PI Point where the data will be written
    /// LowValue:  An integer static attribute that defines the lowest random value
    /// HighValue: An integer static attribute that defines the highest random value
    /// 
    /// APP Token for authentication
    /// <see cref="https://github.com/settings/developers"/>
    /// </summary>
    public class GitHubDataReader : BaseDataReader, IDataCollector
    {

        DataCollectorSettings _settings;


        public override List<AFValue> ReadValues(AFElement orgElement)
        {

            AFElement repoElement;

            //getting repository settings from the element:
            var owner = GetAttributeValue<string>(orgElement, "Owner");
            var gitHubCredentialToken = GetAttributeValue<string>(orgElement, "GitHubCredentialToken");
            var gitHubProductName = GetAttributeValue<string>(orgElement, "GitHubProductName");

            if (string.IsNullOrEmpty(gitHubCredentialToken) || string.IsNullOrEmpty(gitHubProductName))
                throw new GitHubDataCollectorHasInvalidConfiguration();

            var github = new GitHubClient(new ProductHeaderValue(gitHubProductName));
            github.Connection.Credentials = new Credentials(gitHubCredentialToken);

            // Checking the rate limit
            var rateLimit = github.Miscellaneous.GetRateLimits().Result.Resources.Core;
            if (rateLimit.Remaining <= 10)
            {
                Logger.WarnFormat("Current rate limit exceeded. Only {0} left. Will reset at:{1}", rateLimit.Remaining, rateLimit.Reset);
                return new List<AFValue>();
            }


            // Read values from GitHub
            var repos = github.Repository.GetAllForOrg(owner).Result;

            var values = new List<AFValue>();
            foreach (var repo in repos)
            {

                if (!orgElement.Elements.Contains(repo.Name))
                {
                    // todo - validation:
                    // check if element template exist
                    // check if repository was renamed ?? maybe, not certain how to do that.
                    repoElement = new AFElement(repo.Name, orgElement.Database.ElementTemplates["repository"]);
                    orgElement.Elements.Add(repoElement);
                    orgElement.CheckIn();
                    
                    var newTags=AFDataReference.CreateConfig(orgElement, true, (obj, afProgressEventArgs) =>
                        {
                           // here report progess
                        });
                    Logger.InfoFormat("{1} Tags Created for new element {0}", repoElement.Name,newTags);

                }
                else
                {
                    repoElement = orgElement.Elements[repo.Name];
                }

                // pull requests
                var pullRequests = github.PullRequest.GetAllForRepository(repo.Owner.Login, repo.Name);
                var pullRequestsCount = pullRequests.Result.Count;

                // commits
                var contributors = github.Repository.Statistics.GetContributors(repo.Owner.Login, repo.Name);
                var contributorsCount = contributors.Result.Count;
                var totalCommits = contributors.Result.ToList().Sum(contributor => contributor.Total);

                //Create AFValues based on the GitHub Readings      
                values.AddRange(new List<AFValue>()
                    {
                        new AFValue(repoElement.Attributes["Commits"], totalCommits, AFTime.Now),
                        new AFValue(repoElement.Attributes["Contributors"], contributorsCount, AFTime.Now),
                        new AFValue(repoElement.Attributes["Forks"], repo.ForksCount, AFTime.Now),
                        new AFValue(repoElement.Attributes["Name"], repo.Name, AFTime.Now),
                        new AFValue(repoElement.Attributes["Pull Requests"], pullRequestsCount, AFTime.Now),
                        new AFValue(repoElement.Attributes["Stars"], repo.StargazersCount, AFTime.Now),
                        new AFValue(repoElement.Attributes["Url"], repo.HtmlUrl, AFTime.Now),
                        new AFValue(repoElement.Attributes["UpdatedAt"], repo.UpdatedAt.LocalDateTime, AFTime.Now),
                        new AFValue(repoElement.Attributes["HasDownloads"], repo.HasDownloads, AFTime.Now),
                        new AFValue(repoElement.Attributes["HasIssues"], repo.HasIssues, AFTime.Now),
                        new AFValue(repoElement.Attributes["Open Issues"], repo.OpenIssuesCount, AFTime.Now),
                        new AFValue(repoElement.Attributes["HasWiki"], repo.HasWiki, AFTime.Now),
                        new AFValue(repoElement.Attributes["Watchers"], github.Activity.Watching.GetAllWatchers(owner,repo.Name).Result.Count, AFTime.Now),
                    }
                );
                if (GetAttributeValue<DateTime>(repoElement, "CreatedAt") <= new DateTime(1970, 1, 1))
                {
                    values.Add(new AFValue(repoElement.Attributes["CreatedAt"], repo.CreatedAt.LocalDateTime, AFTime.Now));
                }
            }

            var rateLimits = github.Miscellaneous.GetRateLimits().Result.Resources;
            Logger.InfoFormat("GitHub rate limits: Search:{0}, Core: {1}", rateLimits.Search.Remaining, rateLimits.Core.Remaining);

            return values;
        }

        /// <summary>
        /// Returns an attribute value, casted to a specific type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="element"></param>
        /// <param name="attributeName"></param>
        /// <returns></returns>
        private T GetAttributeValue<T>(AFElement element, string attributeName)
        {
            return (T)element.Attributes[attributeName].GetValue().Value;
        }

        public DataCollectorSettings GetSettings()
        {
            return _settings;
        }

        public void SetSettings(DataCollectorSettings settings)
        {
            _settings = settings;
            AfDatabaseName = _settings.AFDatabaseName;
            AfElementTemplateName = _settings.AFElementTemplateName;
            AfServerName = _settings.AFServerName;
        }



    }

    public class GitHubDataCollectorHasInvalidConfiguration : Exception
    {
        public GitHubDataCollectorHasInvalidConfiguration() : base("The collector could not initialize because the settings were not valid.") { }
    }
}
