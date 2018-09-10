namespace FaceFinder
{
    public class ImageInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Metadata { get; set; } = string.Empty;
        public string Attributes { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public string OcrResult { get; set; } = string.Empty;
        public string ThumbUrl { get; set; } = string.Empty; //"Assets/FaceFinder.jpg";
        public string Confidence { get; set; } = string.Empty;
    }
}
