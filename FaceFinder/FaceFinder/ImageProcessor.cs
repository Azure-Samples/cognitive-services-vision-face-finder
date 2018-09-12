using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

namespace FaceFinder
{
    /// <summary>
    /// Processes images to read printed text (OCR), determine a caption
    /// describing the image, and generate a thumbnail of the image.
    /// Dependencies: Computer Vision service.
    /// </summary>
    class ImageProcessor
    {
        private readonly IComputerVisionClient computerVisionClient;

        private const int thumbWidth = 100, thumbHeight = 100;

        public ImageProcessor(IComputerVisionClient computerVisionClient)
        {
            this.computerVisionClient = computerVisionClient;
        }

        // Creates a thumbnail from newImage in the thumbnailsFolder.
        // Overwrites a file of the same name.
        public async Task<string> ProcessImageFileForThumbAsync(
            FileInfo file, ImageInfo newImage, string thumbnailsFolder)
        {
            string thumbName = file.Name.Insert(file.Name.Length - 4, "_thumb");
            string thumbUrl = thumbnailsFolder + Path.DirectorySeparatorChar + thumbName;
            try
            {
                using (FileStream readStream = file.OpenRead(), writeStream = File.Create(thumbUrl))
                using (var thumbStream = await computerVisionClient.GenerateThumbnailInStreamAsync(
                            thumbWidth, thumbHeight, readStream, true))
                {
                    thumbStream.CopyTo(writeStream);
                }
                newImage.ThumbUrl = thumbUrl;
                return thumbUrl;
            }
            catch (ComputerVisionErrorException cve)
            {
                Debug.WriteLine("ProcessImageFileForThumbAsync: " + cve.Message);
                return string.Empty;
            }
        }

        public async Task<string> ProcessImageFileForCaptionAsync(
            FileInfo file, ImageInfo newImage)
        {
            string caption = string.Empty;
            ImageDescription description;
            try
            {
                using (FileStream stream = file.OpenRead())
                {
                    description = await computerVisionClient.DescribeImageInStreamAsync(stream);
                }
                if (description.Captions.Count > 0)
                {
                    caption = description.Captions[0].Text;
                }
                newImage.Caption = caption;
            }
            catch (ComputerVisionErrorException cve)
            {
                Debug.WriteLine("ProcessImageFileForCaptionAsync: " + cve.Message);
            }
            return caption;
        }

        // OCR
        public async Task<string> ProcessImageFileForTextAsync(
            FileInfo file, ImageInfo newImage)
        {
            string ocrResult = string.Empty;
            OcrResult result;
            try
            {
                using (FileStream stream = file.OpenRead())
                {
                    result = await computerVisionClient.RecognizePrintedTextInStreamAsync(true, stream);
                }
                IList<OcrRegion> regions = result.Regions;
                if (regions.Count > 0)
                {
                    foreach (OcrRegion region in regions)
                    {
                        foreach (OcrLine line in region.Lines)
                        {
                            foreach (OcrWord word in line.Words)
                            {
                                ocrResult += word.Text + " ";
                            }
                            break;
                        }
                        break;
                    }
                    newImage.OcrResult = ocrResult;
                }
            }
            catch (ComputerVisionErrorException cve)
            {
                Debug.WriteLine("ProcessImageFileForTextAsync: " + cve.Message);
            }
            return ocrResult;
        }
    }
}
