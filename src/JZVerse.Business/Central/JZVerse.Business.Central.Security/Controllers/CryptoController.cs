using System.Security.Cryptography;
using System.Text;
using MicroHuaxia.Security.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.Business.Central.Security.Controllers;

/// <summary>
/// 加密服务控制器 - 提供密码学操作API
/// </summary>
[ApiController]
[Route("api/v1/security/crypto")]
public class CryptoController : ControllerBase
{
    private readonly ISM2Provider? _sm2Provider;
    private readonly ISM3Provider? _sm3Provider;
    private readonly ISM4Provider? _sm4Provider;
    private readonly ILogger<CryptoController> _logger;

    public CryptoController(
        ILogger<CryptoController> logger,
        ISM2Provider? sm2Provider = null,
        ISM3Provider? sm3Provider = null,
        ISM4Provider? sm4Provider = null)
    {
        _logger = logger;
        _sm2Provider = sm2Provider;
        _sm3Provider = sm3Provider;
        _sm4Provider = sm4Provider;
    }

    /// <summary>
    /// SM3哈希计算
    /// </summary>
    [HttpPost("sm3/hash")]
    [Authorize(Roles = "Admin,Service")]
    public ActionResult<HashResponse> Sm3Hash([FromBody] HashRequest request)
    {
        if (_sm3Provider == null)
        {
            return BadRequest(new { Code = 400, Message = "SM3 provider not available" });
        }

        try
        {
            var input = request.Input + (request.Salt ?? "");
            var hash = _sm3Provider.Hash(input);
            return Ok(new HashResponse { Hash = hash, Algorithm = "SM3" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SM3 hash failed");
            return StatusCode(500, new { Code = 500, Message = "哈希计算失败" });
        }
    }

    /// <summary>
    /// SM4加密
    /// </summary>
    [HttpPost("sm4/encrypt")]
    [Authorize(Roles = "Admin,Service")]
    public ActionResult<CryptoResponse> Sm4Encrypt([FromBody] CryptoRequest request)
    {
        if (_sm4Provider == null)
        {
            return BadRequest(new { Code = 400, Message = "SM4 provider not available" });
        }

        try
        {
            var key = string.IsNullOrEmpty(request.Key)
                ? GenerateSecureKey(16)
                : NormalizeKey(request.Key, 16);

            var encrypted = _sm4Provider.Encrypt(request.Data, key);
            return Ok(new CryptoResponse
            {
                Result = Convert.ToBase64String(encrypted),
                Key = request.Key == null ? Convert.ToBase64String(key) : null,
                Algorithm = "SM4"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SM4 encrypt failed");
            return StatusCode(500, new { Code = 500, Message = "加密失败" });
        }
    }

    /// <summary>
    /// SM4解密
    /// </summary>
    [HttpPost("sm4/decrypt")]
    [Authorize(Roles = "Admin,Service")]
    public ActionResult<CryptoResponse> Sm4Decrypt([FromBody] CryptoRequest request)
    {
        if (_sm4Provider == null)
        {
            return BadRequest(new { Code = 400, Message = "SM4 provider not available" });
        }

        try
        {
            if (string.IsNullOrEmpty(request.Key))
            {
                return BadRequest(new { Code = 400, Message = "解密密钥不能为空" });
            }

            var key = NormalizeKey(request.Key, 16);
            var data = Convert.FromBase64String(request.Data);
            var decrypted = _sm4Provider.Decrypt(data, key);
            return Ok(new CryptoResponse
            {
                Result = Encoding.UTF8.GetString(decrypted),
                Algorithm = "SM4"
            });
        }
        catch (FormatException)
        {
            return BadRequest(new { Code = 400, Message = "无效的Base64数据" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SM4 decrypt failed");
            return StatusCode(500, new { Code = 500, Message = "解密失败" });
        }
    }

    /// <summary>
    /// 生成随机密钥
    /// </summary>
    [HttpGet("generate-key")]
    [Authorize(Roles = "Admin,Service")]
    public ActionResult<KeyResponse> GenerateKey([FromQuery] int length = 32)
    {
        if (length < 8 || length > 64)
        {
            return BadRequest(new { Code = 400, Message = "密钥长度必须在8-64之间" });
        }

        var key = GenerateSecureKey(length);
        return Ok(new KeyResponse
        {
            Key = Convert.ToBase64String(key),
            HexKey = Convert.ToHexString(key),
            Length = length
        });
    }

    /// <summary>
    /// 生成随机盐值
    /// </summary>
    [HttpGet("generate-salt")]
    [Authorize(Roles = "Admin,Service")]
    public ActionResult<SaltResponse> GenerateSalt([FromQuery] int length = 16)
    {
        if (length < 8 || length > 64)
        {
            return BadRequest(new { Code = 400, Message = "盐值长度必须在8-64之间" });
        }

        var salt = GenerateSecureKey(length);
        return Ok(new SaltResponse
        {
            Salt = Convert.ToBase64String(salt),
            HexSalt = Convert.ToHexString(salt),
            Length = length
        });
    }

    /// <summary>
    /// MD5哈希（用于兼容旧系统，不推荐新系统使用）
    /// </summary>
    [HttpPost("md5/hash")]
    [Authorize(Roles = "Admin,Service")]
    public ActionResult<HashResponse> Md5Hash([FromBody] HashRequest request)
    {
        try
        {
            using var md5 = MD5.Create();
            var input = request.Input + (request.Salt ?? "");
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = md5.ComputeHash(bytes);
            var hashString = Convert.ToHexString(hash).ToLowerInvariant();

            if (!string.IsNullOrEmpty(request.Salt))
            {
                hashString = $"{hashString}:{request.Salt}";
            }

            return Ok(new HashResponse { Hash = hashString, Algorithm = "MD5" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MD5 hash failed");
            return StatusCode(500, new { Code = 500, Message = "哈希计算失败" });
        }
    }

    /// <summary>
    /// HMAC-SHA256签名
    /// </summary>
    [HttpPost("hmac/sign")]
    [Authorize(Roles = "Admin,Service")]
    public ActionResult<HmacResponse> HmacSign([FromBody] HmacRequest request)
    {
        try
        {
            var key = string.IsNullOrEmpty(request.Key)
                ? GenerateSecureKey(32)
                : Convert.FromBase64String(request.Key);

            using var hmac = new HMACSHA256(key);
            var data = Encoding.UTF8.GetBytes(request.Data);
            var signature = hmac.ComputeHash(data);

            return Ok(new HmacResponse
            {
                Signature = Convert.ToBase64String(signature),
                Key = request.Key == null ? Convert.ToBase64String(key) : null,
                Algorithm = "HMAC-SHA256"
            });
        }
        catch (FormatException)
        {
            return BadRequest(new { Code = 400, Message = "无效的Base64密钥" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HMAC sign failed");
            return StatusCode(500, new { Code = 500, Message = "签名失败" });
        }
    }

    /// <summary>
    /// 验证HMAC签名
    /// </summary>
    [HttpPost("hmac/verify")]
    [Authorize(Roles = "Admin,Service")]
    public ActionResult<VerifyResponse> HmacVerify([FromBody] VerifyHmacRequest request)
    {
        try
        {
            var key = Convert.FromBase64String(request.Key);
            using var hmac = new HMACSHA256(key);
            var data = Encoding.UTF8.GetBytes(request.Data);
            var computed = hmac.ComputeHash(data);
            var provided = Convert.FromBase64String(request.Signature);

            var isValid = CryptographicOperations.FixedTimeEquals(computed, provided);
            return Ok(new VerifyResponse { IsValid = isValid });
        }
        catch (FormatException)
        {
            return BadRequest(new { Code = 400, Message = "无效的Base64数据" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HMAC verify failed");
            return StatusCode(500, new { Code = 500, Message = "验证失败" });
        }
    }

    /// <summary>
    /// 获取支持的算法列表
    /// </summary>
    [HttpGet("algorithms")]
    [AllowAnonymous]
    public ActionResult GetSupportedAlgorithms()
    {
        return Ok(new
        {
            SM2 = _sm2Provider != null,
            SM3 = _sm3Provider != null,
            SM4 = _sm4Provider != null,
            Standard = new[] { "MD5", "HMAC-SHA256", "AES", "RSA" }
        });
    }

    private byte[] GenerateSecureKey(int length)
    {
        var key = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);
        return key;
    }

    private byte[] NormalizeKey(string key, int targetLength)
    {
        var bytes = Convert.FromBase64String(key);
        if (bytes.Length == targetLength)
            return bytes;

        // 如果密钥长度不对，使用SHA256派生
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(bytes);
        return hash.Take(targetLength).ToArray();
    }
}

// DTOs
public class HashRequest
{
    public string Input { get; set; } = string.Empty;
    public string? Salt { get; set; }
}

public class HashResponse
{
    public string Hash { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
}

public class CryptoRequest
{
    public string Data { get; set; } = string.Empty;
    public string? Key { get; set; }
}

public class CryptoResponse
{
    public string Result { get; set; } = string.Empty;
    public string? Key { get; set; }
    public string Algorithm { get; set; } = string.Empty;
}

public class KeyResponse
{
    public string Key { get; set; } = string.Empty;
    public string HexKey { get; set; } = string.Empty;
    public int Length { get; set; }
}

public class SaltResponse
{
    public string Salt { get; set; } = string.Empty;
    public string HexSalt { get; set; } = string.Empty;
    public int Length { get; set; }
}

public class HmacRequest
{
    public string Data { get; set; } = string.Empty;
    public string? Key { get; set; }
}

public class HmacResponse
{
    public string Signature { get; set; } = string.Empty;
    public string? Key { get; set; }
    public string Algorithm { get; set; } = string.Empty;
}

public class VerifyHmacRequest
{
    public string Data { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

public class VerifyResponse
{
    public bool IsValid { get; set; }
}
