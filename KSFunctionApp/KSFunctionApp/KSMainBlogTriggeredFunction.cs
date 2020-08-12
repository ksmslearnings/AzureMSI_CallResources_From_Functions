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
        static string debugMode = string.Empty;
        static AzureServiceTokenProvider tokenProvider = null;

        [FunctionName("KSMainBlogTriggeredFunction")]
        public static void Run([BlobTrigger("samplecontainer/{name}", Connection = "AzureWebJobsStorage")]Stream myBlob, string name, ILogger log)
        {
            logger = log;
            debugMode = Environment.GetEnvironmentVariable("DebugMode");
            //RunAs=App

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
            CallWebAPIAppWithAndWithoutAuthHeader();

            // logger.LogInformation("Calling Web API without Auth Header");
            //CallWebAPIAppWithAndWithoutAuthHeader(false);


            log.LogInformation("Function Ended");

            //using Microsoft.Azure.Services.AppAuthentication;
            //using Microsoft.Azure.KeyVault;
            //// ...
            //var azureServiceTokenProvider = new AzureServiceTokenProvider();
            //string accessToken = await azureServiceTokenProvider.GetAccessTokenAsync("hhttps://vault.azure.net");
            //// OR
            //var kv = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
        }

        private static void CallWebAPIAppWithAndWithoutAuthHeader()
        {
            try
            {
                logger.LogInformation("Calling HttpClinet to Call Web API and Passing Auth Header");
                //hhttps://kstestapi.azurewebsites.net
                HttpClient c = new HttpClient();

                if (debugMode == "true")
                {
                    logger.LogInformation("using debug mode tokenprovider");
                    tokenProvider = new AzureServiceTokenProvider("RunAs=Developer; DeveloperTool=AzureCli");
                }
                else
                {
                    logger.LogInformation("using production mode tokenprovider");
                    tokenProvider = new AzureServiceTokenProvider("RunAs=App");
                }
                //AzureServiceTokenProvider tokenProvider = new AzureServiceTokenProvider("RunAs=Developer; DeveloperTool=AzureCli");
                //HERE WE ARE GETTING TOKEN FOR CUSTOM APP REGISTRATION & locally Azure CLI client has been registered as client application
                //to access the identity tokens based on App Id

                //Calling App Registration ID based
                //string accessToken = tokenProvider.GetAccessTokenAsync("f8055948-e344-41ec-a139-9ccca2799127").Result;

                //OR

                /*  Remove later.
                 *
                //Calling Managed Identity and Trying with this - Localy unable to get the token due to Azure CLI not trusted to access tokens.
                logger.LogInformation("calling managed identity of Web API to get auth token from Azure AD for called API managed identity");
                string accessToken = tokenProvider.GetAccessTokenAsync("a51a50c9-8afa-4c50-bffb-5c370e633a48").Result; //unable to get it locally but.

                
                c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var responseMessage = c.GetAsync("hhttps://calledkstestapi.azurewebsites.net/api/values").Result;
                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var response = responseMessage.Content.ReadAsStringAsync().Result;                   
                    logger.LogInformation($"Response returned from Http call was {response}");
                }
                if (responseMessage.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    logger.LogInformation("Request is UnAuthorized token is not passed");
                }
                
                 */


                ///repeat for asp.net web api - Next step will be to call with managed identity of actual webapi instance in azure
                //Calling Managed Identity and Trying with this.
                logger.LogInformation("calling app registration token for web api");
                HttpClient cc = new HttpClient();
                //string accessToken = tokenProvider.GetAccessTokenAsync("hhttps://kstestorganization.onmicrosoft.com/testaspnetfxwebapi").Result;

                string accessToken = tokenProvider.GetAccessTokenAsync("da7ecb1a-651c-4c62-9c24-27be3d518340").Result;

                cc.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var responseMessage = cc.GetAsync("https://localhost:44351//api/testing").Result;
                if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var response = responseMessage.Content.ReadAsStringAsync().Result;
                    logger.LogInformation($"Response returned from Http call was {response}");
                }
                if (responseMessage.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    logger.LogInformation("Request is UnAuthorized token is not passed");
                }

                logger.LogInformation("Calling HttpClinet completed");

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
            //CloudStorageAccount storage = CloudStorageAccount.Parse(connection string os storage account);

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
            if (debugMode == "true")
            {
                logger.LogInformation("using debug mode tokenprovider");
                tokenProvider = new AzureServiceTokenProvider("RunAs=Developer; DeveloperTool=AzureCli");
            }
            else
            {
                logger.LogInformation("using production mode tokenprovider");
                tokenProvider = new AzureServiceTokenProvider("RunAs=App");
            }

            return await tokenProvider.GetAccessTokenAsync("https://storage.azure.com/");
        }
    }
}
