using FluentAssertions;
using JZVerse.MicroHuaxia.Gateway.Logging.Storage.InMemory;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.Gateway.Tests.Unit.Logging.Storage;

[TestFixture]
public sealed class CircularBufferTests
{
    [Test]
    public void Add_ShouldAddItem()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);

        // Act
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        // Assert
        buffer.Count.Should().Be(3);
        buffer.ToList().Should().BeEquivalentTo([1, 2, 3]);
    }

    [Test]
    public void Add_WhenFull_ShouldOverwriteOldest()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        // Act
        var overwritten = buffer.Add(4);

        // Assert
        overwritten.Should().Be(1);
        buffer.Count.Should().Be(3);
        buffer.ToList().Should().BeEquivalentTo([2, 3, 4]);
    }

    [Test]
    public void Add_WhenFull_ShouldContinueOverwriting()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);

        // Act
        for (int i = 1; i <= 10; i++)
        {
            buffer.Add(i);
        }

        // Assert
        buffer.Count.Should().Be(3);
        buffer.ToList().Should().BeEquivalentTo([8, 9, 10]);
    }

    [Test]
    public void GetLatest_ShouldReturnMostRecentItems()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        for (int i = 1; i <= 5; i++)
        {
            buffer.Add(i);
        }

        // Act
        var latest = buffer.GetLatest(3);

        // Assert
        latest.Should().BeEquivalentTo([3, 4, 5]);
    }

    [Test]
    public void GetLatest_WhenCountExceedsBuffer_ShouldReturnAll()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        buffer.Add(1);
        buffer.Add(2);

        // Act
        var latest = buffer.GetLatest(10);

        // Assert
        latest.Should().BeEquivalentTo([1, 2]);
    }

    [Test]
    public void Where_ShouldFilterItems()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        for (int i = 1; i <= 10; i++)
        {
            buffer.Add(i);
        }

        // Act
        var evens = buffer.Where(x => x % 2 == 0).ToList();

        // Assert
        evens.Should().BeEquivalentTo([2, 4, 6, 8, 10]);
    }

    [Test]
    public void Clear_ShouldRemoveAllItems()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        // Act
        buffer.Clear();

        // Assert
        buffer.Count.Should().Be(0);
        buffer.ToList().Should().BeEmpty();
    }

    [Test]
    public void RemoveWhere_ShouldRemoveMatchingItems()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);
        for (int i = 1; i <= 5; i++)
        {
            buffer.Add(i);
        }

        // Act
        var removed = buffer.RemoveWhere(x => x > 3);

        // Assert
        removed.Should().Be(2);
        buffer.Count.Should().Be(3);
        buffer.ToList().Should().BeEquivalentTo([1, 2, 3]);
    }

    [Test]
    public void AddRange_ShouldAddMultipleItems()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(10);

        // Act
        buffer.AddRange([1, 2, 3, 4, 5]);

        // Assert
        buffer.Count.Should().Be(5);
        buffer.ToList().Should().BeEquivalentTo([1, 2, 3, 4, 5]);
    }

    [Test]
    public void AddRange_WhenExceedsCapacity_ShouldOverwriteOldest()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(3);
        buffer.Add(1);

        // Act
        buffer.AddRange([2, 3, 4, 5]);

        // Assert
        buffer.Count.Should().Be(3);
        buffer.ToList().Should().BeEquivalentTo([3, 4, 5]);
    }

    [Test]
    public void Enumeration_ShouldEnumerateInOrder()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(5);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        // Act
        var items = new List<int>();
        foreach (var item in buffer)
        {
            items.Add(item);
        }

        // Assert
        items.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Test]
    public void ThreadSafety_ShouldHandleConcurrentAccess()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(100);

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            int start = i * 10;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    buffer.Add(start + j);
                }
            }));
        }

        Task.WaitAll([.. tasks]);

        // Assert
        buffer.Count.Should().Be(100);
    }
}
