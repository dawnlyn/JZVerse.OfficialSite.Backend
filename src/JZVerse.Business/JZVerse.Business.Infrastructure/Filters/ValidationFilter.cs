using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Abstractions.Validation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JZVerse.Business.Infrastructure.Filters;

/// <summary>
/// 参数校验过滤器
/// 在 Action 执行前自动校验所有参数
/// </summary>
public sealed class ValidationFilter : IActionFilter
{
    private readonly IModelValidator _validator;

    public ValidationFilter(IModelValidator validator)
    {
        _validator = validator;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var errors = new List<ValidationError>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            // 只校验引用类型（排除值类型和字符串）
            var type = argument.GetType();
            if (type.IsValueType || type == typeof(string))
                continue;

            // 使用反射调用泛型方法
            var validateMethod = typeof(IModelValidator)
                .GetMethod(nameof(IModelValidator.TryValidate))!
                .MakeGenericMethod(type);

            var parameters = new object?[] { argument, null };
            var isValid = (bool)validateMethod.Invoke(_validator, parameters)!;

            if (!isValid && parameters[1] is IReadOnlyList<ValidationError> validationErrors)
            {
                errors.AddRange(validationErrors);
            }
        }

        if (errors.Count > 0)
        {
            // 抛出异常，包含第一个错误消息
            throw new ValidationException(errors);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // 无需处理
    }
}
