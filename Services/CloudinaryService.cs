using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System.Threading.Tasks;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(string cloudName, string apiKey, string apiSecret)
    {
        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> AddTextToVideoAsync(string videoUrl, string text)
    {
        try
        {
           
            var transformation = new Transformation()
                .Overlay(new TextLayer()
                    .Text(text)
                    .FontFamily("Arial")
                    .FontSize(70) 
                    .FontWeight("bold"))
                .Gravity("center") 
                .Y(30) 
                .Color("white"); 
           
            var uploadParams = new VideoUploadParams
            {
                File = new FileDescription(videoUrl),
                PublicId = "video_with_text",
                Transformation = transformation
            };
            
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception("Failed to upload video with text overlay to Cloudinary.");
            }

            return uploadResult.SecureUrl.AbsoluteUri;
        }
        catch (Exception ex)
        {
            // Log the exception or handle it accordingly
            throw new Exception("Failed to add text to video.", ex);
        }
    }
}
