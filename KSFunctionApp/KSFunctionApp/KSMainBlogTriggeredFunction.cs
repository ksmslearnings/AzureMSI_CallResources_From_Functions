using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Microsoft.Azure.Services.AppAuthentication;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage;
using System.Net.Http;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace KSFunctionApp
{
    public static class KSMainBlogTriggeredFunction
    {
        static ILogger logger = null;

        [FunctionName("KSMainBlogTriggeredFunction")]
        public static void Run([BlobTrigger("samplecontainer/{name}", Connection = "AzureWebJobsStorage")]Stream myBlob, string name, ILogger log)
        {
            logger = log;

            log.LogInformation("Function Started");

            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");

            var storageAcctName = Environment.GetEnvironmentVariable("TobeReadStorageAccountName");
            var containerName = Environment.GetEnvironmentVariable("TobeReadContainerName");

            log.LogInformation($"Calling Another Storage Account {storageAcctName} to read a blob file in container {containerName}");

            log.LogInformation($"Creating token");
            // AzureServiceTokenProvider tokenProvide = new AzureServiceTokenProvider();
            var token = GetAccessTokenAsync().Result;

            log.LogInformation($"Creating token completed");
            var items = GetBlobsWithSdk(token);

            logger.LogInformation($"method successfullt read all the blogs");
            foreach (var item in items)
            {
                logger.LogInformation($"Blog item URL is : {item.Uri.AbsoluteUri}");
            }

            logger.LogInformation("Calling Web API with Auth Header");
            CallWebAPIAppWithAndWithoutAuthHeader(true);

            logger.LogInformation("Calling Web API without Auth Header");
            CallWebAPIAppWithAndWithoutAuthHeader(false);


            log.LogInformation("Function Ended");

            //using Microsoft.Azure.Services.AppAuthentication;
            //using Microsoft.Azure.KeyVault;
            //// ...
            //var azureServiceTokenProvider = new AzureServiceTokenProvider();
            //string accessToken = await azureServiceTokenProvider.GetAccessTokenAsync("hhttps://vault.azure.net");
            //// OR
            //var kv = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
        }

        private static async void CallWebAPIAppWithAndWithoutAuthHeader(bool headerPassed)
        {
            try
            {
                logger.LogInformation("Calling HttpClinet to Call Web API and Passing Auth Header");
                //hhttps://kstestapi.azurewebsites.net
                HttpClient c = new HttpClient();
                if (headerPassed == true)
                {
                    AzureServiceTokenProvider tokenProvider = new AzureServiceTokenProvider();
                    //"9d9965ff-1c98-42b6-8e3b-1a9e836d14e4"
                    string accessToken = tokenProvider.GetAccessTokenAsync("9d9965ff-1c98-42b6-8e3b-1a9e836d14e4").Result;
                    
                    c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                }
                var responseMessage = await c.GetAsync("https://calledkstestapi.azurewebsites.net/api/values");
                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var response = responseMessage.Content.ReadAsStringAsync().Result;
                    logger.LogInformation("Calling HttpClinet completed");
                    logger.LogInformation($"Response returned from Http call was {response}");
                }
                if (responseMessage.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    logger.LogInformation("Request is UnAuthorized token is not passed");
                }

                //return await response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.StackTrace, " some Error occered", null);
                throw ex;
            }
        }

        private static List<IListBlobItem> GetBlobsWithSdk(string accessToken)
        {
            //DefaultEndpointsProtocol=https;AccountName=ksteststorageaccount1234;AccountKey=kar+IY4xmbUPAKm53hWFhbibTnrqsyiQAQn89di+sZBN9b1AdIOc70DjiD4RMupJutKrSxsbJpOaNVcZqAJOrQ==;EndpointSuffix=core.windows.net
            var tokenCredential = new TokenCredential(accessToken);

            var storageCredentials = new StorageCredentials(tokenCredential);

            logger.LogInformation($"Creating storageCredentials using token completed");

            // Define the blob to read
            var storageAcctName = Environment.GetEnvironmentVariable("TobeReadStorageAccountName");
            var containerName = Environment.GetEnvironmentVariable("TobeReadContainerName");


            CloudStorageAccount storage = new CloudStorageAccount(storageCredentials, storageAcctName, "core.windows.net", true);
            //CloudStorageAccount storage = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=ksteststorageaccount1234;AccountKey=kar+IY4xmbUPAKm53hWFhbibTnrqsyiQAQn89di+sZBN9b1AdIOc70DjiD4RMupJutKrSxsbJpOaNVcZqAJOrQ==;EndpointSuffix=core.windows.net");

            CloudBlobClient c = storage.CreateCloudBlobClient();

            //CloudBlobClient c = new CloudBlobClient(new Uri($"https://{storageAcctName}.blob.core.windows.net/"));
            CloudBlobContainer con = c.GetContainerReference(containerName);

            logger.LogInformation($"set the container reference correctly and finally calling using managed identity.");

            BlobContinuationToken continuationToken = null;
            var results = new List<IListBlobItem>();
            do
            {
                var response = con.ListBlobsSegmentedAsync(continuationToken).Result;
                continuationToken = response.ContinuationToken;
                results.AddRange(response.Results);
            }
            while (continuationToken != null);



            return results;

            //var blob = new CloudBlockBlob(new Uri($"https://{storageAcctName}.blob.core.windows.net/{containerName}/{FileName}"), storageCredentials);
            // Open a data stream to the blob
            //return await blob.OpenReadAsync();
        }

        private static async Task<string> GetAccessTokenAsync()
        {
            var tokenProvider = new AzureServiceTokenProvider();
            return await tokenProvider.GetAccessTokenAsync("https://storage.azure.com/");
        }
    }
}
