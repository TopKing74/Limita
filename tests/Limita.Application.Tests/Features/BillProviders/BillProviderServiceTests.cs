using Microsoft.EntityFrameworkCore;
using Limita.Application.Features.BillProviders.Models;
using Limita.Application.Features.BillProviders.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;
using Limita.Persistence.Context;
using Xunit;

namespace Limita.Application.Tests.Features.BillProviders;

public class BillProviderServiceTests : IDisposable
{
    private readonly LimitaDbContext _context;
    private readonly BillProviderService _service;

    public BillProviderServiceTests()
    {
        var options = new DbContextOptionsBuilder<LimitaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new LimitaDbContext(options);
        _service = new BillProviderService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task CreateBillProviderAsync_ShouldCreateProvider()
    {
        // Arrange
        var request = new CreateBillProviderRequest("Test Provider", BillCategory.Water, true);

        // Act
        var result = await _service.CreateBillProviderAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Provider", result.Name);
        Assert.Equal(BillCategory.Water, result.Category);
        
        var inDb = await _context.BillProviders.FirstOrDefaultAsync(p => p.Id == result.Id);
        Assert.NotNull(inDb);
        Assert.Equal("Test Provider", inDb.Name);
    }

    [Fact]
    public async Task CreateBillProviderAsync_ShouldThrow_WhenNameExists()
    {
        // Arrange
        var existing = new BillProvider { Name = "Existing", Category = BillCategory.Gas, IsActive = true };
        _context.BillProviders.Add(existing);
        await _context.SaveChangesAsync();

        var request = new CreateBillProviderRequest("Existing", BillCategory.Water, true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateBillProviderAsync(request));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task GetAllBillProvidersAsync_ShouldReturnAll()
    {
        // Arrange
        _context.BillProviders.Add(new BillProvider { Name = "Provider A", Category = BillCategory.Gas });
        _context.BillProviders.Add(new BillProvider { Name = "Provider B", Category = BillCategory.Water });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllBillProvidersAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "Provider A");
        Assert.Contains(result, p => p.Name == "Provider B");
    }

    [Fact]
    public async Task GetBillProviderByIdAsync_ShouldReturnProvider_WhenExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        _context.BillProviders.Add(new BillProvider { Id = id, Name = "Provider A", Category = BillCategory.Gas });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetBillProviderByIdAsync(id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(id, result.Id);
        Assert.Equal("Provider A", result.Name);
    }
}
