using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EbookController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly EbookAccessService _ebookAccessService;

        public EbookController(
            IWebHostEnvironment environment,
            EbookAccessService ebookAccessService)
        {
            _environment = environment;
            _ebookAccessService = ebookAccessService;
        }

        [HttpGet("download")]
        public IActionResult DownloadEbook(
            [FromQuery] string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message =
                            "Download token required hai."
                    });
                }

                bool valid =
                    _ebookAccessService
                        .ValidateAndConsumeToken(token);

                if (!valid)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message =
                            "Download link invalid ya expire ho gaya hai."
                    });
                }

                string filePath =
                    Path.Combine(
                        _environment.ContentRootPath,
                        "Ebook",
                        "bhakti-aur-jeevan.pdf"
                    );

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound(new
                    {
                        success = false,
                        message =
                            "Ebook file server par nahi mili."
                    });
                }

                byte[] fileBytes =
                    System.IO.File.ReadAllBytes(filePath);

                return File(
                    fileBytes,
                    "application/pdf",
                    "Bhakti-Aur-Jeevan.pdf"
                );
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "Ebook download nahi ho paya.",
                    error = ex.Message
                });
            }
        }
    }
}