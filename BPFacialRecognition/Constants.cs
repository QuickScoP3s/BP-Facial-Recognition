namespace BPFacialRecognition
{
    /// <summary>
    /// General constant variables
    /// </summary>
    public static class GeneralConstants
    {
        // This variable should be set to false for devices, unlike the Raspberry Pi, that have GPU support
        public const bool DisableLiveCameraFeed = false;

        public const string OxfordAPIKey = "8dfe65ba205a49d8aba2b3075d99f50c";

        public const string FaceAPIEndpoint = "https://ww-gezichtsherkenning-2020.cognitiveservices.azure.com/face/v1.0";
        
        // Name of the folder in which all Whitelist data is stored
        public const string WhiteListFolderName = "Facial Recognition Whitelist";

    }
}
