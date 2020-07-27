# media-services-overlays

This sample shows how to create a custom encoding overlay using the StandardEncoderPreset settings.

| File | Description |
| ---- | ----------- |
| AMSLogo.png | The logo that is used for the overlay |
| ConfigWrapper.cs | Uses the Media Services account settings |
| Program.cs | .NET application code |
| appsettings.json | Contains the app setting from you Media Services account |
| ignite.mp4 | The video used in the sample |
| media-services-v3-netcore.csproj | Contains item and property group metadata |

## Getting Started

1. Read [How to create an overlay with Media Encoder Standard](https://docs.microsoft.com).
1. Download or clone this repository.
1. Edit the settings in the *appsettings.json* file with your Azure account information:

    ```json
    {
        "AadClientId": "",
        "AadEndpoint": "https://login.microsoftonline.com",
        "AadSecret": "",
        "AadTenantId": "",
        "AccountName": "",
        "ArmAadAudience": "https://management.core.windows.net/",
        "ArmEndpoint": "https://management.azure.com/",
        "Region": "",
        "ResourceGroup": "",
        "SubscriptionId": ""
    }
    ```

1. Run the code.