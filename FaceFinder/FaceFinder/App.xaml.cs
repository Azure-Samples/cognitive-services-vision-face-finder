using System.Windows;

using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.Face;

namespace FaceFinder
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IComputerVisionClient computerVisionClient;
        public IFaceClient faceClient;

        public void SetupComputerVisionClient(string key, string endpoint)
        {
            computerVisionClient = new ComputerVisionClient(
                new Microsoft.Azure.CognitiveServices.Vision.ComputerVision.ApiKeyServiceClientCredentials(key),
                new System.Net.Http.DelegatingHandler[] { });
            computerVisionClient.Endpoint = endpoint;
        }

        public void SetupFaceClient(string key, string endpoint)
        {
            faceClient = new FaceClient(
                new Microsoft.Azure.CognitiveServices.Vision.Face.ApiKeyServiceClientCredentials(key),
                new System.Net.Http.DelegatingHandler[] { });
            faceClient.Endpoint = endpoint;
        }
    }
}
