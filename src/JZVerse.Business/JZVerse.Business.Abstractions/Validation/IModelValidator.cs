using JZVerse.Business.Abstractions.Models;

namespace JZVerse.Business.Abstractions.Validation;

/// <summary>
/// 模型校验器接口
/// </summary>
public interface IModelValidator
{
    /// <summary>
    /// 校验模型，失败时抛出 ValidationException（包含第一个错误消息）
    /// </summary>
    /// <typeparam name="T">模型类型</typeparam>
    /// <param name="model">待校验的模型</param>
    /// <exception cref="ValidationException">校验失败时抛出</exception>
    void Validate<T>(T model) where T : class;

    /// <summary>
    /// 尝试校验模型，返回所有错误
    /// </summary>
    /// <typeparam name="T">模型类型</typeparam>
    /// <param name="model">待校验的模型</param>
    /// <param name="errors">错误列表</param>
    /// <returns>是否校验通过</returns>
    bool TryValidate<T>(T model, out IReadOnlyList<ValidationError> errors) where T : class;
}
