using JZVerse.MicroHuaxia.MessageQueue.Abstractions;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Models;
using JZVerse.MicroHuaxia.MessageQueue.Abstractions.Transaction;
using JZVerse.MicroHuaxia.MessageQueue.Core.Transaction;
using JZVerse.MicroHuaxia.MessageQueue.Storage.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace JZVerse.MicroHuaxia.MessageQueue.Tests.Unit;

/// <summary>
/// 事务存储测试
/// </summary>
[TestFixture]
public class TransactionStoreTests
{
    private MemoryTransactionStore _transactionStore = null!;

    [SetUp]
    public void Setup()
    {
        _transactionStore = new MemoryTransactionStore(
            NullLogger<MemoryTransactionStore>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _transactionStore.Dispose();
    }

    [Test]
    public async Task StoreHalfMessage_ShouldStoreMessage()
    {
        // Arrange
        var transactionId = "TXN123";
        var message = CreateTestMessage("test-topic", "test-body");

        // Act
        await _transactionStore.StoreHalfMessageAsync(transactionId, message);

        // Assert
        var retrieved = await _transactionStore.GetHalfMessageAsync(transactionId);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.MessageId, Is.EqualTo(message.MessageId));
        Assert.That(retrieved.Topic, Is.EqualTo(message.Topic));
    }

