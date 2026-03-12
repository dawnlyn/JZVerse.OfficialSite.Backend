namespace JZVerse.DataAccess.Abstractions.Caching;

/// <summary>
/// 缓存序列化器接口
/// </summary>
public interface ICacheSerializer
{
    /// <summary>
    /// 序列化对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="value">对象实例</param>
    /// <returns>序列化后的字节数组</returns>
    byte[] Serialize<T>(T value);

    /// <summary>
    /// 反序列化对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="data">字节数组</param>
    /// <returns>反序列化后的对象</returns>
    T? Deserialize<T>(byte[] data);

    /// <summary>
    /// 序列化对象为字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="value">对象实例</param>
    /// <returns>序列化后的字符串</returns>
    string SerializeToString<T>(T value);

    /// <summary>
    /// 从字符串反序列化对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="data">字符串数据</param>
    /// <returns>反序列化后的对象</returns>
    T? DeserializeFromString<T>(string data);
}
