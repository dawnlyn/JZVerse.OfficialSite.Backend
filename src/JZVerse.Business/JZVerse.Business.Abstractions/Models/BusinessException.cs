namespace JZVerse.Business.Abstractions.Models;

/// <summary>
/// 业务异常基类
/// </summary>
public class BusinessException : Exception
{
    /// <summary>
    /// 业务错误码
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// 面向用户的错误消息
    /// </summary>
    public string UserMessage { get; }

    public BusinessException(int errorCode, string userMessage, string? innerMessage = null)
        : base(innerMessage ?? userMessage)
    {
        ErrorCode = errorCode;
        UserMessage = userMessage;
    }

    public BusinessException(int errorCode, string userMessage, Exception innerException)
        : base(userMessage, innerException)
    {
        ErrorCode = errorCode;
        UserMessage = userMessage;
    }
}

/// <summary>
/// 认证异常（401）
/// </summary>
public class AuthenticationException : BusinessException
{
    public AuthenticationException(string message = "认证失败")
        : base(401, message)
    {
    }

    public AuthenticationException(string message, Exception innerException)
        : base(401, message, innerException)
    {
    }
}

/// <summary>
/// 权限拒绝异常（403）
/// </summary>
public class PermissionDeniedException : BusinessException
{
    public PermissionDeniedException(string message = "权限不足")
        : base(403, message)
    {
    }

    public PermissionDeniedException(string message, Exception innerException)
        : base(403, message, innerException)
    {
    }
}

/// <summary>
/// 资源未找到异常（404）
/// </summary>
public class ResourceNotFoundException : BusinessException
{
    public ResourceNotFoundException(string message = "资源不存在")
        : base(404, message)
    {
    }

    public ResourceNotFoundException(string resourceType, object resourceId)
        : base(404, $"{resourceType} '{resourceId}' 不存在")
    {
    }

    public ResourceNotFoundException(string message, Exception innerException)
        : base(404, message, innerException)
    {
    }
}

/// <summary>
/// 参数校验异常（400）
/// </summary>
public class ValidationException : BusinessException
{
    /// <summary>
    /// 校验错误列表
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    public ValidationException(string message)
        : base(400, message)
    {
        Errors = [];
    }

    public ValidationException(string message, IReadOnlyList<ValidationError> errors)
        : base(400, message)
    {
        Errors = errors;
    }

    public ValidationException(IReadOnlyList<ValidationError> errors)
        : base(400, errors.Count > 0 ? errors[0].Message : "参数校验失败")
    {
        Errors = errors;
    }
}

/// <summary>
/// 校验错误信息
/// </summary>
/// <param name="PropertyName">属性名称</param>
/// <param name="Message">错误消息</param>
public sealed record ValidationError(string PropertyName, string Message);
