// based on  https://stackoverflow.com/questions/63311322/how-to-get-list-of-all-projects-with-versions-in-the-azure-devops-server-from-wp
// base API docs here: https://docs.microsoft.com/en-us/rest/api/azure/devops/git/?view=azure-devops-rest-6.0

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace GetRepos
{
    class Program
    {
        const string _adoOrg = "(redacted)";
        const string _userName = "jmcpeak@microsoft.com";
        const string _personalAccessToken = "(redacted)";

        static void Main (string[] args)
        {
            // where we'll keep our results
            var orgRepoInfos = new List<RepoInfo>();

            var client = GetHttpClientForAdo();

            // get list of projects in the organization
            var projects = GetProjectsInOrganization(client);

            // get the repo infos for each project
            foreach (var project in projects)
            {
                Console.WriteLine($"Processing project {project}...");
                orgRepoInfos.AddRange(ProcessProject(project, client));
            }

            // write out results to CSV
            Console.WriteLine($"Writing results...");
            using (StreamWriter outputFile = new StreamWriter("repos.csv"))
            {
                outputFile.WriteLine("Project,Repo,Url,Size(KB),Last Commit User,Last Commit Date,Last Commit Comment");

                foreach (var ri in orgRepoInfos)
                {
                    var line = $"{ri.ProjectName},{ri.Name},{ri.Url},{ri.Size / 1024}";
                    if (ri.LastCommit != null)
                        line += $",{ri.LastCommit.User},{ri.LastCommit.Date},{ri.LastCommit.Comment}";
                    outputFile.WriteLine(line);
                }
            }

            // clean up
            if (client != null)
                client.Dispose();

            Console.WriteLine("Done.");
        }

        private static HttpClient GetHttpClientForAdo ()
        {
            // set up http client
            var client = new HttpClient();
            client.BaseAddress = new Uri(_adoOrg);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _personalAccessToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", _userName, _personalAccessToken))));
            return client;
        }

        private static List<string> GetProjectsInOrganization (HttpClient client)
        {
            dynamic data = CallRestAPI("_apis/projects", client);

            var projects = new List<string>();

            foreach (var item in data.value)
                projects.Add(item.name.Value);

            return projects;
        }

        private static List<RepoInfo> ProcessProject (string projectName, HttpClient client)
        {
            var projectRepoInfos = new List<RepoInfo>();

            // get the list of repos in the project

            dynamic data = CallRestAPI($"{projectName}/_apis/git/repositories", client);

            foreach (var repo in data.value)
            {
                var repoInfo = new RepoInfo
                {
                    ProjectName = projectName,
                    Name = repo.name.Value,
                    Url = repo.webUrl.Value,
                    Size = repo.size.Value,
                };

                repoInfo.LastCommit = GetLastCommitInRepo(repoInfo, client);

                projectRepoInfos.Add(repoInfo);

            }

            return projectRepoInfos;
        }

        private static CommitInfo GetLastCommitInRepo (RepoInfo repoInfo, HttpClient client)
        {
            // note - we assume the commits come back in date desc order!

            dynamic data = CallRestAPI($"{repoInfo.ProjectName}/_apis/git/repositories/{repoInfo.Name}/commits?$top=1", client);

            if (data.value.Count == 0)
                return null;

            dynamic lastCommit = data.value[0];

            var commitInfo = new CommitInfo
            {
                User = lastCommit.author.name.Value,
                Date = lastCommit.author.date.Value,
                Comment = lastCommit.comment.Value,
            };

            return commitInfo;

        }

        #region utilities

        private static dynamic CallRestAPI (string relativeUrl, HttpClient client)
        {
            var response = client.GetAsync(relativeUrl).Result;
            response.EnsureSuccessStatusCode();
            var responseContent = response.Content.ReadAsStringAsync().Result.ToString();
            dynamic data = JsonConvert.DeserializeObject(responseContent);
            return data;
        }

        #endregion
    }
}
