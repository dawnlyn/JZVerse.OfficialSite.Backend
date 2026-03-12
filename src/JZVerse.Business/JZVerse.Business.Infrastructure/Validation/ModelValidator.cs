using System.Reflection;
using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Abstractions.Validation;

namespace JZVerse.Business.Infrastructure.Validation;

/// <summary>
/// 模型校验器实现
/// </summary>
public sealed class ModelValidator : IModelValidator
{
    /// <inheritdoc />
    public void Validate<T>(T model) where T : class
    {
        if (!TryValidate(model, out var errors))
        {
            // 返回第一个错误消息
            throw new ValidationException(errors);
        }
    }

    /// <inheritdoc />
    public bool TryValidate<T>(T model, out IReadOnlyList<ValidationError> errors) where T : class
    {
        ArgumentNullException.ThrowIfNull(model);

        var errorList = new List<ValidationError>();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var value = property.GetValue(model);
            var attributes = property.GetCustomAttributes<ValidationAttribute>(true);

            foreach (var attribute in attributes)
            {
                if (!attribute.IsValid(value))
                {
                    var errorMessage = attribute.FormatErrorMessage(property.Name);
                    errorList.Add(new ValidationError(property.Name, errorMessage));
                    // 每个属性只返回第一个错误
                    break;
                }
            }
        }

        errors = errorList;
        return errorList.Count == 0;
    }
}
