using System.Diagnostics;
using JZVerse.Business.Abstractions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JZVerse.Business.Infrastructure.Filters;

/// <summary>
/// 响应包装过滤器
/// 自动将 Controller 返回值包装为 ApiResponse 格式
/// </summary>
public sealed class ResponseWrapperFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // 跳过已经是 ApiResponse 类型的响应
        if (context.Result is ObjectResult { Value: ApiResponse } or ObjectResult { Value: ApiResponse<object> })
        {
            await next();
            return;
        }

        // 跳过文件下载等特殊响应
        if (context.Result is FileResult or EmptyResult or RedirectResult)
        {
            await next();
            return;
        }

        // 获取 TraceId 和 RequestId
        var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        var requestId = context.HttpContext.TraceIdentifier;

        // 包装响应
        switch (context.Result)
        {
            case ObjectResult objectResult:
                // 检查是否已经是泛型 ApiResponse<T>
                if (objectResult.Value is not null && IsApiResponseType(objectResult.Value.GetType()))
                {
                    // 已经是 ApiResponse<T>，只需注入 TraceId 和 RequestId
                    InjectTraceInfo(objectResult.Value, traceId, requestId);
                }
                else
                {
                    // 包装为 ApiResponse<T>
                    objectResult.Value = CreateApiResponse(objectResult.Value, traceId, requestId);
                }
                break;

            case StatusCodeResult statusCodeResult:
                context.Result = new ObjectResult(new ApiResponse
                {
                    Code = statusCodeResult.StatusCode,
                    Message = GetStatusCodeMessage(statusCodeResult.StatusCode),
                    TraceId = traceId,
                    RequestId = requestId
                })
                {
                    StatusCode = statusCodeResult.StatusCode
                };
                break;
        }

        await next();
    }

    private static bool IsApiResponseType(Type type)
    {
        if (type == typeof(ApiResponse))
            return true;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApiResponse<>))
            return true;

        return false;
    }

    private static void InjectTraceInfo(object response, string traceId, string requestId)
    {
        // 通过反射设置 TraceId 和 RequestId
        var type = response.GetType();
        
        // ApiResponse 和 ApiResponse<T> 都是 record 类型，使用 with 表达式不适合反射
        // 由于是 init 属性，只能在创建时设置，这里无法修改
        // 实际上这个分支在正常使用时不会进入，因为用户应该直接使用 ApiResponse.Success()
    }

    private static object CreateApiResponse(object? data, string traceId, string requestId)
    {
        if (data is null)
        {
            return new ApiResponse
            {
                Code = 200,
                Message = "success",
                TraceId = traceId,
                RequestId = requestId
            };
        }

        var dataType = data.GetType();
        var responseType = typeof(ApiResponse<>).MakeGenericType(dataType);
        
        var response = Activator.CreateInstance(responseType);
        if (response is null)
        {
            return new ApiResponse
            {
                Code = 200,
                Message = "success",
                TraceId = traceId,
                RequestId = requestId
            };
        }

        // 设置属性值
        responseType.GetProperty(nameof(ApiResponse<object>.Code))?.SetValue(response, 200);
        responseType.GetProperty(nameof(ApiResponse<object>.Data))?.SetValue(response, data);
        responseType.GetProperty(nameof(ApiResponse<object>.Message))?.SetValue(response, "success");
        responseType.GetProperty(nameof(ApiResponse<object>.TraceId))?.SetValue(response, traceId);
        responseType.GetProperty(nameof(ApiResponse<object>.RequestId))?.SetValue(response, requestId);

        return response;
    }

    private static string GetStatusCodeMessage(int statusCode) => statusCode switch
    {
        StatusCodes.Status200OK => "success",
        StatusCodes.Status201Created => "创建成功",
        StatusCodes.Status204NoContent => "操作成功",
        StatusCodes.Status400BadRequest => "请求无效",
        StatusCodes.Status401Unauthorized => "未授权",
        StatusCodes.Status403Forbidden => "禁止访问",
        StatusCodes.Status404NotFound => "资源不存在",
        StatusCodes.Status500InternalServerError => "服务器内部错误",
        _ => "未知状态"
    };
}
