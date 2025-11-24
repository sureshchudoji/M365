using Newtonsoft.Json;
using RestSharp;
using System;
using System.IO;

namespace SeleniumTests
{
    public class AzureDevOpsBugCreator
    {
        private readonly string azureDevOpsUrl;
        private readonly string project;
        private readonly string personalAccessToken;
        private readonly string assignedTo;
        private readonly string defaultDescription;
        private readonly string defaultReproSteps;

        public AzureDevOpsBugCreator(string configPath = "appsettings.json")
        {
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Configuration file not found: {configPath}");

            var config = File.ReadAllText(configPath);
            dynamic settings = JsonConvert.DeserializeObject(config);

            azureDevOpsUrl = settings.AzureDevOpsUrl ?? throw new ArgumentNullException("AzureDevOpsUrl missing");
            project = settings.Project ?? throw new ArgumentNullException("Project missing");
            personalAccessToken = settings.PersonalAccessToken ?? throw new ArgumentNullException("PersonalAccessToken missing");
            assignedTo = settings.AssignedTo ?? "shahab@tecoholic.com";
            defaultDescription = settings.DefaultDescription ?? "Bug created automatically due to failed Selenium test.";
            defaultReproSteps = settings.DefaultReproSteps ?? "See attached logs for detailed error.";
        }

        /// <summary>
        /// Creates a bug in Azure DevOps. Returns bug id if creation was successful, otherwise throws exception.
        /// </summary>
        public int CreateBug(string bugTitle, string description = null, string reproSteps = null, string assignedToOverride = null)
        {
            if (string.IsNullOrWhiteSpace(bugTitle))
                throw new ArgumentException("Bug title cannot be empty.", nameof(bugTitle));

            var client = new RestClient($"{azureDevOpsUrl}/{project}/_apis/wit/workitems/$Bug?api-version=6.0");
            var request = new RestRequest(Method.Post);

            request.AddHeader("Content-Type", "application/json-patch+json");
            string authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{personalAccessToken}"));
            request.AddHeader("Authorization", $"Basic {authToken}");

            var bugData = new[]
            {
                new { op = "add", path = "/fields/System.Title", value = bugTitle },
                new { op = "add", path = "/fields/System.Description", value = description ?? defaultDescription },
                new { op = "add", path = "/fields/System.AssignedTo", value = assignedToOverride ?? assignedTo },
                new { op = "add", path = "/fields/Microsoft.VSTS.TCM.ReproSteps", value = reproSteps ?? defaultReproSteps }
            };

            request.AddParameter("application/json-patch+json", JsonConvert.SerializeObject(bugData), ParameterType.RequestBody);

            IRestResponse response;
            try
            {
                response = client.Execute(request);
            }
            catch (Exception ex)
            {
                throw new Exception("Exception occurred during API request.", ex);
            }

            if (response.IsSuccessful)
            {
                dynamic result = JsonConvert.DeserializeObject(response.Content);
                int bugId = result.id;
                Console.WriteLine($"Bug created successfully in Azure DevOps with ID: {bugId}");
                return bugId;
            }
            else
            {
                string errorInfo = !string.IsNullOrWhiteSpace(response.ErrorMessage) ? response.ErrorMessage : response.Content;
                throw new Exception($"Failed to create bug in Azure DevOps: {errorInfo}");
            }
        }
    }
}
