namespace VideoGenerator.Models
{
    public class VideoViewModel
    {
        public string Url { get; set; }
        public string ThumbnailUrl { get; set; }
        public string VideoDownloadUrl { get; set; }
        public int Width { get; set; }  // Add these properties
        public int Height { get; set; }
    }
}
