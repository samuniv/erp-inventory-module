using System.Net;
using System.Net.Http.Json;
using Supplier.Service.DTOs;
using Supplier.Service.IntegrationTests.Fixtures;
using Supplier.Service.Application.Queries;

namespace Supplier.Service.IntegrationTests;

public class SupplierIntegrationTests : IClassFixture<SupplierIntegrationTestFixture>
{
    private readonly SupplierIntegrationTestFixture _fixture;
    private readonly HttpClient _client;

    public SupplierIntegrationTests(SupplierIntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.CreateClient();
    }

    [Fact]
    public async Task GetSuppliers_ShouldReturnSeededSuppliers()
    {
        // Act
        var response = await _client.GetAsync("/api/suppliers");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GetAllSuppliersResponse>();

        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 3); // At least the seeded suppliers
        Assert.NotEmpty(result.Suppliers);
    }

    [Fact]
    public async Task CreateSupplier_ShouldCreateAndReturnSupplier()
    {
        // Arrange
        var createDto = new CreateSupplierDto(
            Name: "Test Supplier Inc.",
            ContactPerson: "Jane Doe",
            Email: "jane.doe@testsupplier.com",
            Phone: "+1-555-9999",
            Address: "123 Test Street",
            City: "Test City",
            State: "Test State",
            PostalCode: "12345",
            Country: "Test Country"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/suppliers", createDto);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SupplierDto>();

        Assert.NotNull(result);
        Assert.Equal(createDto.Name, result.Name);
        Assert.Equal(createDto.ContactPerson, result.ContactPerson);
        Assert.Equal(createDto.Email, result.Email);
        Assert.True(result.Id > 0);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetSupplierById_ShouldReturnSupplier_WhenSupplierExists()
    {
        // Arrange
        var createDto = new CreateSupplierDto(
            Name: "Get Test Supplier",
            ContactPerson: "John Test",
            Email: "john@gettest.com",
            Phone: "+1-555-8888",
            Address: "456 Get Street",
            City: "Get City",
            State: "Get State",
            PostalCode: "54321",
            Country: "Get Country"
        );

        var createResponse = await _client.PostAsJsonAsync("/api/suppliers", createDto);
        var createdSupplier = await createResponse.Content.ReadFromJsonAsync<SupplierDto>();

        // Act
        var response = await _client.GetAsync($"/api/suppliers/{createdSupplier!.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SupplierDto>();

        Assert.NotNull(result);
        Assert.Equal(createdSupplier.Id, result.Id);
        Assert.Equal(createDto.Name, result.Name);
    }

    [Fact]
    public async Task GetSupplierById_ShouldReturnNotFound_WhenSupplierDoesNotExist()
    {
        // Act
        var response = await _client.GetAsync("/api/suppliers/999999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSupplier_ShouldUpdateAndReturnSupplier_WhenSupplierExists()
    {
        // Arrange
        var createDto = new CreateSupplierDto(
            Name: "Update Test Supplier",
            ContactPerson: "Update Test Person",
            Email: "update@test.com",
            Phone: "+1-555-7777",
            Address: "789 Update Street",
            City: "Update City",
            State: "Update State",
            PostalCode: "98765",
            Country: "Update Country"
        );

        var createResponse = await _client.PostAsJsonAsync("/api/suppliers", createDto);
        var createdSupplier = await createResponse.Content.ReadFromJsonAsync<SupplierDto>();

        var updateDto = new UpdateSupplierDto(
            Name: "Updated Supplier Name",
            ContactPerson: "Updated Contact Person",
            Email: "updated@test.com",
            Phone: "+1-555-6666",
            Address: "Updated Address",
            City: "Updated City",
            State: "Updated State",
            PostalCode: "11111",
            Country: "Updated Country",
            IsActive: false
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/suppliers/{createdSupplier!.Id}", updateDto);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SupplierDto>();

        Assert.NotNull(result);
        Assert.Equal(updateDto.Name, result.Name);
        Assert.Equal(updateDto.ContactPerson, result.ContactPerson);
        Assert.Equal(updateDto.Email, result.Email);
        Assert.Equal(updateDto.IsActive, result.IsActive);
    }

    [Fact]
    public async Task UpdateSupplier_ShouldReturnNotFound_WhenSupplierDoesNotExist()
    {
        // Arrange
        var updateDto = new UpdateSupplierDto(
            Name: "Non-existent Supplier",
            ContactPerson: null,
            Email: null,
            Phone: null,
            Address: null,
            City: null,
            State: null,
            PostalCode: null,
            Country: null,
            IsActive: true
        );

        // Act
        var response = await _client.PutAsJsonAsync("/api/suppliers/999999", updateDto);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSupplier_ShouldDeleteSupplier_WhenSupplierExists()
    {
        // Arrange
        var createDto = new CreateSupplierDto(
            Name: "Delete Test Supplier",
            ContactPerson: "Delete Test Person",
            Email: "delete@test.com",
            Phone: "+1-555-5555",
            Address: "Delete Street",
            City: "Delete City",
            State: "Delete State",
            PostalCode: "22222",
            Country: "Delete Country"
        );

        var createResponse = await _client.PostAsJsonAsync("/api/suppliers", createDto);
        var createdSupplier = await createResponse.Content.ReadFromJsonAsync<SupplierDto>();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/suppliers/{createdSupplier!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify supplier is deleted
        var getResponse = await _client.GetAsync($"/api/suppliers/{createdSupplier.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteSupplier_ShouldReturnNotFound_WhenSupplierDoesNotExist()
    {
        // Act
        var response = await _client.DeleteAsync("/api/suppliers/999999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSuppliers_WithSearchTerm_ShouldFilterResults()
    {
        // Arrange
        var searchableSupplier = new CreateSupplierDto(
            Name: "Searchable Electronics Corp",
            ContactPerson: "Search Person",
            Email: "search@electronics.com",
            Phone: "+1-555-4444",
            Address: "Search Address",
            City: "Search City",
            State: "Search State",
            PostalCode: "33333",
            Country: "Search Country"
        );

        await _client.PostAsJsonAsync("/api/suppliers", searchableSupplier);

        // Act
        var response = await _client.GetAsync("/api/suppliers?searchTerm=electronics");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GetAllSuppliersResponse>();

        Assert.NotNull(result);
        Assert.Contains(result.Suppliers, s => s.Name.Contains("Electronics", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetSuppliers_WithPagination_ShouldReturnCorrectPage()
    {
        // Act
        var response = await _client.GetAsync("/api/suppliers?page=1&pageSize=2");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GetAllSuppliersResponse>();

        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.True(result.Suppliers.Count() <= 2);
    }

    [Fact]
    public async Task CreateSupplier_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidDto = new CreateSupplierDto(
            Name: "", // Invalid: empty name
            ContactPerson: null,
            Email: "invalid-email", // Invalid email format
            Phone: null,
            Address: null,
            City: null,
            State: null,
            PostalCode: null,
            Country: null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/suppliers", invalidDto);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