    [Test]
    public async Task StoreHalfMessage_DuplicateTransactionId_ShouldThrow()
    {
        // Arrange
        var transactionId = "TXN123";
        var message1 = CreateTestMessage("topic1", "body1");
        var message2 = CreateTestMessage("topic2", "body2");

        await _transactionStore.StoreHalfMessageAsync(transactionId, message1);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _transactionStore.StoreHalfMessageAsync(transactionId, message2));
    }

    [Test]
    public async Task DeleteHalfMessage_ShouldRemoveMessage()
    {
        // Arrange
        var transactionId = "TXN123";
        var message = CreateTestMessage("test-topic", "test-body");
        await _transactionStore.StoreHalfMessageAsync(transactionId, message);

        // Act
        await _transactionStore.DeleteHalfMessageAsync(transactionId);

        // Assert
        var retrieved = await _transactionStore.GetHalfMessageAsync(transactionId);
        Assert.That(retrieved, Is.Null);
    }

    [Test]
    public async Task UpdateStatus_ShouldChangeTransactionStatus()
    {
        // Arrange
        var transactionId = "TXN123";
        var message = CreateTestMessage("test-topic", "test-body");
        await _transactionStore.StoreHalfMessageAsync(transactionId, message);

        // Act
        await _transactionStore.UpdateStatusAsync(transactionId, TransactionStatus.Committed);

        // Assert
        var status = _transactionStore.GetStatus(transactionId);
        Assert.That(status, Is.EqualTo(TransactionStatus.Committed));
    }

    [Test]
    public async Task GetPendingTransactions_ShouldReturnPreparingTransactions()
    {
        // Arrange
        var txn1 = "TXN1";
        var txn2 = "TXN2";
        var message1 = CreateTestMessage("topic1", "body1");
        var message2 = CreateTestMessage("topic2", "body2");
        
        await _transactionStore.StoreHalfMessageAsync(txn1, message1);
        await _transactionStore.StoreHalfMessageAsync(txn2, message2);

        // Wait a bit then check with short timeout
        await Task.Delay(100);

        // Act - use a very short timeout to find transactions created before
        var pending = await _transactionStore.GetPendingTransactionsAsync(TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.That(pending, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetPendingTransactions_ShouldExcludeCommittedTransactions()
    {
        // Arrange
        var txn1 = "TXN1";
        var txn2 = "TXN2";
        var message1 = CreateTestMessage("topic1", "body1");
        var message2 = CreateTestMessage("topic2", "body2");
        
        await _transactionStore.StoreHalfMessageAsync(txn1, message1);
        await _transactionStore.StoreHalfMessageAsync(txn2, message2);
        await _transactionStore.UpdateStatusAsync(txn1, TransactionStatus.Committed);

        await Task.Delay(100);

        // Act
        var pending = await _transactionStore.GetPendingTransactionsAsync(TimeSpan.FromMilliseconds(50));

        // Assert - only txn2 should be pending (txn1 is committed)
        Assert.That(pending, Has.Count.EqualTo(1));
        Assert.That(pending[0].TransactionId, Is.EqualTo(txn2));
    }

    private static IMessage CreateTestMessage(string topic, string body)
    {
        return MessageBuilder.Create()
            .Topic(topic)
            .Body(body)
            .Build();
    }
}

/// <summary>
/// 事务生产者测试
/// </summary>
[TestFixture]
public class TransactionProducerTests
{
    private MemoryMessageStore _messageStore = null!;
    private MemoryTransactionStore _transactionStore = null!;
    private TransactionProducer _transactionProducer = null!;

    [SetUp]
    public void Setup()
    {
        _messageStore = new MemoryMessageStore(
            NullLogger<MemoryMessageStore>.Instance,
            new MemoryStoreOptions());
        
        _transactionStore = new MemoryTransactionStore(
            NullLogger<MemoryTransactionStore>.Instance);
        
        _transactionProducer = new TransactionProducer(
            NullLogger<TransactionProducer>.Instance,
            _transactionStore,
            _messageStore);
    }

    [TearDown]
    public void TearDown()
    {
        _transactionStore.Dispose();
    }

    [Test]
    public async Task PrepareAsync_ShouldCreateHalfMessage()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "test-body");

        // Act
        var result = await _transactionProducer.PrepareAsync(message);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.TransactionId, Is.Not.Null);
        Assert.That(result.TransactionId, Does.StartWith("TXN"));
        Assert.That(result.MessageId, Is.Not.Null);

        // Verify half message is stored
        var halfMessage = await _transactionStore.GetHalfMessageAsync(result.TransactionId!);
        Assert.That(halfMessage, Is.Not.Null);
        Assert.That(halfMessage!.TransactionId, Is.EqualTo(result.TransactionId));
    }

    [Test]
    public async Task CommitAsync_ShouldMoveMessageToStore()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "commit-test");
        var prepareResult = await _transactionProducer.PrepareAsync(message);
        Assert.That(prepareResult.Success, Is.True);

        // Act
        await _transactionProducer.CommitAsync(prepareResult.TransactionId!);

        // Assert - message should now be in main store
        var storedMessage = await _messageStore.GetByIdAsync(prepareResult.MessageId!);
        Assert.That(storedMessage, Is.Not.Null);
        Assert.That(storedMessage!.Topic, Is.EqualTo("test-topic"));

        // Status should be Committed
        var status = _transactionStore.GetStatus(prepareResult.TransactionId!);
        Assert.That(status, Is.EqualTo(TransactionStatus.Committed));
    }

    [Test]
    public async Task RollbackAsync_ShouldRemoveHalfMessage()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "rollback-test");
        var prepareResult = await _transactionProducer.PrepareAsync(message);
        Assert.That(prepareResult.Success, Is.True);

        // Act
        await _transactionProducer.RollbackAsync(prepareResult.TransactionId!);

        // Assert - half message should be deleted
        var halfMessage = await _transactionStore.GetHalfMessageAsync(prepareResult.TransactionId!);
        Assert.That(halfMessage, Is.Null);
    }

    [Test]
    public async Task ExecuteAsync_WithCommit_ShouldStoreMessage()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "execute-commit-test");

        // Act
        var result = await _transactionProducer.ExecuteAsync(
            message,
            async (msg, ct) =>
            {
                // Simulate successful local transaction
                await Task.Delay(10, ct);
                return LocalTransactionResult.Commit;
            });

        // Assert
        Assert.That(result.Success, Is.True);
        
        // Message should be in store
        var storedMessage = await _messageStore.GetByIdAsync(result.MessageId!);
        Assert.That(storedMessage, Is.Not.Null);
    }

    [Test]
    public async Task ExecuteAsync_WithRollback_ShouldNotStoreMessage()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "execute-rollback-test");

        // Act
        var result = await _transactionProducer.ExecuteAsync(
            message,
            async (msg, ct) =>
            {
                await Task.Delay(10, ct);
                return LocalTransactionResult.Rollback;
            });

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("rollback"));
    }

    [Test]
    public async Task ExecuteAsync_WithException_ShouldRollback()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "execute-exception-test");

        // Act
        var result = await _transactionProducer.ExecuteAsync(
            message,
            (msg, ct) =>
            {
                throw new InvalidOperationException("Local transaction failed");
            });

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("Local transaction failed"));
    }

    [Test]
    public async Task ExecuteAsync_WithUnknown_ShouldKeepPreparing()
    {
        // Arrange
        var message = CreateTestMessage("test-topic", "execute-unknown-test");

        // Act
        var result = await _transactionProducer.ExecuteAsync(
            message,
            async (msg, ct) =>
            {
                await Task.Delay(10, ct);
                return LocalTransactionResult.Unknown;
            });

        // Assert - should still return the transaction ID for later processing
        Assert.That(result.Success, Is.True);
        Assert.That(result.TransactionId, Is.Not.Null);

        // Status should still be Preparing (awaiting check)
        var status = _transactionStore.GetStatus(result.TransactionId!);
        Assert.That(status, Is.EqualTo(TransactionStatus.Preparing));
    }

    [Test]
    public async Task CommitAsync_NonExistentTransaction_ShouldThrow()
    {
        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _transactionProducer.CommitAsync("NON_EXISTENT_TXN"));
    }

    private static IMessage CreateTestMessage(string topic, string body)
    {
        return MessageBuilder.Create()
            .Topic(topic)
            .Body(body)
            .Build();
    }
}
