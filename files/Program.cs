using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Rest;
using Microsoft.Rest.Azure.Authentication;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace OverlayVideos
{
    public class Program
    {
        const String inputMP4FileName = @"ignite.mp4";
        const String overlayFileName = @"AMSLogo.png";
        const String outputFolder = @"Output";
        const String overlayLabel = @"logo";
        const String OverlayTransformName = "OverlayTransform";

        public static async Task Main(string[] args)
        {
            ConfigWrapper config = new ConfigWrapper(new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build());

            try
            {
                await RunAsync(config);
            }
            catch (Exception exception)
            {
                if (exception.Source.Contains("ActiveDirectory"))
                {
                     Console.Error.WriteLine("TIP: Make sure that you have filled out the appsettings.json file before running this sample.");
                }

                Console.Error.WriteLine($"{exception.Message}");

                ApiErrorException apiException = exception.GetBaseException() as ApiErrorException;
                if (apiException != null)
                {
                    Console.Error.WriteLine(
                        $"ERROR: API call failed with error code '{apiException.Body.Error.Code}' and message '{apiException.Body.Error.Message}'.");
                }
            }

            Console.WriteLine("Press Enter to continue.");
            Console.ReadLine();

        }


        /// <summary>
        /// Run the sample async.
        /// </summary>
        /// <param name="config">The parm is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        // <RunAsync>
        private static async Task RunAsync(ConfigWrapper config)
        {
            IAzureMediaServicesClient client = await CreateMediaServicesClientAsync(config);

            // Set the polling interval for long running operations to 2 seconds.
            // The default value is 30 seconds for the .NET client SDK
            client.LongRunningOperationRetryTimeout = 2;

            try
            {
                // Ensure that you have a overlay transform.  This is really a one time setup operation.
                var overlayTransform = EnsureTransformExists(client, config.ResourceGroup, config.AccountName, OverlayTransformName);
                
                // Creating a unique suffix so that we don't have name collisions if you run the sample
                // multiple times without cleaning up.
                var uniqueness = Guid.NewGuid().ToString().Substring(0, 13);
                var jobName = "job-" + uniqueness;
                var inputAssetName = "input-" + uniqueness;
                var logoAssetName = "logo-" + uniqueness;
                var outputAssetName = "output-" + uniqueness;

                CreateInputAsset(client, config.ResourceGroup, config.AccountName, inputAssetName, inputMP4FileName).Wait();
                CreateInputAsset(client, config.ResourceGroup, config.AccountName, logoAssetName, overlayFileName).Wait();

                var input = new JobInputAsset(assetName: inputAssetName);
                var overlay = new JobInputAsset(assetName: logoAssetName, label: overlayLabel);

                var outputAsset = CreateOutputAsset(client,config.ResourceGroup, config.AccountName, outputAssetName);

                // Note that you can now pass custom correlation data Dictionary into the job to use via EventGrid or other Job polling listeners.
                // this is handy for passing tenant ID, or custom workflow data as part of your job.
                var correlationData = new Dictionary<string,string>();
                correlationData.Add("customData1", "some custom data to pass through the job");
                correlationData.Add("custom ID", "some GUID here");

                var job = SubmitJob(client,config.ResourceGroup, config.AccountName, OverlayTransformName, jobName, input, overlay, outputAsset.Name, correlationData);

                var startedTime = DateTime.Now;

                job = WaitForJobToFinish(client,config.ResourceGroup, config.AccountName, OverlayTransformName, jobName);

                var elapsed = DateTime.Now - startedTime;

                if (job.State == JobState.Finished)
                {
                    Console.WriteLine("Job finished.");
                    if (!Directory.Exists(outputFolder))
                        Directory.CreateDirectory(outputFolder);
                    DownloadResults(client, config.ResourceGroup, config.AccountName, outputAsset.Name, outputFolder).Wait();
                }
                else if (job.State == JobState.Error)
                {
                    Console.WriteLine($"ERROR: Job finished with error message: {job.Outputs[0].Error.Message}");
                    Console.WriteLine($"ERROR:                   error details: {job.Outputs[0].Error.Details[0].Message}");
                }
            }
            catch(ApiErrorException ex)
            {
                string code = ex.Body.Error.Code;
                string message = ex.Body.Error.Message;

                Console.WriteLine("ERROR:API call failed with error code: {0} and message: {1}", code, message);
            }          
        }


        /// <summary>
        /// Create the ServiceClientCredentials object based on the credentials
        /// supplied in local configuration file.
        /// </summary>
        /// <param name="config">The parm is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        // <GetCredentialsAsync>
        private static async Task<ServiceClientCredentials> GetCredentialsAsync(ConfigWrapper config)
        {
            // Use ApplicationTokenProvider.LoginSilentWithCertificateAsync or UserTokenProvider.LoginSilentAsync to get a token using service principal with certificate
            //// ClientAssertionCertificate
            //// ApplicationTokenProvider.LoginSilentWithCertificateAsync

            // Use ApplicationTokenProvider.LoginSilentAsync to get a token using a service principal with symetric key
            ClientCredential clientCredential = new ClientCredential(config.AadClientId, config.AadSecret);
            return await ApplicationTokenProvider.LoginSilentAsync(config.AadTenantId, clientCredential, ActiveDirectoryServiceSettings.Azure);
        }
        // </GetCredentialsAsync>

        /// <summary>
        /// Creates the AzureMediaServicesClient object based on the credentials
        /// supplied in local configuration file.
        /// </summary>
        /// <param name="config">The parm is of type ConfigWrapper. This class reads values from local configuration file.</param>
        /// <returns></returns>
        // <CreateMediaServicesClient>
        private static async Task<IAzureMediaServicesClient> CreateMediaServicesClientAsync(ConfigWrapper config)
        {
            var credentials = await GetCredentialsAsync(config);

            return new AzureMediaServicesClient(config.ArmEndpoint, credentials)
            {
                SubscriptionId = config.SubscriptionId,
            };
        }
        // </CreateMediaServicesClient>

       private static Transform EnsureTransformExists(
           IAzureMediaServicesClient client, 
           string resourceGroupName, 
           string accountName, 
           string transformName)
       {
            // Does a Transform already exist with the desired name? Assume that an existing Transform with the desired name
            // also uses the same recipe or Preset for processing content.
            var transform = client.Transforms.Get(resourceGroupName, accountName, transformName);

            if (transform == null)
            {
                // Create a new Transform Outputs array - this defines the set of outputs for the Transform
                var outputs = new TransformOutput[]
                {
                    // Create a new TransformOutput with a custom Standard Encoder Preset and overlay
                    new TransformOutput
                    {
                        Preset = new StandardEncoderPreset
                        {
                            Filters = new Filters
                            {
                                Overlays = new List<Overlay>
                                {
                                    new VideoOverlay
                                    {
                                        InputLabel = overlayLabel   // same as the one used in the JobInput
                                    }
                                }
                            },
                            Codecs = new List<Codec>
                            {
                                new AacAudio
                                {
                                },
                                new H264Video
                                {
                                    KeyFrameInterval = TimeSpan.FromSeconds(2),
                                    Layers = new List<H264Layer>
                                    {
                                        new H264Layer
                                        {
                                            Profile = H264VideoProfile.Baseline,
                                            Bitrate = 1000000,
                                            Width = "1140",
                                            Height = "640"
                                        }
                                    }
                                }
                            },
                            Formats = new List<Format>
                            {
                                new Mp4Format
                                {
                                    FilenamePattern = "{Basename}_{Bitrate}{Extension}",
                                }
                            }
                        }
                    }
                };

                string description = "A simple custom encoding transform with overlay";
                // Create the custom Transform with the outputs defined above
                transform = client.Transforms.CreateOrUpdate(resourceGroupName, accountName, transformName, outputs, description);
            }

            return transform;
        }

        private static async Task<Asset> CreateInputAsset(IAzureMediaServicesClient client, string resourceGroupName, string accountName, string assetName, string fileToUpload)
        {
            Console.WriteLine("Creating Input Asset");
            var asset = client.Assets.CreateOrUpdate(resourceGroupName, accountName,assetName, new Asset());

            var input = new ListContainerSasInput()
            {
                Permissions = AssetContainerPermission.ReadWrite,
                ExpiryTime = DateTime.Now.AddHours(2).ToUniversalTime()
            };

            var response = client.Assets.ListContainerSasAsync(resourceGroupName, accountName, assetName, input.Permissions,input.ExpiryTime).Result;

            var uploadSasUrl = response.AssetContainerSasUrls.First();

            var filename = Path.GetFileName(fileToUpload);
            Console.WriteLine("Uploading file: {0}", filename);

            var sasUri = new Uri(uploadSasUrl);
            var container = new CloudBlobContainer(sasUri);
            var blob = container.GetBlockBlobReference(filename);
            blob.Properties.ContentType = "video/mp4";
            Console.WriteLine("Uploading File to container: {0}", sasUri);
            await blob.UploadFromFileAsync(fileToUpload);

            return asset;
        }

        private static Asset CreateOutputAsset(
            IAzureMediaServicesClient client, 
            string resourceGroupName, 
            string accountName, 
            string assetName)
        {
            return client.Assets.CreateOrUpdate(resourceGroupName, accountName, assetName, new Asset());
        }

        private static Job SubmitJob(
            IAzureMediaServicesClient client, 
            string resourceGroupName, 
            string accountName, 
            string transformName, 
            string jobName, 
            JobInput jobInput, 
            JobInput overlay, 
            string outputAssetName, 
            Dictionary<string,string> correlationData)
        {
            JobOutput[] jobOutputs =
            {
                new JobOutputAsset(outputAssetName), 
            };

            var jobInputs = new List<JobInput>() { jobInput, overlay };

            var job = client.Jobs.Create(
                resourceGroupName, 
                accountName,
                transformName,
                jobName,
                new Job
                {
                    Input = new JobInputs(inputs: jobInputs),
                    Outputs = jobOutputs,
                    CorrelationData = correlationData
                });

            return job;
        }

        private static Job WaitForJobToFinish(IAzureMediaServicesClient client, string resourceGroupName, string accountName, string transformName, string jobName)
        {
            const int SleepInterval = 10 * 1000;

            Job job = null;
            var exit = false;

            do
            {
                job = client.Jobs.Get(resourceGroupName, accountName, transformName, jobName);
                
                if (job.State == JobState.Finished || job.State == JobState.Error || job.State == JobState.Canceled)
                {
                    exit = true;
                }
                else
                {
                    Console.WriteLine($"Job is {job.State}.");

                    for (int i = 0; i < job.Outputs.Count; i++)
                    {
                        JobOutput output = job.Outputs[i];

                        Console.Write($"\tJobOutput[{i}] is {output.State}.");

                        if (output.State == JobState.Processing)
                        {
                            Console.Write($"  Progress: {output.Progress}");
                        }

                        Console.WriteLine();
                    }

                    System.Threading.Thread.Sleep(SleepInterval);
                }
            }
            while (!exit);

            return job;
        }


        /// <summary>
        ///  Downloads the results from the specified output asset, so you can see what you got.
        /// </summary>
        /// <param name="client">The Media Services client.</param>
        /// <param name="resourceGroupName">The name of the resource group within the Azure subscription.</param>
        /// <param name="accountName"> The Media Services account name.</param>
        /// <param name="assetName">The output asset.</param>
        /// <param name="outputFolderName">The name of the folder into which to download the results.</param>
        private static async Task DownloadResults(
            IAzureMediaServicesClient client,
            string resourceGroup,
            string accountName,
            string assetName,
            string outputFolderName)
        {
            if (!Directory.Exists(outputFolderName))
            {
                Directory.CreateDirectory(outputFolderName);
            }

            var assetContainerSas = await client.Assets.ListContainerSasAsync(
                resourceGroup,
                accountName,
                assetName,
                permissions: AssetContainerPermission.Read,
                expiryTime: DateTime.UtcNow.AddHours(1).ToUniversalTime());

            var containerSasUrl = new Uri(assetContainerSas.AssetContainerSasUrls.FirstOrDefault());
            var container = new CloudBlobContainer(containerSasUrl);

            var directory = Path.Combine(outputFolderName, assetName);
            Directory.CreateDirectory(directory);

            Console.WriteLine($"Downloading output results to '{directory}'...");

            BlobContinuationToken continuationToken = null;
            var downloadTasks = new List<Task>();

            do
            {
                // A non-negative integer value that indicates the maximum number of results to be returned at a time,
                // up to the per-operation limit of 5000. If this value is null, the maximum possible number of results
                // will be returned, up to 5000.
                int? ListBlobsSegmentMaxResult = null;    
                
                var segment = await container.ListBlobsSegmentedAsync(null, true, BlobListingDetails.None, ListBlobsSegmentMaxResult, continuationToken, null, null);

                foreach (var blobItem in segment.Results)
                {
                    if (blobItem is CloudBlockBlob blob)
                    {
                        string path = Path.Combine(directory, blob.Name);

                        downloadTasks.Add(blob.DownloadToFileAsync(path, FileMode.Create));
                    }
                }

                continuationToken = segment.ContinuationToken;
            }
            while (continuationToken != null);

            await Task.WhenAll(downloadTasks);

            Console.WriteLine("Download complete.");
        }
    }
}
