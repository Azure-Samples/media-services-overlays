# media-services-overlays

This sample shows how to create a custom encoding overlay using the StandardEncoderPreset settings.

| File | Description |
| ---- | ----------- |
| cloud.png | The image that is used for the overlay |
| ConfigWrapper.cs | Uses the Media Services account settings to connect to Azure. |
| Program.cs | .NET application code |
| appsettings.json | Contains the settings from your Media Services account |
| ignite.mp4 | The video used in the sample |
| media-services-v3-netcore.csproj | The file where the overlay transform information is set. |

# Encode with a custom overlay Transform
This sample shows how to create a custom encoding Transform using the StandardEncoderPreset settings.

It also demonstrates how to submit a job that uses an HTTPs ingest URL (JobInputHttp) instead of having to first create and upload an Asset.


## Sample

This is a sample showing how to use Azure Media Services API and .NET SDK in .NET Core. Open this folder directly (seperately) in Visual Studio Code.

## Required Assemblies in the project

- Microsoft.Azure.Management.Media -Version 2.0.1
- WindowsAzure.Storage  -Version 9.1.1
- Microsoft.Rest.ClientRuntime.Azure.Authentication -Version 2.3.3

## Update the appsettings.json

To use this project, you must first update the *appsettings.json* with your account settings. See [Quickstart: Register an application with the Microsoft identity platform](https://docs.microsoft.com/en-us/azure/active-directory/develop/quickstart-register-app) for more information on registering your application with your Azure AD tenant.
