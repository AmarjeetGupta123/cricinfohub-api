using Microsoft.AspNetCore.Mvc;
using Razorpay.Api;
using System.Security.Cryptography;
using System.Text;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly EbookAccessService _ebookAccessService;

        private const long EbookPriceInPaise = 19900;

        public PaymentController(
            IConfiguration configuration,
            EbookAccessService ebookAccessService)
        {
            _configuration = configuration;
            _ebookAccessService = ebookAccessService;
        }

        // =========================================================
        // CREATE ORDER
        // =========================================================

        [HttpPost("create-order")]
        public IActionResult CreateOrder()
        {
            try
            {
                string keyId =
                    _configuration["Razorpay:KeyId"]!;

                string keySecret =
                    _configuration["Razorpay:KeySecret"]!;

                if (string.IsNullOrWhiteSpace(keyId) ||
                    string.IsNullOrWhiteSpace(keySecret))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Razorpay API keys are missing."
                    });
                }

                RazorpayClient client =
                    new RazorpayClient(keyId, keySecret);

                var options = new Dictionary<string, object>
                {
                    { "amount", EbookPriceInPaise },
                    { "currency", "INR" },
                    {
                        "receipt",
                        "ebook_" +
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    },
                    { "payment_capture", 1 }
                };

                Order order = client.Order.Create(options);

                return Ok(new
                {
                    success = true,
                    orderId = order["id"].ToString(),
                    amount = Convert.ToInt64(order["amount"]),
                    currency = order["currency"].ToString(),
                    keyId = keyId
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Order create nahi ho paya.",
                    error = ex.Message
                });
            }
        }


        // =========================================================
        // VERIFY PAYMENT
        // =========================================================

        [HttpPost("verify-payment")]
        public IActionResult VerifyPayment(
            [FromBody] PaymentVerificationRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.razorpay_order_id) ||
                    string.IsNullOrWhiteSpace(request.razorpay_payment_id) ||
                    string.IsNullOrWhiteSpace(request.razorpay_signature))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Payment details incomplete hain."
                    });
                }

                string keySecret =
                    _configuration["Razorpay:KeySecret"]!;

                if (string.IsNullOrWhiteSpace(keySecret))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Razorpay KeySecret missing hai."
                    });
                }

                // Razorpay signature payload
                string payload =
                    request.razorpay_order_id +
                    "|" +
                    request.razorpay_payment_id;

                using var hmac =
                    new HMACSHA256(
                        Encoding.UTF8.GetBytes(keySecret)
                    );

                byte[] hash =
                    hmac.ComputeHash(
                        Encoding.UTF8.GetBytes(payload)
                    );

                string generatedSignature =
                    Convert.ToHexString(hash)
                        .ToLowerInvariant();

                bool isValid =
                    generatedSignature.Equals(
                        request.razorpay_signature,
                        StringComparison.OrdinalIgnoreCase
                    );

                if (!isValid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Payment verification failed."
                    });
                }

                // =================================================
                // IMPORTANT
                // Signature valid hai.
                // Ab Razorpay order ko retrieve karke
                // amount bhi verify karenge.
                // =================================================

                string keyId =
                    _configuration["Razorpay:KeyId"]!;

                RazorpayClient client =
                    new RazorpayClient(keyId, keySecret);

                Order razorpayOrder =
                    client.Order.Fetch(
                        request.razorpay_order_id
                    );

                long orderAmount =
                    Convert.ToInt64(razorpayOrder["amount"]);

                string orderCurrency =
                    razorpayOrder["currency"]?.ToString()
                    ?? "";

                // Amount check
                if (orderAmount != EbookPriceInPaise)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid payment amount."
                    });
                }

                // Currency check
                if (!orderCurrency.Equals(
                        "INR",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid payment currency."
                    });
                }

                // =================================================
                // PAYMENT COMPLETELY VERIFIED
                // =================================================

                string downloadToken =
                    _ebookAccessService.CreateToken();

                return Ok(new
                {
                    success = true,
                    message =
                        "Payment verified successfully.",
                    orderId =
                        request.razorpay_order_id,
                    paymentId =
                        request.razorpay_payment_id,
                    amount =
                        orderAmount,
                    currency =
                        orderCurrency,
                    downloadToken =
                        downloadToken
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "Payment verification mein error aaya.",
                    error = ex.Message
                });
            }
        }
    }


    // =============================================================
    // REQUEST MODEL
    // =============================================================

    public class PaymentVerificationRequest
    {
        public string razorpay_order_id { get; set; }
            = string.Empty;

        public string razorpay_payment_id { get; set; }
            = string.Empty;

        public string razorpay_signature { get; set; }
            = string.Empty;
    }
}