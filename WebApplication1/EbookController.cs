using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EbookController : ControllerBase
    {
        private readonly EbookAccessService _ebookAccessService;

        public EbookController(EbookAccessService ebookAccessService)
        {
            _ebookAccessService = ebookAccessService;
        }

        [HttpGet("download")]
        public IActionResult DownloadEbook([FromQuery] string token)
        {
            try
            {
                // 1. Token check
                if (string.IsNullOrWhiteSpace(token))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Download token required hai."
                    });
                }

                // 2. Validate token
                bool valid = _ebookAccessService
                    .ValidateAndConsumeToken(token);

                if (!valid)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "Download link invalid ya expire ho gaya hai."
                    });
                }

                // 3. PDF path
                string filePath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Ebook",
                    "bhakti-aur-jeevan.pdf"
                );

                // 4. Check PDF
                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Ebook file server par nahi mili.",
                        path = filePath
                    });
                }

                // 5. Read PDF
                byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);

                // 6. Download
                return File(
                    fileBytes,
                    "application/pdf",
                    "bhakti-aur-jeevan.pdf"
                );
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Ebook download nahi ho paya.",
                    error = ex.Message
                });
            }
        }
    }
}